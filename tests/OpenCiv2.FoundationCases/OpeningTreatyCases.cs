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
    private sealed class OpeningTreatyFixture : IOpeningTreatyEffects
    {
        public TreatyFixture F { get; }
        public GameState World => F.World;
        public RevelationFixture Data => F.Data;
        public OpeningTreaties Service { get; }
        public List<OpeningTreatyNotice> Notices { get; } = [];
        public int Refreshes;
        public bool FailRefresh;
        public OpeningTreatyNoticeKind? FailNotice;
        public OpeningTreatyFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            F = new(choices, data);
            World.Civilizations[1].Government = Government.Despotism; World.Civilizations[2].CityCount = 2;
            World.Diplomacy.AskedEmissary = World.Diplomacy.InitialEvaluationDone = World.Diplomacy.AiMeetingActive = true;
            World.Diplomacy.EmissaryResponse = 1;
            var government = new GovernmentChanges(World, Data.Process.Rules, Data.Stacks,
                _ => throw new InvalidOperationException("Unexpected government selection"), _ => throw new InvalidOperationException("Unexpected rates"), _ => { });
            var stance = new DiplomaticStance(World, Data.Process.Rules, new(World, Data.Process.Rules, government, F.T.T.Random));
            Service = new(World, Data.Rules, F.T.T.Random, new(World, Data.Rules, F.T.T.Random, stance, F.T),
                new(World, Data.Rules, _ => throw new InvalidOperationException("Senate query has no attack notice")), F.T.T.Research,
                War(Data, F.WarEffects, F.T.T.Random), F.Treaties, this);
        }
        public OpeningTreaties.Operation Begin(OpeningTreatyInputs? inputs = null) => Service.Begin(1, 2, new(10, 8), inputs ?? new(false, false, false, false), "opening");
        public void NoPeace(bool war = false) => World.Civilizations[1].Treaties[2] = World.Civilizations[2].Treaties[1] = new() { Contact = true, War = war };
        public void Wonder(int wonder) { Data.AddCity(5, 2, new(24, 8)); World.Globals.WonderCity[wonder] = 5; }
        public void Notice(OpeningTreatyNotice notice)
        { Notices.Add(notice); if (FailNotice == notice.Kind) { throw new InvalidOperationException("Required opening notice failed"); } }
        public void RefreshStatus() { ++Refreshes; if (FailRefresh) { throw new InvalidOperationException("Cosmetic status failed"); } }
        public OpeningTreatyOffer Offer(OpeningTreaties.Operation op) => ((OpeningTreatyDecision.Offer)op.Pending!).Value;
        public void Done(OpeningTreaties.Operation op)
        { Equal(true, op.Completed); Equal(true, World.Diplomacy.AiMeetingActive); F.T.T.Random.RequireChoicesConsumed(); }
    }
    private static Choice OpeningRoll(string site, int bound, int value) => Choice.Create(new("V05.opening-" + site, "opening"),
        new SamplingLaw.PrimaryModulo(bound), ChoiceCode.Primary(value));

    private static void OpeningAllianceAcceptanceAndSolidarity()
    {
        var f = new OpeningTreatyFixture(); using var alliance = f.Begin(); alliance.Resume(); Equal(OpeningTreatyOfferKind.Alliance, f.Offer(alliance).Kind);
        alliance.Resume(new(0)); f.Done(alliance); Equal(true, f.World.Civilizations[1].Treaties[2].Alliance);
        Equal(new OpeningTreatyResult(true, false, false, false), alliance.Result); Equal(NegotiatedTreatyKind.Alliance, f.F.Agreements.Single().Kind);
        foreach (int target in new[] { 0, 3 }) foreach (bool accept in new[] { false, true })
        {
            f = new(); f.World.Diplomacy.WarTarget = target; f.World.Diplomacy.Fear = 1; f.World.Diplomacy.Attitude = 10;
            using var op = f.Begin(); op.Resume(); op.Resume(new(0)); Equal(OpeningTreatyOfferKind.Solidarity, f.Offer(op).Kind);
            Equal(target, f.Offer(op).Target!.Civilization); op.Resume(new(accept ? 1 : 0)); f.Done(op);
            Equal(true, op.Result!.TreatyMade); Equal(accept, f.World.Civilizations[1].Treaties[2].Alliance);
            Equal(accept, f.World.Civilizations[1].Treaties[target].War);
            Equal(accept, f.World.Civilizations[2].Treaties[target].Aggressor && f.World.Civilizations[2].Treaties[target].ExtendedAggressor);
            Equal(0, f.Notices.Count); // A refused solidarity does not imply an alliance, even without DidNotProve.
            if (accept) { Equal((sbyte)1, f.World.Civilizations[2].ReputationCredit[1]); }
        }
        f = new(); f.World.Diplomacy.WarTarget = 3;
        using var refused = f.Begin(); refused.Resume(); refused.Resume(new(0)); refused.Resume(new(0)); f.Done(refused);
        Equal(OpeningTreatyNoticeKind.DidNotProve, f.Notices.Single().Kind); Equal((byte)30, f.World.Civilizations[2].Attitude[1]);
        f = new(); using var declined = f.Begin(); declined.Resume(); declined.Resume(new(1)); f.Done(declined); Equal(false, declined.Result!.TreatyMade);
    }

    private static void OpeningAllianceTargetsAndAdmission()
    {
        var f = new OpeningTreatyFixture(); var w = f.World; w.Globals.Turn = 201; w.Diplomacy.WarTarget = 0;
        foreach (int c in new[] { 3, 5, 7 }) { w.Globals.PowerRank[c] = 4; w.Civilizations[1].Treaties[c].Contact = true; }
        w.Civilizations[2].Treaties[5].War = true; w.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
        using var scan = f.Begin(); scan.Resume(); Equal(7, w.Diplomacy.WarTarget); scan.Resume(new(1)); f.Done(scan);
        foreach (int gate in Enumerable.Range(0, 8))
        {
            f = new(); w = f.World;
            switch (gate)
            {
                case 0: w.Civilizations[1].Treaties[7].Alliance = true; break;
                case 1: w.Diplomacy.TargetIsLeader = true; break;
                case 2: w.Diplomacy.Attitude = 25; break;
                case 3: w.Civilizations[1].Reputation = 4; break;
                case 4: w.Civilizations[2].Treaties[1].Ceasefire = true; break;
                case 5: w.Diplomacy.CapturedCities = 2; break;
                case 6: w.Globals.PowerRank[1] = w.Globals.PowerRank[2] = 6; break;
                case 7: w.Diplomacy.EmissaryResponse = -1; break;
            }
            using var blocked = f.Begin(); blocked.Resume(); f.Done(blocked); Equal(false, blocked.Result!.TreatyMade);
        }
        f = new(); w = f.World; w.Globals.PowerRank[1] = 6; w.Globals.PowerRank[2] = 4;
        w.Diplomacy.Attitude = 49; w.Diplomacy.WarTarget = 3; w.Civilizations[1].Treaties[3].Ceasefire = true;
        using var gap = f.Begin(); gap.Resume(); Equal(OpeningTreatyOfferKind.Alliance, f.Offer(gap).Kind); gap.Resume(new(1)); f.Done(gap);
    }

    private static void OpeningCeasefireRefusalAndDraws()
    {
        foreach (int reputation in new[] { 0, 1 }) foreach (bool vendetta in new[] { false, true })
        {
            int bound = (reputation + 1) * (vendetta ? 2 : 1);
            var f = new OpeningTreatyFixture(bound > 1 ? [OpeningRoll("ceasefire-concession", bound, 1)] : []); f.NoPeace(true);
            f.World.Civilizations[1].Reputation = (byte)reputation; f.World.Civilizations[2].Treaties[1].Vendetta = vendetta;
            f.World.Civilizations[1].LastContactTurn[2] = -99;
            using var op = f.Begin(); op.Resume(); Equal(OpeningTreatyOfferKind.Ceasefire, f.Offer(op).Kind);
            op.Resume(new(1)); f.Done(op); Equal(new OpeningTreatyResult(false, false, false, true), op.Result);
            Equal(OpeningTreatyNoticeKind.ContinueWar, f.Notices.Single().Kind); Equal((short)10, f.World.Civilizations[1].LastContactTurn[2]);
            Equal(true, f.World.Civilizations[1].Treaties[2].War);
        }
        var negative = new OpeningTreatyFixture(); negative.NoPeace(true); negative.World.Civilizations[2].ReputationCredit[1] = 2;
        using var noDraw = negative.Begin(); noDraw.Resume(); noDraw.Resume(new(1)); negative.Done(noDraw);
        var failed = new OpeningTreatyFixture(); failed.NoPeace(true); failed.FailNotice = OpeningTreatyNoticeKind.ContinueWar;
        failed.World.Civilizations[1].LastContactTurn[2] = -9;
        using var stop = failed.Begin(); stop.Resume(); Throws<InvalidOperationException>(() => stop.Resume(new(1)));
        Equal((short)-9, failed.World.Civilizations[1].LastContactTurn[2]); Throws<InvalidOperationException>(() => stop.Resume());
        foreach (var inputs in new[] { new OpeningTreatyInputs(false, true, false, true), new(false, false, false, true) })
        {
            var f = new OpeningTreatyFixture(); f.NoPeace(true); using var op = f.Begin(inputs); op.Resume();
            Equal(inputs.Paid ? OpeningTreatyOfferKind.GrantCease : OpeningTreatyOfferKind.WallCease, f.Offer(op).Kind);
            op.Resume(new(0)); Equal(true, f.World.Civilizations[1].Treaties[2].Ceasefire);
            if (!op.Completed) { Equal(OpeningTreatyOfferKind.Peace, f.Offer(op).Kind); op.Resume(new(0)); }
            f.Done(op);
        }
        var compelled = new OpeningTreatyFixture(); compelled.NoPeace(true); compelled.Wonder(WonderIds.UnitedNations);
        using var un = compelled.Begin(); un.Resume(); un.Resume(new(1));
        Equal(OpeningTreatyNoticeKind.UNOverCease, compelled.Notices.Single().Kind);
        un.Resume(new(0)); compelled.Done(un); Equal(true, un.Result!.SenateBlocked);
        var worthless = new OpeningTreatyFixture(); worthless.NoPeace(true); worthless.World.Civilizations[1].Reputation = 7;
        worthless.FailNotice = OpeningTreatyNoticeKind.Worthless;
        using var signed = worthless.Begin(); signed.Resume(); Throws<InvalidOperationException>(() => signed.Resume(new(0)));
        Equal(true, worthless.World.Civilizations[1].Treaties[2].Ceasefire); Equal(false, worthless.World.Civilizations[1].Treaties[2].War);
        Equal(NegotiatedTreatyKind.Ceasefire, worthless.F.Agreements.Single().Kind); Equal(null, signed.Result);
    }

    private static void OpeningConcessionArithmeticAndCachedGold()
    {
        var f = new OpeningTreatyFixture();
        foreach (var (a, b, expected) in new[] { (0, 0, 3), (10, 21, 1), (10, 16, 2), (10, 10, 3), (20, 10, 4), (21, 10, 5), (65535, 65535, 3) })
        { f.World.Civilizations[1].MilitaryStrength = (ushort)a; f.World.Civilizations[2].MilitaryStrength = (ushort)b; Equal(expected, f.Service.PeaceThreshold(1, 2)); }
        f.World.Diplomacy.Fear = 4; Equal(200, f.Service.ConcessionGold(2)); f.Wonder(WonderIds.GreatWall); Equal(100, f.Service.ConcessionGold(2));
        f.World.Civilizations[2].Gold = -50; Equal(-25, f.Service.ConcessionGold(2));
        f.World.Civilizations[2].Gold = 777; f.World.Diplomacy.Fear = int.MaxValue; Equal(0, f.Service.ConcessionGold(2));
        f = new(); f.NoPeace(true); f.World.Diplomacy.Fear = 4; f.FailRefresh = true;
        using var cash = f.Begin(); cash.Resume(); cash.Resume(new(1)); Equal(OpeningTreatyOfferKind.Gold, f.Offer(cash).Kind); Equal(200, f.Offer(cash).Gold);
        f.World.Civilizations[2].Gold = 1; f.World.Diplomacy.Fear = 20;
        cash.Resume(new(1)); Equal(5200, f.World.Civilizations[1].Gold); Equal(-199, f.World.Civilizations[2].Gold);
        Equal(10, f.World.Diplomacy.LastContact); Equal(true, f.World.Civilizations[1].Treaties[2].Ceasefire);
        if (!cash.Completed) { cash.Resume(new(0)); }
        f.Done(cash); Equal(true, cash.Result!.Conceded); Equal(1, cash.PresentationFailures.Count);
    }

    private static void OpeningTechnologyConcessionContinuation()
    {
        foreach (bool failure in new[] { false, true })
        {
            var f = new OpeningTreatyFixture(); f.NoPeace(true); f.F.T.Know(2, 94); f.F.T.Know(2, AdvanceIds.Monarchy);
            f.World.Diplomacy.Fear = 4; f.World.Diplomacy.WantedTechnology = 94; f.F.T.T.Retained!.FailRevolution = failure;
            using var op = f.Begin(); op.Resume(); op.Resume(new(1)); Equal(94, f.Offer(op).Advance!.Identity);
            f.World.Diplomacy.WantedTechnology = AdvanceIds.Monarchy;
            Equal(true, op.Resume(new(1)) is OpeningTreatyDecision.Grant { Value: ResearchGrantDecision.Government });
            Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy)); Equal(false, f.World.Civilizations[1].Advances.Contains(94));
            Equal(true, f.World.Civilizations[1].Treaties[2].War); Equal(AdvanceIds.Monarchy, f.World.Diplomacy.WantedTechnology);
            if (failure) { Throws<InvalidOperationException>(() => op.Resume(new(1))); Equal(true, f.World.Civilizations[1].Treaties[2].War); }
            else
            {
                op.Resume(new(0)); if (!op.Completed) { op.Resume(new(0)); }
                f.Done(op);
                Equal(true, op.Result!.Conceded); Equal(AdvanceIds.Monarchy, f.World.Diplomacy.WantedTechnology);
                Equal(0, f.Refreshes); Equal(10, f.World.Diplomacy.LastContact);
            }
        }
    }

    private static void OpeningGrovelLiveTreasuryAndAdvances()
    {
        var f = new OpeningTreatyFixture(); f.NoPeace(true); f.World.Diplomacy.Fear = 1; f.World.Civilizations[2].CityCount = 1;
        f.F.T.Know(2, 0); f.F.T.Know(2, 10); f.F.T.Know(2, AdvanceIds.Monarchy);
        f.World.Civilizations[2].Advances = f.World.Civilizations[2].Advances.With(99); // Both disabled.
        using var op = f.Begin(); op.Resume(); op.Resume(new(1)); Equal(OpeningTreatyOfferKind.Grovel, f.Offer(op).Kind);
        f.World.Civilizations[2].Gold = 1234;
        Equal(true, op.Resume(new(1)) is OpeningTreatyDecision.Grant { Value: ResearchGrantDecision.Government });
        Equal(6234, f.World.Civilizations[1].Gold); Equal(0, f.World.Civilizations[2].Gold);
        Equal(true, f.World.Civilizations[1].Advances.Contains(0) && f.World.Civilizations[1].Advances.Contains(10));
        Equal(true, f.World.Civilizations[1].Treaties[2].War); f.F.T.Know(2, 90);
        op.Resume(new(0)); if (!op.Completed) { op.Resume(new(0)); }
        f.Done(op);
        Equal(true, f.World.Civilizations[1].Advances.Contains(90)); Equal(false, f.World.Civilizations[1].Advances.Contains(99));
        Equal(1, f.Refreshes); Equal(true, op.Result!.Conceded);
        var disposed = new OpeningTreatyFixture(); disposed.NoPeace(true); disposed.World.Diplomacy.Fear = 4;
        disposed.F.T.Know(2, AdvanceIds.Monarchy); disposed.World.Diplomacy.WantedTechnology = AdvanceIds.Monarchy;
        using var pending = disposed.Begin(); pending.Resume(); pending.Resume(new(1)); pending.Resume(new(1)); pending.Dispose();
        Throws<ObjectDisposedException>(() => pending.Resume(new(0))); Equal(true, disposed.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
        Equal(true, disposed.World.Civilizations[1].Treaties[2].War);
    }

    private static void OpeningSenateWonderDrawOrder()
    {
        foreach (bool wonder in new[] { false, true }) foreach (bool active in new[] { false, true })
            foreach (int first in new[] { 0, 1 }) foreach (int second in new[] { 0, 1 })
            {
                if (!wonder && !active) { continue; }
                var choices = new List<Choice>();
                if (wonder) { choices.Add(OpeningRoll("peace-wonder", 2, first)); }
                if ((!wonder || first == 0) && active) { choices.Add(OpeningRoll("peace-senate", 2, second)); }
                var f = new OpeningTreatyFixture(choices); f.NoPeace(true);
                f.World.Civilizations[1].Government = Government.Republic; f.World.Civilizations[1].Flags.SenateActive = active;
                if (wonder) { f.Wonder(WonderIds.GreatWall); }
                using var op = f.Begin(); op.Resume(); op.Resume(new(1));
                Equal(OpeningTreatyOfferKind.Peace, f.Offer(op).Kind); Equal(true, f.World.Civilizations[1].Treaties[2].Ceasefire);
                op.Resume(new(1)); f.Done(op); Equal(true, op.Result!.SenateBlocked); Equal(true, op.Result.TreatyMade);
                bool peace = wonder && first == 1 || (!wonder || first == 0) && active && second == 1;
                Equal(peace, f.World.Civilizations[1].Treaties[2].Peace);
                Equal(false, f.Notices.Any(n => n.Kind is OpeningTreatyNoticeKind.ContinueHawks or OpeningTreatyNoticeKind.ContinueUN));
                Equal(peace ? wonder && first == 1 ? OpeningTreatyNoticeKind.WallOverPeace : OpeningTreatyNoticeKind.OverrulePeace
                    : OpeningTreatyNoticeKind.NoPeace, f.Notices.Last().Kind);
            }
    }

    private static void OpeningPeaceAdmissionAndConcessions()
    {
        foreach (int wonder in new[] { WonderIds.GreatWall, WonderIds.UnitedNations })
        {
            var f = new OpeningTreatyFixture(); f.NoPeace(); f.Wonder(wonder);
            using var op = f.Begin(); op.Resume(); Equal(OpeningTreatyOfferKind.Peace, f.Offer(op).Kind); op.Resume(new(1)); f.Done(op);
            Equal(true, f.World.Civilizations[1].Treaties[2].Peace); Equal(false, op.Result!.SenateBlocked);
            Equal(wonder == WonderIds.GreatWall ? OpeningTreatyNoticeKind.WallOverPeace : OpeningTreatyNoticeKind.UNOverPeace, f.Notices.Single().Kind);
        }
        var cash = new OpeningTreatyFixture(); cash.NoPeace(); cash.World.Diplomacy.Fear = 4;
        using var paid = cash.Begin(); paid.Resume(); paid.Resume(new(1)); Equal(OpeningTreatyOfferKind.GoldForPeace, cash.Offer(paid).Kind);
        Equal(200, cash.Offer(paid).Gold); cash.World.Civilizations[2].Gold = 2; paid.Resume(new(1)); cash.Done(paid);
        Equal(5200, cash.World.Civilizations[1].Gold); Equal(-198, cash.World.Civilizations[2].Gold); Equal(true, paid.Result!.Conceded);
        foreach (bool overrideGate in new[] { false, true })
        {
            var f = new OpeningTreatyFixture(); f.NoPeace(); f.World.Diplomacy.TargetIsLeader = true; f.World.Diplomacy.Attitude = 100;
            f.World.Diplomacy.CapturedCities = 50; f.World.Diplomacy.SuppressedAggression = overrideGate;
            using var op = f.Begin(); op.Resume();
            if (overrideGate) { Equal(OpeningTreatyOfferKind.Peace, f.Offer(op).Kind); op.Resume(new(0)); }
            f.Done(op); Equal(overrideGate, op.Result!.TreatyMade);
        }
        var refuse = new OpeningTreatyFixture(); refuse.NoPeace(); refuse.World.Civilizations[1].Government = Government.Republic;
        using var no = refuse.Begin(); no.Resume(); no.Resume(new(1)); refuse.Done(no);
        Equal(new OpeningTreatyResult(true, false, false, false), no.Result);
        Sequence(new[] { OpeningTreatyNoticeKind.ContinueHawks, OpeningTreatyNoticeKind.NoPeace }, refuse.Notices.Select(n => n.Kind));
        foreach (int roll in new[] { 0, 1 })
        {
            var f = new OpeningTreatyFixture([OpeningRoll("peace-concession", 4, roll)]); f.NoPeace();
            f.World.Civilizations[1].Reputation = 3; f.World.Diplomacy.Fear = 4;
            using var op = f.Begin(); op.Resume(); op.Resume(new(1));
            if (roll == 0) { Equal(OpeningTreatyOfferKind.GoldForPeace, f.Offer(op).Kind); op.Resume(new(0)); }
            f.Done(op); Equal(OpeningTreatyNoticeKind.NoPeace, f.Notices.Last().Kind); Equal(false, op.Result!.Conceded);
        }
    }

    private static void OpeningTreatyPrivateGrant()
    {
        using var runtime = new SessionRuntime(new(), new(255));
        var f = new OpeningTreatyFixture(data: CreationFixture(new(runtime.Process, runtime.World))); f.NoPeace(true);
        f.F.T.Know(2, AdvanceIds.Monarchy); f.World.Diplomacy.Fear = 4; f.World.Diplomacy.WantedTechnology = AdvanceIds.Monarchy;
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.opening", "Authored opening treaties", AccessClass.Player, new([]),
            new([new("treatyMade", ValueKind.Boolean), new("conceded", ValueKind.Boolean), new("senateBlocked", ValueKind.Boolean), new("endsAudience", ValueKind.Boolean)])),
            (_, _, _) => new(), _ => new OpeningTreatyOperation(f.Begin(), _ => recipient, _ => throw new InvalidOperationException())));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var player = SessionClient(runtime, 1); recipient = player.Identity.Participant;
        var command = actor.Submit(new("test.opening", new([]))).Correlation.Command; runtime.AdvanceOne();
        foreach (int answer in new[] { 1, 1 })
        { var prompt = RequiredInteraction(player, command); Equal(false, actor.Reply(prompt, answer).Accepted); Equal(true, player.Reply(prompt, answer).Accepted); runtime.AdvanceOne(); }
        var grant = RequiredInteraction(player, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy)); Equal(true, f.World.Civilizations[1].Treaties[2].War);
        Equal(true, player.Reply(grant, 0).Accepted); runtime.AdvanceOne();
        var peace = RequiredInteraction(player, command); Equal(true, player.Reply(peace, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(true, f.World.Civilizations[1].Treaties[2].Peace);
        Equal(true, f.World.Diplomacy.AiMeetingActive); f.F.T.T.Random.RequireChoicesConsumed();
    }

    private static void OpeningTreatyCancellationAndFailures()
    {
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout }) foreach (int kind in new[] { 0, 1, 2 })
        {
            var f = new OpeningTreatyFixture(); if (kind != 0) { f.NoPeace(kind == 1); }
            var context = new OperationContext(f.World, f.Data.Process, f.F.T.T.Random, new PresentationRandom(1, "opening"),
                new(default, new(1)), default, (_, _) => { }, () => { });
            using var op = new OpeningTreatyOperation(f.Begin(), c => new((ulong)c), _ => throw new InvalidOperationException());
            var step = op.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, step.Status);
            Equal("Accept", step.Interaction!.Options!.Single(o => o.Value == 0).Label);
            Equal(CommandStatus.Completed, op.Resume(context, new(input)).Status);
            Equal(kind == 0, f.World.Civilizations[1].Treaties[2].Peace); Equal(false, f.World.Civilizations[1].Treaties[2].Alliance);
            f.F.T.T.Random.RequireChoicesConsumed();
        }
        var failed = new OpeningTreatyFixture(); failed.F.FailAgreement = true;
        using var accepted = failed.Begin(); accepted.Resume(); Throws<InvalidOperationException>(() => accepted.Resume(new(0)));
        Equal(true, failed.World.Civilizations[1].Treaties[2].Alliance); Equal(null, accepted.Result); Throws<InvalidOperationException>(() => accepted.Resume());
        var autoplay = new OpeningTreatyFixture(); autoplay.World.Multiplayer.Saved.AutoplayActive = true;
        using var absent = autoplay.Begin(); absent.Resume(); autoplay.Done(absent); Equal(false, absent.Result!.TreatyMade);
    }
}
