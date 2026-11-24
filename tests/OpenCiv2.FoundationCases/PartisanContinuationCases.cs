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
    // C09 Partisans, U01/W03 creation/contact and S02 negotiation. Actual production diplomacy graph.
    private sealed class RetainedPartisanFixture
    {
        public SinglePlayerDiplomacyFixture Diplomacy { get; } = new();
        public CaptureContinuationFixture F { get; }
        public RevelationFixture Data => F.Data;
        public GameState World => F.World;
        public CapturePartisans Service { get; }
        public RetainedPartisanFixture(List<Choice>? choices = null)
        {
            F = new(choices, Diplomacy.Data);
            World.Civilizations[1].Government = World.Civilizations[2].Government = Government.Despotism;
            World.Civilizations[2].Treaties[1].Contact = false;
            Data.Rules.Units[UnitTypeIds.Partisans] = Data.Rules.Units[0] with { Role = RuleRole.Attack, Domain = RuleDomain.Land };
            PartisanPermission(World, 1);
            var creation = new UnitCreation(World, Data.Process, Data.Stacks,
                new UnitRevelation(World, Data.Rules, Data.Stacks, Diplomacy.Services.Contact, Data), Data);
            Service = new(World, Data.Rules, creation, F.Loss.Lifecycle.Random, F);
        }
        public CapturePartisans.Operation Begin() => Service.Begin(new(0), 1, 2, new(10, 10), 0, "Original name", "partisans");
        public int First => Data.Stacks.FirstAt(new(11, 9));
        public void Consumed() { F.Loss.Lifecycle.Random.RequireChoicesConsumed(); Diplomacy.Events.Consumed(); }
    }
    private static Event PartisanPermission(GameState world, int listener)
    {
        world.Events.Loaded = true;
        var entry = NegotiationEvent(world, 2, listener, deny: true);
        entry.Values.Actions = new() { Text = true, JustOnce = true };
        entry.SetString(Event.TextBegin, new("Partisan contact"));
        return entry;
    }

    private static void PartisansRetainedContactAndLivePlacement()
    {
        var f = new RetainedPartisanFixture([PartisanChoice(0, 3, 1), PartisanChoice(1, 3, 0)]);
        f.World.Civilizations[1].Reputation = 6;
        using var operation = f.Begin(); var pending = operation.Resume();
        Equal(true, pending is DiplomaticContactDecision.Scenario { Value: ScenarioText });
        int first = f.First; Equal(true, first >= 0); var token = f.Data.Unit(first).Token;
        Equal(UnitOrder.None, f.Data.Unit(first).Orders); Equal(false, f.Data.Unit(first).Flags.Veteran);
        Equal(0, f.F.Sounds); Equal(0, f.F.Draws.Count); Equal(null, operation.Result);
        Equal(pending, operation.Resume()); Equal(token, f.Data.Unit(first).Token); Equal(0UL, f.F.Loss.Lifecycle.Random.Draws);
        // Real pending contact separates retained count/threshold from the next live placement scan.
        f.World.Civilizations[2].Government = Government.Democracy;
        f.World.Civilizations[2].Advances = default; f.World.Civilizations[1].Reputation = 250;
        f.World.Map.TileAt(new(8, 10)).Features = MapFeatures.Road | MapFeatures.Fortification;
        Equal(null, operation.Resume(new(0))); Equal(2, operation.Result);
        Equal(UnitOrder.Fortified, f.Data.Unit(first).Orders); Equal(true, f.Data.Unit(first).Flags.Veteran);
        int second = f.Data.Stacks.FirstAt(new(8, 10)); Equal(true, second >= 0);
        Equal(UnitOrder.Fortified, f.Data.Unit(second).Orders); Equal(false, f.Data.Unit(second).Flags.Veteran);
        Equal(true, f.Data.Unit(first).Visibility.Contains(1) && f.Data.Unit(second).Visibility.Contains(1));
        Sequence(new[] { new MapPoint(11, 9), new MapPoint(8, 10) }, f.F.Draws);
        Equal(1, f.F.Sounds); Equal(new UnitSlot((ushort)second), f.F.Notices.Single().LastCreation);
        Equal("Original name", f.F.Notices.Single().CityName); Equal(null, operation.Resume(new(1))); f.Consumed();
    }

    private static void PartisansRetainedFailureAndDisposal()
    {
        var f = new RetainedPartisanFixture();
        using (var stopped = f.Begin())
        {
            Equal(true, stopped.Resume() is DiplomaticContactDecision.Scenario); int first = f.First;
            stopped.Dispose(); Throws<ObjectDisposedException>(() => stopped.Resume(new(0)));
            Equal(UnitOrder.None, f.Data.Unit(first).Orders); Equal(0, f.F.Sounds + f.F.Draws.Count + f.F.Notices.Count);
            Equal(null, stopped.Result); f.Consumed();
        }
        f = new(); f.F.FailPartisanSound = f.F.FailPartisanRedraw = true;
        using (var cosmetic = f.Begin())
        {
            cosmetic.Resume(); cosmetic.Resume(new(0)); Equal(2, cosmetic.Result);
            Equal(3, cosmetic.PresentationFailures.Count); Equal(1, f.F.Notices.Count);
            Equal(UnitOrder.Fortified, f.Data.Unit(f.First).Orders); f.Consumed();
        }
        f = new(); f.F.FailPartisanNotice = true;
        using (var required = f.Begin())
        {
            required.Resume(); Throws<IOException>(() => required.Resume(new(0)));
            Equal(2, f.F.Draws.Count); Equal(UnitOrder.Fortified, f.Data.Unit(f.First).Orders);
            Throws<InvalidOperationException>(() => required.Resume()); Equal(1, f.F.Notices.Count); f.Consumed();
        }
        f = new(); int end = f.World.Entities.Units.End;
        Throws<InvalidOperationException>(() => f.Service.Create(new(0), 1, 2, new(10, 10), 0, "Original name", "partisans"));
        Equal(end, f.World.Entities.Units.End); Equal(0, f.F.Sounds); f.Consumed();
    }

    private static void PartisansRetainedLastAllocation()
    {
        var f = new RetainedPartisanFixture(); f.World.Civilizations[2].MilitaryCount = 1948;
        using var operation = f.Begin(); Equal(true, operation.Resume() is DiplomaticContactDecision.Scenario);
        Equal((short)1949, f.World.Civilizations[2].MilitaryCount);
        operation.Resume(new(0)); Equal(1, operation.Result);
        Equal(null, f.F.Notices.Single().LastCreation); Equal(1, f.F.Draws.Count); Equal(1, f.F.Sounds); f.Consumed();
    }

    private static void CityCapturePartisanPrivateContact()
    {
        using var runtime = new SessionRuntime(new(), new(259));
        var diplomacy = new SinglePlayerDiplomacyFixture(data: CreationFixture(new(runtime.Process, runtime.World)));
        var data = diplomacy.Data; var world = data.World;
        var creation = new UnitCreation(world, data.Process, data.Stacks,
            new UnitRevelation(world, data.Rules, data.Stacks, diplomacy.Services.Contact, data), data);
        var f = new CityCaptureFixture([CaptureParity(0)], data, true, diplomacy.Events.Triggers, creation);
        f.City.Size = 12;
        world.Civilizations[1].Government = world.Civilizations[2].Government = Government.Despotism;
        world.Globals.PowerRank[1] = world.Globals.PowerRank[2] = 0;
        world.Civilizations[2].Advances = default(AdvanceSet).With(AdvanceIds.Conscription).With(AdvanceIds.GuerrillaWarfare);
        world.Globals.FirstDiscoverer[AdvanceIds.GuerrillaWarfare] = 2;
        world.Globals.Status.Scenario = true; world.Scenario.Flags.NoConquestTechnology = true;
        world.Multiplayer.LocalHuman = 3; world.Civilizations[2].Treaties[3].Contact = false;
        data.Rules.Units[UnitTypeIds.Partisans] = data.Rules.Units[0] with { Role = RuleRole.Attack, Domain = RuleDomain.Land };
        // W03 excludes Partisans from foreign-unit contact, but permits empty-city contact.
        var center = f.City.Position; data.AddCity(2, 3, new(center.X + 2, center.Y - 2));
        PartisanPermission(world, 3);
        ParticipantId owner = default, observer = default;
        runtime.RegisterCommand(new(new(1, "test.capture-partisans", "Authored capture with partisan contact", AccessClass.Player, new([]), new([new("city")])),
            (_, _, _) => new(), _ => new CityCaptureOperation(f.Begin(0), c => c == 3 ? observer : owner,
                _ => new("Captured city", owner, null, new([0])))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 1); var witness = SessionClient(runtime, 3);
        owner = actor.Identity.Participant; observer = witness.Identity.Participant;
        var command = actor.Submit(new("test.capture-partisans", new([]))).Correlation.Command; runtime.AdvanceOne();
        // Drain bounded scenario/capture work; any earlier city window belongs to the captor.
        for (int step = 0; step < 50 && witness.Result(command)?.Interaction is null; ++step)
        {
            var update = RequiredResult(actor, command);
            if (update.Status.Terminal())
            { throw new InvalidOperationException($"Capture ended before partisan contact: {update.Status} {update.Code}"); }
            if (update.Interaction is { } cityWindow)
            { Equal(true, actor.Reply(cityWindow.Id, 0).Accepted); }
            runtime.AdvanceOne();
        }
        var prompt = RequiredInteraction(witness, command);
        Equal(null, RequiredResult(actor, command).Interaction); Equal(false, actor.Reply(prompt, 0).Accepted);
        Equal((byte)0, f.City.TurnsCaptured); Equal(new CivilizationSlot(1), f.City.Owner);
        int partisan = data.Stacks.FirstAt(new(center.X + 1, center.Y - 1));
        Equal(true, partisan >= 0); var token = data.Unit(partisan).Token; Equal(UnitOrder.None, data.Unit(partisan).Orders);
        Equal(true, witness.Reply(prompt, 0).Accepted);
        for (int step = 0; step < 50 && !RequiredResult(actor, command).Status.Terminal(); ++step) runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(false, witness.Reply(prompt, 0).Accepted);
        Equal(token, data.Unit(partisan).Token); Equal(UnitOrder.Fortified, data.Unit(partisan).Orders);
        Equal((byte)world.Globals.Turn, f.City.TurnsCaptured); Equal(new CivilizationSlot(2), f.City.Founder);
        Equal(false, world.Civilizations[2].Treaties[3].Contact);
        f.Life.Random.RequireChoicesConsumed(); diplomacy.Events.Consumed();
    }
}
