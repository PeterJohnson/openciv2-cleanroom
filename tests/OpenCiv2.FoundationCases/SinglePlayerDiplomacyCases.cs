using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // V04 §10/V05 §§2–6/S02 §7 production graph joins. Expectations are specification-derived.
    private sealed class SinglePlayerDiplomacyFixture
    {
        public EventFixture Events { get; }
        public GameState World => Events.World;
        public RevelationFixture Data => Events.Data;
        public SinglePlayerDiplomacy Services { get; }
        public ConversationFixture Conversation => Events.Contact.F.C;
        public RetainedGrantFixture Grant => Events.Grant;
        public SinglePlayerDiplomacyFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Events = new(data, choices);
            var audience = Events.Contact.F;
            var conversation = audience.C;
            var demands = conversation.F;
            var treaties = demands.F;
            var theft = audience.T.T;
            var production = new CityProductionChoice(World, Data.Process.Rules, new(),
                new(World, Data.Process.Rules, Data.Stacks), Data.Stacks, new(World, Data.Rules, ClassicShip, ClassicCalendar),
                new(World, Data.Rules, Data.Stacks, Events.Life.Deletion, theft.Random,
                    _ => throw new InvalidOperationException("No airlift in the diplomacy cases")));
            var orders = new ProductionOrders(World, Data.Rules, city => production.Choose(city, "diplomacy-production"), _ => { });
            Services = new(World, Data.Process.Rules, theft.Random, Data.Stacks, new(World, Data.Rules, Data.Stacks),
                Grant.Services, orders, Events.Permissions, new(
                    War: treaties.WarEffects, AllyHelp: Events.Contact, Technology: audience.T, OpeningTreaty: audience,
                    TreatyNegotiation: treaties, Proposal: demands, Favor: conversation, Gift: conversation,
                    Demand: demands, Map: conversation, Audience: audience, ResearchChoice: theft,
                    Attack: _ => throw new InvalidOperationException("No attack notice in the diplomacy cases"),
                    Treaty: treaties.Agreements.Add, Conversation: conversation.Notices.Add,
                    Contact: Events.Contact.Notices.Add, TakenTechnology: theft.Taken.Add));
            // Close the actual S02 -> U01 -> W03 -> V04 -> S02 cycle using this graph.
            Events.RebindDispatcher(Services.Contact, Grant.Services.Grants, Services.War);
        }
        public DiplomaticConversation.Operation BeginConversation()
        {
            World.Civilizations[1].Government = World.Civilizations[2].Government = Government.Despotism;
            World.Diplomacy.MeetingActive = World.Diplomacy.AiMeetingActive = World.Diplomacy.AskedEmissary = true;
            World.Diplomacy.InitialEvaluationDone = true; World.Diplomacy.EmissaryResponse = 1;
            return Services.Conversation.Begin(1, 2, true, new(10, 8), QuietAudience, "conversation");
        }
        public void Finish(DiplomaticConversation.Operation operation)
        {
            Equal(null, operation.Resume(new(0))); Equal(true, operation.Completed);
            Equal(false, World.Diplomacy.MeetingActive || World.Diplomacy.AiMeetingActive || World.Diplomacy.AskedEmissary);
            Equal(1, Grant.Closed); Events.Consumed();
        }
    }

    private static void SinglePlayerDiplomacyConversation()
    {
        var f = new SinglePlayerDiplomacyFixture(); f.World.Civilizations[1].Gold = 150;
        f.Conversation.FailRefresh = true;
        using (var gifts = f.BeginConversation())
        {
            var menu = gifts.Resume(); Equal(true, menu is DiplomaticConversationDecision.Menu);
            Equal(menu, gifts.Resume()); Equal(true, gifts.Resume(new(8)) is DiplomaticConversationDecision.Gift);
            Equal(true, gifts.Resume(new(2)) is DiplomaticConversationDecision.Gift { Decision: DiplomaticGiftDecision.Gold });
            Equal(true, gifts.Resume(new(150)) is DiplomaticConversationDecision.Menu);
            Equal(0, f.World.Civilizations[1].Gold); Equal(927, f.World.Civilizations[2].Gold);
            Equal(1, f.World.Diplomacy.GiftsThisMeeting); f.Finish(gifts);
            Equal(1, gifts.PresentationFailures.Count);
        }

        f = new(); f.Events.Contact.F.T.Know(1, 94);
        using (var technology = f.BeginConversation())
        {
            technology.Resume(); technology.Resume(new(8));
            var offer = technology.Resume(new(1));
            Equal(true, offer is DiplomaticConversationDecision.Gift { Decision: DiplomaticGiftDecision.Technology });
            Equal(offer, technology.Resume()); Equal(false, f.World.Civilizations[2].Advances.Contains(94));
            Equal(true, technology.Resume(new(1)) is DiplomaticConversationDecision.Menu);
            Equal(true, f.World.Civilizations[2].Advances.Contains(94));
            Equal((byte)1, f.World.Civilizations[2].AdvanceSource[94]); Equal((byte)1, f.World.Civilizations[2].NumAdvances);
            Equal(1, f.World.Diplomacy.GiftsThisMeeting); f.Finish(technology);
        }

        f = new(); f.Events.Contact.F.T.Know(1, AdvanceIds.MapMaking); f.Events.Contact.F.T.Know(2, AdvanceIds.MapMaking);
        f.World.Diplomacy.Attitude = 0;
        f.World.Map.TileAt(new(2, 2)).RevealedTo = CivilizationSet.Only(2);
        using (var maps = f.BeginConversation())
        {
            maps.Resume(); Equal(true, maps.Resume(new(7)) is DiplomaticConversationDecision.Favor);
            Equal(true, maps.Resume(new(3)) is DiplomaticConversationDecision.Menu);
            Equal(true, f.World.Map.TileAt(new(2, 2)).RevealedTo.Contains(1));
            Equal(1, f.Conversation.MapAccepted); Equal(1, f.Conversation.Published); f.Finish(maps);
        }

        f = new();
        using var treaty = f.BeginConversation(); treaty.Resume();
        Equal(true, treaty.Resume(new(1)) is DiplomaticConversationDecision.Menu);
        Equal(true, f.World.Civilizations[1].Treaties[2].Alliance && f.World.Civilizations[2].Treaties[1].Alliance);
        Equal(1, f.Conversation.F.F.Agreements.Count); f.Finish(treaty);
    }

    private static void SinglePlayerDiplomacyScenarioCreation()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = new SinglePlayerDiplomacyFixture(); var entry = ScenarioNestedCreation(f.Events);
            int gold = f.World.Civilizations[1].Gold;
            using var operation = f.Events.Dispatcher.Begin(entry, "scenario");
            var pending = operation.Resume();
            Equal(true, pending is LifecycleContact { Decision: DiplomaticContactDecision.Scenario { Value: ResearchGrantDecision.Government } });
            int unit = f.Data.Stacks.FirstAt(new(20, 12)); var token = f.Data.Unit(unit).Token;
            Equal(gold, f.World.Civilizations[1].Gold); Equal(false, entry.Values.Actions.Consumed);
            Equal(pending, operation.Resume()); Equal(token, f.Data.Unit(unit).Token);
            Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
            if (dispose)
            {
                operation.Dispose(); Throws<ObjectDisposedException>(() => operation.Resume(new(0)));
                Equal(gold, f.World.Civilizations[1].Gold); Equal(false, entry.Values.Actions.Consumed);
            }
            else
            {
                Equal(true, operation.Resume(new(0)) is ScenarioText);
                Equal(token, f.Data.Unit(unit).Token); Equal(gold + 7, f.World.Civilizations[1].Gold);
                Equal(false, f.World.Civilizations[1].Treaties[2].Contact);
                Equal(null, operation.Resume(new(0))); Equal(1, operation.Result); Equal(true, entry.Values.Actions.Consumed);
            }
            f.Events.Consumed();
        }
    }

    private static void SinglePlayerDiplomacyAudiencePermission()
    {
        foreach (bool fail in new[] { false, true })
        {
            var f = new SinglePlayerDiplomacyFixture(); var entry = f.Events.Library();
            entry.Values.Trigger = EventTrigger.Negotiation; entry.Values.WhoCiv = 1; entry.Values.WhomCiv = 2;
            entry.Values.TalkerType = entry.Values.ListenerType = 4;
            entry.Values.DenyNegotiation = true; entry.Values.Actions.JustOnce = true; f.World.Events.Loaded = true;
            f.World.Diplomacy.SelectedPair = (6, 7); f.World.Diplomacy.GiftsThisMeeting = 9;
            using var operation = f.Services.Audience.Begin(1, 2, true, new(10, 8), "audience");
            var pending = operation.Resume(); Equal(true, pending is DiplomaticAudienceDecision.Scenario { Value: ResearchGrantDecision.Government });
            Equal(pending, operation.Resume()); Equal((6, 7), f.World.Diplomacy.SelectedPair); Equal(9, f.World.Diplomacy.GiftsThisMeeting);
            Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
            if (fail)
            {
                f.Grant.FailRevolution = true;
                Throws<InvalidOperationException>(() => operation.Resume(new(1)));
                Throws<InvalidOperationException>(() => operation.Resume(new(0)));
                Equal(false, entry.Values.Actions.Consumed); Equal(Government.Despotism, f.World.Civilizations[1].Government);
            }
            else
            {
                Equal(null, operation.Resume(new(0))); Equal(true, operation.Completed && operation.ScenarioDenied);
                Equal(false, f.World.Diplomacy.AiMeetingActive); Equal(true, entry.Values.Actions.Consumed);
            }
            Equal(0, f.Grant.Closed); f.Events.Consumed();
        }
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var f = new SinglePlayerDiplomacyFixture(); var entry = f.Events.Library();
            entry.Values.Trigger = EventTrigger.Negotiation; entry.Values.WhoCiv = 1; entry.Values.WhomCiv = 2;
            entry.Values.TalkerType = entry.Values.ListenerType = 4;
            entry.Values.DenyNegotiation = true; entry.Values.Actions.JustOnce = true; f.World.Events.Loaded = true;
            using var operation = new DiplomaticAudienceOperation(f.Services.Audience.Begin(1, 2, true, new(10, 8), "audience"),
                c => new((ulong)c), _ => throw new InvalidOperationException("No intelligence in a scenario grant"));
            var pending = operation.Resume(f.Events.Context(), new());
            Equal(CommandStatus.AwaitingInteraction, pending.Status); Equal(new ParticipantId(1), pending.Interaction!.Recipient);
            var result = operation.Resume(f.Events.Context(), new(input));
            Equal(CommandStatus.Completed, result.Status); Equal(new CommandValue.Boolean(true), result.Result!.Single().Value);
            Equal(true, entry.Values.Actions.Consumed); Equal(false, f.World.Diplomacy.AiMeetingActive);
            Equal(Government.Despotism, f.World.Civilizations[1].Government);
            Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy)); f.Events.Consumed();
        }
    }

    private static void SinglePlayerDiplomacyPrivateExchange()
    {
        using var runtime = new SessionRuntime(new(), new(258));
        var f = new SinglePlayerDiplomacyFixture(
            [ExchangeDraw(AdvanceIds.Monarchy, call: "contact/exchange"), ExchangeDraw(94, true, call: "contact/exchange")],
            CreationFixture(new(runtime.Process, runtime.World)));
        f.Events.Contact.F.T.Know(3, AdvanceIds.Monarchy); f.Events.Contact.F.T.Know(2, 94);
        f.Data.AddCity(0, 1, new(10, 8)); f.World.Civilizations[1].CityCount = 1;
        f.World.Globals.WonderCity[WonderIds.GreatLibrary] = 0;
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.composed-contact", "Authored composed contact", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new DiplomaticContactOperation(f.Services.Contact.Begin(2, 3, new(10, 8), "contact"),
                _ => recipient, _ => throw new InvalidOperationException("No intelligence in this exchange"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var player = SessionClient(runtime, 1);
        recipient = player.Identity.Participant;
        var command = actor.Submit(new("test.composed-contact", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(player, command); Equal(false, actor.Reply(prompt, 0).Accepted);
        Equal(null, RequiredResult(actor, command).Interaction);
        Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
        Equal(true, f.World.Civilizations[2].Advances.Contains(AdvanceIds.Monarchy));
        Equal(false, f.World.Civilizations[3].Advances.Contains(94)); Equal(false, f.World.Civilizations[2].Treaties[3].Peace);
        Equal(true, player.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(null, player.Result(command));
        Equal(false, player.Reply(prompt, 0).Accepted);
        Equal(true, f.World.Civilizations[3].Advances.Contains(94)); Equal(true, f.World.Civilizations[2].Treaties[3].Peace);
        f.Events.Consumed();
    }
}
