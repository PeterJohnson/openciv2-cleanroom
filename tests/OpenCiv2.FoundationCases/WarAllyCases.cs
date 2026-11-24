using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class WarAllyFixture
    {
        public AllyHelpFixture Aid { get; }
        public GrantFixture Grant { get; }
        public RetainedGrantFixture Retained { get; }
        public GameState World => Aid.World;
        public WarAllyRequests Requests { get; }
        public WarAllyFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Aid = new(choices, data);
            Grant = new(Aid.Data, Aid.Random);
            Retained = new(Grant);
            World.Globals.Turn = 8;
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3).With(4);
            World.Civilizations[2].Treaties[1].Alliance = false;
            World.Civilizations[1].Treaties[2].Contact = true;
            World.Civilizations[1].Treaties[3].Contact = true;
            World.Civilizations[2].Treaties[3].Fighting = true;
            World.Civilizations[3].MilitaryStrength = 40;
            for (int c = 0; c < EntityLimits.Civilizations; ++c) { World.Civilizations[c].Researching = -1; }
            Requests = new(World, Aid.Data.Process.Rules, Aid.Emissaries, Retained.Grants,
                War(Aid.Data, Aid.WarEffects, Aid.Random), Aid.RefreshStatus);
        }
        public WarAllyRequests.Operation Begin() => Requests.Begin(2, 1, "seek");
        public WarAllyOffer Offer(WarAllyRequests.Operation op)
        {
            Equal(true, op.Resume() is WarAllyDecision.Emissary { Decision: EmissaryDecision.Receive });
            return ((WarAllyDecision.Offer)op.Resume(0)!).Request;
        }
        public void Complete(WarAllyRequests.Operation op)
        {
            for (int i = 0; i < 50 && !op.Completed; ++i) { Equal(null, op.Resume()); }
            Equal(true, op.Completed); Aid.Random.RequireChoicesConsumed();
        }
    }

    // A08 §9 contract cases with actual V05, V03 and V04 children.
    private static void WarAllyPaidAndUnpaidAcceptance()
    {
        foreach (bool paid in new[] { false, true })
            foreach (bool alliedEnemy in new[] { false, true })
            {
                var f = new WarAllyFixture([GreetingChoice("seek/3/emissary/greet")]); var w = f.World;
                w.Civilizations[3].MilitaryStrength = (ushort)(paid ? 40 : 30);
                w.Civilizations[1].Treaties[3].Alliance = w.Civilizations[3].Treaties[1].Alliance = alliedEnemy;
                using var op = f.Begin(); var offer = f.Offer(op);
                Equal(paid, offer.Paid); Equal(alliedEnemy, offer.AgainstAlly); Equal(paid ? 500 : 0, offer.Payment);
                Equal((int?)null, offer.Advance); Equal(offer, ((WarAllyDecision.Offer)op.Resume()!).Request);
                op.Resume(7); f.Complete(op); op.Resume();
                Equal(true, op.Requested && op.Helped);
                Equal(true, w.Civilizations[1].Treaties[2].Alliance && w.Civilizations[2].Treaties[1].Alliance);
                Equal(true, w.Civilizations[1].Treaties[3].War && w.Civilizations[3].Treaties[1].War);
                Equal(true, w.Civilizations[1].Treaties[3].Aggressor && w.Civilizations[1].Treaties[3].ExtendedAggressor);
                Equal(false, w.Civilizations[1].Treaties[3].Alliance);
                Equal((short)8, w.Civilizations[1].LastContactTurn[3]); Equal((short)24, w.Civilizations[1].LastContactTurn[2]);
                Equal(paid ? 500 : 1000, w.Civilizations[2].Gold); Equal(paid ? 510 : 10, w.Civilizations[1].Gold);
                Equal(paid ? 1 : 0, f.Aid.Status); Equal(1, f.Aid.Closed);
                Equal(true, w.Civilizations[2].Treaties[3].Fighting); // Requested enemy bypasses cleanup.
                Equal(false, w.Diplomacy.MeetingActive || w.Diplomacy.AskedEmissary);
            }
    }

    private static void WarAllyAdmissionAndEnemyCleanup()
    {
        foreach (string gate in new[] { "turn", "distrust", "alliance", "war", "hostile", "sender-contact", "enemy-contact", "strength", "ranks" })
        {
            var f = new WarAllyFixture(); var w = f.World;
            w.Civilizations[2].Treaties[4].Fighting = true; w.Civilizations[2].Treaties[5].Fighting = true;
            w.Civilizations[2].Treaties[1].Fighting = w.Civilizations[2].Treaties[2].Fighting = true;
            if (gate == "turn") w.Globals.Turn = 9;
            if (gate == "distrust") w.Civilizations[1].Reputation = 6;
            if (gate == "alliance") w.Civilizations[2].Treaties[1].Alliance = true;
            if (gate == "war") w.Civilizations[2].Treaties[1].War = true;
            if (gate == "hostile") w.Civilizations[1].Treaties[3].War = true;
            if (gate == "sender-contact") w.Civilizations[1].Treaties[2].Contact = false;
            if (gate == "enemy-contact") w.Civilizations[1].Treaties[3].Contact = false;
            if (gate == "strength") w.Civilizations[3].MilitaryStrength = 20;
            if (gate == "ranks")
            { w.Civilizations[3].MilitaryStrength = 30; w.Globals.PowerRank[1] = w.Globals.PowerRank[2] = 6; }
            using var op = f.Begin(); f.Complete(op); Equal(false, op.Requested);
            Equal(gate is "turn" or "distrust", w.Civilizations[2].Treaties[3].Fighting);
            Equal(gate is "turn" or "distrust", w.Civilizations[2].Treaties[4].Fighting);
            Equal(true, w.Civilizations[2].Treaties[5].Fighting);
            Equal(true, w.Civilizations[2].Treaties[1].Fighting && w.Civilizations[2].Treaties[2].Fighting);
            Equal(0, f.Aid.Closed);
        }
        var remote = new WarAllyFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var rejected = remote.Begin(); Throws<InvalidOperationException>(() => rejected.Resume());
        Equal(true, remote.World.Civilizations[2].Treaties[3].Fighting);
    }

    private static void WarAllyRefusalContactAndFirstEnemy()
    {
        foreach (bool receive in new[] { false, true })
            foreach (short old in new short[] { -100, 100 })
            {
                var f = new WarAllyFixture(receive ? [GreetingChoice("seek/3/emissary/greet")] : []);
                f.World.Civilizations[1].LastContactTurn[2] = old;
                f.World.Civilizations[2].Treaties[4].Fighting = true;
                f.World.Civilizations[4].MilitaryStrength = 100;
                f.World.Civilizations[1].Treaties[4].Contact = true;
                using var op = f.Begin(); Equal(true, op.Resume() is WarAllyDecision.Emissary);
                if (receive) { Equal(true, op.Resume(0) is WarAllyDecision.Offer); op.Resume(0); }
                else { op.Resume(1); }
                f.Complete(op);
                Equal((short)Math.Max(old, receive ? -6 : -7), f.World.Civilizations[1].LastContactTurn[2]);
                Equal(true, op.Requested); Equal(false, op.Helped); Equal(1, f.Aid.Closed);
                Equal(true, f.World.Civilizations[2].Treaties[3].Fighting && f.World.Civilizations[2].Treaties[4].Fighting);
                Equal(1000, f.World.Civilizations[2].Gold);
            }
    }

    private static void WarAllyBorrowedTechnologyAndGovernment()
    {
        var f = new WarAllyFixture([GreetingChoice("seek/3/emissary/greet")]);
        ExchangeKnowledge(f.Grant, 3, AdvanceIds.Monarchy);
        f.Grant.Rules.Advances[AdvanceIds.Monarchy] = f.Grant.Rules.Advances[AdvanceIds.Monarchy] with { Identity = new("monarchy", "Monarchy") };
        using var op = f.Begin(); var offer = f.Offer(op);
        Equal((int?)AdvanceIds.Monarchy, offer.Advance); Equal("Monarchy", offer.AdvanceName);
        Equal(true, op.Resume(1) is WarAllyDecision.Grant { Decision: ResearchGrantDecision.Government });
        Equal((byte)3, f.World.Civilizations[2].AdvanceSource[AdvanceIds.Monarchy]);
        Equal((byte)2, f.World.Civilizations[1].AdvanceSource[AdvanceIds.Monarchy]);
        Equal(false, f.World.Civilizations[1].Treaties[2].Alliance); Equal(1000, f.World.Civilizations[2].Gold);
        Equal(op.Pending, op.Resume());
        op.Resume(1); f.Complete(op);
        Equal(Government.Anarchy, f.World.Civilizations[1].Government);
        Equal((byte)1, f.World.Civilizations[1].NumAdvances); Equal((byte)1, f.World.Civilizations[2].NumAdvances);
        Equal(true, op.Helped); Equal(500, f.World.Civilizations[2].Gold);
    }

    private static void WarAllyTechnologyIntelligenceAndTies()
    {
        foreach (bool senderKnows in new[] { false, true })
            foreach (string intelligence in new[] { "none", "embassy", "un", "marco", "partner-only" })
            {
                var f = new WarAllyFixture([GreetingChoice("seek/3/emissary/greet")]); var w = f.World;
                int owner = senderKnows ? 2 : 3;
                ExchangeKnowledge(f.Grant, owner, 94); ExchangeKnowledge(f.Grant, owner, 95);
                w.Multiplayer.LocalHuman = 4; // Intelligence gates are local-human gates, not partner gates.
                w.Globals.HumanPlayers = CivilizationSet.Only(1).With(4);
                if (intelligence == "embassy") w.Civilizations[4].Treaties[2].Embassy = true;
                if (intelligence == "partner-only") w.Civilizations[1].Treaties[2].Embassy = true;
                if (intelligence is "un" or "marco")
                {
                    f.Aid.Data.AddCity(0, 4, new(10, 8));
                    w.Globals.WonderCity[intelligence == "un" ? WonderIds.UnitedNations : WonderIds.MarcoPolo] = 0;
                }
                using var op = f.Begin(); var offer = f.Offer(op);
                Equal(senderKnows || intelligence is "none" or "partner-only" ? (int?)94 : null, offer.Advance);
                op.Resume(0); f.Complete(op);
            }
    }

    private static void WarAllyPaymentBoundsAndCosmeticFailures()
    {
        foreach (var (gold, payment) in new[] { (0, 50), (199, 50), (200, 100), (int.MaxValue, 500) })
        {
            var f = new WarAllyFixture([GreetingChoice("seek/3/emissary/greet")]);
            f.World.Civilizations[2].Gold = gold;
            using var op = f.Begin(); Equal(payment, f.Offer(op).Payment);
            op.Resume(1); f.Complete(op); Equal(unchecked(gold - payment), f.World.Civilizations[2].Gold);
            Equal(10 + payment, f.World.Civilizations[1].Gold);
        }
        foreach (string failure in new[] { "status", "close" })
        {
            var f = new WarAllyFixture([GreetingChoice("seek/3/emissary/greet")]); f.Aid.Failure = failure;
            using var op = f.Begin(); f.Offer(op); op.Resume(1); f.Complete(op);
            Equal(true, op.Helped); Equal(1, op.PresentationFailures.Count); Equal(500, f.World.Civilizations[2].Gold);
            Equal(false, f.World.Diplomacy.AskedEmissary); Equal(1, f.Aid.Closed);
        }
    }

    private static void WarAllyRequiredFailureAndDisposal()
    {
        var f = new WarAllyFixture([GreetingChoice("seek/3/emissary/greet")]);
        ExchangeKnowledge(f.Grant, 2, AdvanceIds.Monarchy); f.Retained.FailRevolution = true;
        using var op = f.Begin(); f.Offer(op);
        Equal(true, op.Resume(1) is WarAllyDecision.Grant);
        Throws<InvalidOperationException>(() => op.Resume(1));
        Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
        Equal(Government.Despotism, f.World.Civilizations[1].Government); // V03's required notice precedes the setter.
        Equal(false, f.World.Civilizations[1].Treaties[2].Alliance); Equal(1000, f.World.Civilizations[2].Gold);
        Equal(false, op.Helped); Throws<InvalidOperationException>(() => op.Resume());
        var stopped = new WarAllyFixture([GreetingChoice("seek/3/emissary/greet")]);
        using var pending = stopped.Begin(); stopped.Offer(pending); pending.Dispose();
        Throws<ObjectDisposedException>(() => pending.Resume(1));
        Equal(false, stopped.World.Civilizations[1].Treaties[3].War); Equal(1000, stopped.World.Civilizations[2].Gold);
    }

    private static void WarAllyDiplomacySessionPrivacy()
    {
        foreach (bool failure in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(239));
            var f = new WarAllyFixture([
                Choice.Create(new("V04.turn-random", "turn"), new SamplingLaw.PrimaryModulo(100), ChoiceCode.Primary(25)),
            Choice.Create(new("V04.turn-senate", "turn"), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(1)),
            GreetingChoice("turn/ally/1/3/emissary/greet")
            ], CreationFixture(new(runtime.Process, runtime.World)));
            f.Aid.Failure = failure ? "status" : null;
            ExchangeKnowledge(f.Grant, 2, AdvanceIds.Monarchy);
            ParticipantId recipient = default; var effects = new DiplomacyTurnChildren();
            var turn = new DiplomacyTurn(f.World, f.Grant.Rules, f.Aid.Random,
                new(f.World, f.Aid.Data.Process.Rules, f.Aid.Random, f.Retained.Grants), f.Requests, effects);
            runtime.RegisterCommand(new(new(1, "test.war-ally", "Authored diplomacy turn", AccessClass.Player, new([]), new([new("completed", ValueKind.Boolean)])),
                (_, _, _) => new(), _ => new DiplomacyTurnOperation(turn.Begin(2, "turn"), _ => recipient,
                    _ => throw new InvalidOperationException("No intelligence requested"))));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 1);
            recipient = human.Identity.Participant;
            var command = actor.Submit(new("test.war-ally", new([]))).Correlation.Command; runtime.AdvanceOne();
            var prompt = RequiredInteraction(human, command); Equal(null, RequiredResult(actor, command).Interaction);
            Equal(false, actor.Reply(prompt, 0).Accepted); Equal(true, human.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
            prompt = RequiredInteraction(human, command); Equal(true, human.Reply(prompt, 1).Accepted); runtime.AdvanceOne();
            prompt = RequiredInteraction(human, command); Equal(null, RequiredResult(actor, command).Interaction);
            Equal(true, human.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
            Equal(failure ? CommandStatus.Failed : CommandStatus.Completed, RequiredResult(actor, command).Status);
            Equal(true, f.World.Civilizations[1].Treaties[3].War); Equal(510, f.World.Civilizations[1].Gold);
            Equal(false, effects.Calls.Any(c => c.StartsWith("seek/")));
            Equal(1, f.Aid.Closed); f.Aid.Random.RequireChoicesConsumed();
        }
    }

    private static void WarAllySessionCancellation()
    {
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
            foreach (int stage in new[] { 0, 1, 2 })
            {
                var choices = new List<Choice>
            {
                Choice.Create(new("V04.turn-random", "turn"), new SamplingLaw.PrimaryModulo(100), ChoiceCode.Primary(25)),
                Choice.Create(new("V04.turn-senate", "turn"), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(1))
            };
                if (stage > 0) choices.Add(GreetingChoice("turn/ally/1/3/emissary/greet"));
                var f = new WarAllyFixture(choices); ExchangeKnowledge(f.Grant, 2, AdvanceIds.Monarchy);
                f.World.Civilizations[1].LastContactTurn[2] = -100;
                var turn = new DiplomacyTurn(f.World, f.Grant.Rules, f.Aid.Random,
                    new(f.World, f.Aid.Data.Process.Rules, f.Aid.Random, f.Retained.Grants), f.Requests, new DiplomacyTurnChildren());
                var context = new OperationContext(f.World, f.Aid.Data.Process, f.Aid.Random, new PresentationRandom(1, "war-ally"),
                    new(default, new(2)), default, (_, _) => { }, () => { });
                using var operation = new DiplomacyTurnOperation(turn.Begin(2, "turn"), c => new((ulong)c),
                    _ => throw new InvalidOperationException("No intelligence requested"));
                Equal(CommandStatus.AwaitingInteraction, operation.Resume(context, new()).Status);
                if (stage > 0) Equal(CommandStatus.AwaitingInteraction, operation.Resume(context, new(OperationInputKind.Choice, 0)).Status);
                if (stage > 1) Equal(CommandStatus.AwaitingInteraction, operation.Resume(context, new(OperationInputKind.Choice, 1)).Status);
                Equal(CommandStatus.Completed, operation.Resume(context, new(input)).Status);
                Equal(stage == 2, f.World.Civilizations[1].Treaties[3].War);
                Equal(stage == 2 ? 500 : 1000, f.World.Civilizations[2].Gold);
                Equal((short)(stage == 0 ? -7 : stage == 1 ? -6 : 24), f.World.Civilizations[1].LastContactTurn[2]);
                Equal(Government.Despotism, f.World.Civilizations[1].Government);
                Equal(1, f.Aid.Closed); f.Aid.Random.RequireChoicesConsumed();
            }
    }
}
