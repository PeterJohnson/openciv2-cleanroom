using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // A02 uses cached C03 values, retained A01 census and actual children. These authored inputs
    // are contract cases, not captures of original-game output.
    private sealed class AiProductionFixture
    {
        public RevelationFixture Data { get; } = new();
        public GameState World => Data.World;
        public RuleTables Rules => Data.Rules;
        public ref City City => ref World.Entities.Cities.Storage[0];
        public CityPreferences Preferences { get; } = new();
        public GameplayRandom Random { get; }
        public CityProductionChoice Policy { get; }
        public AiProductionAirlift Airlift { get; }
        public List<AiAirliftNotice> Notices { get; } = [];
        public AiProductionFixture(params Choice[] choices)
        {
            Data.AddCity(0, 1, new(10, 10), 5);
            City.Production = 99;
            World.Globals.Difficulty = Difficulty.Prince; World.Globals.TurnCiv = 1;
            World.Globals.BestHumanRank = 2; World.Globals.WorstHumanRank = 2;
            World.Civilizations[1].Government = Government.Monarchy;
            World.Civilizations[1].CityCount = 1;
            World.Civilizations[1].ContinentPolicy[1] = 4;
            World.Civilizations[1].ContinentCities[1] = 1;
            foreach (ref var tile in World.Map.Tiles) tile.Region = 1;
            Rules.Cosmic = Rules.Cosmic with { FoodPerCitizen = 2, ShieldBoxRows = 10, RoadMovementMultiplier = 3 };
            World.Calculation.Food = 12; World.Calculation.Shields = 8; World.Calculation.ShieldRows = 10;
            World.Calculation.SettlerFood = 1; World.Mood.Happy = 1;
            Random = new(new ControlledChoices(new(choices)), "ai-production");
            var deletion = new UnitDeletion(World, Rules, new(5_000_000, 64), Data);
            Airlift = new(World, Rules, Data.Stacks, deletion, Random, Notices.Add);
            Policy = new(World, Data.Process.Rules, Preferences, new(World, Data.Process.Rules, Data.Stacks), Data.Stacks,
                new(World, Rules, ClassicShip, ClassicCalendar), Airlift);
        }
        public int Choose(ProductionRecommendation? unit = null, ProductionRecommendation? building = null) => Policy.Choose(new(0), "choice", unit, building);
        public void Building(int item) => Rules.Improvements[item] = new() { Prerequisite = AdvanceReference.None, CostRows = 4 };
        public void UnitRule(int type, RuleRole role, int cost = 2) => Rules.Units[type] = new()
        {
            Role = role,
            Domain = RuleDomain.Land,
            Attack = 1,
            Defense = 1,
            Movement = 3,
            HitPoints = 10,
            Firepower = 1,
            CostRows = (byte)cost,
            Prerequisite = AdvanceReference.None
        };
        public void Ai() => World.Globals.HumanPlayers = CivilizationSet.Only(2);
        public void Garrison(int count = 2)
        {
            Rules.Units[60] = new() { Role = RuleRole.Defend, Prerequisite = AdvanceReference.Never };
            for (int i = 0; i < count; ++i) Data.AddUnit(i, 1, 60, City.Position);
        }
    }

    private static void AiTerrainRecommendationGates()
    {
        var f = new AiProductionFixture(); var p = new MapPoint(12, 10);
        f.Rules.Terrain[(int)Terrain.Grassland] = new() { Work = new(new(TerrainResult.ImproveInPlace, AiGovernmentThreshold: 2), new(TerrainResult.ImproveInPlace, AiGovernmentThreshold: 1)) };
        Equal(0, TerrainImprovementChoice.Choose(f.World, f.Rules, 1, p)); // Mining still requires water.
        f.World.Map.TileAt(p).River = true;
        Equal(1, TerrainImprovementChoice.Choose(f.World, f.Rules, 1, p));
        f.World.Civilizations[1].Government = Government.Despotism;
        Equal(2, TerrainImprovementChoice.Choose(f.World, f.Rules, 1, p));
        f.World.Map.TileAt(p).Features = MapFeatures.Irrigation;
        Equal(0, TerrainImprovementChoice.Choose(f.World, f.Rules, 1, p));
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.Refrigeration);
        Equal(0, TerrainImprovementChoice.Choose(f.World, f.Rules, 1, p)); // No mining existing irrigation.
        f.Rules.Terrain[2] = f.Rules.Terrain[2] with { Work = new(new(TerrainResult.ImproveInPlace, AiGovernmentThreshold: 255), new()) };
        Equal(1, TerrainImprovementChoice.Choose(f.World, f.Rules, 1, p));
        f.World.Map.TileAt(p).Features |= MapFeatures.Mine;
        Equal(0, TerrainImprovementChoice.Choose(f.World, f.Rules, 1, p));
        f.World.Map.TileAt(p).Features = MapFeatures.CitySite; f.World.Map.TileAt(p).Owner = 2;
        Equal(0, TerrainImprovementChoice.Choose(f.World, f.Rules, 1, p));
        f.Random.RequireChoicesConsumed();
    }

    private static void AiSpatialControlMediumAndCity()
    {
        var f = new AiProductionFixture(); var p = new MapPoint(11, 9);
        f.Data.AddUnit(0, 2, 2, p); // Air domain, zero attack: only cached owner matters.
        Equal((int?)2, SpatialControl.AdjacentNonalliedUnit(f.World, f.City.Position, 1, true));
        Equal((int?)null, SpatialControl.InEnemyZone(f.World, f.City.Position, 1));
        f.World.Map.TileAt(p).Terrain = Terrain.Ocean;
        Equal((int?)null, SpatialControl.AdjacentNonalliedUnit(f.World, f.City.Position, 1, true));
        Equal((int?)2, SpatialControl.AdjacentNonalliedUnit(f.World, f.City.Position, 1));
        f.World.Civilizations[1].Treaties[2].Peace = true;
        Equal((int?)2, SpatialControl.AdjacentNonalliedUnit(f.World, f.City.Position, 1));
        f.World.Civilizations[1].Treaties[2].Alliance = true;
        Equal((int?)null, SpatialControl.AdjacentNonalliedUnit(f.World, f.City.Position, 1));
    }

    private static Choice AiFlightChoice(string call, int fighter, bool veteran, uint code) =>
        Choice.Create(new("U05.ai-airlift", call, fighter), new SamplingLaw.SecondaryRange(0, veteran ? 3 : 5), ChoiceCode.Secondary(code));

    private static void AiAirliftInterceptionAndOrders()
    {
        var f = new AiProductionFixture(AiFlightChoice("flight", 1, true, uint.MaxValue), AiFlightChoice("flight", 2, false, 0));
        f.Ai(); f.Data.AddCity(1, 1, new(24, 10));
        f.Data.AddUnit(0, 1, 0, new(24, 10)); f.Data.Unit(0).Orders = UnitOrder.Fortified; f.Data.Unit(0).Home = new(0);
        f.Rules.Units[27] = new() { Role = RuleRole.AirSuperiority, Domain = RuleDomain.Air, Movement = 3 };
        f.Data.AddUnit(1, 2, 27, new(24, 10)); f.Data.Unit(1).Flags.Veteran = true;
        f.Data.AddUnit(2, 2, 27, new(10, 10)); f.World.Civilizations[2].Treaties[1].War = true;
        f.World.Civilizations[1].MilitaryCount = 1; f.World.Civilizations[1].UnitsActive[0] = 1;
        Equal((UnitSlot?)null, f.Airlift.Transfer(new(1), new(0), new(0), 1, "flight"));
        Equal(true, f.City.Flags.Airlifted); Equal(true, f.World.Entities.Cities.Storage[1].Flags.Airlifted);
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal((short)0, f.World.Civilizations[1].MilitaryCount);
        Equal(true, f.Notices.Single().ShotDown); Equal(new MapPoint(10, 10), f.Data.Removed.Single());
        f.Random.RequireChoicesConsumed();

        f = new(AiFlightChoice("safe", 1, false, uint.MaxValue)); f.Ai(); f.Data.AddCity(1, 1, new(24, 10));
        f.Data.AddUnit(0, 1, 0, new(24, 10)); f.Data.Unit(0).Orders = UnitOrder.Fortified;
        f.Rules.Units[27] = new() { Role = RuleRole.AirSuperiority, Domain = RuleDomain.Air, Movement = 3 };
        f.Data.AddUnit(1, 2, 27, new(24, 10)); f.World.Civilizations[2].Treaties[1].War = true;
        Equal((UnitSlot?)new UnitSlot(0), f.Airlift.Transfer(new(1), new(0), new(0), 1, "safe"));
        Equal(UnitOrder.Fortified, f.Data.Unit(0).Orders); Equal((byte)6, f.Data.Unit(0).MovesUsed);
        Equal(new MapPoint(10, 10), f.Data.Unit(0).Position); Equal(false, f.Notices.Single().ShotDown); f.Random.RequireChoicesConsumed();
    }

    private static void AiAirliftDirectionAndFailurePrefix()
    {
        var f = new AiProductionFixture(); f.Ai(); f.Data.AddCity(1, 1, new(24, 10)); f.Data.AddUnit(0, 1, 0, new(24, 10));
        f.Rules.Units[27] = new() { Role = RuleRole.AirSuperiority, Domain = RuleDomain.Air, Movement = 3 };
        f.Data.AddUnit(1, 2, 27, f.City.Position); f.World.Civilizations[1].Treaties[2].War = true;
        f.Airlift.Transfer(new(1), new(0), new(0), 1, "none"); Equal(0, f.Notices.Count);
        f.World.Civilizations[2].Treaties[1].War = true; f.Rules.Cosmic = f.Rules.Cosmic with { RoadMovementMultiplier = 0 };
        Throws<DivideByZeroException>(() => f.Airlift.Transfer(new(1), new(0), new(0), 1, "fault"));
        Equal(true, f.City.Flags.Airlifted); Equal(true, f.Data.Unit(0).Token.Occupied); Equal(f.City.Position, f.Data.Unit(0).Position);
        Equal(0, f.Notices.Count); f.Random.RequireChoicesConsumed();
    }

    private static void AiProductionEarlyAndBarbarianChoices()
    {
        var f = new AiProductionFixture(); f.Ai(); f.World.Civilizations[1].Government = Government.Despotism;
        f.UnitRule(3, RuleRole.Defend); f.UnitRule(61, RuleRole.Defend, 80);
        Equal(61, f.Choose()); f.City.Flags.Disorder = true; var unit = new ProductionRecommendation(); var building = new ProductionRecommendation();
        Equal(3, f.Choose(unit, building)); // Advice suppresses the reverse-order shortcut.
        Equal(3, unit.Value); Equal(999, building.Value);
        f.Rules.Units[61] = f.Rules.Units[61] with { Abilities = new() { Paradrop = true } };
        Equal(3, f.Choose());
        f.City.Owner = new(0); f.City.Production = -4;
        Equal(UnitTypeIds.Legion, f.Choose(unit, building)); Equal(999, unit.Value); Equal(999, building.Value);
        f.World.Globals.AdvanceKnownBy[AdvanceIds.Explosives] = CivilizationSet.Only(0);
        Equal(UnitTypeIds.Dragoons, f.Choose(unit, building));
        f.Random.RequireChoicesConsumed();
    }

    private static void AiProductionImprovementTiesAndUnrest()
    {
        var f = new AiProductionFixture(); f.Garrison(); f.Building(5); f.Building(6);
        var unit = new ProductionRecommendation(); var building = new ProductionRecommendation();
        Equal(-5, f.Choose(unit, building)); Equal(-5, building.Value); Equal(999, unit.Value);
        f.Building(4); f.City.Flags.Disorder = true;
        Equal(-4, f.Choose(unit, building)); Equal(-4, building.Value);
        f.City.Flags.Autobuild = true; f.City.Flags.UnitsOnlyAutomation = true; f.City.Flags.ImprovementsOnlyAutomation = true;
        Equal(99, f.Choose(unit, building)); Equal(-4, building.Value);
        f.City.Flags.UnitsOnlyAutomation = false; Equal(-4, f.Choose());
    }

    private static void AiProductionPreferencesAndCutoff()
    {
        var f = new AiProductionFixture(); f.Garrison(); f.Building(3); f.Building(5);
        f.Preferences.Count = 1; f.Preferences.BuildOrder[0] = 3; f.City.Flags.Autobuild = true;
        Equal(-3, f.Choose()); // Preference changes IDs while retaining Marketplace's score.
        f = new(); f.Garrison(); f.Building(2); f.City.Flags.Autobuild = true;
        f.Data.Process.Rules.Leaders[0].Attack = -46;
        Equal(-2, f.Choose()); // Lost-city threshold raises T; 50 *10 =500 is accepted.
        f.Data.Process.Rules.Leaders[0].Attack = -47;
        var building = new ProductionRecommendation(); Equal(99, f.Choose(building: building)); Equal(-2, building.Value);
    }

    private static void AiProductionRetainedCityAndFailure()
    {
        var f = new AiProductionFixture(); f.Ai(); f.Garrison(); f.Building(1); f.Building(5);
        f.World.Calculation.Trade = 20; f.World.Entities.Cities.Storage[1].Size = 15;
        Equal(-1, f.Choose());
        f.World.Entities.Cities.Storage[1].Size = 0;
        Equal(-5, f.Choose()); // Retained zero size gives Palace 20, behind Marketplace 10.
        f.World.Entities.Cities.SetEnd(EntityLimits.Cities);
        f.City.Improvements = f.City.Improvements.With(3); f.World.Globals.WonderCity[0] = 0; f.Building(3);
        var unit = new ProductionRecommendation { Value = 1 }; var building = new ProductionRecommendation { Value = 2 };
        Throws<IndexOutOfRangeException>(() => f.Choose(unit, building));
        Equal(999, unit.Value); Equal(999, building.Value); Equal(false, f.City.Improvements.Contains(3));
        Equal(40, f.World.Civilizations[1].Gold); // Sale precedes rejected retained-size read.
    }

    private static void AiProductionFreeIrrigationVisibility()
    {
        var f = new AiProductionFixture(); f.Ai(); f.World.Calculation.Food = 10;
        f.Rules.Terrain[2] = new() { Work = new(new(TerrainResult.ImproveInPlace, AiGovernmentThreshold: 1), new()) };
        foreach (ref var tile in f.World.Map.Tiles) tile.River = true;
        var chosen = new MapPoint(9, 7); f.World.Map.TileAt(chosen).River = false; // B[19].
        f.World.Map.TileAt(chosen).RevealedTo = CivilizationSet.Only(2);
        f.Choose(); Equal(false, f.World.Map.TileAt(chosen).Features.HasFlag(MapFeatures.Irrigation));
        Equal(0, f.World.Map.Tiles.ToArray().Count(t => t.Features.HasFlag(MapFeatures.Irrigation)));
        f.World.Map.TileAt(chosen).RevealedTo = default;
        f.Choose(); Equal(true, f.World.Map.TileAt(chosen).Features.HasFlag(MapFeatures.Irrigation | MapFeatures.Road));
        Equal(10, f.World.Calculation.Food); // Only A02's local surplus increments.
        f.Random.RequireChoicesConsumed();
    }

    private static void AiProductionSalesAndSpecialRecommendations()
    {
        var f = new AiProductionFixture(); f.Ai(); f.Garrison();
        foreach (var (w, b) in new[] { (0, 3), (26, 26), (21, 33), (10, 11) })
        { f.World.Globals.WonderCity[w] = 0; f.Building(b); f.City.Improvements = f.City.Improvements.With(b); }
        f.Choose(); Equal(160, f.World.Civilizations[1].Gold); Equal(false, f.City.Flags.ImprovementSold);
        f = new(); f.Garrison(); f.UnitRule(48, RuleRole.Trade); f.World.Calculation.Trade = 20;
        var build = new ProductionRecommendation(); var unit = new ProductionRecommendation();
        Equal(48, f.Choose(unit, build)); Equal(48, build.Value); Equal(999, unit.Value);
        f.City.Flags.Autobuild = true; Equal(99, f.Choose(unit, build)); Equal(999, build.Value);
    }

    private static void AiProductionDonorTerminationAndStaleNeed()
    {
        var f = new AiProductionFixture(); f.Ai(); f.World.Civilizations[1].ContinentPolicy[1] = 0;
        f.City.Improvements = f.City.Improvements.With(32);
        f.Data.AddCity(1, 1, new(20, 10)); f.World.Entities.Cities.Storage[1].Improvements = default(ImprovementSet).With(32);
        f.Data.AddCity(2, 1, new(30, 10)); f.World.Entities.Cities.Storage[2].Improvements = default(ImprovementSet).With(32);
        f.Rules.Units[60] = new() { Role = RuleRole.Defend };
        for (int i = 0; i < 3; ++i) f.Data.AddUnit(i, 1, 60, new(20, 10));
        for (int i = 3; i < 6; ++i) f.Data.AddUnit(i, 1, 0, new(30, 10));
        f.UnitRule(3, RuleRole.Defend); f.Choose();
        Equal(false, f.City.Flags.Airlifted); Equal(new MapPoint(30, 10), f.Data.Unit(5).Position);
        f.Rules.Units[60] = f.Rules.Units[60] with { Role = RuleRole.Attack };
        Equal(3, f.Choose()); Equal(true, f.City.Flags.Airlifted); // Captured undefended need still chooses defense.
        Equal(1, f.Data.Stacks.Count(f.Data.Stacks.FirstAt(f.City.Position)));
        f.Random.RequireChoicesConsumed();
    }

    private static void AiProductionWonderSubstitutionAndCapacity()
    {
        var f = new AiProductionFixture(); f.Ai(); f.Garrison(); f.Building(39);
        f.World.Civilizations[1].CityCount = 4; f.World.Civilizations[1].ContinentCities[1] = 4;
        var build = new ProductionRecommendation(); Equal(-4, f.Choose(building: build)); Equal(-39, build.Value);
        f.City.Improvements = f.City.Improvements.With(4); Equal(-39, f.Choose());
        f.City.Production = -1; Equal(-1, f.Choose(building: build)); Equal(-39, build.Value);
        f = new(); f.World.Civilizations[1].MilitaryCount = 512;
        Equal(-38, f.Choose(building: build)); Equal(999, build.Value);
        f.City.Flags.Autobuild = true; Equal(99, f.Choose());
    }

    private static void AiProductionDiplomatTargetsAndRepair()
    {
        var f = new AiProductionFixture(); f.Ai(); f.Garrison(); f.Building(6); f.World.Calculation.Trade = 12;
        f.UnitRule(47, RuleRole.Diplomacy); f.World.Civilizations[1].ContinentCities[1] = 3;
        f.World.Civilizations[2].ContinentCities[1] = 1; f.World.Civilizations[2].NumAdvances = 8;
        f.World.Civilizations[3].ContinentCities[1] = 1;
        Equal(-6, f.Choose()); // Last qualifying target 3 gives score 100, Library gives 60.
        f.World.Civilizations[3].ContinentCities[1] = 0;
        Equal(47, f.Choose()); // Target 2 gives score 20.
        f.Data.AddUnit(2, 1, 47, new(16, 10)); f.World.Civilizations[1].Attitude[0] = 255;
        Equal(47, f.Choose()); // No fabricated ninth treaty and no Homeless exclusion.
        f.World.Civilizations[1].Treaties[2].Peace = true;
        f.World.Civilizations[2].Government = Government.Democracy;
        f.World.Civilizations[2].ContinentAttack[1] = 50;
        Equal(-6, f.Choose()); // Peace target now fails the real military/government gate.
    }

    private static void AiProductionSettlerRepairAndSignedRoles()
    {
        var f = new AiProductionFixture(); f.Ai(); f.Garrison(); f.UnitRule(0, RuleRole.Settle);
        f.Rules.Units[0] = f.Rules.Units[0] with { Attack = 0, Defense = 0 };
        f.World.Calculation.Food = 9; f.World.Civilizations[1].UnitsActive[0] = 1;
        f.Data.AddUnit(2, 1, 0, new(14, 10)); f.Data.Unit(2).Flags.Colonist = true;
        Equal(999, f.Choose()); // Invalid exhausted rule does not reclassify colonists as Flagged.
        f.World.Calculation.Food = 12; Equal(0, f.Choose());
        f.Rules.Units[0] = f.Rules.Units[0] with { Prerequisite = AdvanceReference.Never };
        f.UnitRule(50, unchecked((RuleRole)255));
        Equal(50, f.Choose()); // Signed role -1 belongs to the ordinary loop.
        f.Rules.Cosmic = f.Rules.Cosmic with { RoadMovementMultiplier = 0 };
        var build = new ProductionRecommendation { Value = 3 };
        Throws<DivideByZeroException>(() => f.Choose(building: build)); Equal(999, build.Value);
    }

    private static void AiProductionNavalBordersAndThreat()
    {
        var f = new AiProductionFixture(); f.Ai(); f.Garrison(); f.UnitRule(37, RuleRole.NavalSuperiority);
        f.Rules.Units[37] = f.Rules.Units[37] with { Domain = RuleDomain.Sea };
        f.City.Flags.Coastal = f.City.Flags.OceanAccess = true;
        f.World.Civilizations[1].ContinentMilitary[1] = 8;
        f.World.Map.TileAt(new(11, 9)).Terrain = Terrain.Ocean; f.World.Map.TileAt(new(11, 9)).Region = 2;
        f.World.Map.Regions[2] = f.World.Map.Regions[2] with { Water = 47 };
        f.World.Map.Regions[0] = f.World.Map.Regions[0] with { NeighboringWater = default(RegionSet).With(2) };
        f.World.Map.Regions[63] = f.World.Map.Regions[63] with { NeighboringWater = default(RegionSet).With(2) };
        Equal(999, f.Choose()); // Border counts exclude 0 and 63.
        f.World.Map.Regions[2] = f.World.Map.Regions[2] with { Water = 48 };
        Equal(37, f.Choose());
        f.Rules.Units[37] = f.Rules.Units[37] with { Role = RuleRole.SeaTransport };
        Equal(999, f.Choose()); // A transport still needs two bordering land regions.
        for (int i = 1; i <= 2; ++i) f.World.Map.Regions[i] = f.World.Map.Regions[i] with { NeighboringWater = default(RegionSet).With(2) };
        Equal(37, f.Choose());
    }

    private static void AiProductionDefenseOverrideAndSam()
    {
        var f = new AiProductionFixture(); f.Building(4); f.UnitRule(3, RuleRole.Defend); f.City.Flags.MoodOnEdge = f.City.Flags.SpecialistHeld = true;
        var build = new ProductionRecommendation(); Equal(3, f.Choose(building: build)); Equal(-4, build.Value);
        f.City.Flags.Disorder = true; Equal(-4, f.Choose()); // Disorder prevents the forced override of a lower score.
        f = new(); f.Garrison(); f.Building(27); f.Building(4); f.City.Flags.SamScoringInput = true;
        f.World.Civilizations[2].UnitsActive[UnitTypeIds.Bomber] = 30;
        Equal(-27, f.Choose()); // Clamp to 1, then subtract 3; do not clamp the negative result.
    }

    private static void AiProductionWonderExpiryAndRivalryRepair()
    {
        var f = new AiProductionFixture(); f.Ai(); f.Garrison(); f.City.Improvements = default(ImprovementSet).With(4);
        f.World.Civilizations[1].CityCount = f.World.Civilizations[1].ContinentCities[1] = 4;
        f.Building(39); f.Building(6); f.World.Calculation.Trade = 0;
        Equal(-39, f.Choose()); // Pyramids raw 4 ->80, Library 100 (P=4 makes wonder defensive preference equal T).
        f.Rules.WonderObsolescence[0] = (AdvanceReference)1;
        f.World.Civilizations[7].Advances = default(AdvanceSet).With(1);
        Equal(-6, f.Choose()); // Dead seat's knowledge expires the candidate and adds the difficulty penalty.
        f.Rules.WonderObsolescence[0] = AdvanceReference.Never;
        foreach (int production in new[] { 10, 18, 22, -14 })
        { f.City.Production = (sbyte)production; Equal(-39, f.Choose()); } // No adjacent frame inputs.
        f.City.Production = -39; f.City.ShieldBox = 70;
        f.Data.AddCity(1, 2, new(24, 10)); f.World.Entities.Cities.Storage[1].Production = -39;
        Equal(-39, f.Choose());
        f.Random.RequireChoicesConsumed();
    }

    private static void AiProductionInvalidSurveyAndArithmetic()
    {
        var f = new AiProductionFixture(); f.Ai(); f.Garrison(); f.Building(6);
        f.Data.AddCity(1, 1, new(24, 10)); f.World.Entities.Cities.Storage[1].Production = 99;
        f.World.Calculation.Shields = int.MaxValue;
        Equal(-6, f.Choose()); // Omit J[99]; doubled shield capture wraps.
        f.Data.Process.Rules.Leaders[0].Civilize = -3;
        var unit = new ProductionRecommendation { Value = 1 }; var build = new ProductionRecommendation { Value = 2 };
        Throws<DivideByZeroException>(() => f.Choose(unit, build)); Equal(999, unit.Value); Equal(999, build.Value);
        f.World.Entities.Cities.Storage[1].Production = -127;
        Throws<IndexOutOfRangeException>(() => f.Choose(unit, build)); // Exclude the rivalry write, then reject the unbacked cost.
    }

    private static void AiProductionWonderRawChoiceRetainsWeightedMinimum()
    {
        var f = new AiProductionFixture(); f.Ai(); f.Garrison(); f.Building(44); f.Building(53);
        f.City.Improvements = default(ImprovementSet).With(1).With(4);
        f.World.Globals.WonderCity[1] = 0;
        f.World.Civilizations[1].CityCount = 4; f.World.Civilizations[1].ContinentCities[1] = 4;
        f.World.Civilizations[1].WondersByGroup[2] = 1; f.World.HumanWonders.Minimum[2] = 1;
        f.World.Civilizations[2].WondersByGroup[2] = 2;
        f.UnitRule(3, RuleRole.Defend); f.Data.AddUnit(2, 1, 3, f.City.Position);
        var unit = new ProductionRecommendation(); var build = new ProductionRecommendation();
        // Oracle raw7/weighted23 precedes Leonardo raw6/weighted40. The later raw winner
        // retains score23, so the defense unit at score32 does not replace it.
        Equal(-53, f.Choose(unit, build)); Equal(-53, build.Value); Equal(3, unit.Value);
    }
}
