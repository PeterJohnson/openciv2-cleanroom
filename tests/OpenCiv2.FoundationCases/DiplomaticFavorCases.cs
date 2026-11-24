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
    private sealed class FavorFixture : IDiplomaticFavorEffects, INegotiatedMapEffects
    {
        public TechnologyFixture T { get; }
        public GameState World => T.World;
        public RevelationFixture Data => T.T.Data;
        public DiplomaticFavors Favors { get; }
        public NegotiatedMaps Maps { get; }
        public WarNotices WarEffects { get; } = new();
        public List<DiplomaticFavorNotice> Notices { get; } = [];
        public List<string> Events { get; } = [];
        public int MapAccepted, Published, Refreshed;
        public bool FailMap, FailRedraw, FailRefresh;
        public int FailPublish;
        public DiplomaticFavorNoticeKind? FailNotice;
        public FavorFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            T = new(choices, data);
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3).With(5);
            World.Globals.PowerRank[3] = 3;
            World.Civilizations[2].Treaties[3] = new() { Contact = true, Peace = true };
            World.Civilizations[3].Treaties[2] = new() { Contact = true, Peace = true };
            World.Civilizations[1].Treaties[3].War = true;
            Maps = new(World, this);
            var government = new GovernmentChanges(World, Data.Process.Rules, Data.Stacks,
                _ => throw new InvalidOperationException("Unexpected government choice"), _ => throw new InvalidOperationException("Unexpected rates"), _ => { });
            var stance = new DiplomaticStance(World, Data.Process.Rules, new(World, Data.Process.Rules, government, T.T.Random));
            Favors = new(World, Data.Process.Rules, T.T.Random, stance, T.Service, T.T.Research, Maps, War(Data, WarEffects, T.T.Random), this);
        }
        public DiplomaticFavors.Operation Begin(int choice)
        {
            var op = Favors.Begin(1, 2, new(10, 8), "favor"); Equal(true, op.Resume() is DiplomaticFavorDecision.Menu);
            op.Resume(new(choice)); return op;
        }
        public MercenaryOffer Offer(DiplomaticFavors.Operation op) => ((DiplomaticFavorDecision.War)op.Resume(new(3))!).Offer;
        public void Notice(DiplomaticFavorNotice notice)
        { Notices.Add(notice); Events.Add("notice:" + notice.Kind); if (FailNotice == notice.Kind) { throw new InvalidOperationException("Missing favor notice"); } }
        public void RefreshStatus() { ++Refreshed; Events.Add("status"); if (FailRefresh) { throw new InvalidOperationException("Missing status"); } }
        public void Publish() { ++Published; Events.Add("publish"); if (FailPublish == Published) { throw new InvalidOperationException("Publication failed"); } }
        public void Accepted(int requester, DiplomaticParty grantor)
        { ++MapAccepted; Events.Add("maps"); if (FailMap) { throw new InvalidOperationException("Missing map notice"); } }
        public void RedrawMaps(int localHuman) { Events.Add("redraw:" + localHuman); if (FailRedraw) { throw new InvalidOperationException("Missing map view"); } }
    }
    private static Choice FavorBetray(string kind, int roll) => Choice.Create(new("V05.mercenary-betray-" + kind, "favor/round-0"),
        new SamplingLaw.PrimaryModulo(2), ChoiceCode.Primary(roll));

    private static void NegotiatedMapsDirectionalKnowledge()
    {
        var f = new FavorFixture(); var w = f.World;
        for (int known = 0; known < 4; ++known)
        {
            var p = new MapPoint(known * 2, 0);
            w.Map.TileAt(p).Features = MapFeatures.Road | MapFeatures.Irrigation;
            w.Map.TileAt(p).RevealedTo = (known & 1) != 0 ? CivilizationSet.Only(2) : default;
            if ((known & 2) != 0) { w.Map.TileAt(p).RevealedTo = w.Map.TileAt(p).RevealedTo.With(1); }
            w.Map.Remembered(2, p) = MapFeatures.Road | MapFeatures.Railroad;
            w.Map.Remembered(1, p) = MapFeatures.Mine | MapFeatures.Irrigation;
        }
        w.Civilizations[2].DiploRequestCount = 127;
        Equal(null, f.Maps.Exchange(1, 2)); Equal((sbyte)-127, w.Civilizations[2].DiploRequestCount);
        for (int known = 0; known < 4; ++known)
        {
            var p = new MapPoint(known * 2, 0);
            Equal(known != 0, w.Map.TileAt(p).RevealedTo.Contains(1)); Equal(known != 0, w.Map.TileAt(p).RevealedTo.Contains(2));
            Equal(MapFeatures.Mine | MapFeatures.Irrigation | ((known & 1) != 0 ? MapFeatures.Road : MapFeatures.None), w.Map.Remembered(1, p));
            Equal(MapFeatures.Road | MapFeatures.Railroad | (known == 2 ? MapFeatures.Irrigation : MapFeatures.None), w.Map.Remembered(2, p));
        }
        Sequence(new[] { "maps", "publish", "redraw:1" }, f.Events); Equal(0, f.Data.Meetings.Count);
        var barbarian = new FavorFixture(); barbarian.World.Map.TileAt(new(0, 0)).RevealedTo = CivilizationSet.Only(1);
        barbarian.World.Map.Remembered(1, new(0, 0)) = MapFeatures.Mine;
        Equal(null, barbarian.Maps.Exchange(1, 0)); Equal(true, barbarian.World.Map.TileAt(new(0, 0)).RevealedTo.Contains(0));
        Equal(MapFeatures.Mine, barbarian.World.Map.Remembered(1, new(0, 0)));
    }

    private static void NegotiatedMapsUnitsCitiesFailures()
    {
        var f = new FavorFixture(); var w = f.World;
        f.Data.AddUnit(0, 2, 0, new(2, 2)); f.Data.AddUnit(1, 1, 0, new(4, 2)); f.Data.AddUnit(2, 3, 0, new(6, 2));
        f.Data.AddUnit(3, 3, 0, new(8, 2)); f.Data.Unit(3).Visibility = CivilizationSet.Only(2);
        f.Data.AddCity(0, 1, new(10, 4), 5); f.Data.AddCity(1, 2, new(14, 4), 7); f.Data.AddCity(2, 3, new(18, 4), 9);
        w.Entities.Cities.Storage[0].RememberedSize[2] = 1; w.Entities.Cities.Storage[1].RememberedSize[1] = 2;
        w.Entities.Cities.Storage[2].KnownTo = CivilizationSet.Only(2); w.Entities.Cities.Storage[2].RememberedSize[2] = 3;
        f.FailRedraw = true; Equal(true, f.Maps.Exchange(1, 2) is InvalidOperationException);
        Equal(true, f.Data.Unit(0).Visibility.Contains(1)); Equal(true, f.Data.Unit(1).Visibility.Contains(2));
        Equal(default(CivilizationSet), f.Data.Unit(2).Visibility); Equal(true, f.Data.Unit(3).Visibility.Contains(1));
        Equal(true, w.Entities.Cities.Storage[0].KnownTo.Contains(2)); Equal((byte)1, w.Entities.Cities.Storage[0].RememberedSize[2]);
        Equal((byte)7, w.Entities.Cities.Storage[1].RememberedSize[1]); Equal((byte)9, w.Entities.Cities.Storage[2].RememberedSize[1]);
        Equal(false, w.Map.TileAt(new(2, 2)).RevealedTo.Contains(1)); Equal(1, f.Published);
        f = new(); f.FailMap = true; Throws<InvalidOperationException>(() => f.Maps.Exchange(1, 2)); Equal((sbyte)0, f.World.Civilizations[2].DiploRequestCount);
        f = new(); f.FailPublish = 1; Throws<InvalidOperationException>(() => f.Maps.Exchange(1, 2)); Equal((sbyte)2, f.World.Civilizations[2].DiploRequestCount);
        Equal(false, f.Events.Any(e => e.StartsWith("redraw:")));
    }

    private static void DiplomaticFavorPricesAndDistrust()
    {
        (Action<FavorFixture> Change, int Price)[] cases = [
            (_ => { }, 400),
            (f => f.World.Civilizations[2].Treaties[3].Peace = false, 250),
            (f => f.World.Civilizations[1].Gold = 1200, 550),
            (f => f.World.Civilizations[2].Treaties[3].Alliance = true, 1250),
            (f => { f.World.Globals.PowerRank[3] = 7; f.World.Civilizations[3].CityCount = 5; }, 200),
            (f => f.World.Civilizations[1].Treaties[3].War = false, 550),
            (f => f.World.Civilizations[2].Treaties[1].Alliance = true, 300),
            (f => { f.World.Civilizations[2].Treaties[1].Alliance = true; f.World.Civilizations[2].Treaties[7].War = true; }, 400),
            (f => f.World.Civilizations[3].Treaties[1].SneakAttack = true, 300),
            (f => f.World.Globals.PowerRank[1] = 2, 250),
            (f => { f.World.Civilizations[3].Treaties[1].Nuked = true; f.World.Globals.PowerRank[3] = 4; }, 100),
            (f => { f.World.Civilizations[3].Treaties[1].Nuked = f.World.Civilizations[1].Treaties[3].Nuked = true; f.World.Globals.PowerRank[3] = 4; }, 200)
        ];
        foreach (var (change, price) in cases)
        {
            var f = new FavorFixture(); var w = f.World;
            w.Civilizations[1].Gold = w.Civilizations[2].Gold = 1000;
            w.Civilizations[1].Reputation = 10; w.Civilizations[3].Reputation = 2; w.Civilizations[2].ReputationCredit[1] = 1;
            w.Civilizations[3].MilitaryStrength = 100;
            change(f); Equal(price, f.Favors.WarPrice(1, 2, 3)); f.T.T.Random.RequireChoicesConsumed();
        }
        var distrust = new FavorFixture(); var d = distrust.World;
        d.Civilizations[1].Reputation = 10; d.Civilizations[2].ReputationCredit[1] = 1;
        distrust.Data.Process.Rules.Leaders[2].Attack = 2;
        Equal(7, distrust.Favors.WarDistrust(1, 2, 3));
        d.Civilizations[2].Treaties[1].Vendetta = true; d.Civilizations[1].Treaties[3].War = false;
        d.Globals.PowerRank[1] = 7; Equal(12, distrust.Favors.WarDistrust(1, 2, 3));
        d.Civilizations[2].Treaties[1].Alliance = true; Equal(6, distrust.Favors.WarDistrust(1, 2, 3));
        d.Civilizations[2].ReputationCredit[1] = 127; Equal(-57, distrust.Favors.WarDistrust(1, 2, 3));
        Equal(50, new FavorFixture().Favors.WarPrice(1, 2, 3));
    }

    private static void DiplomaticFavorMapAdmission()
    {
        for (int gate = 0; gate < 7; ++gate)
        {
            var f = new FavorFixture(); f.World.Diplomacy.Attitude = 0;
            if (gate != 0) { f.T.Know(1, AdvanceIds.MapMaking); }
            if (gate != 1) { f.T.Know(2, AdvanceIds.MapMaking); }
            if (gate >= 2) { f.World.Diplomacy.Attitude = gate == 3 ? 26 : 25; }
            if (gate == 3 || gate == 4) { f.World.Civilizations[1].Treaties[2].Alliance = true; }
            if (gate == 5 || gate == 6)
            {
                f.Data.AddCity(1, 1, new(20, 8));
                f.World.Globals.WonderCity[gate == 5 ? WonderIds.MarcoPolo : WonderIds.Magellan] = 1;
            }
            using var op = f.Begin(3); Equal(true, op.Completed);
            Equal(gate >= 4 ? 1 : 0, f.MapAccepted); Equal(gate >= 4 ? 0 : 1, f.Notices.Count);
            Equal((sbyte)(gate >= 4 ? 2 : 0), f.World.Civilizations[2].DiploRequestCount); f.T.T.Random.RequireChoicesConsumed();
        }
    }

    private static void DiplomaticFavorWarAdmissionAndPayment()
    {
        for (int gate = 0; gate < 4; ++gate)
        {
            var f = new FavorFixture();
            if (gate == 0) { f.World.Civilizations[2].Treaties[3].Contact = false; }
            if (gate == 1) { f.World.Civilizations[2].Treaties[3].War = true; }
            if (gate == 2) { f.World.Civilizations[1].Reputation = 10; f.World.Civilizations[2].Treaties[1].Vendetta = true; }
            if (gate == 3) { f.World.Civilizations[2].Treaties[3].Alliance = true; }
            using var op = f.Begin(2); Sequence(new[] { 3, 5 }, ((DiplomaticFavorDecision.Target)op.Pending!).Options.Select(p => p.Civilization));
            op.Resume(new(3)); Equal(true, op.Completed);
            Equal(new[] { DiplomaticFavorNoticeKind.NoContact, DiplomaticFavorNoticeKind.AtWar, DiplomaticFavorNoticeKind.RefusedWar, DiplomaticFavorNoticeKind.EternalAllies }[gate], f.Notices.Single().Kind);
        }
        foreach (int response in new[] { 0, 1, -1 })
        {
            var f = new FavorFixture(); f.World.Civilizations[1].Reputation = 10; // Distrust override admits an existing enemy of equal rank.
            using var op = f.Begin(2); var offer = f.Offer(op); Equal(50, offer.Gold); Equal(null, offer.First);
            op.Resume(new(response)); Equal(true, op.Completed);
            Equal(response == 0 ? 5000 : 4950, f.World.Civilizations[1].Gold); Equal(response == 0 ? 777 : 827, f.World.Civilizations[2].Gold);
            Equal(response != 0, f.World.Civilizations[2].Treaties[3].War); Equal(response != 0, f.World.Civilizations[3].Treaties[2].RecentContact);
            Equal(response != 0, f.World.Civilizations[2].Treaties[3].ExtendedAggressor); Equal(false, f.World.Civilizations[3].Treaties[2].Aggressor);
            Equal(response != 0, f.World.Civilizations[2].Treaties[1].Mercenary); Equal((byte)0, f.World.Civilizations[2].Reputation);
            Equal(response == 0 ? 0 : 2, f.Published); f.T.T.Random.RequireChoicesConsumed();
        }
        var poor = new FavorFixture(); using var unaffordable = poor.Begin(2); poor.Offer(unaffordable); poor.World.Civilizations[1].Gold = 49;
        unaffordable.Resume(new(1)); Equal(DiplomaticFavorNoticeKind.Unaffordable, poor.Notices.Single().Kind); Equal(49, poor.World.Civilizations[1].Gold);
        var back = new FavorFixture(); using var repeat = back.Begin(2); Equal(true, repeat.Resume(new(0)) is DiplomaticFavorDecision.Menu);
        repeat.Resume(new(0)); Equal((sbyte)-1, back.World.Civilizations[2].DiploRequestCount);
        using var invalid = new FavorFixture().Begin(2); Throws<ArgumentOutOfRangeException>(() => invalid.Resume(new(-1)));
        var cached = new FavorFixture(); cached.World.Civilizations[1].Gold = cached.World.Civilizations[2].Gold = 1000;
        cached.World.Civilizations[1].Reputation = 5; cached.World.Civilizations[3].MilitaryStrength = 100;
        using var priceHeld = cached.Begin(2); Equal(350, cached.Offer(priceHeld).Gold);
        cached.World.Civilizations[2].Gold = 6000; priceHeld.Resume(new(1));
        Equal(650, cached.World.Civilizations[1].Gold); Equal(6350, cached.World.Civilizations[2].Gold);
    }

    private static void DiplomaticFavorBetrayalAndFailures()
    {
        foreach (bool alliance in new[] { false, true })
            foreach (int roll in new[] { 0, 1 })
            {
                var f = new FavorFixture([FavorBetray(alliance ? "alliance" : "peace", roll)]); var w = f.World;
                w.Civilizations[1].Treaties[3] = w.Civilizations[3].Treaties[1] = new() { Peace = true, Alliance = alliance };
                using var op = f.Begin(2); f.Offer(op); op.Resume(new(1)); Equal(true, op.Completed);
                Equal((byte)(roll == 0 ? 25 : 0), w.Civilizations[3].Attitude[1]);
                Equal(alliance && roll == 1, w.Civilizations[1].Treaties[3].Alliance);
                Equal(!alliance && roll == 0, w.Civilizations[1].Treaties[3].War);
                Equal(!alliance && roll == 0, w.Civilizations[3].Treaties[1].Vendetta);
                Equal(2, f.Published); Equal(alliance && roll == 0 ? 1 : 0, f.WarEffects.Notices.Count); f.T.T.Random.RequireChoicesConsumed();
            }
        for (int failure = 0; failure < 3; ++failure)
        {
            var f = new FavorFixture(); f.FailRefresh = failure == 0; f.FailPublish = failure == 1 ? 1 : 0;
            f.FailNotice = failure == 2 ? DiplomaticFavorNoticeKind.Declare : null;
            using var op = f.Begin(2); f.Offer(op);
            if (failure == 0) { op.Resume(new(1)); Equal(true, op.Completed); Equal(1, op.PresentationFailures.Count); }
            else { Throws<InvalidOperationException>(() => op.Resume(new(1))); Throws<InvalidOperationException>(() => op.Resume()); }
            Equal(4950, f.World.Civilizations[1].Gold); Equal(true, f.World.Civilizations[2].Treaties[3].War);
            Equal(failure == 0 ? 2 : 1, f.Published);
        }
        var betrayal = new FavorFixture([FavorBetray("alliance", 0)]); betrayal.FailNotice = DiplomaticFavorNoticeKind.BetrayAlly;
        betrayal.World.Civilizations[1].Treaties[3] = betrayal.World.Civilizations[3].Treaties[1] = new() { Alliance = true, Peace = true };
        using var broken = betrayal.Begin(2); betrayal.Offer(broken); Throws<InvalidOperationException>(() => broken.Resume(new(1)));
        Equal((byte)25, betrayal.World.Civilizations[3].Attitude[1]); Equal(true, betrayal.World.Civilizations[1].Treaties[3].Alliance);
        Equal(true, betrayal.World.Civilizations[2].Treaties[3].War); Equal(1, betrayal.Published); betrayal.T.T.Random.RequireChoicesConsumed();
    }

    private static void DiplomaticFavorTechnologyPayments()
    {
        var f = new FavorFixture(); f.T.Know(1, AdvanceIds.Monarchy); f.T.Know(1, 94); f.T.Know(1, 95);
        f.World.Diplomacy.BestTechnology = AdvanceIds.Monarchy; f.World.Diplomacy.SecondTechnology = 94;
        f.World.Civilizations[2].Gold = 6000; f.World.Multiplayer.LocalHuman = 2; f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
        f.World.Civilizations[2].Government = Government.Despotism;
        using var op = f.Begin(2); var offer = f.Offer(op); Equal(null, offer.Gold); Equal(AdvanceIds.Monarchy, offer.First!.Identity);
        Equal(true, op.Resume(new(1)) is DiplomaticFavorDecision.Grant { Decision: ResearchGrantDecision.Government });
        Equal(true, f.World.Civilizations[2].Advances.Contains(AdvanceIds.Monarchy)); Equal(false, f.World.Civilizations[2].Advances.Contains(94));
        Equal(false, f.World.Civilizations[2].Treaties[3].War); Equal(AdvanceIds.Monarchy, f.World.Diplomacy.BestTechnology);
        f.World.Diplomacy.SecondTechnology = 95; // A real grant decision can change the later live choice.
        op.Resume(new(0)); Equal(true, op.Completed); Equal(true, f.World.Civilizations[2].Advances.Contains(95));
        Equal(false, f.World.Civilizations[2].Advances.Contains(94));
        Equal((int?)null, f.World.Diplomacy.BestTechnology); Equal((int?)null, f.World.Diplomacy.SecondTechnology);
        Equal(5000, f.World.Civilizations[1].Gold); Equal(6000, f.World.Civilizations[2].Gold); Equal(true, f.World.Civilizations[2].Treaties[3].War);
        f.T.T.Random.RequireChoicesConsumed();
        var failed = new FavorFixture(); failed.T.Know(1, AdvanceIds.Monarchy); failed.T.Know(1, 94);
        failed.World.Diplomacy.BestTechnology = AdvanceIds.Monarchy; failed.World.Diplomacy.SecondTechnology = 94;
        failed.World.Civilizations[2].Gold = 6000; failed.World.Multiplayer.LocalHuman = 2;
        failed.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); failed.World.Civilizations[2].Government = Government.Despotism;
        failed.T.T.Retained!.FailRevolution = true;
        using var partial = failed.Begin(2); failed.Offer(partial); partial.Resume(new(1));
        Throws<InvalidOperationException>(() => partial.Resume(new(1)));
        Equal(true, failed.World.Civilizations[2].Advances.Contains(AdvanceIds.Monarchy)); Equal(false, failed.World.Civilizations[2].Advances.Contains(94));
        Equal(AdvanceIds.Monarchy, failed.World.Diplomacy.BestTechnology); Equal(false, failed.World.Civilizations[2].Treaties[3].War);
    }

    private static void DiplomaticFavorExchangeRepetition()
    {
        foreach (sbyte count in new sbyte[] { 0, 3 })
        {
            var f = new FavorFixture([GreetingChoice("favor/round-0/exchange/round-0/emissary/greet")]); f.T.Pair();
            f.World.Civilizations[2].DiploRequestCount = count;
            using var op = f.Begin(1); Equal(true, op.Pending is DiplomaticFavorDecision.Technology { Decision: TechnologyNegotiationDecision.Emissary });
            op.Resume(new(0)); op.Resume(new(1)); op.Resume(new(90));
            if (count == 0)
            {
                Equal(true, op.Pending is DiplomaticFavorDecision.Menu); Equal((sbyte)1, f.World.Civilizations[2].DiploRequestCount);
                op.Resume(new(0)); Equal((sbyte)0, f.World.Civilizations[2].DiploRequestCount); Equal(0, f.Published);
            }
            else { Equal(true, op.Completed); Equal(1, f.Published); Equal(count, f.World.Civilizations[2].DiploRequestCount); }
            Equal(true, f.World.Civilizations[1].Advances.Contains(90)); f.T.T.Random.RequireChoicesConsumed();
        }
        // Use mode0's actual conditional refusal draw.
        var refused = new FavorFixture([Choice.Create(new("V05.exchange-refusal", "favor/round-0/exchange/round-0"), new SamplingLaw.SecondaryRange(0, 2), ChoiceCode.Secondary(0))]);
        refused.World.Diplomacy.Attitude = 76; using var stop = refused.Begin(1); Equal(true, stop.Completed); Equal(1, refused.Published);
        refused.T.T.Random.RequireChoicesConsumed();
    }

    private static void DiplomaticFavorPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(253));
        var f = new FavorFixture([GreetingChoice("favor/round-0/exchange/round-0/emissary/greet")], CreationFixture(new(runtime.Process, runtime.World))); f.T.Pair();
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.favor", "Authored diplomatic favor", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new DiplomaticFavorOperation(f.Favors.Begin(1, 2, new(10, 8), "favor"), _ => recipient,
                _ => throw new InvalidOperationException("Unexpected intelligence"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var player = SessionClient(runtime, 1); recipient = player.Identity.Participant;
        var command = actor.Submit(new("test.favor", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(player, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(false, actor.Reply(prompt, 1).Accepted); Equal(true, player.Reply(prompt, 1).Accepted); runtime.AdvanceOne();
        prompt = RequiredInteraction(player, command); Equal(true, player.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
        prompt = RequiredInteraction(player, command); Equal(true, player.Reply(prompt, 1).Accepted); runtime.AdvanceOne();
        prompt = RequiredInteraction(player, command); Equal(false, player.Cancel(command).Accepted);
        Equal(true, player.Reply(prompt, 90).Accepted); runtime.AdvanceOne();
        Equal((sbyte)1, f.World.Civilizations[2].DiploRequestCount);
        Equal(true, player.Cancel(command).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal((sbyte)0, f.World.Civilizations[2].DiploRequestCount);
        Equal(true, f.World.Civilizations[1].Advances.Contains(90)); f.T.T.Random.RequireChoicesConsumed();
    }

    private static void DiplomaticFavorSessionCancellation()
    {
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
            for (int stage = 0; stage < 3; ++stage)
            {
                var f = new FavorFixture();
                var context = new OperationContext(f.World, f.Data.Process, f.T.T.Random, new PresentationRandom(1, "favor"),
                    new(default, new(1)), default, (_, _) => { }, () => { });
                using var op = new DiplomaticFavorOperation(f.Favors.Begin(1, 2, new(10, 8), "favor"), c => new((ulong)c),
                    _ => throw new InvalidOperationException("Unexpected intelligence"));
                Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
                if (stage > 0) { Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new(OperationInputKind.Choice, 2)).Status); }
                if (stage == 2) { Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new(OperationInputKind.Choice, 3)).Status); }
                var result = op.Resume(context, new(input));
                if (stage == 1)
                { Equal(CommandStatus.AwaitingInteraction, result.Status); result = op.Resume(context, new(input)); }
                Equal(CommandStatus.Completed, result.Status); Equal(5000, f.World.Civilizations[1].Gold);
                Equal(false, f.World.Civilizations[2].Treaties[3].War); Equal((sbyte)(stage == 2 ? 0 : -1), f.World.Civilizations[2].DiploRequestCount);
                f.T.T.Random.RequireChoicesConsumed();
            }
    }
}
