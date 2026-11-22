using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class FoodEffects : ICityFoodEffects, ICityFoodContinuations
    {
        public List<(FamineCause Cause, string? Detail)> Famines { get; } = [];
        public List<int> Facilities { get; } = [];
        public List<(int Owner, int Killer)> Eliminations { get; } = [];
        public List<CitySlot> Reconsidered { get; } = [];
        public int Redraws, Warnings;
        public Action? OnElimination;
        public void Famine(CitySlot city, FamineCause cause, string? detail) => Famines.Add((cause, detail));
        public void GrowthHalted(CitySlot city, int facility) => Facilities.Add(facility);
        public void FoodShortage(CitySlot city) => ++Warnings;
        public void RedrawCity(CitySlot city) => ++Redraws;
        public void ReconsiderProduction(CitySlot city) => Reconsidered.Add(city);
        public void CheckCivilization(int owner, int killer) { Eliminations.Add((owner, killer)); OnElimination?.Invoke(); }
    }

    private static RevelationFixture FoodFixture()
    {
        var f = WorkingFixture();
        f.Rules.Cosmic = f.Rules.Cosmic with
        {
            FoodBoxRows = 10,
            AqueductSize = 8,
            SewerSize = 12,
            SettlerFoodLowGovernment = 1,
            SettlerFoodHighGovernment = 2
        };
        f.World.Civilizations[1].CityCount = 1;
        return f;
    }

    private static CityFood Food(RevelationFixture f, FoodEffects effects, List<CityTarget>? removed = null) =>
        new(f.World, f.Rules, Removal(f, removed ?? []), Loss(f, new()), effects, effects);

    private static void FoodGrowthAndBlockedRefill()
    {
        // C05 published size8/Aqueduct example. Excess stock is discarded, attempted-size refill retained.
        var f = FoodFixture(); var effects = new FoodEffects(); var food = Food(f, effects);
        ref var city = ref f.World.Entities.Cities.Storage[0]; city.Size = 8; city.FoodBox = 90;
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Granary, true);
        Equal(false, food.Run(new(0))); Equal((sbyte)8, city.Size); Equal((short)50, city.FoodBox);
        Equal(BuildingIds.Aqueduct, effects.Facilities.Single()); Equal(1, effects.Redraws);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Aqueduct, true); city.FoodBox = 300;
        food.Run(new(0)); Equal((sbyte)9, city.Size); Equal((short)50, city.FoodBox); // One attempt only.
        city.Size = 12; city.FoodBox = 130; food.Run(new(0));
        Equal((sbyte)12, city.Size); Equal((short)70, city.FoodBox); Equal(BuildingIds.SewerSystem, effects.Facilities.Last());
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Granary, false); city.Size = 3; city.FoodBox = 39;
        food.Run(new(0)); Equal((sbyte)3, city.Size); Equal((short)39, city.FoodBox);
        city.FoodBox = 40; food.Run(new(0)); Equal((sbyte)4, city.Size); Equal((short)0, city.FoodBox);
        Equal(0, effects.Reconsidered.Count);
    }

    private static void FoodAiFactorsAndProductionBoundary()
    {
        var f = FoodFixture(); var effects = new FoodEffects(); var food = Food(f, effects);
        f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Globals.Difficulty = Difficulty.Deity;
        ref var city = ref f.World.Entities.Cities.Storage[0]; city.FoodBox = 31;
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Granary, true);
        food.Run(new(0)); Equal((sbyte)3, city.Size); Equal((short)31, city.FoodBox);
        city.FoodBox = 32; food.Run(new(0)); Equal((sbyte)4, city.Size); Equal((short)25, city.FoodBox);
        f.Rules.Cosmic = f.Rules.Cosmic with { FoodBoxRows = 9 }; city.Size = 3; city.FoodBox = 32;
        food.Run(new(0)); Equal((sbyte)4, city.Size); Equal((short)20, city.FoodBox); // Raw9 halves to4, not AI rows/2.
        city.Size = 1; city.FoodBox = 16; city.Production = 4; food.Run(new(0));
        Equal(new CitySlot(0), effects.Reconsidered.Single()); Equal((sbyte)4, city.Production);
        f.Rules.Cosmic = f.Rules.Cosmic with { AqueductSize = 2 }; city.FoodBox = 24;
        food.Run(new(0)); Equal((sbyte)2, city.Size); Equal(2, effects.Reconsidered.Count); // Also after a blocked attempt.
        city.FoodBox = 23; food.Run(new(0)); Equal(2, effects.Reconsidered.Count); // No request merely for being size2.
    }

    private static void FoodWondersAndScalarWrap()
    {
        var f = FoodFixture(); var effects = new FoodEffects(); var food = Food(f, effects);
        ref var city = ref f.World.Entities.Cities.Storage[0]; city.FoodBox = 40;
        f.World.Globals.WonderCity[WonderIds.Pyramids] = 0;
        food.Run(new(0)); Equal((short)25, city.FoodBox);
        f.Rules.WonderObsolescence[WonderIds.Pyramids] = (AdvanceReference)0;
        f.World.Civilizations[7].Advances = default(AdvanceSet).With(0); // Expiry includes dead AI seats.
        city.FoodBox = 50; food.Run(new(0)); Equal((short)0, city.FoodBox);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Granary, true);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Aqueduct, true);
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.SewerSystem, true);
        city.Size = 127; city.FoodBox = 1280; food.Run(new(0));
        Equal((sbyte)-128, city.Size); Equal((short)-635, city.FoodBox);
        food.Run(new(0)); Equal((sbyte)127, city.Size); Equal((short)0, city.FoodBox);
        Equal(0, effects.Eliminations.Count); Equal(true, city.Token.Occupied);
    }

    private static void FoodFamineSelectionAndVisibility()
    {
        var f = FoodFixture(); var effects = new FoodEffects(); var food = Food(f, effects);
        f.Rules.Units[4] = f.Rules.Units[0] with { Role = RuleRole.Settle, Identity = new("unit.farmers", "Custom farmers") };
        f.AddUnit(0, 2, 4, new(10, 6)); f.AddUnit(1, 1, 4, new(12, 6));
        f.Unit(0).Home = f.Unit(1).Home = new(0);
        ref var city = ref f.World.Entities.Cities.Storage[0]; city.FoodBox = -1;
        f.World.Civilizations[2].CityCount = 1;
        food.Run(new(0)); Equal(false, f.Unit(0).Token.Occupied); Equal(true, f.Unit(1).Token.Occupied);
        Equal((sbyte)3, city.Size); Equal((short)0, city.FoodBox);
        Equal((FamineCause.SettlerSupport, (string?)"Custom farmers"), effects.Famines.Single());
        f.World.Globals.HumanPlayers = CivilizationSet.Only(2); city.FoodBox = -7; city.KnownTo = default;
        food.Run(new(0)); Equal(true, f.Unit(1).Token.Occupied); Equal((sbyte)3, city.Size); // Unknown AI keeps the settler.
        city.KnownTo = CivilizationSet.Only(2); city.FoodBox = -7; f.World.Multiplayer.Saved.AutoplayActive = true;
        food.Run(new(0)); Equal(false, f.Unit(1).Token.Occupied); Equal((sbyte)3, city.Size); Equal(2, effects.Famines.Count);
        city.FoodBox = -7; food.Run(new(0)); Equal((sbyte)2, city.Size); Equal((short)0, city.FoodBox);
        Equal(4, effects.Redraws);
    }

    private static void FoodFamineCargoAndRawHome()
    {
        var f = FoodFixture(); var effects = new FoodEffects(); var food = Food(f, effects);
        f.Rules.Units[1] = f.Rules.Units[1] with { Role = RuleRole.Settle, Capacity = 1 };
        f.World.Map.TileAt(CargoPoint).Terrain = Terrain.Ocean;
        f.AddUnit(0, 1, 1, CargoPoint); f.AddUnit(1, 1, 0, CargoPoint); f.AddUnit(2, 1, 0, CargoPoint);
        f.Unit(0).Home = new(0); f.World.Entities.Cities.Storage[0].FoodBox = -1;
        food.Run(new(0));
        for (int i = 0; i < 3; ++i) Equal(false, f.Unit(i).Token.Occupied); // Real cargo loss removes the whole physical stack.
        f = FoodFixture(); effects = new(); food = Food(f, effects);
        f.AddCity(255, 1, new(12, 6), 3); f.AddUnit(0, 1, 0, new(10, 6)); f.Unit(0).Home = null;
        f.Rules.Units[0] = f.Rules.Units[0] with { Role = RuleRole.Settle };
        f.World.Entities.Cities.Storage[255].FoodBox = -1;
        food.Run(new(255)); Equal(false, f.Unit(0).Token.Occupied); Equal((sbyte)3, f.World.Entities.Cities.Storage[255].Size);
    }

    private static void FoodFamineRoutesAndIncomingWorkspace()
    {
        var f = FoodFixture(); var effects = new FoodEffects(); var food = Food(f, effects);
        f.AddCity(1, 2, new(14, 6)); f.World.Entities.Cities.Storage[1].Name = "Distant partner";
        ref var city = ref f.World.Entities.Cities.Storage[0]; city.FoodBox = -1;
        f.World.Calculation.FoodPartner = new(1); f.World.Calculation.FoodOut = 0;
        food.Run(new(0)); Equal((sbyte)2, city.Size);
        Equal((FamineCause.FoodRoute, (string?)"Distant partner"), effects.Famines.Single()); // Message override does not imply outgoing food.
        f.World.Calculation.FoodOut = 1; city.FoodBox = -1; city.RouteCount = 3;
        TradeRoutes.Write(f.World, new(0), 0, 1, -1); TradeRoutes.Write(f.World, new(0), 1, 0, -1);
        TradeRoutes.Write(f.World, new(0), 2, 1, -1); city.RouteGoods[2] = -128;
        food.Run(new(0)); Equal((byte)1, city.RouteCount); Equal((CitySlot?)new CitySlot(0), city.RoutePartners[0]);
        Equal((sbyte)2, city.Size); Equal(true, city.Flags.TradeDirty); Equal((short)0, city.FoodBox);
        city.FoodBox = -1; food.Run(new(0)); Equal((byte)1, city.RouteCount); Equal((sbyte)2, city.Size); // No matching-route fallback.
        city.RouteCount = 255; city.FoodBox = -1; food.Run(new(0)); Equal((byte)255, city.RouteCount); // Signed count scans nothing.
        f.World.Calculation.FoodPartner = new(65534); city.FoodBox = -1;
        food.Run(new(0)); Equal(FamineCause.Population, effects.Famines.Last().Cause); // Signed negative partner has no city read.
    }

    private static void FoodCityRemovalAndContinuation()
    {
        var f = FoodFixture(); var effects = new FoodEffects(); List<CityTarget> removed = [];
        var food = Food(f, effects, removed);
        ref var city = ref f.World.Entities.Cities.Storage[0]; city.Size = 1; city.FoodBox = -7;
        f.AddCity(1, 2, new(14, 6)); f.World.Entities.Cities.Storage[1].RouteCount = 1;
        TradeRoutes.Write(f.World, new(1), 0, 0, -1); f.World.Globals.WonderCity[0] = 0;
        f.AddUnit(0, 1, 0, new(10, 6)); f.Unit(0).Home = new(0);
        effects.OnElimination = () =>
        {
            Equal(false, f.World.Entities.Cities.Storage[0].Token.Occupied);
            Equal(false, f.Unit(0).Token.Occupied); Equal((byte)0, f.World.Entities.Cities.Storage[1].RouteCount);
            Equal((short)-2, f.World.Globals.WonderCity[0]); Equal((short)0, f.World.Civilizations[1].CityCount);
        };
        Equal(true, food.Run(new(0))); Equal((1, -1), effects.Eliminations.Single());
        Equal((short)-7, city.FoodBox); Equal((sbyte)0, city.Size); Equal(0, effects.Redraws); Equal(1, removed.Count);
        // The owner continuation is a boundary observation here, not a claimed implementation of V01.
    }

    private static void FoodAccumulationAndWarnings()
    {
        var f = FoodFixture(); var effects = new FoodEffects(); var food = Food(f, effects);
        var work = f.World.Calculation; ref var city = ref f.World.Entities.Cities.Storage[0];
        city.FoodBox = 5; work.Food = 7; work.SupportedSettlers = 2; work.SettlerFood = 2;
        Equal(-3, food.Accumulate(new(0), 5)); Equal((short)2, city.FoodBox); Equal(1, effects.Warnings);
        city.FoodBox = 0; food.Accumulate(new(0), 0); Equal((short)-3, city.FoodBox); Equal(1, effects.Warnings);
        Equal((sbyte)3, city.Size); Equal(0, effects.Famines.Count); // Negative accumulation is next turn's famine.
        city.FoodBox = 5; f.World.Globals.MessageOptions.FoodLow = true; food.Accumulate(new(0), 5);
        Equal(1, effects.Warnings); f.World.Globals.MessageOptions.FoodLow = false;
        city.FoodBox = 5; f.World.Multiplayer.Saved.AutoplayActive = true; food.Accumulate(new(0), 5); Equal(1, effects.Warnings);
        f.World.Multiplayer.Saved.AutoplayActive = false; work.SupportedSettlers = 0; work.Food = 16; city.FoodBox = 39;
        Equal(10, food.Accumulate(new(0), 39)); Equal((short)49, city.FoodBox); Equal((sbyte)3, city.Size); // No growth yet.
        food.Run(new(0)); Equal((sbyte)4, city.Size); Equal((short)0, city.FoodBox);
        city.Size = 3; city.FoodBox = 32767; work.Food = 7;
        Equal(1, food.Accumulate(new(0), 32767)); Equal(short.MinValue, city.FoodBox); Equal(2, effects.Warnings);
        work.Food = int.MaxValue; work.SupportedSettlers = int.MaxValue; work.SettlerFood = 2; city.FoodBox = 1;
        Equal(2147483643, food.Accumulate(new(0), 1)); Equal((short)-4, city.FoodBox); Equal(2, effects.Warnings); // Warning expression wraps positive.
    }

    private static void FoodRecalculationAndFailureBoundaries()
    {
        var f = FoodFixture(); var effects = new FoodEffects(); var food = Food(f, effects);
        ref var city = ref f.World.Entities.Cities.Storage[0]; city.FoodBox = 40;
        f.AddUnit(0, 1, 0, new(10, 6)); f.Unit(0).Home = new(0);
        f.Rules.Units[0] = f.Rules.Units[0] with { Role = RuleRole.Settle };
        food.Run(new(0)); Equal((sbyte)4, city.Size);
        new CityEconomy(f.World, f.Process.Rules, f.Stacks).Calculate(new CitySlot(0));
        Equal(1, f.World.Calculation.SupportedSettlers); Equal(1, f.World.Calculation.SettlerFood);
        int collected = f.World.Calculation.Food;
        food.Accumulate(new(0), 40); Equal((short)(collected - 9), city.FoodBox);
        f.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => food.Run(new(0)));
        Throws<InvalidOperationException>(() => food.Accumulate(new(0), 40));
        f.World.Globals.Multiplayer = GameMode.SinglePlayer;
        city.Size = 3; city.FoodBox = 40; f.World.Globals.WonderCity[0] = 1000;
        Throws<ArgumentOutOfRangeException>(() => food.Run(new(0)));
        Equal((sbyte)4, city.Size); Equal((short)40, city.FoodBox); // Actual C08 invalid record read after population increment.
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Granary, true); city.FoodBox = 50;
        food.Run(new(0)); Equal((sbyte)5, city.Size); Equal((short)30, city.FoodBox); // Granary bypasses the wonder query.
    }
}
