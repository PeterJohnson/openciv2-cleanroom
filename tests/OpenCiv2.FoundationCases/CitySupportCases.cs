using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void CitySupportBoxFactors()
    {
        var f = WorkingFixture(); var support = new CitySupport(f.World, f.Rules); var w = f.World.Calculation;
        int[] expected = [15, 13, 12, 10, 9, 8];
        for (int i = 0; i < 6; ++i)
        { f.World.Globals.Difficulty = (Difficulty)i; Equal(expected[i], support.AiBoxFactor(true, 10)); }
        f.World.Globals.Difficulty = Difficulty.King;
        f.World.Globals.CivByRank[7] = 1;
        f.World.Globals.Turn = 200;
        Equal(10, support.AiBoxFactor(true, 10)); Equal(8, support.AiBoxFactor(false, 10));
        f.World.Globals.Turn = 201;
        Equal(8, support.AiBoxFactor(true, 10));
        f.World.Globals.HumanPlayers = CivilizationSet.Only(7);
        f.World.Globals.CivByRank[7] = 7;
        f.World.Globals.WonderCity[WonderIds.Manhattan] = BuildingQueries.Destroyed;
        f.World.Globals.AdvanceKnownBy[AdvanceIds.Rocketry] = CivilizationSet.Only(7);
        f.World.Globals.AdvanceKnownBy[AdvanceIds.NuclearFission] = CivilizationSet.Only(7);
        Equal(9, support.AiBoxFactor(true, 10)); // Human civilization 7 satisfies the special nonhuman test.
        Equal(10, support.AiBoxFactor(true, 11)); Equal(4, support.AiBoxFactor(true, 5));
        f.World.Globals.WonderCity[WonderIds.Apollo] = BuildingQueries.Destroyed;
        f.World.Globals.AdvanceKnownBy[AdvanceIds.SpaceFlight] = CivilizationSet.Only(2);
        Equal(10, support.AiBoxFactor(true, 10)); // p=1, no human Space Flight -> a=-1, clamped to0.
        f.World.Globals.CivByRank[7] = 33; // Low-five selector aliases human 1.
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
        f.World.Globals.AdvanceKnownBy[AdvanceIds.SpaceFlight] = default;
        f.World.Globals.AdvanceKnownBy[AdvanceIds.Rocketry] = default;
        Equal(8, support.AiBoxFactor(false, 10));
        f.Rules.Cosmic = f.Rules.Cosmic with { FoodBoxRows = 5, SettlerFoodLowGovernment = 1, SettlerFoodHighGovernment = 3 };
        support.FoodFactor(1); Equal(5, w.FoodRows); Equal(1, w.SettlerFood); // Human factors stay odd.
        f.World.Civilizations[1].Government = Government.Communism;
        support.FoodFactor(1); Equal(3, w.SettlerFood);
        f.World.Globals.Difficulty = (Difficulty)255;
        Equal(-244, support.AiBoxFactor(true, 10)); Equal(0, support.AiBoxFactor(true, 0)); // Late-game subtraction still applies.
    }

    private static void CitySupportChargeAndDomains()
    {
        var f = WorkingFixture(); var support = new CitySupport(f.World, f.Rules); var w = f.World.Calculation;
        f.Rules.Cosmic = f.Rules.Cosmic with { FundamentalismFreeSupport = 1, MonarchyFreeSupport = 2, CommunismFreeSupport = 3 };
        f.Rules.Units[4] = new() { Role = RuleRole.Attack, Abilities = new() { FundamentalismFreeSupport = true } };
        f.AddUnit(0, 1, 4, new(8, 6)); f.AddUnit(1, 1, 0, new(8, 6));
        Equal(0, support.Charge(new(0), new(0), Government.Fundamentalism));
        Equal(1, support.Charge(new(0), new(1), Government.Fundamentalism)); // Free unit used the allowance position.
        w.SupportedUnits = 1; w.ShieldSupport = -1;
        Equal(0, support.Charge(new(0), new(1), Government.Republic)); // Charged accumulated result is zero.
        Equal(2, w.SupportedUnits);
        w.SupportedUnits = int.MaxValue;
        Equal(0, support.Charge(new(0), new(1), Government.Despotism)); Equal(int.MinValue, w.SupportedUnits);
        f.AddCity(255, 1, new(12, 6), 1);
        f.Unit(0).Home = null; f.Unit(1).Home = null;
        f.Unit(0).Flags.CostsShield = f.Unit(0).Flags.Aggressive = true;
        f.Rules.Units[4] = f.Rules.Units[4] with { Role = RuleRole.Trade };
        f.Rules.Units[0] = f.Rules.Units[0] with { Role = (RuleRole)255 };
        f.World.Civilizations[1].Government = Government.Republic;
        support.Calculate(new(255));
        Equal(1, w.SupportedUnits); Equal(1, w.ShieldSupport);
        Equal(false, f.Unit(0).Flags.CostsShield || f.Unit(0).Flags.Aggressive); // Clear before excluded role.
        Equal(true, f.Unit(1).Flags.CostsShield); // Raw absent home255 matched full city255, negative role passed.
        f.Unit(1).Home = new(0); f.Unit(1).Flags.Aggressive = true;
        support.Calculate(new(255));
        Equal(true, f.Unit(1).Flags.CostsShield && f.Unit(1).Flags.Aggressive); // Nonmatching home untouched.
    }

    private static void CitySupportMilitaryMembership()
    {
        var f = WorkingFixture(); var support = new CitySupport(f.World, f.Rules); var w = f.World.Calculation;
        f.Rules.Units[4] = new() { Domain = RuleDomain.Air, Role = RuleRole.Attack, Attack = 1 };
        f.Rules.Units[5] = f.Rules.Units[4] with { Role = RuleRole.AirSuperiority };
        f.Rules.Units[6] = new() { Domain = RuleDomain.Land, Role = RuleRole.Settle };
        f.AddUnit(0, 1, 4, new(8, 2)); // Aircraft shares only x: aggressive and home attack.
        f.AddUnit(1, 1, 5, new(12, 6)); // Fighter shares only y: home attack only.
        f.AddUnit(2, 1, 0, new(8, 6));
        f.AddUnit(3, 1, 0, new(10, 6));
        f.World.Map.TileAt(new(10, 6)).Features |= MapFeatures.Fortification; // Near own city suppresses aggression.
        f.AddUnit(4, 1, 0, new(12, 8));
        f.World.Map.TileAt(new(12, 8)).Features |= MapFeatures.Fortification | MapFeatures.CitySite; // Airbase does not suppress.
        f.AddCity(1, 2, new(16, 8));
        f.AddUnit(5, 1, 0, new(16, 8)); // Any city owner suppresses elsewhere aggression.
        f.AddUnit(6, 1, 6, new(8, 6));
        for (int i = 0; i < 7; ++i) f.Unit(i).Home = new(0);
        f.Rules.Cosmic = f.Rules.Cosmic with { MonarchyFreeSupport = 2, FoodBoxRows = 10, ShieldBoxRows = 10 };
        support.Calculate(new(0));
        Equal(7, w.SupportedUnits); Equal(5, w.ShieldSupport); Equal(1, w.SupportedSettlers);
        Equal(2, w.AggressiveUnits); Equal(3, w.HomeAttackUnits);
        Sequence(new[] { true, false, false, false, true, false, false }, Enumerable.Range(0, 7).Select(i => f.Unit(i).Flags.Aggressive));
        Equal(10, w.FoodRows); Equal(10, w.ShieldRows);
    }

    private static void CityGrowthFacilityOrder()
    {
        var f = WorkingFixture(); var support = new CitySupport(f.World, f.Rules);
        f.Rules.Cosmic = f.Rules.Cosmic with { AqueductSize = 6, SewerSize = 4 };
        f.World.Entities.Cities.Storage[0].Size = 6;
        Equal((int?)BuildingIds.Aqueduct, support.GrowthFacilityNeeded(new(0)));
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Aqueduct, true);
        Equal((int?)BuildingIds.SewerSystem, support.GrowthFacilityNeeded(new(0)));
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.SewerSystem, true);
        Equal((int?)null, support.GrowthFacilityNeeded(new(0)));
        f.World.Entities.Cities.Storage[0].Size = -128;
        Equal((int?)null, support.GrowthFacilityNeeded(new(0)));
    }
}
