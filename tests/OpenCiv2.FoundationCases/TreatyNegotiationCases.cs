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
    private sealed class TreatyFixture : ITreatyNegotiationEffects
    {
        public TechnologyFixture T { get; }
        public GameState World => T.World;
        public RevelationFixture Data => T.T.Data;
        public NegotiatedTreaties Treaties { get; }
        public TreatyNegotiation Negotiation { get; }
        public WarNotices WarEffects { get; } = new();
        public List<TreatyNegotiationNotice> Notices { get; } = [];
        public List<NegotiatedTreatyNotice> Agreements { get; } = [];
        public bool FailAgreement, FailNotice, FailRefresh;
        public int Refreshed;
        public TreatyFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            T = new(choices, data);
            World.Globals.Turn = 10; World.Diplomacy.Attitude = 20; World.Civilizations[2].Attitude[1] = 20;
            World.Diplomacy.PeaceInclination = 10;
            var government = new GovernmentChanges(World, Data.Process.Rules, Data.Stacks,
                _ => throw new InvalidOperationException("Unexpected government choice"), _ => throw new InvalidOperationException("Unexpected rates"), _ => { });
            var stance = new DiplomaticStance(World, Data.Process.Rules, new(World, Data.Process.Rules, government, T.T.Random));
            Treaties = new(World, stance, n => { Agreements.Add(n); if (FailAgreement) { throw new InvalidOperationException("Missing agreement notice"); } });
            Negotiation = new(World, Data.Process.Rules, T.T.Random, stance, T.T.Research, Treaties, War(Data, WarEffects, T.T.Random), this);
        }
        public TreatyNegotiation.Operation Begin(bool alliance = true) => Negotiation.Begin(1, 2, alliance, "treaty");
        public void Notice(TreatyNegotiationNotice notice)
        { Notices.Add(notice); if (FailNotice) { throw new InvalidOperationException("Missing treaty refusal"); } }
        public void RefreshStatus() { ++Refreshed; if (FailRefresh) { throw new InvalidOperationException("Missing treaty status"); } }
        public void Wonder(int wonder)
        { Data.AddCity(1, 1, new(20, 8)); World.Globals.WonderCity[wonder] = 1; }
    }
    private static Choice TreatyPayment(string kind, uint raw = 0) => Choice.Create(new("V05.treaty-" + kind, "treaty"),
        new SamplingLaw.SecondaryRange(0, 1), ChoiceCode.Secondary(raw));

    private static void NegotiatedTreatyWrappers()
    {
        foreach (var kind in Enum.GetValues<NegotiatedTreatyKind>())
        {
            var f = new TreatyFixture(); var w = f.World;
            w.Civilizations[2].Attitude[1] = 80; w.Diplomacy.Attitude = 80; w.Civilizations[2].DiploRequestCount = 5;
            w.Civilizations[1].Treaties[2] = w.Civilizations[2].Treaties[1] = new() { War = true, Tribute = true, Aggressor = true, ExtendedAggressor = true };
            for (int c = 0; c < 8; ++c) { w.Civilizations[c].Treaties[1].Aggressor = w.Civilizations[c].Treaties[1].ExtendedAggressor = true; }
            w.Multiplayer.Saved.AutoplayActive = true;
            if (kind == NegotiatedTreatyKind.Alliance) { f.Treaties.FormAlliance(1, 2, "wrapper"); }
            else if (kind == NegotiatedTreatyKind.Peace) { f.Treaties.SignPeace(1, 2, "wrapper"); }
            else { f.Treaties.AgreeCeasefire(1, 2, "wrapper"); }
            Equal(kind, f.Agreements.Single().Kind); Equal((byte)(kind == NegotiatedTreatyKind.Alliance ? 55 : 50), w.Civilizations[2].Attitude[1]);
            Equal((sbyte)(kind == NegotiatedTreatyKind.Ceasefire ? 5 : 0), w.Civilizations[2].DiploRequestCount);
            Equal(w.Globals.Turn, w.Civilizations[1].LastContactTurn[2]); Equal(false, w.Civilizations[1].Treaties[2].War);
            Equal(kind == NegotiatedTreatyKind.Alliance, w.Civilizations[1].Flags.HasAllied && w.Civilizations[2].Flags.HasAllied);
            Equal(kind != NegotiatedTreatyKind.Ceasefire, w.Civilizations[1].Treaties[2].Peace);
            Equal(kind != NegotiatedTreatyKind.Ceasefire, w.Civilizations[1].Treaties[2].Tribute);
            Equal(kind != NegotiatedTreatyKind.Alliance, w.Civilizations[2].Treaties[1].ChangedThisTurn);
            Equal(false, w.Civilizations[1].Treaties[2].Contact); // No implicit first contact.
            Equal(true, w.Civilizations[0].Treaties[1].Aggressor); Equal(kind != NegotiatedTreatyKind.Ceasefire, w.Civilizations[7].Treaties[1].Aggressor);
            Equal(true, w.Civilizations[7].Treaties[1].ExtendedAggressor); f.T.T.Random.RequireChoicesConsumed();
        }
        var failed = new TreatyFixture(); failed.FailAgreement = true; failed.World.Civilizations[7].Treaties[1].Aggressor = true;
        Throws<InvalidOperationException>(() => failed.Treaties.AgreeCeasefire(1, 2, "failure"));
        Equal(true, failed.World.Civilizations[1].Treaties[2].Ceasefire); Equal(true, failed.World.Civilizations[7].Treaties[1].Aggressor);
        var hotseat = new TreatyFixture(); hotseat.World.Globals.Multiplayer = GameMode.Hotseat;
        hotseat.World.Multiplayer.HotseatParticipants = CivilizationSet.Only(2); hotseat.World.Civilizations[2].Attitude[1] = 90;
        hotseat.Treaties.SignPeace(1, 2, "hotseat"); Equal((byte)90, hotseat.World.Civilizations[2].Attitude[1]);
    }

    private static void TreatyPeaceReasons()
    {
        var f = new TreatyFixture(); var w = f.World;
        Equal(PeaceReasons.Ordinary, f.Negotiation.PeaceWillingness(1, 2));
        w.Diplomacy.TargetIsLeader = true; Equal(PeaceReasons.None, f.Negotiation.PeaceWillingness(1, 2));
        w.Diplomacy.WarTarget = 3; w.Civilizations[1].Treaties[3].Contact = true;
        w.Civilizations[2].Treaties[3].Fighting = true; w.Civilizations[2].MilitaryStrength = 20; w.Civilizations[3].MilitaryStrength = 39;
        Equal(PeaceReasons.None, f.Negotiation.PeaceWillingness(1, 2));
        w.Civilizations[3].MilitaryStrength = 40; Equal(PeaceReasons.Ordinary | PeaceReasons.DangerousEnemy, f.Negotiation.PeaceWillingness(1, 2));
        w.Civilizations[1].Treaties[3].Peace = true; w.Civilizations[3].MilitaryStrength = 30;
        Equal(PeaceReasons.Ordinary | PeaceReasons.DangerousEnemy, f.Negotiation.PeaceWillingness(1, 2));
        w.Civilizations[1].Treaties[3].Alliance = true;
        Equal(PeaceReasons.Ordinary | PeaceReasons.DangerousEnemy | PeaceReasons.ExistingAlliedTarget, f.Negotiation.PeaceWillingness(1, 2));
        w.Diplomacy.Attitude = 76; Equal(PeaceReasons.None, f.Negotiation.PeaceWillingness(1, 2));
        f.Wonder(WonderIds.GreatWall); Equal(PeaceReasons.Wonder, f.Negotiation.PeaceWillingness(1, 2));
        w.Globals.WonderCity[WonderIds.GreatWall] = -1; f.Wonder(WonderIds.UnitedNations);
        Equal(PeaceReasons.Wonder, f.Negotiation.PeaceWillingness(1, 2));
        w.Globals.WonderCity[WonderIds.UnitedNations] = -1; w.Diplomacy.WarTarget = null; w.Diplomacy.TargetIsLeader = false; w.Diplomacy.Attitude = 20;
        w.Civilizations[1].Reputation = 6; Equal(PeaceReasons.None, f.Negotiation.PeaceWillingness(1, 2));
        w.Globals.Difficulty = Difficulty.Chieftain; Equal(PeaceReasons.Ordinary, f.Negotiation.PeaceWillingness(1, 2));
        w.Civilizations[2].Treaties[1].Vendetta = true; Equal(PeaceReasons.None, f.Negotiation.PeaceWillingness(1, 2));
    }

    private static void TreatyGoldPricingAndPayment()
    {
        foreach (bool alliance in new[] { false, true })
        {
            var f = new TreatyFixture(); var w = f.World;
            w.Civilizations[1].Gold = 1000; w.Civilizations[1].MilitaryStrength = 20; w.Civilizations[2].MilitaryStrength = 40;
            w.Civilizations[1].Reputation = 2;
            Equal(alliance ? 850 : 750, f.Negotiation.GoldPrice(1, 2, alliance));
            w.Civilizations[1].MilitaryStrength = 80; Equal(alliance ? 1000 : 800, f.Negotiation.GoldPrice(1, 2, alliance));
            w.Civilizations[1].Gold = 49; Equal(0, f.Negotiation.GoldPrice(1, 2, alliance));
            w.Civilizations[1].Gold = -1; Equal(true, f.Negotiation.GoldPrice(1, 2, alliance) < 0);
        }
        foreach (bool accept in new[] { false, true })
        {
            var f = new TreatyFixture([TreatyPayment("gold")]); f.World.Civilizations[2].DiploRequestCount = 2;
            f.World.Civilizations[1].Gold = 1000; f.FailRefresh = true;
            using var op = f.Begin(); var offer = (TreatyNegotiationDecision.Gold)op.Resume()!;
            Equal(450, offer.Price); // 20*20/41 = 9 units.
            f.World.Civilizations[1].Gold = 0; // Accepted payment has no second affordability check.
            op.Resume(new(accept ? 1 : 0)); Equal(true, op.Completed);
            Equal(accept ? -450 : 0, f.World.Civilizations[1].Gold); Equal(accept ? 1227 : 777, f.World.Civilizations[2].Gold);
            Equal(accept, f.World.Civilizations[1].Treaties[2].Alliance); Equal(accept ? 1 : 0, op.PresentationFailures.Count);
            Equal(accept ? 1 : 0, f.Agreements.Count); Equal(false, op.EndsAudience); f.T.T.Random.RequireChoicesConsumed();
        }
    }

    private static void TreatyTechnologyLiveValuation()
    {
        var f = new TreatyFixture([TreatyPayment("secret", uint.MaxValue)]); var w = f.World;
        f.T.Know(1, AdvanceIds.Monarchy, 5); f.T.Know(1, 94, 9); f.T.Know(1, 95, 3);
        w.Diplomacy.BestTechnology = AdvanceIds.Monarchy; w.Diplomacy.SecondTechnology = 95; w.Civilizations[2].DiploRequestCount = 2;
        w.Globals.Difficulty = Difficulty.Chieftain; w.Diplomacy.Attitude = 60; w.Civilizations[2].Attitude[1] = 60;
        w.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); w.Multiplayer.LocalHuman = 2; w.Civilizations[2].Government = Government.Despotism;
        using var op = f.Begin(); var offer = (TreatyNegotiationDecision.Technology)op.Resume()!; Equal(1, offer.Variant);
        Equal(true, op.Resume(new(1)) is TreatyNegotiationDecision.Grant { Decision: ResearchGrantDecision.Government });
        Equal(true, w.Civilizations[2].Advances.Contains(AdvanceIds.Monarchy)); Equal(false, w.Civilizations[1].Treaties[2].Alliance);
        w.Diplomacy.BestTechnology = 94; f.FailAgreement = true;
        Throws<InvalidOperationException>(() => op.Resume(new(0)));
        Equal((byte)17, w.Civilizations[2].Attitude[1]); // 60 - 2*9, then alliance's -25.
        Equal(95, w.Diplomacy.BestTechnology); Equal((int?)null, w.Diplomacy.SecondTechnology);
        Equal(false, w.Civilizations[2].Advances.Contains(94)); Equal(true, w.Civilizations[1].Treaties[2].Alliance);
        Equal((sbyte)0, w.Civilizations[2].DiploRequestCount); Throws<InvalidOperationException>(() => op.Resume()); f.T.T.Random.RequireChoicesConsumed();
        var zero = new TreatyFixture([TreatyPayment("gold")]); zero.T.Know(1, 0); zero.World.Diplomacy.BestTechnology = 0;
        zero.World.Civilizations[2].DiploRequestCount = 2;
        using var noTech = zero.Begin(); Equal(true, noTech.Resume() is TreatyNegotiationDecision.Gold); noTech.Resume(new(0)); zero.T.T.Random.RequireChoicesConsumed();
    }

    private static void TreatySolidarityPaymentsAndRefusal()
    {
        foreach (bool alliance in new[] { false, true })
            foreach (bool pay in new[] { false, true })
            {
                var f = new TreatyFixture(pay ? [TreatyPayment("secret")] : []); var w = f.World;
                w.Diplomacy.WarTarget = 3; w.Globals.PowerRank[1] = 7; w.Globals.Difficulty = Difficulty.Chieftain;
                if (pay)
                { f.T.Know(1, 94); w.Diplomacy.BestTechnology = 94; w.Civilizations[2].DiploRequestCount = 2; }
                using var op = f.Begin(alliance); var pending = op.Resume();
                if (pay) { Equal(true, pending is TreatyNegotiationDecision.Technology); pending = op.Resume(new(1)); }
                Equal(true, pending is TreatyNegotiationDecision.Solidarity); op.Resume(new(0)); Equal(true, op.Completed);
                Equal(!pay, op.EndsAudience); Equal(0, f.Agreements.Count); Equal(pay, w.Civilizations[2].Advances.Contains(94));
                Equal(pay ? TreatyNegotiationNoticeKind.ThanksAnyway : TreatyNegotiationNoticeKind.Goodbye, f.Notices.Single().Kind);
                Equal((sbyte)(pay ? 3 : 1), w.Civilizations[2].DiploRequestCount); f.T.T.Random.RequireChoicesConsumed();
            }
        var permissive = new TreatyFixture(); permissive.World.Diplomacy.WarTarget = 3;
        using var ordinary = permissive.Begin(false); Equal(true, ordinary.Resume() is TreatyNegotiationDecision.Solidarity); ordinary.Resume(new(0));
        Equal(NegotiatedTreatyKind.Peace, permissive.Agreements.Single().Kind); Equal(false, ordinary.EndsAudience);
    }

    private static void TreatyWonderCompulsionAfterRefusal()
    {
        var f = new TreatyFixture([TreatyPayment("gold")]); var w = f.World;
        f.Wonder(WonderIds.GreatWall); w.Diplomacy.WarTarget = 3; w.Diplomacy.TargetIsLeader = true;
        w.Civilizations[1].Treaties[3].Contact = true; w.Civilizations[2].Treaties[3].Fighting = true;
        w.Civilizations[3].MilitaryStrength = 40; w.Civilizations[2].DiploRequestCount = 2;
        using var op = f.Begin(false); Equal(true, op.Resume() is TreatyNegotiationDecision.Gold);
        Equal(true, op.Resume(new(0)) is TreatyNegotiationDecision.Solidarity); op.Resume(new(0));
        Equal(true, op.Completed); Equal(NegotiatedTreatyKind.Peace, f.Agreements.Single().Kind);
        Equal(0, f.Notices.Count); Equal(5000, w.Civilizations[1].Gold); f.T.T.Random.RequireChoicesConsumed();
    }

    private static void TreatySolidarityActualWar()
    {
        foreach (bool alliance in new[] { false, true })
        {
            var f = new TreatyFixture(); var w = f.World;
            w.Globals.Turn = 101; w.Globals.PowerRank[3] = 5;
            w.Civilizations[1].Treaties[3].Contact = true; w.Diplomacy.WarTarget = 3;
            using var op = f.Begin(alliance); Equal(true, op.Resume() is TreatyNegotiationDecision.Solidarity); op.Resume(new(1));
            Equal(true, op.Completed); Equal(true, w.Civilizations[1].Treaties[3].War);
            Equal(true, w.Civilizations[3].Treaties[1].Vendetta); Equal(alliance, w.Civilizations[2].Treaties[3].War);
            Equal(alliance, w.Civilizations[2].Treaties[3].Contact); Equal(alliance, w.Civilizations[2].Treaties[3].ExtendedAggressor);
            Equal(alliance ? NegotiatedTreatyKind.Alliance : NegotiatedTreatyKind.Peace, f.Agreements.Single().Kind);
            Equal((sbyte)1, w.Civilizations[2].ReputationCredit[1]); // Actual mediated war.
            f.T.T.Random.RequireChoicesConsumed();
        }
    }

    private static void TreatyRefusalBranches()
    {
        foreach (bool alliance in new[] { false, true })
        {
            var f = new TreatyFixture(); var w = f.World;
            w.Civilizations[1].Reputation = 7; w.Diplomacy.Attitude = 99; w.Diplomacy.Fear = 0;
            using var weak = f.Begin(alliance); weak.Resume(); Equal(TreatyNegotiationNoticeKind.NoBetrayWeak, f.Notices.Single().Kind);
            Equal((sbyte)1, w.Civilizations[2].DiploRequestCount); Equal(0, f.Agreements.Count);
            f = new(); f.World.Civilizations[2].Treaties[1].Vendetta = true; f.World.Civilizations[1].Reputation = 7;
            using var betrayal = f.Begin(alliance); betrayal.Resume(); Equal(TreatyNegotiationNoticeKind.NoBetray, f.Notices.Single().Kind);
        }
        var small = new TreatyFixture(); small.World.Diplomacy.PeaceInclination = -10; small.World.Globals.PowerRank[2] = 4;
        using var noSmall = small.Begin(); noSmall.Resume(); Equal(TreatyNegotiationNoticeKind.NoSmall, small.Notices.Single().Kind);
        var leader = new TreatyFixture(); leader.World.Diplomacy.TargetIsLeader = true;
        using var noPeace = leader.Begin(false); noPeace.Resume(); Equal(TreatyNegotiationNoticeKind.NoWinning, leader.Notices.Single().Kind);
        var failure = new TreatyFixture(); failure.World.Civilizations[1].Reputation = 7;
        failure.World.Civilizations[2].DiploRequestCount = 127; failure.FailNotice = true;
        using var required = failure.Begin(); Throws<InvalidOperationException>(() => required.Resume());
        Equal((sbyte)-128, failure.World.Civilizations[2].DiploRequestCount); Equal(0, failure.Agreements.Count);
    }

    private static void TreatyPrivateSessionGrant()
    {
        using var runtime = new SessionRuntime(new(), new(254));
        var f = new TreatyFixture([TreatyPayment("secret")], CreationFixture(new(runtime.Process, runtime.World)));
        f.T.Know(1, AdvanceIds.Monarchy); f.World.Diplomacy.BestTechnology = AdvanceIds.Monarchy;
        f.World.Civilizations[2].DiploRequestCount = 2; f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
        f.World.Multiplayer.LocalHuman = 2; f.World.Civilizations[2].Government = Government.Despotism;
        Dictionary<int, ParticipantId> recipients = [];
        runtime.RegisterCommand(new(new(1, "test.treaty", "Authored treaty negotiation", AccessClass.Player, new([]), new([new("endsAudience", ValueKind.Boolean)])),
            (_, _, _) => new(), _ => new TreatyNegotiationOperation(f.Begin(), c => recipients[c],
                _ => throw new InvalidOperationException("Unexpected intelligence"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 1); var partner = SessionClient(runtime, 2);
        recipients[1] = actor.Identity.Participant; recipients[2] = partner.Identity.Participant;
        var command = actor.Submit(new("test.treaty", new([]))).Correlation.Command; runtime.AdvanceOne();
        var payment = RequiredInteraction(actor, command); Equal(true, actor.Reply(payment, 1).Accepted); runtime.AdvanceOne();
        var government = RequiredInteraction(partner, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(false, actor.Reply(government, 0).Accepted); Equal(true, partner.Reply(government, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
        Equal(true, f.World.Civilizations[1].Treaties[2].Alliance); Equal(true, f.World.Civilizations[2].Advances.Contains(AdvanceIds.Monarchy));
        f.T.T.Random.RequireChoicesConsumed();
    }

    private static void TreatySessionCancellationAndDisposal()
    {
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
            for (int stage = 0; stage < 3; ++stage)
            {
                var f = new TreatyFixture(stage < 2 ? [TreatyPayment(stage == 0 ? "secret" : "gold")] : []);
                if (stage == 0) { f.T.Know(1, 94); f.World.Diplomacy.BestTechnology = 94; }
                if (stage < 2) { f.World.Civilizations[2].DiploRequestCount = 2; }
                else { f.World.Diplomacy.WarTarget = 3; f.World.Globals.PowerRank[1] = 7; f.World.Globals.Difficulty = Difficulty.Chieftain; }
                var context = new OperationContext(f.World, f.Data.Process, f.T.T.Random, new PresentationRandom(1, "treaty"),
                    new(default, new(1)), default, (_, _) => { }, () => { });
                using var op = new TreatyNegotiationOperation(f.Begin(), c => new((ulong)c),
                    _ => throw new InvalidOperationException("Unexpected intelligence"));
                Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
                Equal(CommandStatus.Completed, op.Resume(context, new(input)).Status);
                Equal(false, f.World.Civilizations[1].Treaties[2].Alliance); Equal(5000, f.World.Civilizations[1].Gold);
                Equal(false, f.World.Civilizations[2].Advances.Contains(94)); f.T.T.Random.RequireChoicesConsumed();
            }
        var stopped = new TreatyFixture([TreatyPayment("secret")]); stopped.T.Know(1, 94);
        stopped.World.Diplomacy.BestTechnology = 94; stopped.World.Civilizations[2].DiploRequestCount = 2;
        using var pending = stopped.Begin(); pending.Resume(); pending.Dispose();
        Throws<ObjectDisposedException>(() => pending.Resume(new(1))); Equal(false, stopped.World.Civilizations[2].Advances.Contains(94));
    }
}
