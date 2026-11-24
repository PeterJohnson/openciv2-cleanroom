using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // W06 rewards await actual U01/C01 -> W03/V04 -> S02 before reward and movement tails.
    private sealed class RetainedHutFixture
    {
        public SinglePlayerDiplomacyFixture Diplomacy { get; }
        public HutFixture F { get; }
        public RevelationFixture Data => F.Data;
        public GameState World => F.World;
        public Event Permission { get; }
        public RetainedHutFixture(List<Choice> choices, RevelationFixture? data = null)
        {
            Diplomacy = new(data: data);
            var units = new UnitCreation(Diplomacy.World, Diplomacy.Data.Process, Diplomacy.Data.Stacks,
                new UnitRevelation(Diplomacy.World, Diplomacy.Data.Rules, Diplomacy.Data.Stacks, Diplomacy.Services.Contact, Diplomacy.Data), Diplomacy.Data);
            F = new(choices, Diplomacy.Data, units: units);
            World.Civilizations[1].Treaties[2].Contact = false;
            World.Multiplayer.LocalHuman = 2;
            Data.AddUnit(10, 2, 4, new(17, 11));
            World.Events.Loaded = true;
            Permission = NegotiationEvent(World, 1, 2, deny: true);
            Permission.Values.Actions = new() { Text = true, JustOnce = true };
            Permission.SetString(Event.TextBegin, new("Hut reward contact"));
        }
        public int Reward => Data.Stacks.FirstAt(F.Point);
        public void Consumed() { F.Random.RequireChoicesConsumed(); Diplomacy.Events.Consumed(); }
    }

    private static void HutRetainedUnitRewards()
    {
        foreach (bool nomads in new[] { false, true })
        {
            var f = new RetainedHutFixture(nomads ? HutStart(0) : HutStart(1, HutDraw("mercenary-type", 2, 0)));
            f.F.Known(AdvanceIds.Chivalry);
            if (nomads) f.World.Map.TileAt(f.F.Point).Fertility = 1;
            using var operation = f.F.Begin(); var pending = operation.Resume();
            for (int i = 0; i < 10 && pending is null && !operation.Completed; ++i) pending = operation.Resume();
            Equal(true, pending is HutDecision.Contact { Decision: DiplomaticContactDecision.Scenario { Value: ScenarioText } });
            int reward = f.Reward; var token = f.Data.Unit(reward).Token;
            Equal(new UnitType((byte)(nomads ? UnitTypeIds.Settlers : UnitTypeIds.Knights)), f.Data.Unit(reward).Type);
            Equal(true, f.Data.Unit(reward).Home is not null); Equal(0, f.F.NeighborDraws);
            var random = f.F.Random.Capture(); Equal(pending, operation.Resume()); Equal(random, f.F.Random.Capture());
            Equal(token, f.Data.Unit(reward).Token);
            // The remote/AI mercenary branch and nomad branch clear support only after creation returns.
            f.F.Movement.LocalHuman = false;
            Equal(null, operation.ResumeDecision(new(0))); Equal(true, operation.Completed);
            Equal(null, f.Data.Unit(reward).Home); Equal(0, f.F.NeighborDraws); Equal(token, f.Data.Unit(reward).Token);
            f.Consumed();
        }
        var barbarians = new RetainedHutFixture(HutStart(3)); barbarians.World.Globals.Turn = 40;
        using var spawn = barbarians.F.Begin(); barbarians.F.Complete(spawn);
        Equal(1, barbarians.F.Draws.Count);
        int barbarian = barbarians.Data.Stacks.FirstAt(barbarians.F.Draws.Single());
        Equal(new CivilizationSlot(0), barbarians.Data.Unit(barbarian).Owner); barbarians.Consumed();
    }

    private static void HutRetainedCityAwards()
    {
        var f = new RetainedHutFixture([HutDraw("outcome", 5, 0), HutDraw("city-size", 4, 1),
            HutDraw("city-marketplace", 2, 1), HutDraw("city-granary", 3, 0), HutDraw("city-library", 4, 1)]);
        f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Civilizations[1].CityCount = 0;
        f.Data.Rules.Units[3] = f.Data.Rules.Units[3] with { Role = RuleRole.Defend, Defense = 2, Prerequisite = AdvanceReference.None };
        using var operation = f.F.Begin(); var pending = operation.Resume();
        Equal(true, pending is HutDecision.Contact); Equal(1, f.F.Random.Capture().Trace!.Events.Count);
        var city = CityQueries.At(f.World, f.F.Point)!.Value;
        Equal((sbyte)3, f.World.Entities.Cities.At(city).Size); Equal(0, f.F.NeighborDraws);
        Equal(pending, operation.Resume()); f.World.Globals.Year = 1000;
        var visit = (HutCityVisit)operation.ResumeDecision(new(0))!;
        Equal(city, visit.City.Slot); Equal((sbyte)2, f.World.Entities.Cities.At(city).Size);
        Equal(5, f.F.Random.Capture().Trace!.Events.Count);
        Equal(true, BuildingQueries.HasBuilding(f.World, city, BuildingIds.Granary));
        Equal(1, f.F.NeighborDraws); Equal(false, operation.Completed);
        operation.Resume(0); Equal(true, operation.Completed); f.Consumed();
    }

    private static void HutRetainedFailureAndPrivateReplies()
    {
        var stopped = new RetainedHutFixture(HutStart(1, HutDraw("mercenary-type", 2, 0))); stopped.F.Known(AdvanceIds.Chivalry);
        using (var operation = stopped.F.Begin())
        {
            operation.Resume(); int reward = stopped.Reward;
            operation.Dispose(); Throws<ObjectDisposedException>(() => operation.Resume(0));
            Equal(true, stopped.Data.Unit(reward).Home is not null); Equal(0, stopped.F.NeighborDraws); stopped.Consumed();
        }
        var failed = new RetainedHutFixture(HutStart(1, HutDraw("mercenary-type", 2, 0))); failed.F.Known(AdvanceIds.Chivalry);
        failed.Permission.Values.Actions.PlayWaveFile = true; failed.Permission.SetString(34, new("reward.wav"));
        failed.World.Globals.GameOptions.Sound = true; failed.Diplomacy.Events.Media.FailProbe = true;
        using (var operation = failed.F.Begin())
        {
            Throws<IOException>(() => operation.Resume()); Throws<InvalidOperationException>(() => operation.Resume(0));
            Equal(new UnitType(UnitTypeIds.Knights), failed.Data.Unit(failed.Reward).Type);
            Equal(true, failed.Data.Unit(failed.Reward).Home is not null); Equal(0, failed.F.NeighborDraws); failed.Consumed();
        }
        var timeout = new RetainedHutFixture(HutStart(1, HutDraw("mercenary-type", 2, 0))); timeout.F.Known(AdvanceIds.Chivalry);
        using (var operation = new HutOperation(timeout.F.Begin(), c => new((ulong)c),
            _ => throw new InvalidOperationException("Mercenaries have no city window")))
        {
            Equal(CommandStatus.AwaitingInteraction, operation.Resume(timeout.Diplomacy.Events.Context(), new()).Status);
            Equal(CommandStatus.Completed, operation.Resume(timeout.Diplomacy.Events.Context(), new(OperationInputKind.Timeout)).Status);
            Equal(1, timeout.F.NeighborDraws); timeout.Consumed();
        }
        foreach (int mode in new[] { 0, 1, 3 })
        {
            using var runtime = new SessionRuntime(new(), new((ulong)(261 + mode)));
            var f = new RetainedHutFixture(HutStart(1, HutDraw("mercenary-type", 2, 0)), CreationFixture(new(runtime.Process, runtime.World)));
            f.F.Known(AdvanceIds.Chivalry); f.F.FailDraw = mode == 3;
            ParticipantId owner = default, neighbor = default;
            runtime.RegisterCommand(new(new(1, "test.hut-reward", "Retained hut reward", AccessClass.Player, new([]), new([])),
                (_, _, _) => new(), _ => new HutOperation(f.F.Begin(), c => c == 2 ? neighbor : owner,
                    _ => throw new InvalidOperationException("Mercenaries have no city window"))));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 1); var witness = SessionClient(runtime, 2);
            owner = actor.Identity.Participant; neighbor = witness.Identity.Participant;
            var command = actor.Submit(new("test.hut-reward", new([]))).Correlation.Command; runtime.AdvanceOne();
            var prompt = RequiredInteraction(witness, command);
            Equal(null, RequiredResult(actor, command).Interaction); Equal(false, actor.Reply(prompt, 0).Accepted);
            int reward = f.Reward; var token = f.Data.Unit(reward).Token; Equal(0, f.F.NeighborDraws);
            if (mode == 1) Equal(true, actor.Cancel(command).Accepted);
            else Equal(true, witness.Reply(prompt, 0).Accepted);
            for (int i = 0; i < 50 && !RequiredResult(actor, command).Status.Terminal(); ++i) runtime.AdvanceOne();
            var result = RequiredResult(actor, command);
            Equal(mode == 3 ? CommandStatus.Failed : CommandStatus.Completed, result.Status);
            if (mode == 3) Equal("hut_presentation_failed", result.Code);
            Equal(token, f.Data.Unit(reward).Token); Equal(1, f.F.NeighborDraws);
            Equal(false, witness.Reply(prompt, 0).Accepted); f.Consumed();
        }
    }

    private static void MovementFinishRetainsHutContact()
    {
        var f = new RetainedHutFixture(CompletionHut(1, HutDraw("mercenary-type", 2, 0)));
        var completion = new CompletionFixture(data: f.Data, retainedHuts: f.F.Huts);
        f.Data.Stacks.Relocate(0, f.F.Point, false); f.F.Known(AdvanceIds.Chivalry);
        f.World.Multiplayer.LocalHuman = 2; f.Data.Unit(0).LastDirection = 255;
        completion.Step = completion.Step with { To = f.F.Point };
        completion.Inputs = completion.Inputs with { HasHut = true, OrdinaryArrival = false };
        using var operation = completion.Begin(); var pending = operation.Resume();
        Equal(true, pending is MovementCompletionDecision.Contact);
        Equal((byte)255, f.Data.Unit(0).LastDirection); Equal(null, operation.Result);
        Equal(pending, operation.Resume()); operation.Resume(new(0));
        Equal(1, operation.Result); Equal((byte)1, f.Data.Unit(0).LastDirection);
        f.Consumed(); completion.Life.Random.RequireChoicesConsumed();
    }
}
