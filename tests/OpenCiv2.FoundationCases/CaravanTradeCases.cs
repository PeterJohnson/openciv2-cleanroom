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
    private static Choice DeliveryCap(string call = "deliver", int code = 0) => Choice.Create(new("C07.delivery-cap", call), new SamplingLaw.PrimaryModulo(10), ChoiceCode.Primary(code));
    private static List<Choice> DeliveryChoices(string call = "deliver", int cap = 0, int a = 0, int b = 0, int c = 0) =>
        [DeliveryCap(call, cap), .. new[] { a, b, c }.Select((code, i) => Choice.Create(new("C07.delivery-return", call, i), new SamplingLaw.PrimaryModulo(8), ChoiceCode.Primary(code)))];
    private sealed class CaravanFixture : ICaravanTradeEffects
    {
        public RevelationFixture Data { get; }
        public GameState World => Data.World;
        public EliminationFixture Life { get; }
        public CaravanTrade Trade { get; }
        public List<CaravanSound> Sounds { get; } = [];
        public List<CaravanDeliveryNotice> Deliveries { get; } = [];
        public List<CaravanReciprocalNotice> Reciprocals { get; } = [];
        public List<CaravanDonationNotice> Donations { get; } = [];
        public int Status;
        public bool FailSound, FailStatus, FailDelivery;
        public CaravanFixture(List<Choice>? choices = null, RevelationFixture? data = null, IReadOnlyDictionary<int, string>? extra = null)
        {
            Data = data ?? CommodityFixture();
            Data.AddCity(0, 1, new(8, 6), 3); Data.AddCity(1, 2, new(12, 6), 3);
            Life = new(choices ?? DeliveryChoices(), Data);
            World.Globals.Turn = 200; World.Globals.Difficulty = Difficulty.Prince; World.Globals.Rules.NoRestart = true;
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3); World.Globals.HumanPlayers = CivilizationSet.Only(1);
            Data.Rules.Cosmic = Data.Rules.Cosmic with { TechnologyCostParadigm = 10, ShieldBoxRows = 10, FoodBoxRows = 99 };
            Data.Rules.Units[2] = Data.Rules.Units[0] with { Domain = RuleDomain.Land };
            Data.Rules.Units[48] = Data.Rules.Units[0] with { Role = RuleRole.Trade, CostRows = 5, Identity = new("trade", "Caravan") };
            Data.Rules.Units[49] = Data.Rules.Units[48];
            for (int i = 0; i < 16; ++i) Data.Rules.Commodities[i] = new("cargo" + i, "Cargo " + i);
            Data.AddUnit(0, 1, 48, new(8, 6)); Data.Unit(0).Home = new(0); World.Civilizations[1].UnitsActive[48] = 1;
            World.Civilizations[1].Gold = 10; World.Civilizations[1].ResearchProgress = 0;
            World.Civilizations[1].Attitude[2] = 50; World.Civilizations[2].Attitude[1] = 60;
            for (int city = 0; city < 2; ++city)
            {
                ref var record = ref World.Entities.Cities.Storage[city]; record.Name = city == 0 ? "Home" : "Destination"; record.BaseTrade = 24;
                for (int i = 0; i < 3; ++i) { record.Supplies[i] = (sbyte)(i + 1); record.Demands[i] = -1; }
            }
            Cargo(0);
            var incidents = new IncidentFixture(Life.Random, Data);
            World.Globals.Turn = 200;
            Trade = new(World, Data.Rules, Life.Random, new(World, Data.Process.Rules, Data.Stacks), Life.Elimination, incidents.Incidents, this, "Food", extra);
        }
        public void Cargo(byte cargo)
        {
            Data.Unit(0).Counter = cargo;
            // An existing exact route suppresses the first refresh, leaving authored reciprocal inputs.
            World.Entities.Cities.Storage[0].RouteCount = 1;
            TradeRoutes.Write(World, new(0), 0, 1, unchecked((sbyte)cargo)); World.Entities.Cities.Storage[0].Flags.TradeDirty = false;
        }
        public CaravanTrade.Operation Deliver() => Trade.BeginDelivery(new(0), new(1), "deliver");
        public CaravanTrade.Operation Enter(CitySlot? city = null) => Trade.BeginEntry(new(0), city ?? new(1), "entry");
        public void Complete(CaravanTrade.Operation op)
        {
            for (int i = 0; i < 100 && !op.Completed; ++i) Equal(null, op.Resume());
            Equal(true, op.Completed); Life.Random.RequireChoicesConsumed();
        }
        public void Sound(CaravanSound sound) { Sounds.Add(sound); if (FailSound) throw new InvalidOperationException("Unavailable sound"); }
        public void Delivery(CaravanDeliveryNotice notice) { Deliveries.Add(notice); if (FailDelivery) throw new InvalidOperationException("Unavailable notice"); }
        public void Reciprocal(CaravanReciprocalNotice notice) => Reciprocals.Add(notice);
        public void Donation(CaravanDonationNotice notice) => Donations.Add(notice);
        public void RefreshStatus() { ++Status; if (FailStatus) throw new InvalidOperationException("Unavailable status"); }
        public void NestedLoss()
        {
            Data.Rules.Units[48] = Data.Rules.Units[48] with { Role = RuleRole.Settle };
            World.Civilizations[1].CityCount = 0; World.Entities.Cities.Storage[0].Owner = new(2);
            World.Entities.Cities.Storage[0].RouteCount = 0;
            Data.AddUnit(1, 3, 4, new(16, 8)); Data.Unit(1).Flags.SpyMission = true; Data.Unit(1).Counter = 1;
            World.Civilizations[1].Treaties[3].Peace = true;
        }
    }

    private static void CaravanPaymentFormula()
    {
        foreach (var (cargo, matches, expected) in new[] { (0, 0, 26), (0, 1, 52), (3, 1, 65), (9, 1, 78), (14, 1, 91), (15, 1, 104), (9, 2, 182), (9, 3, 200) })
        {
            var f = new CaravanFixture(); f.Cargo((byte)cargo);
            for (int i = 0; i < matches; ++i) f.World.Entities.Cities.Storage[1].Demands[i] = (sbyte)cargo;
            using var op = f.Deliver(); f.Complete(op); Equal(expected, op.Payment!.Payment);
            Equal(expected + 10, f.World.Civilizations[1].Gold); Equal((short)expected, f.World.Civilizations[1].ResearchProgress);
            Equal((byte)40, f.World.Civilizations[1].Attitude[2]); Equal((byte)50, f.World.Civilizations[2].Attitude[1]);
            Equal(false, f.Data.Unit(0).Token.Occupied); Equal(1, f.Status); Equal(1, op.Result);
            Equal("Home", f.Deliveries.Single().HomeName); Equal(cargo == 3 ? 2 : 3, op.ReturnCargo); Equal((byte)1, f.World.Entities.Cities.Storage[1].RouteCount);
        }
        var cap = new CaravanFixture(DeliveryChoices(cap: 9)); cap.Cargo(9);
        for (int i = 0; i < 3; ++i) cap.World.Entities.Cities.Storage[1].Demands[i] = 9;
        using var capped = cap.Deliver(); cap.Complete(capped); Equal(209, capped.Payment!.Payment);
        var research = new CaravanFixture(); research.Cargo(9); research.World.Civilizations[1].NumAdvances = 40;
        for (int i = 0; i < 3; ++i) research.World.Entities.Cities.Storage[1].Demands[i] = 9;
        using var richer = research.Deliver(); research.Complete(richer); Equal(390, richer.Payment!.Payment);
        var wrap = new CaravanFixture(); wrap.World.Civilizations[1].Gold = int.MaxValue; wrap.World.Civilizations[1].ResearchProgress = short.MaxValue;
        using var wrapped = wrap.Deliver(); wrap.Complete(wrapped); Equal(unchecked(int.MaxValue + 26), wrap.World.Civilizations[1].Gold);
        Equal(unchecked((short)(short.MaxValue + 26)), wrap.World.Civilizations[1].ResearchProgress);
    }

    private static void CaravanDistanceFacilitiesAndTechnology()
    {
        foreach (var (mode, expected) in new[] { (0, 39), (1, 24), (2, 26), (3, 24), (4, 52), (5, 13), (6, 104), (7, 52), (8, 12) })
        {
            var f = new CaravanFixture();
            if (mode == 0) f.Data.Unit(0).Type = new(UnitTypeIds.Freight);
            if (mode is 1 or 3) f.World.Globals.Status.LargeMap = true;
            if (mode is 2 or 3) f.World.Globals.Status.SmallMap = true;
            if (mode == 4) f.World.Map.TileAt(new(12, 6)).Region = 2;
            if (mode == 5) f.World.Entities.Cities.Storage[1].Owner = new(1);
            if (mode == 6)
            {
                f.World.Map.TileAt(new(12, 6)).Region = 2;
                BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Airport, true); BuildingQueries.SetBuilding(f.World, new(1), BuildingIds.Airport, true);
                // Different regions give 52, then two airport connection increments double it.
            }
            if (mode == 7) { BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Superhighways, true); BuildingQueries.SetBuilding(f.World, new(1), BuildingIds.Superhighways, true); }
            if (mode == 8) f.World.Civilizations[1].Advances = f.World.Civilizations[1].Advances.With(AdvanceIds.Railroad).With(AdvanceIds.Flight);
            using var op = f.Deliver(); f.Complete(op); Equal(expected, op.Payment!.Payment);
            Equal(mode == 0 ? CaravanSound.Freight : CaravanSound.Delivery, f.Sounds[0]);
        }
        foreach (int knowledge in new[] { -1, AdvanceIds.Invention, AdvanceIds.Navigation })
        {
            var f = new CaravanFixture(); f.World.Globals.Turn = 199;
            if (knowledge >= 0) f.World.Civilizations[1].Advances = f.World.Civilizations[1].Advances.With(knowledge);
            using var op = f.Deliver(); f.Complete(op); Equal(knowledge < 0 ? 52 : 26, op.Payment!.Payment);
        }
    }

    private static void CaravanFoodAndHomeDecoding()
    {
        var food = new CaravanFixture([DeliveryCap()]); food.Cargo(255); food.World.Calculation.FoodRows = 7;
        food.World.Mood.City = new(0); food.World.Mood.Happy = 9; food.World.Mood.Unhappy = 2;
        food.World.Entities.Cities.Storage[1].FoodBox = short.MaxValue;
        using var delivery = food.Deliver(); food.Complete(delivery); Equal(200, delivery.Payment!.Payment);
        Equal(unchecked((short)(short.MaxValue + 14)), food.World.Entities.Cities.Storage[1].FoodBox); Equal(7, food.World.Calculation.FoodRows);
        Equal(new CitySlot(0), food.World.Mood.City); Equal(9, food.World.Mood.Happy); Equal(10, food.World.Civilizations[1].Gold);
        Equal((short)0, food.World.Civilizations[1].ResearchProgress); Equal(null, delivery.ReturnCargo); Equal(0, food.Status); Equal("Food", food.Deliveries.Single().CargoName);
        var same = new CaravanFixture([DeliveryCap()]); same.Cargo(254); same.World.Entities.Cities.Storage[1].Owner = new(1); same.World.Civilizations[1].Attitude[1] = 15;
        using var own = same.Deliver(); same.Complete(own); Equal((byte)0, same.World.Civilizations[1].Attitude[1]);
        foreach (bool fallback in new[] { false, true })
        {
            var f = new CaravanFixture(); f.Data.Unit(0).Home = null;
            if (fallback) f.World.Entities.Cities.Storage[0].Owner = new(2);
            else { f.Data.AddCity(2, 1, new(8, 6)); f.World.Entities.Cities.Storage[2].BaseTrade = 24; }
            using var op = f.Deliver(); f.Complete(op); Equal(new CitySlot(fallback ? (ushort)0 : (ushort)2), op.Payment!.Home);
        }
        var unsigned = new CaravanFixture(); unsigned.Data.AddCity(200, 1, new(8, 6)); unsigned.World.Entities.Cities.Storage[200].BaseTrade = 24; unsigned.Data.Unit(0).Home = new(200);
        using var high = unsigned.Deliver(); unsigned.Complete(high); Equal(new CitySlot(200), high.Payment!.Home);
    }

    private static void CaravanReciprocalSelectionAndMissingNames()
    {
        var ai = new CaravanFixture(); ai.World.Globals.HumanPlayers = CivilizationSet.Only(2); ai.World.Multiplayer.LocalHuman = 2;
        ai.World.Multiplayer.Saved.AutoplayActive = true;
        using var direct = ai.Deliver(); ai.Complete(direct); Equal(0, ai.Deliveries.Count); Equal(1, ai.Reciprocals.Count);
        Equal(1, ai.Sounds.Count); Equal(36, ai.World.Civilizations[1].Gold); Equal(0, ai.Status);
        var f = new CaravanFixture(DeliveryChoices(a: 7, b: 0, c: 0)); f.World.Entities.Cities.Storage[1].Supplies[0] = -1;
        f.World.Entities.Cities.Storage[1].Supplies[1] = 2; f.World.Entities.Cities.Storage[1].Supplies[2] = 3;
        f.World.Entities.Cities.Storage[0].Demands[0] = f.World.Entities.Cities.Storage[0].Demands[1] = 2;
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); f.World.Multiplayer.LocalHuman = 2;
        using var op = f.Deliver(); f.Complete(op); Equal(2, op.ReturnCargo); Equal("Cargo 2", f.Reciprocals.Single().ReturnName);
        Equal(new UnitType(48), f.Reciprocals[0].Type); Equal(2, f.Sounds.Count); Equal(2, f.Deliveries.Single().Recipient); Equal(0, f.Status);
        foreach (bool backing in new[] { false, true })
        {
            var missing = new CaravanFixture(extra: backing ? new Dictionary<int, string> { [-1] = "Backed return name" } : null);
            missing.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
            for (int i = 0; i < 3; ++i) missing.World.Entities.Cities.Storage[1].Supplies[i] = 0;
            using var result = missing.Deliver();
            if (backing) missing.Complete(result); else { Throws<InvalidOperationException>(() => result.Resume()); Throws<InvalidOperationException>(() => result.Resume()); }
            Equal(-1, result.ReturnCargo); Equal(false, missing.Data.Unit(0).Token.Occupied); Equal(36, missing.World.Civilizations[1].Gold);
            Equal((sbyte)-1, missing.World.Entities.Cities.Storage[1].RouteGoods[0]); Equal(2, missing.Sounds.Count); Equal(0, missing.Reciprocals.Count);
            missing.Life.Random.RequireChoicesConsumed();
        }
    }

    private static void CaravanEntryAdmissionAndDonation()
    {
        foreach (int mode in new[] { 0, 1, 2, 3 })
        {
            var f = new CaravanFixture([]); f.World.Entities.Cities.Storage[1].Owner = new(1);
            if (mode == 0) f.World.Multiplayer.Saved.AutoplayActive = true;
            if (mode == 1) { f.World.Globals.HumanPlayers = default; f.Data.Unit(0).Home = new(1); }
            if (mode == 2) { f.World.Globals.HumanPlayers = default; f.World.Entities.Cities.Storage[1].Flags.FerryPort = true; }
            if (mode == 3) { f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(18, 8); }
            using var op = f.Enter(); f.Complete(op); Equal(0, op.Result); Equal(true, f.Data.Unit(0).Token.Occupied); Equal(0, f.Deliveries.Count);
        }
        var ownHome = new CaravanFixture([]); using var none = ownHome.Enter(new(0)); ownHome.Complete(none); Equal(0, none.Result);
        var highHome = new CaravanFixture([]); highHome.Data.AddCity(200, 1, new(8, 6)); highHome.Data.Unit(0).Home = new(200);
        using var high = highHome.Enter(new(200)); var highMenu = (CaravanDecision.Menu)high.Resume()!;
        Equal(true, highMenu.Request.Actions.Contains(CaravanAction.Deliver)); high.Resume(0); highHome.Complete(high);
        foreach (bool freight in new[] { false, true })
        {
            var f = new CaravanFixture([]); f.World.Entities.Cities.Storage[1].Owner = new(1); f.World.Entities.Cities.Storage[1].Production = -39;
            f.World.Entities.Cities.Storage[1].ShieldBox = 10; f.World.Entities.Cities.Storage[1].Demands[0] = 0;
            if (freight) f.Data.Unit(0).Type = new(49);
            using var op = f.Enter(); var menu = (CaravanDecision.Menu)op.Resume()!; Equal(menu, op.Resume()); Equal(true, menu.Request.Demanded);
            Sequence(new[] { CaravanAction.Continue, CaravanAction.Deliver, CaravanAction.HelpBuildWonder }, menu.Request.Actions);
            f.World.Entities.Cities.Storage[1].Production = 4; f.Data.Rules.Improvements[4] = new() { CostRows = 12 };
            f.Data.Rules.Units[freight ? 49 : 48] = f.Data.Rules.Units[48] with { CostRows = 255 };
            f.Data.Rules.Cosmic = f.Data.Rules.Cosmic with { ShieldBoxRows = 20 }; f.World.Calculation.ShieldRows = 999;
            op.Resume(2); f.Complete(op); Equal((short)-10, f.World.Entities.Cities.Storage[1].ShieldBox);
            Equal(-20, f.Donations.Single().Shields); Equal(250, f.Donations[0].Remaining); Equal(true, f.World.Entities.Cities.Storage[0].Flags.TradeDirty);
            Equal(false, f.Data.Unit(0).Token.Occupied); Equal(freight ? CaravanSound.Freight : CaravanSound.WonderDonation, f.Sounds.Single());
            Equal(10, f.World.Civilizations[1].Gold); Equal(1, op.Result);
        }
    }

    private static void CaravanNestedLossAndFailurePrefixes()
    {
        var f = new CaravanFixture(); f.NestedLoss(); using var op = f.Deliver(); var pending = op.Resume();
        Equal(true, pending is CaravanDecision.Lifecycle); Equal(pending, op.Resume()); Equal(36, f.World.Civilizations[1].Gold);
        Equal(false, f.Data.Unit(0).Token.Occupied || f.Data.Unit(1).Token.Occupied); Equal((byte)0, f.World.Entities.Cities.Storage[0].RouteCount);
        op.Resume(0); f.Complete(op); Equal((byte)1, f.World.Entities.Cities.Storage[0].RouteCount); Equal(EndReason.Defeat, f.World.PendingEndReason);
        Equal(null, op.Resume()); Equal(36, f.World.Civilizations[1].Gold);
        var rejected = new CaravanFixture(); rejected.Data.AddCity(2, 1, new(18, 8));
        rejected.World.Entities.Cities.Storage[2].BaseTrade = 30000; rejected.World.Map.TileAt(new(18, 8)).Region = 2;
        rejected.World.Entities.Cities.Storage[0].RouteCount = 3;
        for (int i = 0; i < 3; ++i) TradeRoutes.Write(rejected.World, new(0), i, 2, i + 1);
        using var paid = rejected.Deliver(); rejected.Complete(paid); Equal((byte)3, rejected.World.Entities.Cities.Storage[0].RouteCount);
        Equal(new CitySlot(2), rejected.World.Entities.Cities.Storage[0].RoutePartners[0]); Equal(36, rejected.World.Civilizations[1].Gold);
        Equal(false, rejected.Data.Unit(0).Token.Occupied); Equal((byte)1, rejected.World.Entities.Cities.Storage[1].RouteCount);
        var failed = new CaravanFixture([DeliveryCap()]) { FailDelivery = true }; using var notice = failed.Deliver(); Throws<InvalidOperationException>(() => notice.Resume());
        Equal(26, notice.Payment!.Payment); Equal(10, failed.World.Civilizations[1].Gold); Equal((byte)50, failed.World.Civilizations[1].Attitude[2]); Equal(true, failed.Data.Unit(0).Token.Occupied);
        var cosmetic = new CaravanFixture { FailSound = true, FailStatus = true }; using var shown = cosmetic.Deliver(); cosmetic.Complete(shown); Equal(2, shown.PresentationFailures.Count);
        var remote = new CaravanFixture([]); remote.World.Globals.Multiplayer = GameMode.Tcp; using var denied = remote.Enter(); Throws<InvalidOperationException>(() => denied.Resume()); Equal(0, remote.Sounds.Count);
        var disposed = new CaravanFixture([]); disposed.World.Entities.Cities.Storage[1].Owner = new(1); var stopped = disposed.Enter(); stopped.Resume(); stopped.Dispose();
        Throws<ObjectDisposedException>(() => stopped.Resume(1)); Equal(true, disposed.Data.Unit(0).Token.Occupied);
    }

    private static void CaravanPrivateSession()
    {
        foreach (bool nested in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(250));
            var f = new CaravanFixture(DeliveryChoices(nested ? "deliver" : "entry/delivery"), CreationFixture(new(runtime.Process, runtime.World)));
            if (nested) f.NestedLoss(); else f.World.Entities.Cities.Storage[1].Owner = new(1);
            ParticipantId recipient = default;
            runtime.RegisterCommand(new(new(1, "test.caravan", "Authored caravan entry/delivery", AccessClass.Player, new([]), new([new("result")])),
                (_, _, _) => new(), _ => new CaravanOperation(nested ? f.Deliver() : f.Enter(), _ => recipient)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 1); recipient = human.Identity.Participant;
            var command = actor.Submit(new("test.caravan", new([]))).Correlation.Command; runtime.AdvanceOne();
            var question = RequiredInteraction(human, command); Equal(null, RequiredResult(actor, command).Interaction);
            Equal(!nested, f.Data.Unit(0).Token.Occupied); Equal(false, actor.Reply(question, 0).Accepted);
            Equal(true, human.Reply(question, nested ? 0 : 1).Accepted); runtime.AdvanceOne();
            Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(false, f.Data.Unit(0).Token.Occupied);
            Equal((byte)1, f.World.Entities.Cities.Storage[1].RouteCount); f.Life.Random.RequireChoicesConsumed();
        }
    }
}
