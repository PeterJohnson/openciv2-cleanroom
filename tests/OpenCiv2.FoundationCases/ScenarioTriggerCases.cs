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
    // S02 §7/§11; actual dispatcher/actions, U06 casualty and C09 capture children.
    private static Event TriggerMoney(EventFixture f, EventTrigger kind, int amount = 1)
    {
        var e = f.World.Events.Collection.Append(); e.Values.Trigger = kind;
        e.Values.Actions.ChangeMoney = true; e.Values.MoneyReceiverCiv = 1; e.Values.MoneyAmount = amount;
        return e;
    }
    private static void FinishTrigger(ScenarioTriggers.Operation op, int result)
    {
        Equal(null, op.Resume()); Equal(result, op.Result); Equal(null, op.Resume(new(0)));
    }
    private static void ScenarioTurnTriggerScans()
    {
        var f = new EventFixture(); int gold = f.World.Civilizations[1].Gold;
        var first = f.Text(); first.Values.Trigger = EventTrigger.Turn; first.Values.Turn = -1;
        var second = TriggerMoney(f, EventTrigger.Turn); second.Values.Turn = 12;
        TriggerMoney(f, EventTrigger.TurnInterval, 100).Values.Turn = 0;
        using (var op = f.Triggers.BeginTurn(12, "turn"))
        {
            Equal(true, op.Resume() is ScenarioText); f.World.Globals.Turn = 99;
            TriggerMoney(f, EventTrigger.Turn, 2).Values.Turn = 12;
            Equal(null, op.Resume(new(0))); Equal(1, op.Result); Equal(gold + 3, f.World.Civilizations[1].Gold);
        }
        first.Values.Trigger = EventTrigger.None;
        using (var op = f.Triggers.BeginTurn(99, "miss")) { FinishTrigger(op, 0); }
        using (var op = f.Triggers.BeginInterval(7, "zero")) { FinishTrigger(op, 1); }
        var interval = f.World.Events.Collection.At(2); interval.Values.Turn = -3;
        using (var op = f.Triggers.BeginInterval(-6, "signed")) { FinishTrigger(op, 1); }
        using (var op = f.Triggers.BeginInterval(7, "remainder")) { FinishTrigger(op, 0); }
        interval.Values.Turn = -1;
        using (var op = f.Triggers.BeginInterval(int.MinValue, "overflow"))
        { Throws<OverflowException>(() => op.Resume()); Throws<InvalidOperationException>(() => op.Resume()); }
        TriggerMoney(f, EventTrigger.ScenarioLoaded, 10);
        f.World.Events.Loaded = false; f.World.Globals.GameOptions.Music = false;
        using (var op = f.Triggers.BeginLoaded("loaded")) { FinishTrigger(op, 1); }
        Equal(gold + 213, f.World.Civilizations[1].Gold); f.Consumed();
    }
    private static void ScenarioRandomTriggerScans()
    {
        var choices = new List<Choice>
        {
            Choice.Create(new("S02.random-turn", "roll/event/2"), new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(3)),
            Choice.Create(new("S02.random-turn", "roll/event/3"), new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(4)),
            Choice.Create(new("S02.random-turn", "consumed/event/3"), new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(4))
        };
        var f = new EventFixture(choices: choices); int gold = f.World.Civilizations[1].Gold;
        TriggerMoney(f, EventTrigger.RandomTurn).Values.Denominator = 1;
        TriggerMoney(f, EventTrigger.RandomTurn, 10).Values.Denominator = 5;
        var once = TriggerMoney(f, EventTrigger.RandomTurn, 100); once.Values.Denominator = 5; once.Values.Actions.JustOnce = true;
        TriggerMoney(f, EventTrigger.RandomTurn, 1000).Values.Denominator = 0;
        TriggerMoney(f, EventTrigger.RandomTurn, 10000).Values.Denominator = -1;
        using (var op = f.Triggers.BeginRandom("roll")) { FinishTrigger(op, 1); }
        Equal(gold + 101, f.World.Civilizations[1].Gold); Equal(true, once.Values.Actions.Consumed);
        f.World.Events.Collection.At(0).Values.Denominator = 0; f.World.Events.Collection.At(1).Values.Denominator = -2;
        using (var op = f.Triggers.BeginRandom("consumed")) { FinishTrigger(op, 1); }
        Equal(gold + 101, f.World.Civilizations[1].Gold); f.Consumed();
    }
    private static void ScenarioStickyTriggerBindings()
    {
        var f = new EventFixture(); var e = TriggerMoney(f, EventTrigger.UnitKilled);
        e.Values.UnitType = 4; e.Values.WhoCiv = e.Values.WhomCiv = -2; e.Values.Actions.JustOnce = e.Values.Actions.Consumed = true;
        using (var op = f.Triggers.BeginKilled(new(new(5), 2, 1), "wrong")) { FinishTrigger(op, 0); }
        Equal(-2, e.Values.WhoCiv); Equal(-2, e.Values.WhomCiv);
        using (var op = f.Triggers.BeginKilled(new(new(4), 2, 1), "bind")) { FinishTrigger(op, 1); }
        Equal(2, e.Values.WhoCiv); Equal(1, e.Values.WhomCiv);
        using (var op = f.Triggers.BeginKilled(new(new(4), 3, 1), "sticky")) { FinishTrigger(op, 0); }
        var first = f.Text(); first.Values.Trigger = EventTrigger.CityTaken; first.SetString(1, new("Birch")); first.Values.WhoCiv = first.Values.WhomCiv = -2;
        var next = TriggerMoney(f, EventTrigger.CityTaken, 9); next.SetString(1, new("OAK")); next.Values.WhoCiv = next.Values.WhomCiv = -2;
        var any = TriggerMoney(f, EventTrigger.CityTaken, 99); any.SetString(1, new("AnyCity")); any.Values.WhoCiv = any.Values.WhomCiv = -2;
        string name = "bIrCh"; int gold = f.World.Civilizations[1].Gold;
        using (var op = f.Triggers.BeginTaken(() => name, 1, 2, "city"))
        {
            Equal(true, op.Resume() is ScenarioText); Equal(1, first.Values.WhoCiv); Equal(2, first.Values.WhomCiv);
            name = "Oak"; Equal(null, op.Resume(new(0))); Equal(1, op.Result);
        }
        Equal(gold + 9, f.World.Civilizations[1].Gold); Equal(1, next.Values.WhoCiv); Equal(-2, any.Values.WhoCiv); f.Consumed();
    }
    private static void ScenarioTechnologyTriggerPolling()
    {
        var f = new EventFixture(); var e = f.Text(); e.Values.Trigger = EventTrigger.ReceivedTechnology;
        e.Values.WhomCiv = -2; e.Values.Technology = 54; e.Values.Actions.JustOnce = true;
        f.World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3);
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(54); f.World.Civilizations[2].Advances = default(AdvanceSet).With(54);
        using (var op = f.Triggers.BeginTechnology("poll"))
        {
            Equal(true, op.Resume() is ScenarioText); Equal(1, e.Values.WhomCiv);
            // First wildcard branch continues, checking live knowledge and Alive after actions.
            f.World.Globals.CivsAlive = f.World.Globals.CivsAlive.With(2, false).With(4);
            f.World.Civilizations[3].Advances = default(AdvanceSet).With(55); f.World.Civilizations[4].Advances = default(AdvanceSet).With(55);
            e.Values.Technology = 55;
            Equal(null, op.Resume(new(0))); Equal(1, op.Result); Equal(4, e.Values.WhomCiv); Equal(true, e.Values.Actions.Consumed);
        }
        using (var op = f.Triggers.BeginTechnology("again")) { FinishTrigger(op, 1); }
        e.Values.WhomCiv = 33; f.World.Globals.CivsAlive = CivilizationSet.Only(1);
        using (var op = f.Triggers.BeginTechnology("invalid")) { Throws<ArgumentOutOfRangeException>(() => op.Resume()); }
        e.Values.Technology = -1;
        using (var op = f.Triggers.BeginTechnology("sentinel")) { FinishTrigger(op, 1); }
        e.Values.Technology = -2;
        using (var op = f.Triggers.BeginTechnology("none")) { FinishTrigger(op, 0); }
        f.Consumed();
    }
    private static void ScenarioTriggerFailureAndLifetime()
    {
        var f = new EventFixture(); var e = f.Text(); e.Values.Trigger = EventTrigger.ScenarioLoaded;
        e.Values.Actions.ChangeMoney = true; e.Values.MoneyReceiverCiv = 1; e.Values.MoneyAmount = 7;
        e.Values.Actions.JustOnce = true; int gold = f.World.Civilizations[1].Gold;
        using (var op = f.Triggers.BeginLoaded("dispose")) { op.Resume(); }
        Equal(gold + 7, f.World.Civilizations[1].Gold); Equal(false, e.Values.Actions.Consumed);
        using (var op = f.Triggers.BeginLoaded("reset"))
        {
            op.Resume(); f.World.Events.Collection.Reset(); Throws<InvalidOperationException>(() => op.Resume(new(0)));
            Equal(true, e.Values.Actions.Consumed); Throws<InvalidOperationException>(() => op.Resume());
        }
        var fault = f.Text(); fault.Values.Trigger = EventTrigger.ScenarioLoaded; fault.Values.Actions.PlayWaveFile = true;
        fault.SetString(34, new("bad.wav")); f.Media.FailProbe = true; f.World.Globals.GameOptions.Sound = true;
        using (var op = f.Triggers.BeginLoaded("failure")) { Throws<IOException>(() => op.Resume()); Equal(null, op.Result); }
        f.World.Globals.Multiplayer = GameMode.Tcp;
        using (var op = f.Triggers.BeginLoaded("network")) { Throws<InvalidOperationException>(() => op.Resume()); }
    }
    private static void ScenarioCasualtyRetainedTrigger()
    {
        var f = new EventFixture(CargoFixture(4, 4, 4)); var casualty = new CasualtyFixture(f.Triggers, f.Data);
        f.World.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
        f.Data.Unit(0).Owner = new(1); f.Data.Unit(0).Type = new(4); f.Data.Unit(0).Home = null;
        var e = f.Library(); e.Values.Trigger = EventTrigger.UnitKilled; e.Values.UnitType = 4;
        e.Values.WhoCiv = e.Values.WhomCiv = -2; e.Values.Actions.JustOnce = true;
        f.World.Events.Loaded = true;
        using var op = casualty.Single();
        Equal(typeof(ResearchGrantDecision.Government), op.Resume()?.GetType()); Equal(true, f.Data.Unit(0).Token.Occupied);
        Equal((byte)1, f.World.Civilizations[1].UnitsLost[4]); Equal(1, casualty.State.KillCount); Equal(1, f.World.Casualties[1].Count);
        Equal(2, e.Values.WhoCiv); Equal(1, e.Values.WhomCiv); Equal(false, e.Values.Actions.Consumed);
        Equal(null, op.Resume(0)); casualty.Complete(op); Equal(false, f.Data.Unit(0).Token.Occupied);
        Equal(true, e.Values.Actions.Consumed); Equal(true, f.Data.Unit(1).Token.Occupied); f.Consumed();
    }
    private static Event CaptureTrigger(EventFixture f)
    {
        var e = f.Text(); e.Values.Trigger = EventTrigger.CityTaken; e.SetString(1, new("Birch"));
        e.Values.WhoCiv = e.Values.WhomCiv = -2; e.Values.Actions.JustOnce = true;
        e.Values.Actions.GiveTechnology = true; e.Values.GiveTechCiv = 2; e.Values.GiveTech = 54;
        f.Data.AddCity(2, 1, new(30, 20)); f.World.Civilizations[1].CityCount = 1;
        f.World.Globals.WonderCity[WonderIds.GreatLibrary] = 2;
        f.World.Civilizations[3].Advances = f.World.Civilizations[3].Advances.With(54);
        f.World.Globals.AdvanceKnownBy[54] = CivilizationSet.Only(3);
        f.World.Events.Loaded = true;
        return e;
    }
    private static void ScenarioCaptureRetainedTrigger()
    {
        var f = new EventFixture(); var capture = new CityCaptureFixture(data: f.Data, events: f.Triggers);
        var e = CaptureTrigger(f); var center = capture.City.Position;
        using var op = capture.Begin();
        Equal(true, op.Resume() is CityCaptureDecision.Lifecycle { Decision: ResearchGrantDecision.Government });
        Equal(new CivilizationSlot(1), capture.City.Owner); Equal((byte?)2, f.World.Map.Owner(center));
        Equal((byte)1, f.World.Map.TileAt(center).Claimant); Equal(false, e.Values.Actions.Consumed);
        Equal(0, capture.Notices.Count(n => n.Kind == CaptureNoticeKind.Capture));
        Equal(true, op.Resume(new(0)) is CityCaptureDecision.Lifecycle { Decision: ScenarioText });
        Equal(false, e.Values.Actions.Consumed); Equal(true, op.Resume(new(0)) is CityCaptureDecision.Window);
        Equal((byte?)1, f.World.Map.Owner(center)); Equal(true, e.Values.Actions.Consumed);
        Equal(1, capture.Notices.Count(n => n.Kind == CaptureNoticeKind.Capture));
        op.Resume(new(0)); capture.Complete(op); f.Consumed();
    }
    private static void ScenarioTriggerCallerPresentation()
    {
        var f = new EventFixture(CargoFixture(4, 4, 4),
            [Choice.Create(new("V03.emissary", "combat/casualty/0/event/event/1/technology/grant"),
                new SamplingLaw.SecondaryRange(0, 2), ChoiceCode.Secondary(0))]);
        var casualty = new CasualtyFixture(f.Triggers, f.Data);
        f.World.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
        f.Data.Unit(0).Owner = new(1); f.Data.Unit(0).Type = new(4); f.Data.Unit(0).Home = null;
        f.World.Globals.RevealMap = 0; f.World.Diplomacy.EmissaryResponse = 0;
        f.World.Civilizations[1].Treaties[2] = f.World.Civilizations[2].Treaties[1] = new() { Contact = true };
        f.World.Events.Loaded = true; f.Grant.FailClose = true;
        var e = f.World.Events.Collection.Append(); e.Values.Trigger = EventTrigger.UnitKilled; e.Values.UnitType = 4;
        e.Values.WhoCiv = e.Values.WhomCiv = -2; e.Values.Actions.JustOnce = true;
        e.Values.Actions.GiveTechnology = true; e.Values.GiveTechCiv = 2; e.Values.GiveTech = 54;
        using (var operation = casualty.Single())
        using (var adapter = new CombatCasualtyOperation(operation, c => new((ulong)c)))
        {
            Equal(CommandStatus.AwaitingInteraction, adapter.Resume(f.Context(), new()).Status);
            Equal(true, f.Data.Unit(0).Token.Occupied); Equal(false, e.Values.Actions.Consumed);
            var done = adapter.Resume(f.Context(), new(OperationInputKind.Cancel));
            Equal(CommandStatus.Failed, done.Status); Equal("combat_casualties_presentation_failed", done.Code);
            Equal(true, operation.Completed); Equal(1, operation.PresentationFailures.Count);
            Equal(false, f.Data.Unit(0).Token.Occupied); Equal(true, e.Values.Actions.Consumed);
            Equal(done, adapter.Resume(f.Context(), new())); f.Consumed();
        }
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var local = new EventFixture(); var capture = new CityCaptureFixture(data: local.Data, events: local.Triggers);
            var entry = CaptureTrigger(local);
            using var operation = capture.Begin();
            using var adapter = new CityCaptureOperation(operation, c => new((ulong)c),
                _ => new("Captured city", new(1), null, new([0])));
            Equal(CommandStatus.AwaitingInteraction, adapter.Resume(local.Context(), new()).Status);
            Equal(CommandStatus.AwaitingInteraction, adapter.Resume(local.Context(), new(input)).Status);
            Equal(false, entry.Values.Actions.Consumed);
            Equal(CommandStatus.AwaitingInteraction, adapter.Resume(local.Context(), new(input)).Status);
            Equal(true, entry.Values.Actions.Consumed);
            Equal(CommandStatus.Completed, adapter.Resume(local.Context(), new(input)).Status);
            Equal(true, operation.Completed); local.Consumed();
        }
    }
    private static void ScenarioTriggerPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(255)); var f = new EventFixture(new(runtime.Process, runtime.World));
        var e = f.Library(); e.Values.Trigger = EventTrigger.ScenarioLoaded; e.Values.Actions.JustOnce = true;
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.scenario-trigger", "Scenario trigger", AccessClass.Player, new([]), new([new("result", ValueKind.Integer)])),
            (_, _, _) => new(), _ => new ScenarioTriggerOperation(f.Triggers.BeginLoaded("session"), _ => recipient)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 1); recipient = human.Identity.Participant;
        var command = actor.Submit(new("test.scenario-trigger", new([]))).Correlation.Command; runtime.AdvanceOne();
        var grant = RequiredInteraction(human, command); Equal(false, actor.Reply(grant, 0).Accepted); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(true, human.Reply(grant, 0).Accepted); runtime.AdvanceOne(); Equal(false, human.Reply(grant, 0).Accepted);
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(true, e.Values.Actions.Consumed); f.Consumed();
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var local = new EventFixture(); var entry = local.Text(); entry.Values.Trigger = EventTrigger.ScenarioLoaded; entry.Values.Actions.JustOnce = true;
            using var adapter = new ScenarioTriggerOperation(local.Triggers.BeginLoaded("cancel"), c => new((ulong)c));
            Equal(CommandStatus.AwaitingInteraction, adapter.Resume(local.Context(), new()).Status);
            Equal(CommandStatus.Completed, adapter.Resume(local.Context(), new(input)).Status); Equal(true, entry.Values.Actions.Consumed);
        }
    }
}
