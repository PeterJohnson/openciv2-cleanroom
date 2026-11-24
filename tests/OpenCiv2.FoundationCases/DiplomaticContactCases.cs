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
    // V04 §10: real V05 parent, A08 security/exchange, V03 grants and V04 obligations.
    private sealed class ContactFixture : IAllyHelpEffects
    {
        public AudienceFixture F { get; }
        public GameState World => F.World;
        public RevelationFixture Data => F.Data;
        public DiplomaticContact Service { get; }
        public List<ContactNotice> Notices { get; } = [];
        public ContactNoticeKind? FailNotice;
        public int NoHelp, Refreshed;
        public ContactFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            F = new(choices, data); World.Globals.Turn = 99; World.Globals.PlayerCiv = 1;
            World.Civilizations[2].Government = World.Civilizations[3].Government = Government.Republic;
            World.Civilizations[3].Tribe = 3;
            var war = War(Data, F.C.F.F.WarEffects, F.T.T.Random);
            Service = new(World, Data.Process.Rules, F.C.Stance, F.Service,
                new(World, Data.Process.Rules, F.T.T.Random, F.T.T.Research), war, new(World, F.C.Emissaries, war, this),
                F.ExecuteEvent, n => { Notices.Add(n); if (FailNotice == n.Kind) { throw new InvalidOperationException("Required contact notice failed"); } });
        }
        public DiplomaticContact.Operation Begin(int a = 2, int b = 3) => Service.Begin(a, b, new(10, 8), "contact");
        public void DidNotHelp(AllyHelpRequest request) => ++NoHelp;
        public void RefreshStatus() => ++Refreshed;
        public void Done(DiplomaticContact.Operation op)
        { Equal(true, op.Completed); F.T.T.Random.RequireChoicesConsumed(); }
        public void Finish(DiplomaticContact.Operation op) { Equal(null, op.Resume()); Done(op); }
        public void Pair(bool peace = false, bool alliance = false) =>
            World.Civilizations[2].Treaties[3] = World.Civilizations[3].Treaties[2] = new() { Contact = true, Peace = peace, Alliance = alliance };
        public void Hostile(int a, int b, bool war = false)
        { World.Civilizations[a].Treaties[b] = new() { Contact = true, War = war }; World.Civilizations[a].Attitude[b] = 100; }
        public void Wonder(int owner, int wonder, int slot)
        { Data.AddCity(slot, owner, new(20 + 2 * slot, 8)); World.Globals.WonderCity[wonder] = (short)slot; }
    }

    private static void ContactAdmissionAndNeedsAudience()
    {
        foreach (int gate in Enumerable.Range(0, 3))
        {
            var f = new ContactFixture(); f.World.Events.Loaded = true; NegotiationEvent(f.World, 2, 3); f.F.FailEvent = true;
            if (gate == 2) { f.World.Globals.Turn = 0; }
            using var op = f.Begin(gate == 0 ? 0 : 2, gate == 1 ? 0 : 3); f.Finish(op);
            Equal(0, f.F.Events); Equal(false, f.World.Civilizations[2].Treaties[3].Contact);
        }
        foreach (bool deny in new[] { false, true })
        {
            var f = new ContactFixture(); f.World.Events.Loaded = true; var e = NegotiationEvent(f.World, 2, 3, deny: deny);
            e.Values.Actions.ChangeMoney = true; e.Values.MoneyReceiverCiv = 2; e.Values.MoneyAmount = 10;
            using var op = f.Begin(); f.Finish(op); Equal(1, f.F.Events); Equal(787, f.World.Civilizations[2].Gold);
            Equal(!deny, f.World.Civilizations[2].Treaties[3].Contact); Equal(false, f.World.Diplomacy.AiMeetingActive);
        }
        var needs = new ContactFixture(); var w = needs.World;
        w.Civilizations[1].LastContactTurn[2] = 84; Equal(false, needs.Service.NeedsAudience(1, 2));
        w.Civilizations[1].LastContactTurn[2] = 83; Equal(true, needs.Service.NeedsAudience(1, 2));
        w.Civilizations[1].LastContactTurn[2] = 100; Equal(false, needs.Service.NeedsAudience(1, 2));
        w.Civilizations[1].Treaties[2].Incident = true; Equal(true, needs.Service.NeedsAudience(1, 2));
        w.Civilizations[1].Treaties[2].Incident = false; w.Civilizations[1].LastContactTurn[2] = -1; Equal(true, needs.Service.NeedsAudience(1, 2));
        w.Civilizations[1].LastContactTurn[2] = 99; w.Civilizations[1].Treaties[2].Contact = false; Equal(true, needs.Service.NeedsAudience(1, 2));
        // No added alive or distinct-pair filter.
        var self = new ContactFixture(); using var same = self.Begin(3, 3); self.Finish(same);
        Equal(true, self.World.Civilizations[3].Treaties[3].Contact && self.World.Civilizations[3].Treaties[3].Peace);
    }

    private static void ContactPublicationCadenceAndHistory()
    {
        foreach (bool contact in new[] { false, true }) foreach (int turn in new[] { 98, 99 })
        {
            var f = new ContactFixture(); f.World.Globals.Turn = (short)turn; f.Pair();
            f.World.Civilizations[2].Treaties[3].Contact = contact;
            using var op = f.Begin(); f.Finish(op);
            var forward = f.World.Civilizations[2].Treaties[3]; var reverse = f.World.Civilizations[3].Treaties[2];
            Equal(true, forward.Contact && reverse.Contact && forward.RecentContact && reverse.RecentContact);
            Equal(!contact, forward.ChangedThisTurn && reverse.ChangedThisTurn); Equal(!contact || turn == 99, forward.Peace);
        }
        foreach (bool historical in new[] { false, true })
        {
            var f = new ContactFixture(); var w = f.World; w.Globals.HumanPlayers = default; w.Globals.Turn = 4;
            w.Globals.Status.Scenario = true; w.Scenario.Flags.HistoricalAi = historical;
            w.Civilizations[1].Government = Government.Republic;
            using var op = f.Begin(1, 3); f.Finish(op); Equal(!historical, w.Civilizations[1].Treaties[3].Peace);
            Equal(true, w.Civilizations[1].Treaties[3].ChangedThisTurn && w.Civilizations[1].Treaties[3].RecentContact);
        }
    }

    private static void ContactPeaceNoticesAndForcing()
    {
        foreach (int observer in Enumerable.Range(0, 4)) foreach (bool autoplay in new[] { false, true })
        {
            var f = new ContactFixture(); f.Pair(); var w = f.World;
            w.Multiplayer.Saved.AutoplayActive = autoplay;
            if (observer == 1) { w.Civilizations[1].Treaties[2].Alliance = true; }
            if (observer == 2) { w.Civilizations[1].Treaties[3].Alliance = true; }
            if (observer == 3) { w.Civilizations[1].Treaties[3].Embassy = true; }
            w.Civilizations[2].Attitude[3] = 90; w.Civilizations[3].DiploRequestCount = 6;
            using var op = f.Begin(); f.Finish(op); Equal(true, w.Civilizations[2].Treaties[3].Peace);
            Equal(false, w.Civilizations[2].Treaties[3].ChangedThisTurn);
            Equal((byte)90, w.Civilizations[2].Attitude[3]); Equal((sbyte)6, w.Civilizations[3].DiploRequestCount);
            Equal(autoplay || observer == 0 ? 0 : 1, f.Notices.Count);
            if (!autoplay && observer != 0)
            { Equal(observer == 3 ? ContactNoticeKind.SignPeace : ContactNoticeKind.AllyMakesPeace, f.Notices.Single().Kind); Equal(observer == 2 ? 3 : 2, f.Notices.Single().First.Civilization); }
        }
        var forced = new ContactFixture(); var world = forced.World; forced.Pair();
        world.Civilizations[2].Treaties[3].Aggressor = world.Civilizations[3].Treaties[2].Aggressor = true;
        forced.Wonder(3, WonderIds.UnitedNations, 4); forced.Wonder(3, WonderIds.GreatWall, 5); forced.Wonder(2, WonderIds.GreatWall, 6);
        using var treaty = forced.Begin(); forced.Finish(treaty);
        Sequence(new[] { ContactNoticeKind.UNForce, ContactNoticeKind.WallForce }, forced.Notices.Select(n => n.Kind));
        Equal(2, forced.Notices[0].First.Civilization); Equal(3, forced.Notices[1].First.Civilization);
        Equal(false, world.Civilizations[2].Treaties[3].Aggressor || world.Civilizations[3].Treaties[2].Aggressor);
    }

    private static void ContactExchangeAndFailurePrefix()
    {
        foreach (bool fail in new[] { false, true })
        {
            var f = new ContactFixture([ExchangeDraw(94, true, call: "contact/exchange"), ExchangeDraw(95, call: "contact/exchange")]);
            f.F.T.Know(2, 94); f.F.T.Know(3, 95); f.World.Globals.RevealMap = 1;
            if (fail) { f.FailNotice = ContactNoticeKind.SignPeace; }
            using var op = f.Begin();
            if (fail) { Throws<InvalidOperationException>(() => op.Resume()); Throws<InvalidOperationException>(() => op.Resume()); }
            else { f.Finish(op); }
            Equal(true, f.World.Civilizations[2].Advances.Contains(95)); Equal(true, f.World.Civilizations[3].Advances.Contains(94));
            Equal(!fail, f.World.Civilizations[2].Treaties[3].Peace); Equal(true, f.World.Civilizations[2].Treaties[3].Contact);
            f.F.T.T.Random.RequireChoicesConsumed();
        }
    }

    private static void ContactAllianceObserverAndJoins()
    {
        foreach (bool observed in new[] { false, true }) foreach (bool autoplay in new[] { false, true })
        {
            var f = new ContactFixture(); f.Pair(true); var w = f.World;
            f.Hostile(2, 4, true); f.Hostile(3, 4); w.Globals.PowerRank[4] = 5;
            w.Globals.RevealMap = observed ? (byte)1 : (byte)0; w.Multiplayer.Saved.AutoplayActive = autoplay;
            using var op = f.Begin(); f.Finish(op);
            Equal(true, w.Civilizations[2].Treaties[3].Alliance); Equal(observed, w.Civilizations[3].Treaties[4].War);
            Equal(observed ? 1 : 0, f.F.C.F.F.WarEffects.Notices.Count); // JoinedWar still runs under autoplay.
            Equal(observed && !autoplay ? 1 : 0, f.Notices.Count);
            if (f.Notices.Count != 0) { Equal(ContactNoticeKind.SignAllied, f.Notices.Single().Kind); Equal(4, f.Notices.Single().Target!.Civilization); }
        }
        // A human target plus local Contact qualifies even without an embassy or reveal-map.
        var human = new ContactFixture(); human.Pair(true); var world = human.World; world.Globals.Turn = 203;
        world.Globals.PowerRank[1] = 7; world.Civilizations[1].CityCount = 5;
        human.Hostile(2, 1); human.Hostile(3, 1, true); world.Civilizations[1].LastContactTurn[2] = 300;
        using var join = human.Begin(); human.Finish(join);
        Equal(true, world.Civilizations[2].Treaties[1].War); Equal(false, world.Civilizations[2].Treaties[1].Incident);
        Equal(ContactNoticeKind.SignNato, human.Notices.Single().Kind);
        Equal(1, human.F.C.F.F.WarEffects.Notices.Count); Equal((short)203, world.Civilizations[1].LastContactTurn[2]);
    }

    private static void ContactAllianceLeaderAndReputation()
    {
        foreach (byte reputation in new byte[] { 0, 1 })
        {
            var f = new ContactFixture(); f.Pair(true); var w = f.World; w.Globals.Turn = 203;
            w.Globals.PlayerCiv = 7; w.Civilizations[7].Reputation = reputation; w.Civilizations[1].Reputation = 255;
            w.Civilizations[2].Treaties[1].Alliance = true;
            w.Globals.HumanPlayers = w.Globals.HumanPlayers.With(6); w.Globals.PowerRank[6] = 7; w.Civilizations[6].CityCount = 5;
            w.Globals.RevealMap = 1;
            f.Data.AddCity(5, 2, new(8, 8)); f.Data.AddCity(6, 3, new(12, 8)); w.Entities.Cities.Storage[6].Name = "Later treaty city";
            using var op = f.Begin(); f.Finish(op); Equal(reputation == 1, w.Civilizations[2].Treaties[3].Alliance);
            if (reputation == 1)
            { Equal(ContactNoticeKind.SignNato, f.Notices.Single().Kind); Equal(new CitySlot(6), f.Notices.Single().City); Equal("Later treaty city", f.Notices.Single().CityName); }
            else { Equal(0, f.Notices.Count); }
        }
    }

    private static void ContactAllianceRawCancellationAndHistory()
    {
        var f = new ContactFixture(); f.Pair(true, true); var w = f.World;
        w.Globals.PowerRank[2] = 7; w.Globals.PowerRank[3] = 4; w.Globals.RevealMap = 1;
        f.Hostile(2, 4, true); f.Hostile(3, 4, true); w.Globals.PowerRank[4] = 7;
        var position = f.Data.Unit(0).Position;
        using var op = f.Begin(); f.Finish(op); Equal(false, w.Civilizations[2].Treaties[3].Alliance);
        Equal(true, w.Civilizations[2].Treaties[3].Peace); Equal(position, f.Data.Unit(0).Position);
        Equal(0, f.F.C.F.F.WarEffects.Redraws.Count); Equal(ContactNoticeKind.CancelAllied, f.Notices.Single().Kind);
        foreach (bool historical in new[] { false, true })
        {
            f = new(); w = f.World; w.Globals.Status.Scenario = true; w.Scenario.Flags.HistoricalAi = historical;
            w.Civilizations[6].Government = w.Civilizations[7].Government = Government.Republic;
            w.Civilizations[6].Treaties[7] = w.Civilizations[7].Treaties[6] = new() { Contact = true, Peace = true, Alliance = true };
            using var special = f.Begin(6, 7); f.Finish(special); Equal(!historical, w.Civilizations[6].Treaties[7].Alliance);
        }
    }

    private static void ContactAllianceIncidentDirection()
    {
        foreach (bool firstWar in new[] { false, true }) foreach (bool launched in new[] { false, true })
        {
            var f = new ContactFixture(); var w = f.World; f.Pair(true); w.Globals.Turn = 203;
            w.Globals.PowerRank[1] = 7; w.Civilizations[1].CityCount = 5; w.Civilizations[1].SpaceshipFlags.Launched = launched;
            w.Civilizations[1].Treaties[2].Contact = w.Civilizations[1].Treaties[3].Contact = false;
            f.Hostile(2, 1, firstWar); f.Hostile(3, 1, !firstWar);
            using var op = f.Begin(); f.Finish(op);
            Equal(true, w.Civilizations[2].Treaties[3].Alliance);
            Equal(!firstWar && !launched, w.Civilizations[2].Treaties[1].Incident);
            Equal(!firstWar && !launched, w.Civilizations[3].Treaties[1].Incident); // Both tests read 2→1 War.
            Equal(0, f.F.C.F.F.WarEffects.Notices.Count);
        }
    }

    private static void ContactWarFlagsAndFailure()
    {
        foreach (bool peace in new[] { false, true }) foreach (bool fail in new[] { false, true })
        {
            var f = new ContactFixture(); f.Pair(peace); var w = f.World; w.Globals.RevealMap = 1;
            w.Civilizations[2].Government = Government.Despotism;
            w.Civilizations[2].Treaties[3].Vendetta = w.Civilizations[3].Treaties[2].Vendetta = true;
            w.Civilizations[2].Reputation = 4; w.Civilizations[2].Attitude[3] = 7;
            var kind = peace ? ContactNoticeKind.CancelPeace : ContactNoticeKind.DeclareWar;
            if (fail) { f.FailNotice = kind; }
            using var op = f.Begin();
            if (fail) { Throws<InvalidOperationException>(() => op.Resume()); }
            else { f.Finish(op); }
            Equal(kind, f.Notices.Single().Kind); Equal(!fail, w.Civilizations[2].Treaties[3].War);
            Equal(fail, w.Civilizations[2].Treaties[3].Vendetta);
            Equal(!fail, w.Civilizations[2].Treaties[3].Aggressor); Equal(false, w.Civilizations[3].Treaties[2].Aggressor);
            Equal((byte)4, w.Civilizations[2].Reputation); Equal((byte)7, w.Civilizations[2].Attitude[3]);
            Equal(0, f.F.C.F.F.WarEffects.Redraws.Count);
        }
        foreach (bool first in new[] { false, true })
        {
            var f = new ContactFixture(); var w = f.World; f.Pair();
            w.Civilizations[2].Government = Government.Despotism; w.Globals.RevealMap = 1;
            w.Civilizations[2].Treaties[3].War = true; w.Civilizations[2].Treaties[3].Contact = !first;
            w.Civilizations[2].Treaties[3].Vendetta = true;
            using var op = f.Begin(); f.Finish(op); Equal(first ? 1 : 0, f.Notices.Count);
            Equal(!first, w.Civilizations[2].Treaties[3].Vendetta); Equal(first, w.Civilizations[2].Treaties[3].Aggressor);
        }
    }

    private static void ContactActualAllyHelp()
    {
        foreach (bool fail in new[] { false, true })
        {
            var f = new ContactFixture([GreetingChoice("contact/ally-help/emissary/greet")]); var w = f.World; f.Pair();
            w.Civilizations[2].Government = w.Civilizations[3].Government = Government.Despotism;
            w.Civilizations[3].UnitsActive[UnitTypeIds.NuclearMissile] = 1; // Actual security says 2 needs help.
            w.Civilizations[1].Treaties[2].Alliance = w.Civilizations[2].Treaties[1].Alliance = true;
            w.Diplomacy.EmissaryResponse = 0;
            if (fail) { f.FailNotice = ContactNoticeKind.AllyMakesWar; }
            using var op = f.Begin(); Equal(true, op.Resume() is DiplomaticContactDecision.Ally { Value: AllyHelpDecision.Emissary });
            Equal(true, w.Civilizations[2].Treaties[3].RecentContact); Equal(false, w.Civilizations[2].Treaties[3].War);
            Equal(true, op.Resume(new(0)) is DiplomaticContactDecision.Ally { Value: AllyHelpDecision.Demand });
            if (fail) { Throws<InvalidOperationException>(() => op.Resume(new(1))); }
            else { op.Resume(new(1)); f.Done(op); }
            Equal(true, w.Civilizations[1].Treaties[3].War); Equal(!fail, w.Civilizations[2].Treaties[3].War);
            Equal(ContactNoticeKind.AllyMakesWar, f.Notices.Single().Kind); Equal(2, f.Notices.Single().First.Civilization);
            Equal(1, f.F.C.Closed); f.F.T.T.Random.RequireChoicesConsumed();
        }
    }

    private static void ContactAudienceRetentionAndScenarioRecheck()
    {
        var f = new ContactFixture([GreetingChoice("contact/first-audience/emissary/greet")]); var w = f.World;
        w.Events.Loaded = true; var e = NegotiationEvent(w, deny: false);
        e.Values.Actions.ChangeMoney = true; e.Values.MoneyReceiverCiv = 1; e.Values.MoneyAmount = 1;
        w.Civilizations[1].Treaties[2].Contact = w.Civilizations[2].Treaties[1].Contact = false;
        using var op = f.Begin(1, 2);
        Equal(true, op.Resume() is DiplomaticContactDecision.Audience { Value: DiplomaticAudienceDecision.Emissary });
        Equal(2, f.F.Events); Equal(5002, w.Civilizations[1].Gold);
        Equal(false, w.Civilizations[1].Treaties[2].RecentContact);
        // The retained audience can publish Contact before the parent's common continuation.
        w.Civilizations[1].Treaties[2].Contact = true; w.Diplomacy.Fear = 3;
        w.Civilizations[2].Treaties[1].Peace = true;
        op.Resume(new(0)); f.Done(op); Equal(true, w.Civilizations[2].Treaties[1].Contact);
        Equal(false, w.Civilizations[1].Treaties[2].ChangedThisTurn); Equal(1, f.F.C.Closed);
        var disposed = new ContactFixture(); using var pending = disposed.Begin(1, 2); pending.Resume(); pending.Dispose();
        Throws<ObjectDisposedException>(() => pending.Resume(new(1))); Equal(false, disposed.World.Civilizations[1].Treaties[2].RecentContact);
    }

    private static void ContactSessionPrivateCancellation()
    {
        foreach (var mode in new[] { GameMode.SinglePlayer, GameMode.Hotseat, GameMode.Email })
        {
            using var runtime = new SessionRuntime(new(), new(255));
            var f = new ContactFixture(data: CreationFixture(new(runtime.Process, runtime.World))); f.World.Globals.Multiplayer = mode;
            if (mode != GameMode.SinglePlayer) { f.World.Globals.HumanPlayers = f.World.Globals.HumanPlayers.With(2); }
            ParticipantId recipient = default;
            runtime.RegisterCommand(new(new(1, "test.contact", "Authored local contact", AccessClass.Player, new([]), new([])),
                (_, _, _) => new(), _ => new DiplomaticContactOperation(f.Begin(1, 2), _ => recipient,
                    _ => throw new InvalidOperationException("Unexpected intelligence"))));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var local = SessionClient(runtime, 1); recipient = local.Identity.Participant;
            var command = actor.Submit(new("test.contact", new([]))).Correlation.Command; runtime.AdvanceOne();
            var prompt = RequiredInteraction(local, command); Equal(null, RequiredResult(actor, command).Interaction);
            Equal(false, actor.Reply(prompt, 1).Accepted); Equal(true, local.Reply(prompt, 1).Accepted); runtime.AdvanceOne();
            Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
            Equal(true, f.World.Civilizations[1].Treaties[2].RecentContact); Equal(1, f.F.C.Closed);
            f.F.T.T.Random.RequireChoicesConsumed();
        }
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var f = new ContactFixture(); f.F.C.FailClose = true;
            var context = new OperationContext(f.World, f.Data.Process, f.F.T.T.Random,
                new PresentationRandom(1, "contact"), new(default, new(1)), default, (_, _) => { }, () => { });
            using var op = new DiplomaticContactOperation(f.Begin(1, 2), c => new((ulong)c), _ => throw new InvalidOperationException());
            Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
            var result = op.Resume(context, new(input)); Equal(CommandStatus.Failed, result.Status);
            Equal("contact_presentation_failed", result.Code); Equal(true, f.World.Civilizations[1].Treaties[2].RecentContact);
            Equal(false, f.World.Diplomacy.AiMeetingActive); Equal(1, f.F.C.Closed);
        }
    }

    private static void ContactPrivateLibraryGrant()
    {
        using var runtime = new SessionRuntime(new(), new(255));
        var f = new ContactFixture([ExchangeDraw(AdvanceIds.Monarchy, call: "contact/exchange"), ExchangeDraw(94, true, call: "contact/exchange")],
            CreationFixture(new(runtime.Process, runtime.World)));
        f.F.T.Know(3, AdvanceIds.Monarchy); f.F.T.Know(2, 94);
        f.Data.AddCity(0, 1, new(10, 8)); f.World.Civilizations[1].CityCount = 1; f.World.Globals.WonderCity[WonderIds.GreatLibrary] = 0;
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.contact-library", "Authored contact exchange", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new DiplomaticContactOperation(f.Begin(), _ => recipient, _ => throw new InvalidOperationException())));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var player = SessionClient(runtime, 1); recipient = player.Identity.Participant;
        var command = actor.Submit(new("test.contact-library", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(player, command); Equal(false, actor.Reply(prompt, 0).Accepted);
        Equal(null, RequiredResult(actor, command).Interaction);
        Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
        Equal(true, f.World.Civilizations[2].Advances.Contains(AdvanceIds.Monarchy));
        Equal(false, f.World.Civilizations[3].Advances.Contains(94)); Equal(false, f.World.Civilizations[2].Treaties[3].Peace);
        Equal(true, f.World.Civilizations[2].Treaties[3].RecentContact);
        Equal(true, player.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(null, player.Result(command));
        Equal(true, f.World.Civilizations[3].Advances.Contains(94)); Equal(true, f.World.Civilizations[2].Treaties[3].Peace);
        f.F.T.T.Random.RequireChoicesConsumed();
    }
}
