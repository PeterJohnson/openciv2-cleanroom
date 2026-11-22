using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // C06 category/snapshot, setting orders and AI wonder-change/reset contracts.
    // Controlled chooser replies test the required A02 boundary, not the missing policy itself.
    private sealed class OrdersFixture
    {
        public RevelationFixture Data { get; } = new();
        public GameState World => Data.World;
        public RuleTables Rules => Data.Rules;
        public ref City City => ref World.Entities.Cities.Storage[0];
        public ProductionOrders Orders { get; }
        public List<CitySlot> Choices { get; } = [];
        public List<ProductionWonderNotice> Notices { get; } = [];
        public Func<CitySlot, int>? OnChoose;
        public Action<ProductionWonderNotice>? OnNotice;

        public OrdersFixture(bool human = true)
        {
            Data.AddCity(0, 1, new(10, 10)); City.Production = 2;
            World.Globals.HumanPlayers = CivilizationSet.Only(human ? 1 : 2);
            World.Globals.Difficulty = Difficulty.King; World.Names[1].Tribe = "First people";
            World.Calculation.ShieldRows = 10; World.Calculation.Shields = 4; World.Calculation.ShieldSupport = 1;
            Rules.Cosmic = Rules.Cosmic with { ProductionSwitchLossPercent = 50 };
            Rules.Units[2] = Rules.Units[2] with { CostRows = 4 };
            for (int i = 1; i < Rules.Improvements.Length; ++i)
                Rules.Improvements[i] = new() { Identity = new($"building-{i}", $"Building {i}"), CostRows = 3 };
            Orders = new(World, Rules, city => { Choices.Add(city); return OnChoose?.Invoke(city) ?? throw new InvalidOperationException("A02 choice not supplied"); },
                n => { Notices.Add(n); OnNotice?.Invoke(n); });
        }
    }

    private static void ProductionSnapshotAndPenalty()
    {
        var f = new OrdersFixture(); f.City.ShieldBox = 101; f.Orders.SaveBuildState(new(0));
        foreach (int item in new[] { 0, 61, 99, 999, int.MaxValue }) Equal(ProductionCategory.Units, ProductionOrders.Category(item));
        foreach (int item in new[] { -1, -34 }) Equal(ProductionCategory.Buildings, ProductionOrders.Category(item));
        foreach (int item in new[] { -35, -38, -39, -66, -65537, int.MinValue }) Equal(ProductionCategory.OtherNonunit, ProductionOrders.Category(item));
        f.City.ShieldBox = 999;
        Equal(51, f.Orders.ShieldsAfterChange(-1)); Equal(101, f.Orders.ShieldsAfterChange(4));
        Equal(ProductionCategory.Units, f.World.Production.SavedCategory); Equal(101, f.World.Production.SavedStock);
        f.World.Production.SavedStock = -1; Equal(-32768, f.Orders.ShieldsAfterChange(-1));
        f.World.Production.SavedStock = 70001; Equal(67769, f.Orders.ShieldsAfterChange(-1));
        f.World.Production.SavedStock = 101; f.Rules.Cosmic = f.Rules.Cosmic with { ProductionSwitchLossPercent = 255 };
        Equal(-156, f.Orders.ShieldsAfterChange(-1));
        f.World.Globals.Difficulty = Difficulty.Chieftain; Equal(101, f.Orders.ShieldsAfterChange(-1));
        f.Data.AddCity(1, 1, new(12, 10)); f.World.Entities.Cities.Storage[1].ShieldBox = 77;
        f.Orders.Set(new(1), 4); Equal((short)101, f.World.Entities.Cities.Storage[1].ShieldBox); // Snapshot has no city identity.
        Equal(101, f.World.Production.SavedStock);
    }

    private static void ProductionBuildForecast()
    {
        var f = new OrdersFixture(); Equal(10, f.Orders.BuildTurns(2, 10));
        f.World.Calculation.Shields = 0; Equal(30, f.Orders.BuildTurns(2, 10));
        f.Rules.Units[2] = f.Rules.Units[2] with { CostRows = 255 }; Equal(1, f.Orders.BuildTurns(2, 0));
        f.Rules.Improvements[1] = f.Rules.Improvements[1] with { CostRows = 255 };
        Equal(999, f.Orders.BuildTurns(-1, 0));
        f.World.Calculation.Shields = 1000; Equal(26, f.Orders.BuildTurns(-1, 0));
        f.World.Calculation.Shields = int.MinValue; Equal(26, f.Orders.BuildTurns(-1, 0)); // Wrapped net rate clamps to99.
        f.Rules.Improvements[1] = f.Rules.Improvements[1] with { CostRows = 2 };
        f.World.Calculation.ShieldRows = int.MaxValue; Equal(1, f.Orders.BuildTurns(-1, 0));
    }

    private static void ProductionHumanCountersAndNarrowing()
    {
        var f = new OrdersFixture(); f.City.ShieldBox = 101; f.Orders.SaveBuildState(new(0));
        f.World.Civilizations[1].UnitsInProduction[4] = 255;
        f.Orders.Set(new(0), 4);
        Equal((byte)255, f.World.Civilizations[1].UnitsInProduction[2]); Equal((byte)0, f.World.Civilizations[1].UnitsInProduction[4]);
        f.Orders.Set(new(0), 4); Equal((byte)0, f.World.Civilizations[1].UnitsInProduction[4]);
        f.Orders.Set(new(0), -1); Equal((short)51, f.City.ShieldBox); Equal((byte)255, f.World.Civilizations[1].UnitsInProduction[4]);
        f.Orders.Set(new(0), 2); Equal((short)101, f.City.ShieldBox); Equal((byte)0, f.World.Civilizations[1].UnitsInProduction[2]);
        f.World.Production.SavedCategory = ProductionCategory.Buildings;
        f.Orders.Set(new(0), -258); Equal((sbyte)-2, f.City.Production); Equal((short)51, f.City.ShieldBox); // Classify full request before signed8 store.
        f.Orders.Set(new(0), 62); Equal((sbyte)62, f.City.Production); // No off-array count write.
        f.Orders.Set(new(0), int.MinValue); Equal((sbyte)0, f.City.Production); Equal((byte)1, f.World.Civilizations[1].UnitsInProduction[0]);
        Equal(0, f.Choices.Count); Equal(0, f.Notices.Count); Equal(101, f.World.Production.SavedStock);
    }

    private static void ProductionChooserPhaseAndOwner()
    {
        var f = new OrdersFixture(); f.City.ShieldBox = 101; f.Orders.SaveBuildState(new(0));
        f.World.Production.CivilizationTurnInProgress = true; f.City.ShieldBox = 9;
        f.Orders.Set(new(0), -1); Equal((short)9, f.City.ShieldBox);
        f.World.Production.CivilizationTurnInProgress = false; f.City.Production = 2;
        f.OnChoose = slot =>
        {
            Equal(new CitySlot(0), slot); Equal((byte)254, f.World.Civilizations[1].UnitsInProduction[2]);
            f.City.Owner = new(2); f.City.ShieldBox = 88; return 4;
        };
        f.Orders.Set(new(0), 1000);
        Equal((sbyte)4, f.City.Production); Equal((short)101, f.City.ShieldBox);
        Equal((byte)1, f.World.Civilizations[1].UnitsInProduction[4]); Equal((byte)0, f.World.Civilizations[2].UnitsInProduction[4]);
        var ai = new OrdersFixture(false); ai.City.ShieldBox = 19; ai.World.Production.SavedStock = 999;
        ai.Orders.Set(new(0), -1); Equal((short)19, ai.City.ShieldBox);
        ai.OnChoose = _ => { ai.World.Globals.HumanPlayers = CivilizationSet.Only(1); return 4; };
        ai.Orders.Set(new(0), 99); Equal((short)999, ai.City.ShieldBox); // Human membership is read after the chooser.
    }

    private static void ProductionChooserSentinelsAndFailure()
    {
        var f = new OrdersFixture(); f.City.ShieldBox = 9;
        f.OnChoose = _ => { Equal((byte)255, f.World.Civilizations[1].UnitsInProduction[2]); return 99; };
        f.Orders.Set(new(0), 99); Equal((sbyte)2, f.City.Production); Equal((short)9, f.City.ShieldBox);
        Equal((byte)0, f.World.Civilizations[1].UnitsInProduction[2]);
        f.OnChoose = _ => { f.City.Production = 4; return 999; };
        f.Orders.Set(new(0), 99); Equal((sbyte)4, f.City.Production); Equal((byte)1, f.World.Civilizations[1].UnitsInProduction[4]);
        f.OnChoose = _ => throw new InvalidOperationException("chooser stopped");
        Throws<InvalidOperationException>(() => f.Orders.Set(new(0), 99));
        Equal((byte)0, f.World.Civilizations[1].UnitsInProduction[4]); Equal((sbyte)4, f.City.Production);
        f.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => f.Orders.Set(new(0), 2)); Equal((byte)0, f.World.Civilizations[1].UnitsInProduction[4]);
        Equal(3, f.Choices.Count);
    }

    private static void ProductionAiWonderStartAndBonus()
    {
        var f = new OrdersFixture(false); f.City.ShieldBox = 9;
        f.OnNotice = _ => Equal(true, f.World.Production.WonderNotices[1][0].Started);
        f.Orders.Set(new(0), -39);
        Equal((short)4, f.City.ShieldBox); Equal(ProductionWonderNoticeKind.Start, f.Notices.Single().Kind);
        Equal("First people", f.Notices[0].Civilization); Equal("Building 39", f.Notices[0].Name);
        var negative = new OrdersFixture(false); negative.City.ShieldBox = -9; negative.Orders.Set(new(0), -39);
        Equal((short)-4, negative.City.ShieldBox); // Truncation rather than arithmetic shift.
        foreach (bool autoplay in new[] { false, true })
        {
            f = new(false); f.World.Multiplayer.Saved.AutoplayActive = autoplay;
            f.World.Civilizations[2].WondersByGroup[0] = 1;
            f.Orders.Set(new(0), -39); Equal((short)9, f.City.ShieldBox); Equal(autoplay ? 0 : 1, f.Notices.Count);
            Equal(true, f.World.Production.WonderNotices[1][0].Started);
        }
        f = new(false); f.World.Globals.HumanPlayers = default; f.Orders.Set(new(0), -39); Equal((short)9, f.City.ShieldBox);
        f = new(false); f.Orders.Set(new(0), -39); Equal((short)0, f.City.ShieldBox); // Inactive human2 with no group wonder still blocks bonus.
        f = new(false); f.World.Civilizations[2].WondersByGroup[0] = 1; f.World.Civilizations[1].WondersByGroup[0] = 1;
        f.Orders.Set(new(0), -39); Equal((short)0, f.City.ShieldBox);
    }

    private static void ProductionAiWonderRivalsAndSwitch()
    {
        foreach (short location in new short[] { -1, -2 })
        {
            var f = new OrdersFixture(false); f.City.Production = -39; f.City.ShieldBox = 25;
            f.Data.AddCity(1, 1, new(12, 10)); f.World.Entities.Cities.Storage[1].Production = -39;
            f.Data.AddCity(2, 2, new(14, 10)); f.World.Entities.Cities.Storage[2].Production = -39;
            f.World.Globals.WonderCity[0] = location; f.Orders.Set(new(0), -40);
            Equal((sbyte)(location == -1 ? -39 : -40), f.World.Entities.Cities.Storage[1].Production);
            Equal((sbyte)-39, f.World.Entities.Cities.Storage[2].Production); Equal((short)25, f.City.ShieldBox);
            Equal(location == -1 ? ProductionWonderNoticeKind.Start : ProductionWonderNoticeKind.Switch, f.Notices.Single().Kind);
            Equal(location == -1, f.World.Production.WonderNotices[1][1].Started);
            if (location == -2) { Equal((int?)0, f.Notices[0].PreviousWonder); Equal("Building 39", f.Notices[0].PreviousName); }
        }
        var only = new OrdersFixture(false); only.City.Production = -39; only.Orders.Set(new(0), -40);
        Equal(ProductionWonderNoticeKind.Switch, only.Notices.Single().Kind); // No other old builder, even while old wonder is unbuilt.
        var duplicate = new OrdersFixture(false); duplicate.City.ShieldBox = 9;
        duplicate.Data.AddCity(1, 1, new(12, 10)); duplicate.World.Entities.Cities.Storage[1].Production = -39;
        duplicate.Orders.Set(new(0), -39); Equal((short)4, duplicate.City.ShieldBox); Equal(0, duplicate.Notices.Count);
        Equal(false, duplicate.World.Production.WonderNotices[1][0].Started);
    }

    private static void ProductionAiWonderAbandon()
    {
        var f = new OrdersFixture(false); f.City.Production = -39;
        f.OnNotice = _ => { Equal(true, f.World.Production.WonderNotices[1][0].Abandoned); Equal((byte)0, f.World.Civilizations[1].UnitsInProduction[4]); };
        f.Orders.Set(new(0), 4); Equal(ProductionWonderNoticeKind.Abandon, f.Notices.Single().Kind);
        Equal((byte)1, f.World.Civilizations[1].UnitsInProduction[4]);
        f.City.Production = -39; f.Orders.Set(new(0), 4); Equal(1, f.Notices.Count);
        f = new(false); f.City.Production = -39; f.World.Multiplayer.Saved.AutoplayActive = true;
        f.Orders.Set(new(0), 4); Equal(0, f.Notices.Count); Equal(true, f.World.Production.WonderNotices[1][0].Abandoned);
        f = new(false); f.City.Production = -39; f.World.Globals.WonderCity[0] = 0;
        f.Orders.Set(new(0), 4); Equal(false, f.World.Production.WonderNotices[1][0].Abandoned);
        f = new(false); f.City.Production = -39; f.Data.AddCity(1, 1, new(12, 10)); f.World.Globals.WonderCity[0] = 1;
        f.Orders.Set(new(0), 4); Equal(0, f.Notices.Count); // Active ownership at another city suppresses abandonment.
        f.City.Production = -39; f.Rules.WonderObsolescence[0] = (AdvanceReference)3;
        f.World.Civilizations[7].Advances = default(AdvanceSet).With(3); f.Orders.Set(new(0), 4); Equal(1, f.Notices.Count);
        f = new(false); f.City.Production = -39; f.Data.AddCity(1, 1, new(12, 10)); f.World.Entities.Cities.Storage[1].Production = -39;
        f.Orders.Set(new(0), 4); Equal(0, f.Notices.Count); // Another builder continues the wonder.
    }

    private static void ProductionWonderLiveContinuation()
    {
        var f = new OrdersFixture(false); f.City.ShieldBox = 9;
        f.OnNotice = _ =>
        {
            Equal((short)4, f.City.ShieldBox); f.City.Owner = new(2); f.City.Production = 4; f.City.ShieldBox = 0;
            f.Rules.Improvements[39] = f.Rules.Improvements[39] with { CostRows = 255 };
            f.World.Globals.Difficulty = Difficulty.Deity; f.World.Civilizations[2].WondersByGroup[0] = 1;
        };
        f.Orders.Set(new(0), -39);
        Equal((short)1275, f.City.ShieldBox); // Retained wonder/group, live cost/difficulty/stock, captured owner.
        Equal((byte)1, f.World.Civilizations[1].UnitsInProduction[4]); Equal((byte)0, f.World.Civilizations[2].UnitsInProduction[4]);
        Equal((sbyte)4, f.City.Production);
        f = new(false); f.City.ShieldBox = 9; f.OnNotice = _ => throw new InvalidOperationException("notice stopped");
        Throws<InvalidOperationException>(() => f.Orders.Set(new(0), -39));
        Equal((sbyte)-39, f.City.Production); Equal((short)4, f.City.ShieldBox);
        Equal((byte)255, f.World.Civilizations[1].UnitsInProduction[2]); Equal(true, f.World.Production.WonderNotices[1][0].Started);
        f = new(false); f.City.Production = -39; f.Data.AddCity(1, 1, new(12, 10)); f.World.Entities.Cities.Storage[1].Production = -39;
        f.World.Globals.WonderCity[0] = -2; f.OnNotice = _ => throw new InvalidOperationException("switch notice stopped");
        Throws<InvalidOperationException>(() => f.Orders.Set(new(0), -40)); Equal((sbyte)-40, f.World.Entities.Cities.Storage[1].Production);
    }

    private static void ProductionResetFiltersAndLiveBounds()
    {
        var f = new OrdersFixture(); f.World.Map.TileAt(f.City.Position).Region = 2;
        f.Data.AddCity(1, 2, new(12, 10)); f.World.Map.TileAt(new(12, 10)).Region = 2;
        f.Data.AddCity(2, 1, new(14, 10)); f.World.Map.TileAt(new(14, 10)).Region = 3;
        f.Data.AddCity(3, 1, new(16, 10)); f.World.Entities.Cities.Storage[3].Token = default;
        f.Data.AddCity(4, 255, new(18, 10));
        f.OnChoose = _ => 2;
        f.Orders.ResetCivilization(257, 2); Sequence(new[] { new CitySlot(0) }, f.Choices);
        f.Choices.Clear(); f.Orders.ResetCivilization(255); Equal(0, f.Choices.Count); // Signed saved owner does not equal unsigned255.
        f.World.Globals.Multiplayer = GameMode.Hotseat; f.World.Multiplayer.HotseatParticipants = CivilizationSet.Only(1);
        f.Orders.ResetCivilization(1); Equal(0, f.Choices.Count); // Includes the local hotseat human.
        f.World.Globals.Multiplayer = GameMode.Email;
        f.OnChoose = city => { if (city.Value == 0) f.Data.AddCity(5, 1, new(20, 10)); return 4; };
        f.Orders.ResetCivilization(1, -9); Sequence(new[] { new CitySlot(0), new CitySlot(2), new CitySlot(5) }, f.Choices);
        f.Choices.Clear(); f.OnChoose = city => city.Value == 2 ? throw new InvalidOperationException("reset stopped") : 2;
        Throws<InvalidOperationException>(() => f.Orders.ResetCivilization(1));
        Equal((sbyte)2, f.City.Production); Equal((sbyte)4, f.World.Entities.Cities.Storage[5].Production);
        Sequence(new[] { new CitySlot(0), new CitySlot(2) }, f.Choices);
    }

    private static void ProductionWorkspaceLifetime()
    {
        var f = new OrdersFixture(false); f.City.ShieldBox = 101; f.Orders.SaveBuildState(new(0)); f.Orders.Set(new(0), -39);
        f.City.Production = 2;
        var sameWorld = new ProductionOrders(f.World, f.Rules, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException("repeat notice"));
        sameWorld.Set(new(0), -39); Equal(101, f.World.Production.SavedStock);
        var fresh = new GameState(f.Data.Process);
        Equal(0, fresh.Production.SavedStock); Equal(ProductionCategory.Units, fresh.Production.SavedCategory);
        Equal(false, fresh.Production.WonderNotices[1][0].Started); Equal(false, fresh.Production.CivilizationTurnInProgress);
        Equal(true, f.World.Production.WonderNotices[1][0].Started);
    }

    private static void ProductionPurchaseUsesActualOrderSetting()
    {
        var f = new OrdersFixture(false); f.OnChoose = _ => -39;
        f.World.Civilizations[1].Gold = 640; f.World.Civilizations[2].WondersByGroup[0] = 1;
        var purchase = new AiProductionPurchase(f.World, f.Rules, city => f.Orders.Set(city, 99),
            _ => throw new InvalidOperationException("Wonder is not almost complete"));
        purchase.Run(new(0), 1, 3);
        Equal((sbyte)-39, f.City.Production); Equal((short)19, f.City.ShieldBox); // Start bonus9, then actual purchase10.
        Equal(630, f.World.Civilizations[1].Gold); Equal((byte)255, f.World.Civilizations[1].UnitsInProduction[2]);
        Equal(1, f.Choices.Count); Equal(ProductionWonderNoticeKind.Start, f.Notices.Single().Kind);
        Equal(BuildingQueries.Unbuilt, (int)f.World.Globals.WonderCity[0]);
    }
}
