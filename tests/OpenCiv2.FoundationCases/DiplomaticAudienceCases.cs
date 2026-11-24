using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // V05 §6: the fixture composes actual children. Controlled live changes occur only
    // at retained player decisions, never at invented formatting or query callbacks.
    private sealed class AudienceFixture : IDiplomaticAudienceEffects, IOpeningTreatyEffects
    {
        public ConversationFixture C { get; }
        public GameState World => C.World;
        public RevelationFixture Data => C.Data;
        public TechnologyFixture T => C.F.T;
        public DiplomaticAudience Service { get; }
        public List<AudienceNotice> Notices { get; } = [];
        public List<OpeningTreatyNotice> TreatyNotices { get; } = [];
        public int Events, Refreshes;
        public bool FailRefresh, FailEvent;
        public AudienceNoticeKind? FailNotice;
        public AudienceFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            C = new(choices, data);
            World.Diplomacy.AskedEmissary = false;
            World.Diplomacy.AiMeetingActive = World.Diplomacy.MeetingActive = false;
            World.Civilizations[1].Government = World.Civilizations[2].Government = Government.Despotism;
            var war = War(Data, C.F.F.WarEffects, T.T.Random);
            var opening = new OpeningTreaties(World, Data.Rules, T.T.Random, C.Emissaries,
                new(World, Data.Rules, _ => throw new InvalidOperationException("Unexpected attack notice")), T.T.Research,
                war, C.F.F.Treaties, this);
            Service = new(World, Data.Process.Rules, T.T.Random, C.Stance, C.Emissaries, T.Service, T.T.Research,
                war, opening, C.Conversation, ExecuteEvent, this);
        }
        public void ExecuteEvent(Event entry)
        {
            ++Events;
            if (FailEvent) { throw new InvalidOperationException("Required authored action failed"); }
            ScenarioActions.Execute(entry, (action, e) => action == ScenarioAction.ChangeMoney
                ? ScenarioActions.ChangeMoney(World, e) : throw new InvalidOperationException("Unexpected authored action"));
        }
        public DiplomaticAudience.Operation Begin(bool player = false, int a = 1, int b = 2) => Service.Begin(a, b, player, new(10, 8), "audience");
        public void Notice(AudienceNotice n) { Notices.Add(n); if (FailNotice == n.Kind) { throw new InvalidOperationException("Required audience notice failed"); } }
        public void Notice(OpeningTreatyNotice n) => TreatyNotices.Add(n);
        public void RefreshStatus() { ++Refreshes; if (FailRefresh) { throw new InvalidOperationException("Cosmetic status failed"); } }
        public void Done(DiplomaticAudience.Operation op)
        { if (!op.Completed) { throw new InvalidOperationException("Audience still pending: " + op.Pending); } Equal(false, World.Diplomacy.AiMeetingActive); Equal(1, C.Closed); T.T.Random.RequireChoicesConsumed(); }
        public void Receive(DiplomaticAudience.Operation op)
        { Equal(true, op.Resume() is DiplomaticAudienceDecision.Emissary { Value: EmissaryDecision.Receive }); }
        public AudienceOffer Offer(DiplomaticAudience.Operation op) => ((DiplomaticAudienceDecision.Offer)op.Pending!).Value;
        public void End(DiplomaticAudience.Operation op)
        {
            if (op.Pending is DiplomaticAudienceDecision.Treaty { Value: OpeningTreatyDecision.Offer }) { op.Resume(new(1)); }
            Equal(true, op.Pending is DiplomaticAudienceDecision.Conversation { Value: DiplomaticConversationDecision.Menu });
            op.Resume(new(0)); Done(op);
        }
    }
    private static Choice AudienceGreeting() => GreetingChoice("audience/emissary/greet");
    private static Choice AudienceSecondary(string site, int max, uint raw = 0) => Choice.Create(new("V05.audience-" + site, "audience"),
        new SamplingLaw.SecondaryRange(0, max), ChoiceCode.Secondary(raw));
    private static Choice AudiencePrimary(string site, int value) => Choice.Create(new("V05.audience-" + site, "audience"),
        new SamplingLaw.PrimaryModulo(2), ChoiceCode.Primary(value));

    private static void AudienceScenarioEntryAndFailure()
    {
        foreach (bool loaded in new[] { false, true }) foreach (bool deny in new[] { false, true })
        {
            var f = new AudienceFixture(loaded && deny ? [] : [AudienceGreeting()]); var w = f.World;
            w.Events.Loaded = loaded; var entry = NegotiationEvent(w, deny: deny);
            entry.Values.Actions.ChangeMoney = true; entry.Values.Actions.JustOnce = true;
            entry.Values.MoneyReceiverCiv = 1; entry.Values.MoneyAmount = 10;
            w.Diplomacy.GiftsThisMeeting = 7; w.Diplomacy.SelectedPair = (6, 7);
            w.Civilizations[1].Treaties[2].Incident = true;
            using var op = f.Begin(true); op.Resume(); Equal(loaded ? 1 : 0, f.Events);
            Equal(loaded ? 5010 : 5000, w.Civilizations[1].Gold);
            if (loaded && deny)
            {
                Equal(true, op.ScenarioDenied && op.Completed); Equal(false, w.Diplomacy.AiMeetingActive);
                Equal((6, 7), w.Diplomacy.SelectedPair); Equal(7, w.Diplomacy.GiftsThisMeeting);
                Equal(true, w.Civilizations[1].Treaties[2].Incident); Equal(0, f.C.Closed);
            }
            else { Equal((2, 1), w.Diplomacy.SelectedPair); Equal(0, w.Diplomacy.GiftsThisMeeting); f.End(op); }
            f.T.T.Random.RequireChoicesConsumed();
        }
        var failed = new AudienceFixture(); failed.World.Events.Loaded = true; NegotiationEvent(failed.World); failed.FailEvent = true;
        using var fault = failed.Begin(); Throws<InvalidOperationException>(() => fault.Resume());
        Equal(true, failed.World.Diplomacy.AiMeetingActive); Equal(0, failed.C.Closed);
        Throws<InvalidOperationException>(() => fault.Resume());
    }

    private static void AudiencePlayerEntryRejections()
    {
        foreach (bool war in new[] { false, true }) foreach (bool allied in new[] { false, true })
        {
            var f = new AudienceFixture(); var w = f.World;
            if (allied) { f.C.F.Alliance(); }
            w.Civilizations[2].Treaties[1].WarThisTurn = war;
            w.Civilizations[1].Treaties[2].MetThisTurn = true; w.Civilizations[2].DiploRequestCount = 9;
            using var op = f.Begin(true); op.Resume(); f.Done(op);
            Equal(allied && !war ? AudienceNoticeKind.OutaHereAlly : AudienceNoticeKind.OutaHere, f.Notices.Single().Kind);
            Equal((sbyte)9, w.Civilizations[2].DiploRequestCount); Equal(0, f.C.Notices.Count);
        }
        var incident = new AudienceFixture([AudienceGreeting()]); var world = incident.World;
        world.Civilizations[2].DiploRequestCount = -5;
        world.Civilizations[1].Treaties[2].Aggressor = world.Civilizations[1].Treaties[2].Incident = true;
        world.Civilizations[2].Treaties[1].WarThisTurn = world.Civilizations[1].Treaties[2].WarThisTurn = true;
        using var admitted = incident.Begin(true); admitted.Resume(); Equal((sbyte)0, world.Civilizations[2].DiploRequestCount);
        Equal(false, world.Civilizations[2].Treaties[1].WarThisTurn || world.Civilizations[1].Treaties[2].WarThisTurn);
        incident.End(admitted);
        var ship = new AudienceFixture(); world = ship.World; ship.C.War(); world.Globals.Difficulty = Difficulty.Emperor;
        world.Civilizations[1].SpaceshipFlags.Launched = world.Civilizations[2].SpaceshipFlags.Launched = true;
        world.Civilizations[1].SpaceshipArrivalYear = 2100; world.Civilizations[2].SpaceshipArrivalYear = 2101;
        using var rejected = ship.Begin(true); rejected.Resume(); ship.Done(rejected);
        Equal(AudienceNoticeKind.OutaHere, ship.Notices.Single().Kind); Equal(true, world.Civilizations[1].Treaties[2].WarThisTurn);
    }

    private static void AudienceInitiationAndRefusedReception()
    {
        foreach (bool accept in new[] { false, true })
        {
            var f = new AudienceFixture(accept ? [AudienceGreeting()] : []); var w = f.World;
            w.Civilizations[2].DiploRequestCount = 8; w.Civilizations[1].LastContactTurn[2] = 42;
            w.Civilizations[1].Treaties[2].Ceasefire = w.Civilizations[2].Treaties[1].Ceasefire = true;
            w.Civilizations[1].Treaties[2].Tribute = w.Civilizations[2].Treaties[1].Tribute = true;
            w.Civilizations[2].Treaties[1].CityCaptured = true;
            using var op = f.Begin(); f.Receive(op);
            Equal(42, w.Diplomacy.LastContact); Equal((short)100, w.Civilizations[1].LastContactTurn[2]);
            Equal((sbyte)0, w.Civilizations[2].DiploRequestCount); Equal(true, w.Diplomacy.HadCeasefire);
            Equal(false, w.Civilizations[2].Treaties[1].Ceasefire || w.Civilizations[1].Treaties[2].Tribute);
            w.Diplomacy.Fear = 3; op.Resume(new(accept ? 0 : 1)); f.Done(op);
            Equal(true, w.Civilizations[1].Treaties[2].MetThisTurn && w.Civilizations[2].Treaties[1].MetThisTurn);
            Equal(false, w.Civilizations[2].Treaties[1].CityCaptured); Equal(0, f.C.Notices.Count);
        }
    }

    private static void AudienceTechnologyGapIdentities()
    {
        foreach (int advance in new[] { AdvanceIds.MobileWarfare, AdvanceIds.AdvancedFlight, AdvanceIds.Conscription, AdvanceIds.Tactics, AdvanceIds.Gunpowder })
        {
            var f = new AudienceFixture(); f.T.Know(1, advance); Equal(true, f.Service.TechnologyGap(1, 2));
            f.T.Know(2, advance); Equal(false, f.Service.TechnologyGap(1, 2));
        }
        var tier = new AudienceFixture(); tier.T.Know(1, AdvanceIds.MobileWarfare); tier.T.Know(2, AdvanceIds.AdvancedFlight);
        Equal(false, tier.Service.TechnologyGap(1, 2));
        tier = new(); tier.T.Know(1, AdvanceIds.MobileWarfare); tier.T.Know(2, AdvanceIds.Conscription);
        Equal(false, tier.Service.TechnologyGap(1, 2)); tier.World.Civilizations[1].NumAdvances = 2;
        Equal(true, tier.Service.TechnologyGap(1, 2));
    }

    private static void AudienceTributeLivePaymentAndVariants()
    {
        foreach (bool allied in new[] { false, true }) foreach (int reply in new[] { 0, 1, 2 })
        {
            var f = new AudienceFixture([AudienceGreeting(), AudienceSecondary("tribute", 3, uint.MaxValue)]); var w = f.World;
            using var op = f.Begin(); f.Receive(op);
            if (allied) { f.C.F.Alliance(); }
            w.Diplomacy.Demand = 4000; w.Diplomacy.RequestMode = 2; w.Diplomacy.Attitude = 10;
            w.Civilizations[1].MilitaryStrength = 10; w.Civilizations[2].MilitaryStrength = 1;
            w.Diplomacy.HadCeasefire = true; op.Resume(new(0)); var offer = f.Offer(op);
            Equal(AudienceOfferKind.Tribute, offer.Kind); Equal(allied ? 2500 : 4000, offer.Gold); Equal(allied ? 9 : 1, offer.Variant);
            w.Diplomacy.Demand = 75; w.Civilizations[2].DiploRequestCount = -1; f.FailRefresh = true;
            op.Resume(new(reply)); Equal(reply == 1 ? 4925 : 5000, w.Civilizations[1].Gold);
            Equal(reply == 1 ? 852 : 777, w.Civilizations[2].Gold); Equal(999, w.Diplomacy.Demand);
            Equal(reply == 1, w.Civilizations[2].Treaties[1].Ceasefire);
            Equal((sbyte)(!allied && reply != 1 ? -1 : 0), w.Civilizations[2].DiploRequestCount); // Treaty precedes the conversation clamp.
            f.End(op); Equal(reply == 1 ? 1 : 0, op.PresentationFailures.Count); Equal(reply == 1 ? 1 : 0, f.Refreshes);
        }
        var gap = new AudienceFixture([AudienceGreeting(), AudienceSecondary("tribute", 3)]); gap.T.Know(1, AdvanceIds.Gunpowder);
        using var demand = gap.Begin(); gap.Receive(demand); gap.World.Diplomacy.RequestMode = 2; gap.World.Diplomacy.Demand = 100;
        demand.Resume(new(0)); Equal(8, gap.Offer(demand).Variant); demand.Resume(new(0)); gap.End(demand);
    }

    private static void AudienceTechnologyAndSecondDemand()
    {
        foreach (bool second in new[] { false, true })
        {
            var f = new AudienceFixture([AudienceGreeting(), AudienceSecondary("technology", 2)]); var w = f.World;
            f.T.Know(1, 94); f.T.Know(1, 95);
            using var op = f.Begin(); f.Receive(op); w.Diplomacy.Aggressive = true; w.Diplomacy.NearbyUnits = 1;
            op.Resume(new(0)); Equal(AudienceOfferKind.Technology, f.Offer(op).Kind); Equal(95, f.Offer(op).Advance!.Identity);
            op.Resume(new(1)); Equal(true, w.Civilizations[2].Advances.Contains(95));
            Equal(AudienceOfferKind.OverABarrel, f.Offer(op).Kind); Equal(94, f.Offer(op).Advance!.Identity);
            // An actual retained second offer is a boundary for live changes.
            if (!second) { w.Diplomacy.Aggressive = false; }
            op.Resume(new(second ? 1 : 0)); Equal(second, w.Civilizations[2].Advances.Contains(94));
            Equal(second ? null : 94, w.Diplomacy.BestTechnology); Equal(null, w.Diplomacy.SecondTechnology);
            Equal(999, w.Diplomacy.Demand); f.End(op);
        }
        var allied = new AudienceFixture([AudienceGreeting(), AudienceSecondary("technology", 2)]); allied.T.Know(1, 94);
        using var declined = allied.Begin(); allied.Receive(declined); allied.C.F.Alliance();
        allied.World.Civilizations[1].Reputation = 3; allied.World.Diplomacy.Attitude = 10;
        declined.Resume(new(0)); Equal(9, allied.Offer(declined).Variant); declined.Resume(new(2));
        Equal((sbyte)1, allied.World.Civilizations[2].DiploRequestCount); Equal(15, allied.World.Diplomacy.Attitude);
        Equal(false, allied.World.Civilizations[2].Advances.Contains(94)); allied.End(declined);
    }

    private static void AudienceWarRequestAndDismissal()
    {
        foreach (bool allied in new[] { false, true }) foreach (int roll in new[] { 0, 1 })
        {
            var f = new AudienceFixture(allied ? [AudienceGreeting()] : [AudienceGreeting(), AudiencePrimary("war-payment", roll)]); var w = f.World;
            using var op = f.Begin(); f.Receive(op); w.Globals.CivsAlive = w.Globals.CivsAlive.With(3);
            w.Diplomacy.WarTarget = 3; w.Diplomacy.Demand = 50; w.Diplomacy.HadCeasefire = true;
            if (allied) { f.C.F.Alliance(); }
            w.Civilizations[1].Treaties[3].Peace = true;
            op.Resume(new(0)); Equal(AudienceOfferKind.JoinWar, f.Offer(op).Kind); Equal(1, f.Offer(op).Variant);
            op.Resume(new(1)); Equal(true, w.Civilizations[1].Treaties[3].War); Equal(null, w.Diplomacy.WarTarget);
            Equal(allied || roll == 1 ? 0 : 50, w.Diplomacy.Demand);
            Equal(!allied && roll == 1, w.Civilizations[2].Treaties[1].Ceasefire); f.End(op);
        }
        var dismiss = new AudienceFixture([AudienceGreeting()]); using var rejected = dismiss.Begin(); dismiss.Receive(rejected);
        dismiss.C.F.Alliance(); dismiss.World.Diplomacy.Attitude = 50; rejected.Resume(new(0)); dismiss.Done(rejected);
        Equal(AudienceNoticeKind.Begone, dismiss.Notices.Single().Kind); Equal(1, dismiss.Notices.Single().Variant);
        Equal(false, dismiss.World.Civilizations[1].Treaties[2].Alliance);
    }

    private static void AudienceThreatOrderingAndFailure()
    {
        foreach (int branch in Enumerable.Range(0, 4)) foreach (bool failed in new[] { false, true })
        {
            var f = new AudienceFixture(branch == 0 ? [AudienceGreeting(), AudienceSecondary("provoke", 2)] : [AudienceGreeting()]); var w = f.World;
            using var op = f.Begin(); f.Receive(op); w.Diplomacy.Aggressive = true; w.Diplomacy.RequestMode = 0;
            w.Diplomacy.Demand = branch < 2 ? 999 : 0; w.Diplomacy.LastContact = branch == 2 ? -1 : 0;
            if (branch > 0) { f.C.F.Peace(false); }
            var kind = branch == 0 ? AudienceNoticeKind.Provoke : branch == 1 ? AudienceNoticeKind.Reject : AudienceNoticeKind.Rid;
            if (failed && branch != 3) { f.FailNotice = kind; }
            w.Civilizations[1].LastContactTurn[2] = 12; w.Civilizations[2].Attitude[1] = 20; w.Diplomacy.Attitude = 20;
            if (failed && branch != 3)
            {
                Throws<InvalidOperationException>(() => op.Resume(new(0))); Equal(true, w.Civilizations[1].Treaties[2].War);
                Equal((short)12, w.Civilizations[1].LastContactTurn[2]); Equal(0, f.C.Closed);
                Throws<InvalidOperationException>(() => op.Resume());
            }
            else { op.Resume(new(0)); f.Done(op); Equal(branch == 3 ? 0 : 1, f.Notices.Count); }
            if (branch != 3) { Equal(kind, f.Notices.Single().Kind); Equal((byte)75, w.Civilizations[2].Attitude[1]); Equal(20, w.Diplomacy.Attitude); }
            f.T.T.Random.RequireChoicesConsumed();
        }
    }

    private static void AudiencePrivateGrantAndDisposal()
    {
        using var runtime = new SessionRuntime(new(), new(255));
        var f = new AudienceFixture([AudienceGreeting(), AudienceSecondary("technology", 2)], CreationFixture(new(runtime.Process, runtime.World)));
        f.T.Know(1, AdvanceIds.Monarchy); f.World.Globals.HumanPlayers = f.World.Globals.HumanPlayers.With(2);
        f.World.Multiplayer.LocalHuman = 2;
        ParticipantId first = default, second = default;
        runtime.RegisterCommand(new(new(1, "test.audience", "Authored local audience", AccessClass.Player, new([]), new([new("scenarioDenied", ValueKind.Boolean)])),
            (_, _, _) => new(), _ => new DiplomaticAudienceOperation(f.Begin(true), c => c == 1 ? first : second,
                _ => throw new InvalidOperationException("Unexpected intelligence"))));
        runtime.BeginClientSession(new()); var a = SessionClient(runtime, 1); var b = SessionClient(runtime, 2);
        first = a.Identity.Participant; second = b.Identity.Participant;
        var command = a.Submit(new("test.audience", new([]))).Correlation.Command; runtime.AdvanceOne();
        var offer = RequiredInteraction(a, command); Equal(true, a.Reply(offer, 1).Accepted); runtime.AdvanceOne();
        var grant = RequiredInteraction(b, command); Equal(false, a.Reply(grant, 0).Accepted);
        Equal(null, RequiredResult(a, command).Interaction); Equal(true, f.World.Civilizations[2].Advances.Contains(AdvanceIds.Monarchy));
        Equal(0, f.C.Closed); Equal(true, b.Reply(grant, 0).Accepted); runtime.AdvanceOne();
        var treaty = RequiredInteraction(a, command); Equal(null, b.Result(command));
        Equal(true, RequiredResult(a, command).Interaction!.Spec.Prompt.Contains("alliance")); Equal(true, a.Reply(treaty, 1).Accepted); runtime.AdvanceOne();
        var menu = RequiredInteraction(a, command);
        Equal(true, a.Reply(menu, 0).Accepted); runtime.AdvanceOne(); Equal("", RequiredResult(a, command).Code); Equal(CommandStatus.Completed, RequiredResult(a, command).Status);
        Equal(false, f.World.Diplomacy.AiMeetingActive); Equal(1, f.C.Closed); f.T.T.Random.RequireChoicesConsumed();
        var disposed = new AudienceFixture([AudienceGreeting(), AudienceSecondary("technology", 2)]);
        disposed.T.Know(1, 94); using var op = disposed.Begin(true); op.Resume(); op.Dispose();
        Throws<ObjectDisposedException>(() => op.Resume(new(1))); Equal(false, disposed.World.Civilizations[2].Advances.Contains(94));
        Equal(true, disposed.World.Diplomacy.AiMeetingActive); Equal(0, disposed.C.Closed);
    }

    private static void AudienceSessionCancellationAndTreaties()
    {
        foreach (var kind in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var f = new AudienceFixture([AudienceGreeting()]); f.World.Civilizations[2].Attitude[1] = 10;
            var context = new OperationContext(f.World, f.Data.Process, f.T.T.Random, new PresentationRandom(1, "audience"),
                new(default, new(1)), default, (_, _) => { }, () => { });
            using var op = new DiplomaticAudienceOperation(f.Begin(true), c => new((ulong)c), _ => throw new InvalidOperationException());
            var step = op.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, step.Status);
            Equal(true, step.Interaction!.Prompt.Contains("alliance")); // Opening treaty reply zero accepts; cancellation must decline.
            Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new(kind)).Status);
            Equal(false, f.World.Civilizations[1].Treaties[2].Alliance);
            Equal(CommandStatus.Completed, op.Resume(context, new(kind)).Status); Equal(false, f.World.Diplomacy.AiMeetingActive);
        }
    }

    private static void AudienceReconsiderAndProtection()
    {
        foreach (int roll in new[] { 0, 1 })
        {
            var f = new AudienceFixture([AudienceGreeting(), AudiencePrimary("reconsider", roll)]); var w = f.World;
            w.Civilizations[2].Treaties[1].Contact = false;
            using var op = f.Begin(); f.Receive(op);
            f.C.F.Peace(false); w.Diplomacy.Attitude = 99; w.Diplomacy.Demand = 999; w.Diplomacy.RequestMode = 0;
            w.Civilizations[2].Government = Government.Republic; // 5 < 6-even, but not 5 < 6-odd.
            op.Resume(new(0));
            if (roll == 0) { f.Done(op); Equal(AudienceNoticeKind.Reject, f.Notices.Single().Kind); }
            else
            {
                if (op.Pending is not DiplomaticAudienceDecision.Treaty { Value: OpeningTreatyDecision.Offer { Value.Kind: OpeningTreatyOfferKind.Peace } }) { throw new InvalidOperationException("Expected peace after odd reconsider: " + op.Pending); }
                op.Resume(new(0)); f.End(op); Equal(0, f.Notices.Count);
            }
        }
        foreach (int wonder in new[] { WonderIds.GreatWall, WonderIds.UnitedNations })
        {
            var f = new AudienceFixture([AudienceGreeting()]); var w = f.World;
            w.Civilizations[2].Treaties[1].Contact = false;
            using var op = f.Begin(); f.Receive(op); f.C.Wonder(wonder, 1);
            w.Diplomacy.Attitude = 99; w.Diplomacy.Demand = 999; w.Diplomacy.RequestMode = 0;
            w.Diplomacy.SuppressedAggression = true; op.Resume(new(0));
            Equal(wonder == WonderIds.GreatWall ? AudienceNoticeKind.AccursedWall : AudienceNoticeKind.AccursedUN, f.Notices.Single().Kind);
            f.End(op); // Protection excludes the reconsider draw even at first contact.
        }
        var captured = new AudienceFixture([AudienceGreeting()]); captured.World.Civilizations[2].Treaties[1].CityCaptured = true;
        using var cease = captured.Begin(); captured.Receive(cease); captured.C.War(); captured.World.Civilizations[2].Treaties[1].CityCaptured = true; captured.World.Diplomacy.Aggressive = true;
        cease.Resume(new(0)); Equal(true, cease.Pending is DiplomaticAudienceDecision.Treaty
        { Value: OpeningTreatyDecision.Offer { Value.Kind: OpeningTreatyOfferKind.WallCease } });
        Equal(false, captured.World.Diplomacy.Aggressive); cease.Resume(new(0));
        // Accept any following peace proposal before reaching the actual conversation.
        if (cease.Pending is DiplomaticAudienceDecision.Treaty) { cease.Resume(new(0)); }
        captured.End(cease); Equal(0, captured.Notices.Count);
    }

    private static void AudienceExchangeLoopAndTargetFiltering()
    {
        var f = new AudienceFixture([AudienceGreeting()]); f.T.Pair();
        using var op = f.Begin(); f.Receive(op); op.Resume(new(0));
        Equal(true, op.Pending is DiplomaticAudienceDecision.Technology { Value: TechnologyNegotiationDecision.Exchange });
        op.Resume(new(1)); Equal(true, op.Pending is DiplomaticAudienceDecision.Technology { Value: TechnologyNegotiationDecision.Captured });
        Equal(true, f.World.Civilizations[2].Advances.Contains(95)); op.Resume(new(90));
        Equal(true, op.Pending is DiplomaticAudienceDecision.Technology { Value: TechnologyNegotiationDecision.Exchange });
        op.Resume(new(1)); op.Resume(new(91));
        Equal(true, f.World.Civilizations[1].Advances.Contains(90) && f.World.Civilizations[1].Advances.Contains(91));
        Equal(true, f.World.Civilizations[2].Advances.Contains(94)); Equal(null, f.World.Diplomacy.BestTechnology);
        f.End(op);
        foreach (bool alive in new[] { false, true })
        {
            f = new([AudienceGreeting()]); using var target = f.Begin(); f.Receive(target);
            f.World.Globals.CivsAlive = f.World.Globals.CivsAlive.With(3, alive);
            f.World.Diplomacy.WarTarget = 3; target.Resume(new(0));
            if (alive)
            { Equal(AudienceOfferKind.JoinWar, f.Offer(target).Kind); target.Resume(new(0)); Equal((sbyte)1, f.World.Civilizations[2].DiploRequestCount); }
            else { Equal(null, f.World.Diplomacy.WarTarget); }
            f.End(target);
        }
        // Historical pair {3,6} clears a live threat before the technology survey.
        f = new([AudienceGreeting()]); f.World.Globals.Status.Scenario = true; f.World.Scenario.Flags.HistoricalAi = true;
        f.World.Globals.HumanPlayers = CivilizationSet.Only(3); f.World.Multiplayer.LocalHuman = 3;
        f.World.Civilizations[3].Treaties[6] = f.World.Civilizations[6].Treaties[3] = new() { Contact = true, Peace = true };
        f.World.Civilizations[3].Government = f.World.Civilizations[6].Government = Government.Despotism;
        using var historical = f.Begin(a: 3, b: 6); f.Receive(historical);
        f.World.Diplomacy.WarTarget = 1; f.World.Diplomacy.Demand = 100; f.World.Diplomacy.Fear = 3;
        f.World.Diplomacy.Aggressive = false; historical.Resume(new(0)); f.Done(historical);
        Equal(0, f.World.Diplomacy.WarTarget); Equal(0, f.World.Diplomacy.Demand);
    }
}
