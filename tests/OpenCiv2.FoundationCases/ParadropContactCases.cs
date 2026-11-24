using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // U05 §9 placement -> W03/V04 contact -> city notification/garrison/status.
    // Actual production lifecycle/diplomacy and S02 permission events own the nested choices.
    private sealed class ParadropContactFixture
    {
        public SinglePlayerLifecycleFixture Life { get; }
        public ParadropFixture F { get; }
        public RevelationFixture Data => F.Data;
        public GameState World => F.World;
        public Event Permission { get; }
        public ParadropContactFixture(RevelationFixture? data = null)
        {
            Life = new(data: data);
            F = new(DropRolls(), Life.Data, revelation: Life.Services.Revelation);
            Permission = DropContact(Life, DropTarget);
            Data.Unit(0).Flags.Garrison = true;
        }
        public void Consumed() { F.Capture.Life.Random.RequireChoicesConsumed(); Life.Consumed(); }
    }
    private static Event DropContact(SinglePlayerLifecycleFixture f, MapPoint target)
    {
        f.World.Globals.Turn = 41;
        f.World.Civilizations[1].Government = f.World.Civilizations[2].Government = Government.Despotism;
        f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        f.World.Multiplayer.LocalHuman = 2;
        f.World.Civilizations[1].Treaties[2].Contact = false;
        f.Data.AddUnit(10, 2, 10, new(target.X + 1, target.Y - 1));
        f.World.Events.Loaded = true;
        var permission = NegotiationEvent(f.World, 1, 2, deny: true);
        permission.Values.Actions = new() { Text = true, JustOnce = true };
        permission.SetString(Event.TextBegin, new("Paratrooper contact"));
        return permission;
    }

    private static void ParadropRetainedContactOrder()
    {
        var f = new ParadropContactFixture(); var token = f.Data.Unit(0).Token;
        using var operation = f.F.Begin(); var pending = operation.Resume();
        Equal(true, pending is ParadropDecision.Contact { Decision: DiplomaticContactDecision.Scenario { Value: ScenarioText } });
        Equal(true, operation.Landed); Equal(false, operation.Completed);
        Equal(DropTarget, f.Data.Unit(0).Position); Equal(true, f.Data.Unit(0).Flags.Paradropped);
        Equal(true, f.Data.Unit(0).Flags.Garrison); Equal(false, f.F.Effects.Contains("city:0"));
        Equal(true, f.World.Map.TileAt(DropTarget).RevealedTo.Contains(1));
        var random = f.F.Capture.Life.Random.Capture();
        Equal(pending, operation.Resume()); Equal(random, f.F.Capture.Life.Random.Capture()); Equal(token, f.Data.Unit(0).Token);
        // A real contact pause permits later actor state to change; the continuing actor remains slot zero.
        f.World.Globals.MovingUnit = new(10); f.Data.Unit(0).Damage = 3;
        f.F.OnChanged = () => Equal(true, f.Data.Unit(0).Flags.Garrison);
        Equal(null, operation.Resume(new(0))); Equal(true, operation.Completed);
        Equal(false, f.Data.Unit(0).Flags.Garrison); Equal((byte)3, f.Data.Unit(0).Damage);
        Equal("city:0,status", string.Join(',', f.F.Effects.TakeLast(2)));
        Equal(true, f.Permission.Values.Actions.Consumed); Equal(null, operation.Resume(new(0)));
        Equal(1, f.F.Effects.Count(e => e == "status")); f.Consumed();

        var library = new ParadropContactFixture(); library.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); library.World.Multiplayer.LocalHuman = 1; ScenarioLibrary(library.Life.Diplomacy.Grant);
        library.Permission.Values.Actions.GiveTechnology = true;
        library.Permission.Values.GiveTechCiv = 2; library.Permission.Values.GiveTech = AdvanceIds.Monarchy;
        using var grant = library.F.Begin();
        Equal(true, grant.Resume() is ParadropDecision.Contact { Decision: DiplomaticContactDecision.Scenario { Value: ResearchGrantDecision.Government } });
        Equal(true, library.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
        Equal(false, library.F.Effects.Contains("status"));
        Equal(true, grant.Resume(new(0)) is ParadropDecision.Contact { Decision: DiplomaticContactDecision.Scenario { Value: ScenarioText } });
        Equal(null, grant.Resume(new(0))); Equal(true, grant.Completed); library.Consumed();
    }

    private static void ParadropContactPrivateReplies()
    {
        foreach (bool cancel in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(cancel ? 272UL : 271UL));
            var f = new ParadropContactFixture(new(runtime.Process, runtime.World)); ParticipantId human = default;
            runtime.RegisterCommand(new(new(1, "test.drop-contact", "Paradrop with retained contact", AccessClass.Player,
                new([]), new([new("landed", ValueKind.Boolean)])), (_, _, _) => new(),
                _ => new ParadropOperation(f.F.Begin(), _ => human, _ => throw new InvalidOperationException("No captured city"))));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 1); var witness = SessionClient(runtime, 2); human = witness.Identity.Participant;
            var command = actor.Submit(new("test.drop-contact", new([]))).Correlation.Command; runtime.AdvanceOne();
            var prompt = RequiredInteraction(witness, command); Equal(false, actor.Reply(prompt, 0).Accepted);
            Equal(null, RequiredResult(actor, command).Interaction); Equal(true, f.Data.Unit(0).Flags.Garrison);
            if (cancel) Equal(true, actor.Cancel(command).Accepted); else Equal(true, witness.Reply(prompt, 0).Accepted);
            runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
            Equal(false, f.Data.Unit(0).Flags.Garrison); Equal(true, f.Permission.Values.Actions.Consumed);
            Equal(false, witness.Reply(prompt, 0).Accepted); Equal(1, f.F.Effects.Count(e => e == "status")); f.Consumed();
        }
        var timeout = new ParadropContactFixture();
        using var session = new ParadropOperation(timeout.F.Begin(), c => new((ulong)c), _ => throw new InvalidOperationException("No city"));
        var context = timeout.Life.Diplomacy.Events.Context();
        Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new()).Status);
        var completed = session.Resume(context, new(OperationInputKind.Timeout)); Equal(CommandStatus.Completed, completed.Status);
        Equal(completed, session.Resume(context, new())); timeout.Consumed();
    }

    private static void ParadropContactFailureAndDisposal()
    {
        var stopped = new ParadropContactFixture();
        using (var operation = stopped.F.Begin())
        {
            operation.Resume(); operation.Dispose(); Throws<ObjectDisposedException>(() => operation.Resume(new(0)));
            Equal(DropTarget, stopped.Data.Unit(0).Position); Equal(true, stopped.Data.Unit(0).Flags.Garrison);
            Equal(false, stopped.Permission.Values.Actions.Consumed); Equal(false, stopped.F.Effects.Contains("status")); stopped.Consumed();
        }
        var failed = new ParadropContactFixture(); failed.Permission.Values.Actions.PlayWaveFile = true;
        failed.Permission.SetString(34, new("drop.wav")); failed.World.Globals.GameOptions.Sound = true;
        failed.Life.Diplomacy.Events.Media.FailProbe = true;
        using (var operation = failed.F.Begin())
        {
            Throws<IOException>(() => operation.Resume()); Throws<InvalidOperationException>(() => operation.Resume());
            Equal(true, operation.Landed); Equal(false, operation.Completed);
            Equal(true, failed.Data.Unit(0).Flags.Garrison); Equal(false, failed.F.Effects.Contains("status")); failed.Consumed();
        }
        var cosmetic = new ParadropContactFixture(); cosmetic.World.Events.Loaded = false;
        cosmetic.Life.Diplomacy.Grant.FailClose = true;
        using var session = new ParadropOperation(cosmetic.F.Begin(), c => new((ulong)c), _ => throw new InvalidOperationException("No city"));
        var context = cosmetic.Life.Diplomacy.Events.Context();
        Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new()).Status);
        var result = session.Resume(context, new(OperationInputKind.Cancel));
        Equal(CommandStatus.Failed, result.Status); Equal("paradrop_presentation_failed", result.Code);
        Equal(false, cosmetic.Data.Unit(0).Flags.Garrison); Equal(true, cosmetic.F.Effects.Contains("status"));
        Equal(result, session.Resume(context, new())); Equal(1, cosmetic.Life.Diplomacy.Grant.Closed); cosmetic.Consumed();
    }

    private static void CombatResponseRetainsParadropContact()
    {
        foreach (bool cosmetic in new[] { false, true })
        {
            var choices = DropRolls().Select(c => c with { Key = c.Key with { Call = "finish/response/paradrop" } }).ToList();
            var life = new SinglePlayerLifecycleFixture();
            var f = new ContinuationFixture(choices, life.Data, revelation: life.Services.Revelation);
            f.ClearDefender(); f.Data.AddCity(2, 1, f.Data.Unit(0).Position);
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Abilities = new() { Paradrop = true } };
            // Keep the target city owned by the attacker so this isolates response -> drop -> contact.
            f.World.Entities.Cities.Storage[0].Owner = new(1); f.World.Map.TileAt(f.Prepared.Target).Owner = 1;
            DropContact(life, f.Prepared.Target); f.Data.Unit(0).Flags.Garrison = true;
            if (cosmetic) { f.World.Events.Loaded = false; life.Diplomacy.Grant.FailClose = true; }
            using var session = new CombatContinuationOperation(f.Continuation.Survive(f.Prepared, "finish"), c => new((ulong)c),
                _ => throw new InvalidOperationException("No captured city"));
            var context = life.Diplomacy.Events.Context(); var prompt = session.Resume(context, new());
            Equal(CommandStatus.AwaitingInteraction, prompt.Status); Equal(new ParticipantId(2), prompt.Interaction!.Recipient);
            Equal(true, f.Data.Unit(0).Flags.Paradropped && f.Data.Unit(0).Flags.Garrison);
            var result = session.Resume(context, new(cosmetic ? OperationInputKind.Cancel : OperationInputKind.Choice, 0));
            Equal(cosmetic ? CommandStatus.Failed : CommandStatus.Completed, result.Status);
            if (cosmetic) Equal("combat_continuation_presentation_failed", result.Code);
            Equal(new CommandValue.Integer(0), result.Result!.Single().Value); Equal(false, f.Data.Unit(0).Flags.Garrison);
            Equal(result, session.Resume(context, new())); f.Life.Random.RequireChoicesConsumed(); life.Consumed();
        }
    }

    private static void UnitTacticsRetainsParadropContact()
    {
        foreach (bool direct in new[] { false, true })
            foreach (bool cosmetic in new[] { false, true })
            {
                var rolls = DropRolls().Select(c => c with { Key = c.Key with { Call = "tactics/paratrooper/paradrop" } }).ToList();
                rolls.Add(Choice.Create(new("C09.capture.building-parity", "tactics/paratrooper/paradrop/capture"),
                    new SamplingLaw.PrimaryModulo(2), ChoiceCode.Primary(0)));
                var life = new SinglePlayerLifecycleFixture();
                var f = new TacticsFixture(data: life.Data, paraChoices: rolls, revelation: life.Services.Revelation);
                f.City(f.Point); f.City(new(14, 10), 2, 1, 5); f.City(new(30, 10), 2, 2);
                f.Rules.Units[4] = f.Rules.Units[4] with { Abilities = new() { Paradrop = true } };
                f.World.Civilizations[1].Treaties[2].War = true;
                DropContact(life, new(14, 10)); f.Para.Capture.OnTaken = (_, _, _) => { };
                if (cosmetic) { f.World.Events.Loaded = false; life.Diplomacy.Grant.FailClose = true; }
                using IContinuation session = direct
                    ? new ParatrooperTacticsOperation(new ParatrooperTactics(f.World, f.Rules, f.Data.Stacks, f.Para.Drop)
                        .Begin(ParaContext(f.Para), "tactics/paratrooper"), c => new((ulong)c), _ => throw new InvalidOperationException("AI capture has no window"))
                    : new UnitTacticsOperation(f.Begin(), c => new((ulong)c), _ => throw new InvalidOperationException("AI capture has no window"));
                var context = life.Diplomacy.Events.Context(); OperationStep result = new();
                for (int i = 0; i < 50; ++i)
                {
                    result = session.Resume(context, new());
                    if (result.Status == CommandStatus.AwaitingInteraction) break;
                    Equal(CommandStatus.Pending, result.Status);
                }
                Equal(CommandStatus.AwaitingInteraction, result.Status); Equal(new ParticipantId(2), result.Interaction!.Recipient);
                Equal(new MapPoint(14, 10), f.Point); Equal(new CivilizationSlot(1), f.World.Entities.Cities.Storage[1].Owner);
                result = session.Resume(context, new(cosmetic ? OperationInputKind.Cancel : OperationInputKind.Choice, 0));
                Equal(cosmetic ? CommandStatus.Failed : CommandStatus.Completed, result.Status);
                if (cosmetic) Equal(direct ? "paratrooper_presentation_failed" : "unit_tactics_presentation_failed", result.Code);
                if (direct) Equal(new CommandValue.Boolean(true), result.Result!.Single(v => v.Name == "landed").Value);
                else Equal(new CommandValue.Integer(0), result.Result!.Single().Value);
                Equal(true, f.Para.Effects.Contains("status")); Equal(result, session.Resume(context, new()));
                f.Random.RequireChoicesConsumed(); f.Para.Capture.Life.Random.RequireChoicesConsumed(); life.Consumed();
            }
    }

    private static void CombatSessionCooperativeRounds()
    {
        using var runtime = new SessionRuntime(new(), new(273));
        var f = new ExecutionFixture([ExecutionRoll("attack", 80, 79), ExecutionRoll("defense", 4, 0),
            ExecutionRoll("attack", 80, 79, 1), ExecutionRoll("defense", 4, 0, 1), ExecutionPromotion()],
            new(runtime.Process, runtime.World));
        f.F.Data.Rules.Units[0] = f.F.Data.Rules.Units[0] with { Firepower = 5 };
        ParticipantId human = default;
        runtime.RegisterCommand(new(new(1, "test.combat-cooperative", "Bounded local combat", AccessClass.Player,
            new([]), new([new("result", ValueKind.Integer)])), (_, _, _) => new(),
            _ => new CombatOperation(f.Begin(budget: 1), _ => human,
                _ => throw new InvalidOperationException("No intelligence"), _ => throw new InvalidOperationException("No capture"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 1); human = actor.Identity.Participant;
        var command = actor.Submit(new("test.combat-cooperative", new([]))).Correlation.Command;
        runtime.AdvanceOne(); Equal(CommandStatus.Pending, RequiredResult(actor, command).Status);
        Equal((byte)5, f.F.Data.Unit(1).Damage); Equal(null, RequiredResult(actor, command).Interaction);
        for (int i = 0; i < 10 && !RequiredResult(actor, command).Status.Terminal(); ++i) runtime.AdvanceOne();
        var result = RequiredResult(actor, command); Equal(CommandStatus.Completed, result.Status);
        Equal(new CommandValue.Integer(1), result.Result.Single().Value); Equal(1, f.State.KillCount);
        Equal((byte)3, f.F.Data.Unit(0).MovesUsed); Equal(false, f.F.Data.Unit(1).Token.Occupied);
        runtime.AdvanceOne(); Equal(1, f.State.KillCount); f.F.Life.Random.RequireChoicesConsumed();
    }

}
