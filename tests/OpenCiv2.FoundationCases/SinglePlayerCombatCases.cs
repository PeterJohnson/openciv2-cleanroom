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
    // U06 execution/casualties, C09 capture and S02 caller hooks. Actual production bundles.
    private sealed class SinglePlayerCombatFixture
    {
        public SinglePlayerLifecycleFixture Life { get; }
        public ExecutionFixture F { get; }
        public RevelationFixture Data => Life.Data;
        public GameState World => Life.World;
        public SinglePlayerCombat Services { get; }
        public bool FailOpening;
        public int Published, Opened;
        public SinglePlayerCombatFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Life = new(choices, data); F = new(data: Life.Data);
            World.Civilizations[1].Treaties[2] = World.Civilizations[2].Treaties[1] = default;
            World.Civilizations[1].Government = World.Civilizations[2].Government = Government.Despotism;
            World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); World.Multiplayer.LocalHuman = 1;
            World.Globals.Turn = 41; World.Globals.RevealMap = 0; World.Events.Loaded = false;
            var capture = F.F.Drop.Capture;
            Services = new(World, Data.Rules, Life.Random, Data.Stacks, new(World, Data.Rules, Data.Stacks),
                Life.Services, Life.Diplomacy.Grant.Services, Life.Diplomacy.Services, Life.Diplomacy.Production,
                new(World, Data.Rules, capture.Upgrades.Add, capture.Redraw),
                new(capture, capture, capture, F.F.Drop, F.F, F.F, new RoundEffects(), F.F, F, F.F,
                    _ => F.Calls.Add("treaty"), _ => F.Calls.Add("promotion"),
                    _ => { ++Opened; if (FailOpening) throw new IOException("Combat display unavailable"); }, () => ++Published));
        }
        public CombatExecution.Operation Begin(int budget = 64) => Services.Execution.Begin(new(0), 1, new(false, true), "", "combat", roundBudget: budget);
        public void Complete(CombatExecution.Operation operation)
        {
            for (int i = 0; i < 100 && !operation.Completed; ++i) Equal(null, operation.Resume());
            Equal(true, operation.Completed); Consumed();
        }
        public void Consumed() { Life.Consumed(); F.F.Life.Random.RequireChoicesConsumed(); }
        public Event Event(EventTrigger trigger, bool library = true, bool text = true)
        {
            World.Events.Loaded = true;
            var entry = World.Events.Collection.Append(); entry.Values.Trigger = trigger;
            entry.Values.WhoCiv = entry.Values.WhomCiv = -2; entry.Values.UnitType = 4;
            entry.SetString(1, new("Birch")); entry.SetString(OpenCiv2.Core.State.Event.TextBegin, new("Combat event"));
            entry.Values.Actions = new() { GiveTechnology = true, Text = text, JustOnce = true };
            entry.Values.GiveTechCiv = 2; entry.Values.GiveTech = AdvanceIds.Monarchy;
            if (library)
            {
                Data.AddCity(2, 1, new(30, 20)); World.Civilizations[1].CityCount = 1;
                World.Globals.WonderCity[WonderIds.GreatLibrary] = 2;
                World.Civilizations[3].Advances = World.Civilizations[3].Advances.With(AdvanceIds.Monarchy);
                World.Globals.AdvanceKnownBy[AdvanceIds.Monarchy] = CivilizationSet.Only(3);
                for (int i = 0; i < Data.Rules.Terrain.Length; ++i)
                    Data.Rules.Terrain[i] = Data.Rules.Terrain[i] with
                    { Food = 2, Shields = 1, Trade = 1, Work = new(new(TerrainResult.ImproveInPlace, 1), new(TerrainResult.ImproveInPlace, 2)) };
            }
            return entry;
        }
    }

    private static void SinglePlayerCombatExecutionAndPermission()
    {
        var f = new SinglePlayerCombatFixture(ExecutionWin());
        using (var operation = f.Begin())
        {
            f.Complete(operation); Equal(1, operation.Result); Equal(1, f.Services.State.KillCount);
            Equal(false, f.Data.Unit(1).Token.Occupied); Equal(true, f.Data.Unit(0).Flags.Veteran);
            Equal((sbyte)4, f.World.Entities.Cities.Storage[0].Size); Equal(1, f.World.Casualties[2].Count);
            Equal(2, f.Published); Equal(1, f.Opened); Equal("advisor", f.F.Calls[^1]);
            Equal(null, operation.Resume(new(0))); Equal((byte)3, f.Data.Unit(0).MovesUsed);
        }
        var rejected = new SinglePlayerCombatFixture(); rejected.World.Civilizations[1].Treaties[2].Peace = true;
        using (var operation = rejected.Begin())
        {
            var pending = operation.Resume(); Equal(true, pending is CombatDecision.Attack);
            Equal(pending, operation.Resume()); Equal((byte)3, rejected.Data.Unit(0).MovesUsed);
            operation.Resume(new(0)); rejected.Complete(operation); Equal(1, operation.Result);
            Equal(true, rejected.Data.Unit(1).Token.Occupied); Equal(0, rejected.Published + rejected.Opened);
        }
        var loss = new SinglePlayerCombatFixture([ExecutionRoll("attack", 80, 0), ExecutionRoll("defense", 4, 3), ExecutionPromotion()]);
        loss.Data.Rules.Units[4] = loss.Data.Rules.Units[4] with { Firepower = 10 };
        using var defeated = loss.Begin(); loss.Complete(defeated); Equal(0, defeated.Result);
        Equal(false, loss.Data.Unit(0).Token.Occupied); Equal(1, loss.World.Casualties[1].Count);
    }

    private static void SinglePlayerCombatPrivateCasualtyEvent()
    {
        using var runtime = new SessionRuntime(new(), new(274));
        var f = new SinglePlayerCombatFixture(ExecutionWin(), new(runtime.Process, runtime.World));
        var entry = f.Event(EventTrigger.UnitKilled); ParticipantId human = default;
        runtime.RegisterCommand(new(new(1, "test.composed-combat", "Composed combat with casualty event", AccessClass.Player,
            new([]), new([new("result", ValueKind.Integer)])), (_, _, _) => new(),
            _ => new CombatOperation(f.Begin(), _ => human, _ => throw new InvalidOperationException("No intelligence"),
                _ => throw new InvalidOperationException("No capture"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var witness = SessionClient(runtime, 1); human = witness.Identity.Participant;
        var command = actor.Submit(new("test.composed-combat", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(witness, command); Equal(false, actor.Reply(prompt, 0).Accepted);
        Equal(null, RequiredResult(actor, command).Interaction); Equal(true, f.Data.Unit(1).Token.Occupied);
        Equal(1, f.Services.State.KillCount); Equal(1, f.World.Casualties[2].Count); Equal((sbyte)5, f.World.Entities.Cities.Storage[0].Size);
        Equal(true, witness.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
        var text = RequiredInteraction(witness, command); Equal(false, witness.Reply(prompt, 0).Accepted);
        Equal(false, entry.Values.Actions.Consumed); Equal(true, f.Data.Unit(1).Token.Occupied);
        Equal(true, witness.Reply(text, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(false, f.Data.Unit(1).Token.Occupied);
        Equal(true, entry.Values.Actions.Consumed); Equal((sbyte)4, f.World.Entities.Cities.Storage[0].Size);
        Equal(1, f.World.Casualties[2].Count); Equal(2, f.Published); f.Consumed();
    }

    private static void SinglePlayerCombatCaptureEvent()
    {
        var f = new SinglePlayerCombatFixture(); var entry = f.Event(EventTrigger.CityTaken);
        var point = f.World.Entities.Cities.Storage[0].Position;
        using var operation = f.Services.Capture.Begin(new(0), 1, 1, 77, "capture");
        var pending = operation.Resume(); Equal(true, pending is CityCaptureDecision.Lifecycle { Decision: ResearchGrantDecision.Government });
        Equal(new CivilizationSlot(1), f.World.Entities.Cities.Storage[0].Owner);
        Equal((byte?)2, f.World.Map.Owner(point)); Equal(false, entry.Values.Actions.Consumed);
        Equal(pending, operation.Resume()); Equal(true, operation.Resume(new(0)) is CityCaptureDecision.Lifecycle { Decision: ScenarioText });
        Equal(true, operation.Resume(new(0)) is CityCaptureDecision.Window); Equal(true, entry.Values.Actions.Consumed);
        Equal((byte?)1, f.World.Map.Owner(point)); Equal(null, operation.Resume(new(0))); Equal(true, operation.Completed);
        Equal((byte)41, f.World.Entities.Cities.Storage[0].TurnsCaptured); f.Consumed();
    }

    private static void SinglePlayerCombatResponseCaptureContact()
    {
        var choices = DropRolls().Select(c => c with { Key = c.Key with { Call = "response/paradrop" } }).ToList();
        choices.Add(CaptureParity(0) with { Key = new("C09.capture.building-parity", "response/paradrop/capture") });
        var f = new SinglePlayerCombatFixture(choices); f.Life.Life.Deletion.Erase(1, false);
        f.Data.AddCity(2, 1, f.Data.Unit(0).Position); f.Data.AddCity(3, 3, new(14, 8));
        f.World.Globals.CivsAlive = f.World.Globals.CivsAlive.With(3);
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(3); f.World.Multiplayer.LocalHuman = 3;
        f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Abilities = new() { Paradrop = true } };
        f.Data.Unit(0).Flags.Garrison = true;
        f.World.Events.Loaded = true;
        var permission = NegotiationEvent(f.World, 1, 3, deny: true);
        permission.Values.Actions = new() { Text = true, JustOnce = true };
        permission.SetString(OpenCiv2.Core.State.Event.TextBegin, new("Landing contact"));
        using var operation = f.Services.Response.Begin(1, new(12, 8), "response");
        var visit = operation.Resume(); Equal(true, visit is ParadropDecision.Capture { Decision: CityCaptureDecision.Window });
        Equal(new CivilizationSlot(1), f.World.Entities.Cities.Storage[0].Owner);
        Equal(true, f.Data.Unit(0).Flags.Garrison); Equal(visit, operation.Resume());
        var contact = operation.Resume(new(0));
        Equal(true, contact is ParadropDecision.Contact { Decision: DiplomaticContactDecision.Scenario { Value: ScenarioText { Recipient: 3 } } });
        Equal((byte)41, f.World.Entities.Cities.Storage[0].TurnsCaptured); Equal(true, f.Data.Unit(0).Flags.Garrison);
        Equal(contact, operation.Resume()); Equal(null, operation.Resume(new(0))); Equal(true, operation.Completed);
        Equal(new UnitSlot(0), operation.Paratrooper); Equal(false, f.Data.Unit(0).Flags.Garrison);
        Equal(new MapPoint(12, 8), f.Data.Unit(0).Position); Equal(true, permission.Values.Actions.Consumed); f.Consumed();
    }

    private static void SinglePlayerCombatNuclearDelivery()
    {
        foreach (bool intercept in new[] { false, true })
        {
            var f = new SinglePlayerCombatFixture(); f.F.F.Missile(); f.F.F.Ocean();
            if (intercept) BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.SdiDefense, true);
            using var operation = f.Begin(); f.Complete(operation); Equal(0, operation.Result);
            Equal(false, f.Data.Unit(0).Token.Occupied); Equal(intercept, f.Data.Unit(1).Token.Occupied);
            Equal((sbyte)(intercept ? 5 : 3), f.World.Entities.Cities.Storage[0].Size);
            Equal(0, f.World.Casualties[2].Count); Equal(false, f.F.Calls.Contains("advisor"));
        }
    }

    private static void SinglePlayerCombatFailureAndDisposal()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = new SinglePlayerCombatFixture(ExecutionWin()); var entry = f.Event(EventTrigger.UnitKilled);
            using var operation = f.Begin(); Equal(true, operation.Resume() is CombatDecision.Lifecycle);
            if (dispose) { operation.Dispose(); Throws<ObjectDisposedException>(() => operation.Resume(new(0))); }
            else
            {
                f.Life.Diplomacy.Grant.FailRevolution = true;
                Throws<InvalidOperationException>(() => operation.Resume(new(1))); Throws<InvalidOperationException>(() => operation.Resume());
            }
            Equal(true, f.Data.Unit(1).Token.Occupied); Equal(1, f.World.Casualties[2].Count);
            Equal(false, entry.Values.Actions.Consumed); Equal(false, f.F.Calls.Contains("advisor")); f.Consumed();
        }
        var cosmetic = new SinglePlayerCombatFixture(ExecutionWin()) { FailOpening = true };
        using var continued = cosmetic.Begin(); cosmetic.Complete(continued);
        Equal(1, continued.PresentationFailures.Count); Equal(1, continued.Result); Equal(false, cosmetic.Data.Unit(1).Token.Occupied);

        var choices = ExecutionWin(); choices.Add(Choice.Create(new("V03.emissary", "combat/resolution/loss/casualty/0/event/event/1/technology/grant"),
            new SamplingLaw.SecondaryRange(0, 2), ChoiceCode.Secondary(0)));
        var grant = new SinglePlayerCombatFixture(choices); var entry2 = grant.Event(EventTrigger.UnitKilled, library: false, text: false);
        grant.World.Globals.HumanPlayers = CivilizationSet.Only(1); grant.World.Civilizations[2].Treaties[1].Contact = true;
        grant.World.Diplomacy.EmissaryResponse = 0; grant.Life.Diplomacy.Grant.FailClose = true;
        // Field combat keeps this diagnostic case outside the AI city-production branch.
        var center = grant.World.Entities.Cities.Storage[0].Position;
        grant.World.Entities.Cities.Storage[0].Token = default;
        grant.World.Map.TileAt(center).Features &= ~MapFeatures.CitySite;
        grant.World.Civilizations[2].CityCount = 1; grant.World.Civilizations[2].TotalCitySize = 4;
        using var session = new CombatOperation(grant.Begin(), c => new((ulong)c), _ => throw new InvalidOperationException("No intelligence"),
            _ => throw new InvalidOperationException("No capture"));
        var context = grant.Life.Diplomacy.Events.Context(); Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new()).Status);
        Equal(true, grant.Data.Unit(1).Token.Occupied);
        var result = session.Resume(context, new(OperationInputKind.Cancel)); Equal(CommandStatus.Failed, result.Status);
        Equal("combat_presentation_failed", result.Code); Equal(false, grant.Data.Unit(1).Token.Occupied);
        Equal(true, entry2.Values.Actions.Consumed); Equal(result, session.Resume(context, new())); grant.Consumed();
    }
}
