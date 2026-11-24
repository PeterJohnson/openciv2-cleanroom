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
    private static readonly AudienceConclusion QuietAudience = new(false, false, false, false, false, true, false);
    private sealed class ConversationFixture : IEmissaryEffects, IDiplomaticGiftEffects, IDiplomaticFavorEffects, INegotiatedMapEffects
    {
        public DemandFixture F { get; }
        public GameState World => F.World;
        public RevelationFixture Data => F.Data;
        public DiplomaticConversation Conversation { get; }
        public DiplomaticStance Stance { get; }
        public EmissaryReception Emissaries { get; }
        public List<ConversationNotice> Notices { get; } = [];
        public int Closed, Refreshed, MapAccepted, Published;
        public bool FailClose, FailRefresh;
        public ConversationNoticeKind? FailNotice;
        public ConversationFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            F = new(choices, data);
            var government = new GovernmentChanges(World, Data.Process.Rules, Data.Stacks,
                _ => throw new InvalidOperationException("Unexpected government choice"), _ => throw new InvalidOperationException("Unexpected rates"), _ => { });
            var stance = Stance = new DiplomaticStance(World, Data.Process.Rules, new(World, Data.Process.Rules, government, F.T.T.Random));
            var emissaries = Emissaries = new EmissaryReception(World, Data.Rules, F.T.T.Random, stance, this);
            var gifts = new DiplomaticGifts(World, Data.Process.Rules, F.T.T.Random, F.T.Service, F.T.T.Research, Data.Stacks, this);
            var favors = new DiplomaticFavors(World, Data.Process.Rules, F.T.T.Random, stance, F.T.Service, F.T.T.Research,
                new(World, this), FoundationSuite.War(Data, F.F.WarEffects, F.T.T.Random), this);
            Conversation = new(World, Data.Rules, stance, emissaries,
                new(World, Data.Rules, _ => throw new InvalidOperationException("Senate query must not present an attack notice")),
                new(World, Data.Rules, Data.Stacks), F.Proposals, favors, gifts,
                n => { Notices.Add(n); if (FailNotice == n.Kind) { throw new InvalidOperationException("Required conversation notice failed"); } });
            World.Diplomacy.MeetingActive = World.Diplomacy.AiMeetingActive = World.Diplomacy.AskedEmissary = true;
            World.Diplomacy.InitialEvaluationDone = true; World.Diplomacy.EmissaryResponse = 1;
        }
        public DiplomaticConversation.Operation Begin(bool player = true, AudienceConclusion? opening = null) =>
            Conversation.Begin(1, 2, player, new(10, 8), opening ?? QuietAudience, "conversation");
        public DiplomaticConversation.Operation Cleanup(bool player = true, AudienceConclusion? opening = null) =>
            Conversation.BeginCleanup(1, 2, player, opening ?? QuietAudience, "conversation");
        public void Notice(EmissaryNotice notice) { }
        public void OpenLeader(int counterpart) { }
        public void CloseLeader() { ++Closed; if (FailClose) { throw new InvalidOperationException("Cosmetic leader close failed"); } }
        public void EnsureVisible(MapPoint point, int observer) { }
        public void Notice(DiplomaticGiftNotice notice) { }
        public void Notice(DiplomaticFavorNotice notice) { }
        public void RefreshStatus() { ++Refreshed; if (FailRefresh) { throw new InvalidOperationException("Cosmetic status failed"); } }
        public void RefreshCity(CitySlot city) { }
        public void Publish() => ++Published;
        public void Accepted(int requester, DiplomaticParty grantor) => ++MapAccepted;
        public void RedrawMaps(int localHuman) { }
        public void War() => World.Civilizations[1].Treaties[2] = World.Civilizations[2].Treaties[1] = new() { Contact = true, War = true };
        public void Wonder(int wonder, int owner = 2)
        { Data.AddCity(5, owner, new(24, 8)); World.Globals.WonderCity[wonder] = 5; }
        public void Done(DiplomaticConversation.Operation op)
        { Equal(true, op.Completed); Equal(false, World.Diplomacy.AiMeetingActive); Equal(1, Closed); F.T.T.Random.RequireChoicesConsumed(); }
    }

    private static void ConversationActualMenus()
    {
        var f = new ConversationFixture(); f.World.Civilizations[1].Gold = 150; f.FailRefresh = true;
        using var gift = f.Begin(); Equal(true, gift.Resume() is DiplomaticConversationDecision.Menu);
        Equal(true, gift.Resume(new(8)) is DiplomaticConversationDecision.Gift { Decision: DiplomaticGiftDecision.Menu });
        Equal(true, gift.Resume(new(2)) is DiplomaticConversationDecision.Gift { Decision: DiplomaticGiftDecision.Gold });
        Equal(true, gift.Resume(new(150)) is DiplomaticConversationDecision.Menu);
        Equal(0, f.World.Civilizations[1].Gold); Equal(927, f.World.Civilizations[2].Gold);
        Equal(1, f.World.Diplomacy.GiftsThisMeeting); Equal((sbyte)0, f.World.Civilizations[2].DiploRequestCount);
        Equal(true, f.World.Diplomacy.AiMeetingActive); gift.Resume(new(0)); f.Done(gift); Equal(1, gift.PresentationFailures.Count);
        f = new(); f.F.T.Know(1, AdvanceIds.MapMaking); f.F.T.Know(2, AdvanceIds.MapMaking); f.World.Diplomacy.Attitude = 0;
        f.World.Map.TileAt(new(2, 2)).RevealedTo = CivilizationSet.Only(2);
        using var favor = f.Begin(); favor.Resume(); Equal(true, favor.Resume(new(7)) is DiplomaticConversationDecision.Favor);
        Equal(true, favor.Resume(new(3)) is DiplomaticConversationDecision.Menu);
        Equal(1, f.MapAccepted); Equal(1, f.Published); Equal(true, f.World.Map.TileAt(new(2, 2)).RevealedTo.Contains(1));
        Equal((sbyte)3, f.World.Civilizations[2].DiploRequestCount); favor.Resume(new(0)); f.Done(favor);
        f = new(); using var proposal = f.Begin(); proposal.Resume();
        Equal(true, proposal.Resume(new(1)) is DiplomaticConversationDecision.Menu);
        Equal(true, f.World.Civilizations[1].Treaties[2].Alliance); Equal(1, f.F.F.Agreements.Count);
        Equal(1, f.F.BlinkClears); proposal.Resume(new(0)); f.Done(proposal);
        f = new([DemandProvoke(true, "conversation/round-0/proposal/demand")]); f.World.Diplomacy.DeclareWar = true;
        using var provoke = f.Begin(); provoke.Resume(); provoke.Resume(new(4)); f.Done(provoke);
        Equal(true, f.World.Civilizations[1].Treaties[2].War); Equal((sbyte)5, f.World.Civilizations[2].DiploRequestCount);
    }

    private static void ConversationGreetingsAndAdmission()
    {
        foreach (bool player in new[] { false, true }) foreach (int relation in new[] { 0, 1, 2 })
            foreach (bool demanded in new[] { false, true })
            {
                var f = new ConversationFixture(); f.F.Peace(relation == 1); if (relation == 2) { f.F.Alliance(); }
                f.World.Diplomacy.Demand = demanded ? 999 : 0;
                using var op = f.Begin(player); Equal(true, op.Resume() is DiplomaticConversationDecision.Menu);
                var expected = demanded ? new[] { ConversationNoticeKind.Attitude, ConversationNoticeKind.AttitudePeace, ConversationNoticeKind.AttitudeAlly }
                    : player ? new[] { ConversationNoticeKind.Howdy, ConversationNoticeKind.HowdyPeace, ConversationNoticeKind.HowdyAlly }
                    : new[] { ConversationNoticeKind.Welcome, ConversationNoticeKind.WelcomePeace, ConversationNoticeKind.WelcomeAlly };
                Equal(expected[relation], f.Notices[0].Kind); Equal(demanded ? 4 : 2, f.Notices[0].Reaction);
                op.Resume(new(0)); f.Done(op);
            }
        foreach (bool allied in new[] { false, true })
        {
            var f = new ConversationFixture(); if (allied) { f.F.Alliance(); }
            f.World.Civilizations[2].DiploRequestCount = 2;
            using var op = f.Begin(); op.Resume(); Equal(allied ? ConversationNoticeKind.DoodyAlly : ConversationNoticeKind.Doody, f.Notices[0].Kind);
            Equal(allied ? 2 : 3, f.Notices[0].Reaction); op.Resume(new(0)); f.Done(op);
        }
        foreach (bool vendetta in new[] { false, true })
        {
            var f = new ConversationFixture(); f.World.Diplomacy.Fear = 3; f.World.Civilizations[2].Treaties[1].Vendetta = vendetta;
            using var op = f.Begin(false); op.Resume(); Equal(vendetta ? DiplomaticAddress.Stern : DiplomaticAddress.Fearful, f.Notices[0].Address);
            op.Resume(new(0)); f.Done(op);
        }
        var quiet = new ConversationFixture(); quiet.World.Diplomacy.AskedEmissary = false;
        using var skipped = quiet.Begin(false); skipped.Resume(); quiet.Done(skipped);
        Equal(0, quiet.Notices.Count); Equal((sbyte)1, quiet.World.Civilizations[2].DiploRequestCount);
        foreach (bool accept in new[] { false, true })
        {
            var f = new ConversationFixture(accept ? [GreetingChoice("conversation/emissary/greet")] : []);
            f.F.Peace(false); f.World.Civilizations[2].Treaties[1].Contact = false;
            f.World.Diplomacy.AskedEmissary = false; f.World.Diplomacy.EmissaryResponse = 0;
            using var op = f.Begin(false, QuietAudience with { NoContact = true });
            Equal(true, op.Resume() is DiplomaticConversationDecision.Emissary { Decision: EmissaryDecision.Receive });
            var next = op.Resume(new(accept ? 0 : 1));
            if (accept) { Equal(true, next is DiplomaticConversationDecision.Menu); op.Resume(new(0)); }
            f.Done(op);
        }
    }

    private static void ConversationDenunciation()
    {
        foreach (bool blocked in new[] { false, true }) foreach (bool failed in new[] { false, true })
        {
            var f = new ConversationFixture(); var w = f.World;
            w.Civilizations[1].ReputationCredit[3] = w.Civilizations[1].ReputationCredit[7] = -2;
            w.Civilizations[1].ReputationCredit[6] = -8; w.Civilizations[2].Treaties[6].War = true;
            w.Civilizations[1].Treaties[7].War = true; w.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
            w.Civilizations[2].Treaties[1].Aggressor = blocked;
            w.Civilizations[2].MilitaryStrength = 100; w.Civilizations[1].MilitaryStrength = 10;
            if (failed) { f.FailNotice = ConversationNoticeKind.NotoriousBig; }
            using var op = f.Begin(opening: QuietAudience with { TreatyMade = true });
            if (failed && !blocked)
            {
                Throws<InvalidOperationException>(() => op.Resume()); Equal((sbyte)-1, w.Civilizations[1].ReputationCredit[7]);
                Equal(true, w.Diplomacy.AiMeetingActive); Equal(0, f.Closed); Throws<InvalidOperationException>(() => op.Resume());
            }
            else
            {
                op.Resume(); Equal(blocked ? 0 : 1, f.Notices.Count);
                if (!blocked) { Equal(7, f.Notices[0].Third!.Civilization); Equal(ConversationNoticeKind.NotoriousBig, f.Notices[0].Kind); }
                Equal((sbyte)-2, w.Civilizations[1].ReputationCredit[3]); Equal((sbyte)-8, w.Civilizations[1].ReputationCredit[6]);
                op.Resume(new(0)); f.Done(op);
            }
        }
    }

    private static void ConversationForcedTreaties()
    {
        foreach (bool active in new[] { false, true }) foreach (bool fear in new[] { false, true })
        {
            var f = new ConversationFixture(); f.War(); var w = f.World;
            w.Diplomacy.AskedEmissary = false; w.Diplomacy.Fear = fear ? 1 : 0;
            w.Civilizations[1].Government = Government.Republic; w.Civilizations[1].Flags.SenateActive = active;
            using var op = f.Begin(false); op.Resume(); f.Done(op);
            Equal(active && fear, w.Civilizations[1].Treaties[2].Peace);
            Equal(!(active && fear), w.Civilizations[1].Treaties[2].Ceasefire);
            Equal((short)(active && fear ? 116 : 100), w.Civilizations[1].LastContactTurn[2]);
            Equal(0, f.F.F.Agreements.Count); // Bare treaty primitives, not negotiated wrappers.
            Equal(active ? fear ? 0 : 1 : 2, f.Notices.Count);
            if (!(active && fear)) { Equal(active ? ConversationNoticeKind.SenateCease : ConversationNoticeKind.ContinueHawks, f.Notices[0].Kind); }
        }
        foreach (int wonder in new[] { WonderIds.GreatWall, WonderIds.UnitedNations })
        {
            var f = new ConversationFixture(); f.Wonder(wonder); f.F.Peace(false); f.World.Diplomacy.AskedEmissary = false;
            f.World.Civilizations[2].Treaties[1].Contact = false;
            using var op = f.Begin(false); op.Resume(); f.Done(op); Equal((short)116, f.World.Civilizations[1].LastContactTurn[2]); Equal(0, f.Notices.Count);
            f = new(); f.Wonder(wonder); f.War(); f.World.Diplomacy.AskedEmissary = false;
            using var cease = f.Begin(false); cease.Resume(); f.Done(cease);
            Equal(wonder == WonderIds.GreatWall ? ConversationNoticeKind.WallCease : ConversationNoticeKind.UNCease, f.Notices.Single().Kind);
            f = new(); f.Wonder(wonder); f.War(); f.World.Diplomacy.AskedEmissary = false; f.World.Civilizations[2].Treaties[1].Peace = true;
            using var gated = f.Begin(false); gated.Resume(); f.Done(gated); Equal(true, f.World.Civilizations[2].Treaties[1].War); Equal(0, f.Notices.Count);
        }
        var hostile = new ConversationFixture(); hostile.War(); hostile.World.Diplomacy.AskedEmissary = false;
        hostile.World.Civilizations[1].Reputation = 7; hostile.World.Civilizations[2].Treaties[1].Vendetta = true;
        hostile.World.Civilizations[2].Treaties[7].Alliance = true;
        using var declared = hostile.Begin(false); declared.Resume(); hostile.Done(declared);
        Equal(true, hostile.World.Diplomacy.DeclareWar); Equal(true, hostile.World.Civilizations[1].Treaties[2].War); Equal(0, hostile.Notices.Count);
    }

    private static void ConversationBordersAndRegions()
    {
        foreach (int slot in new[] { 0, 2 })
        {
            var f = new ConversationFixture(); f.F.Peace(false);
            foreach (ref var city in f.World.Entities.Cities.Scanned) { if (city.Token.Occupied) { city.Owner = new(3); } }
            f.Data.AddCity(slot, 2, new(8, 8));
            f.World.Entities.Cities.Storage[slot].Name = "Border city"; f.Data.Unit(0).Owner = new(1);
            f.Data.Unit(0).Type = new(0); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Attack };
            f.Data.Stacks.Relocate(0, new(10, 8), false);
            using var op = f.Begin(); op.Resume(); var border = f.Notices.Single(n => n.City is not null);
            Equal(slot < 2 ? ConversationNoticeKind.PleaseCity : ConversationNoticeKind.PleaseCities, border.Kind);
            Equal("Border city", border.CityName); Equal(1, f.World.Diplomacy.Intrusions.Cities);
            Equal(true, f.Data.Unit(0).Flags.BorderWarned && f.Data.Unit(0).Flags.BorderIntruder); op.Resume(new(0)); f.Done(op);
        }
        var regions = new ConversationFixture(); regions.F.Alliance(); var w = regions.World;
        w.Diplomacy.StrongestEnemy = 3; w.Diplomacy.WeakestEnemy = 4;
        w.Civilizations[2].CityCount = 2; w.Civilizations[2].ContinentCities[63] = 3; w.Civilizations[3].ContinentAttack[63] = 10;
        w.Civilizations[4].CityCount = 1; w.Civilizations[4].ContinentCities[62] = 1; w.Civilizations[2].ContinentAttack[62] = 10;
        using var brag = regions.Begin(); brag.Resume(); Equal(ConversationNoticeKind.AllyBrag, regions.Notices.Last().Kind); brag.Resume(new(0)); regions.Done(brag);
        regions = new(); regions.F.Alliance(); w = regions.World; w.Diplomacy.StrongestEnemy = 3;
        w.Civilizations[2].CityCount = 2; w.Civilizations[2].ContinentCities[62] = 2; w.Civilizations[3].ContinentAttack[62] = 10;
        using var plea = regions.Begin(); plea.Resume(); Equal(ConversationNoticeKind.AllyPlea, regions.Notices.Last().Kind); plea.Resume(new(0)); regions.Done(plea);
    }

    private static void ConversationCleanupAndBoundedCounters()
    {
        var f = new ConversationFixture(); var w = f.World; f.F.Peace(false); w.Diplomacy.HadCeasefire = true;
        w.Civilizations[1].Treaties[2].Incident = w.Civilizations[2].Treaties[1].Incident = true;
        w.Diplomacy.DeclareWar = true; w.Civilizations[2].DiploRequestCount = -128;
        using var op = f.Cleanup(opening: QuietAudience with { Ended = true, Conceded = true });
        Equal(null, op.Resume()); Equal(false, op.Completed); Equal((sbyte)-64, w.Civilizations[2].DiploRequestCount);
        Equal(false, w.Civilizations[1].Treaties[2].Incident || w.Civilizations[2].Treaties[1].Incident);
        Equal(true, w.Civilizations[2].Treaties[1].HostileIntent); Equal(0, f.Closed);
        op.Resume(); Equal((sbyte)0, w.Civilizations[2].DiploRequestCount); Equal(false, op.Completed);
        w.Diplomacy.Attitude = 100; op.Resume(); f.Done(op); Equal((sbyte)1, w.Civilizations[2].DiploRequestCount);
        Equal(ConversationNoticeKind.CeaseExpire, f.Notices.Single().Kind);
        Equal(false, w.Diplomacy.AskedEmissary || w.Diplomacy.MeetingActive || w.Diplomacy.InitialEvaluationDone); Equal(0, w.Diplomacy.EmissaryResponse);
        foreach (bool war in new[] { false, true })
        {
            f = new(); if (war) { f.War(); } else { f.F.Alliance(); }
            f.World.Diplomacy.DeclareWar = true; f.World.Civilizations[2].Treaties[1].HostileIntent = true;
            using var clear = f.Cleanup(opening: QuietAudience with { Hostile = true }); clear.Resume(); f.Done(clear);
            Equal(false, f.World.Civilizations[2].Treaties[1].HostileIntent); Equal((sbyte)(war ? 3 : 7), f.World.Civilizations[2].DiploRequestCount);
        }
    }

    private static void ConversationCleanupIntelligenceAndTutorial()
    {
        var f = new ConversationFixture(); var w = f.World; w.Globals.Difficulty = Difficulty.Chieftain;
        f.F.T.Know(2, AdvanceIds.Writing); w.Multiplayer.LocalHuman = 3;
        using var op = f.Cleanup(); Equal(true, op.Resume() is DiplomaticConversationDecision.Intelligence);
        Equal(new UnitType(UnitTypeIds.Diplomat), f.Notices.Single().Unit);
        Equal(f.Data.Rules.Units[UnitTypeIds.Diplomat].Identity.Name, f.Notices.Single().UnitName);
        Equal(true, w.Civilizations[1].Treaties[2].Embassy); Equal(false, w.Civilizations[3].Treaties[2].Embassy);
        Equal(false, w.Diplomacy.MeetingActive); Equal(true, w.Diplomacy.AiMeetingActive); Equal(1, f.Closed);
        Equal(op.Pending, op.Resume()); op.Resume(new(0)); f.Done(op);
        foreach (bool player in new[] { false, true })
        {
            f = new(); f.War(); w = f.World; w.Globals.GameOptions.Tutorial = true;
            w.Diplomacy.EmissaryResponse = -1; w.Civilizations[2].CityCount = 5; w.Globals.PowerRank[1] = 1; w.Globals.PowerRank[2] = 4;
            using var tutorial = f.Cleanup(player); tutorial.Resume(); f.Done(tutorial);
            Equal(player ? 1 : 0, f.Notices.Count); if (player) { Equal(ConversationNoticeKind.SchismTutorial, f.Notices[0].Kind); }
        }
    }

    private static void ConversationFailuresAndPatience()
    {
        var f = new ConversationFixture(); f.FailClose = true;
        using var cosmetic = f.Cleanup(); cosmetic.Resume(); f.Done(cosmetic); Equal(1, cosmetic.PresentationFailures.Count);
        f = new(); f.F.Peace(false); f.World.Diplomacy.HadCeasefire = true; f.FailNotice = ConversationNoticeKind.CeaseExpire;
        using var failed = f.Cleanup(); Throws<InvalidOperationException>(() => failed.Resume());
        Equal(1, f.Closed); Equal(false, f.World.Diplomacy.MeetingActive); Equal(true, f.World.Diplomacy.AiMeetingActive);
        Throws<InvalidOperationException>(() => failed.Resume());
        f = new(); f.World.Diplomacy.Attitude = 90; f.World.Civilizations[2].DiploRequestCount = 1;
        using var patience = f.Begin(); patience.Resume(); patience.Resume(new(7)); patience.Resume(new(3)); f.Done(patience);
        Equal(ConversationNoticeKind.Patience, f.Notices.Last().Kind); Equal((sbyte)3, f.World.Civilizations[2].DiploRequestCount);
        f = new(); f.World.Civilizations[2].DiploRequestCount = 127;
        using var wrap = f.Begin(); wrap.Resume(); Equal(true, wrap.Resume(new(99)) is DiplomaticConversationDecision.Menu);
        Equal((sbyte)0, f.World.Civilizations[2].DiploRequestCount); wrap.Resume(new(0)); f.Done(wrap);
        f = new(); f.World.Globals.HumanPlayers = f.World.Globals.HumanPlayers.With(2); f.F.T.Know(1, AdvanceIds.Monarchy);
        f.World.Multiplayer.LocalHuman = 2; f.World.Civilizations[2].Government = Government.Despotism;
        using var stopped = f.Begin(); stopped.Resume(); stopped.Resume(new(8)); stopped.Resume(new(1));
        Equal(true, stopped.Resume(new(1)) is DiplomaticConversationDecision.Gift { Decision: DiplomaticGiftDecision.Grant });
        Equal(true, f.World.Civilizations[2].Advances.Contains(AdvanceIds.Monarchy)); stopped.Dispose();
        Throws<ObjectDisposedException>(() => stopped.Resume(new(0))); Equal(0, f.Closed); Equal(true, f.World.Diplomacy.AiMeetingActive);
    }

    private static void ConversationPrivateGrantSession()
    {
        using var runtime = new SessionRuntime(new(), new(255));
        var f = new ConversationFixture(data: CreationFixture(new(runtime.Process, runtime.World)));
        f.World.Globals.HumanPlayers = f.World.Globals.HumanPlayers.With(2); f.F.T.Know(1, AdvanceIds.Monarchy);
        f.World.Multiplayer.LocalHuman = 2; f.World.Civilizations[2].Government = Government.Despotism;
        ParticipantId first = default, second = default;
        runtime.RegisterCommand(new(new(1, "test.conversation", "Authored audience conversation", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new DiplomaticConversationOperation(f.Begin(), c => c == 1 ? first : second,
                _ => throw new InvalidOperationException("Unexpected intelligence"))));
        runtime.BeginClientSession(new()); var a = SessionClient(runtime, 1); var b = SessionClient(runtime, 2);
        first = a.Identity.Participant; second = b.Identity.Participant;
        var command = a.Submit(new("test.conversation", new([]))).Correlation.Command; runtime.AdvanceOne();
        foreach (int answer in new[] { 8, 1, 1 })
        { var prompt = RequiredInteraction(a, command); Equal(true, a.Reply(prompt, answer).Accepted); runtime.AdvanceOne(); }
        var grant = RequiredInteraction(b, command); Equal(null, RequiredResult(a, command).Interaction);
        Equal(false, a.Reply(grant, 0).Accepted); Equal(true, f.World.Civilizations[2].Advances.Contains(AdvanceIds.Monarchy));
        Equal(0, f.Closed); Equal(true, b.Reply(grant, 0).Accepted); runtime.AdvanceOne();
        var menu = RequiredInteraction(a, command); Equal(null, b.Result(command));
        Equal(true, a.Reply(menu, 0).Accepted); runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(a, command).Status);
        Equal(1, f.World.Diplomacy.GiftsThisMeeting); Equal(false, f.World.Diplomacy.AiMeetingActive); Equal(1, f.Closed);
    }

    private static void ConversationSessionCancellationAndIntelligence()
    {
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var f = new ConversationFixture(); var context = new OperationContext(f.World, f.Data.Process, f.F.T.T.Random,
                new PresentationRandom(1, "conversation"), new(default, new(1)), default, (_, _) => { }, () => { });
            using var op = new DiplomaticConversationOperation(f.Begin(), c => new((ulong)c), _ => throw new InvalidOperationException());
            Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
            foreach (int menu in new[] { 7, 8 })
            {
                Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new(OperationInputKind.Choice, menu)).Status);
                Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new(input)).Status);
                Equal((sbyte)0, f.World.Civilizations[2].DiploRequestCount);
            }
            Equal(CommandStatus.Completed, op.Resume(context, new(input)).Status); Equal(1, f.Closed);
        }
        var intel = new ConversationFixture(); intel.World.Globals.Difficulty = Difficulty.Chieftain; intel.F.T.Know(2, AdvanceIds.Writing);
        var ctx = new OperationContext(intel.World, intel.Data.Process, intel.F.T.T.Random,
            new PresentationRandom(1, "intel"), new(default, new(1)), default, (_, _) => { }, () => { });
        int opened = 0;
        using var report = new DiplomaticConversationOperation(intel.Cleanup(), c => new((ulong)c), visit =>
        { ++opened; return new("Intelligence", new(99), null, new([0])); });
        var step = report.Resume(ctx, new()); Equal(new ParticipantId(1), step.Interaction!.Recipient);
        Equal(CommandStatus.AwaitingInteraction, report.Resume(ctx, new()).Status); Equal(1, opened);
        Equal(CommandStatus.Completed, report.Resume(ctx, new(OperationInputKind.Cancel)).Status); Equal(false, intel.World.Diplomacy.AiMeetingActive);
    }
}
