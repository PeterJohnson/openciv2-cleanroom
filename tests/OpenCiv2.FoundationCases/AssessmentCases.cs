using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // Specification-derived A01/A08/V08 cases, using actual production, government,
    // support, loss and elimination services. These are not original-game captures.
    private sealed class AssessmentFixture
    {
        public EliminationFixture Lifecycle { get; }
        public RevelationFixture Data => Lifecycle.Data;
        public GameState World => Data.World;
        public RuleTables Rules => Data.Rules;
        public ref Civilization Civ => ref World.Civilizations[1];
        public GameplayRandom Random => Lifecycle.Random;
        public GovernmentPolicy Policy { get; }
        public CivilizationAssessment Assessment { get; }
        public List<GovernmentChangeNotice> Governments { get; } = [];
        public List<ProductionWonderNotice> ProductionNotices { get; } = [];

        public AssessmentFixture(params Choice[] choices) : this(null, choices) { }
        public AssessmentFixture(RevelationFixture? data, params Choice[] choices)
        {
            Lifecycle = new(choices.ToList(), data);
            Civ.Government = Government.Despotism;
            Civ.ContinentPolicy[1] = 5;
            World.Globals.TurnCiv = 1;
            Rules.Cosmic = Rules.Cosmic with { FoodPerCitizen = 2, ShieldBoxRows = 10, ParadropRange = 4 };
            for (int t = 0; t < RuleLimits.UnitTypes; ++t)
                Rules.Units[t] = Rules.Units[t] with { Prerequisite = AdvanceReference.Never, ObsoleteBy = AdvanceReference.Never };
            foreach (ref var civ in World.Civilizations.AsSpan()) SharedGoals.ResetStanding(ref civ);
            var changes = new GovernmentChanges(World, Data.Process.Rules, Data.Stacks,
                _ => throw new InvalidOperationException("No human government choice in this fixture"),
                _ => throw new InvalidOperationException("No rate dialog in this fixture"), Governments.Add);
            Policy = new(World, Data.Process.Rules, changes, Random);
            var choice = new CityProductionChoice(World, Data.Process.Rules, new(),
                new(World, Data.Process.Rules, Data.Stacks), Data.Stacks, new(World, Rules, ClassicShip, ClassicCalendar),
                new(World, Rules, Data.Stacks, Lifecycle.Deletion, Random, _ => throw new InvalidOperationException("No airlift")));
            var orders = new ProductionOrders(World, Rules, city => choice.Choose(city, "assessment-production"), ProductionNotices.Add);
            Assessment = new(World, Data.Process.Rules, Data.Stacks, orders, Policy, Lifecycle.Loss, Lifecycle.Elimination, Random);
        }

        public void Ai() => World.Globals.HumanPlayers = CivilizationSet.Only(2);
        public void Run() { Assessment.Assess(1, "assess"); Random.RequireChoicesConsumed(); }
        public void Rule(int type, RuleRole role, int attack = 1, int defense = 1) => Rules.Units[type] = new()
        {
            Role = role,
            Attack = (byte)attack,
            Defense = (byte)defense,
            Domain = RuleDomain.Land,
            Movement = 6,
            HitPoints = 10,
            Firepower = 1,
            CostRows = 4,
            Prerequisite = AdvanceReference.None,
            ObsoleteBy = AdvanceReference.Never
        };
        public void Goal(int index, MapPoint point, int role, int priority) => Civ.Goals[index] = new(point, (sbyte)role, (sbyte)priority);
    }

    private static void AssessmentQueriesContract()
    {
        var f = new AssessmentFixture(); f.Rule(0, RuleRole.Settle); f.Data.AddUnit(0, 1, 0, new(10, 10));
        Equal(5, AssessmentQueries.Role(f.Rules, f.Data.Unit(0)));
        f.Data.Unit(0).Flags.Colonist = true; Equal(21, AssessmentQueries.Role(f.Rules, f.Data.Unit(0)));
        foreach (var (cities, expected) in new[] { (0, 0), (5, 15), (10, 25), (15, 30), (255, 30) })
        { f.Civ.ContinentCities[1] = (byte)cities; Equal(expected, AssessmentQueries.DefenseQuota(f.World, 1, 1)); }
        f.Data.AddCity(0, 1, new(10, 10));
        Equal(true, AssessmentQueries.CityTouchesRegion(f.World, new(0), 1));
        f.World.Map.Regions[2] = new(10, 10, 0, default(RegionSet).With(4));
        var first = MapNeighborhoods.Offset(f.World.Map.Geometry, new(10, 10), MapNeighborhoods.Directions[0], true);
        var skipped = MapNeighborhoods.Offset(f.World.Map.Geometry, new(10, 10), MapNeighborhoods.Directions[1], true);
        f.World.Map.TileAt(first).Terrain = f.World.Map.TileAt(skipped).Terrain = Terrain.Ocean;
        f.World.Map.TileAt(first).Region = 3; f.World.Map.TileAt(skipped).Region = 4;
        Equal(false, AssessmentQueries.CityTouchesRegion(f.World, new(0), 2));
        f.World.Map.TileAt(first).Terrain = Terrain.Grassland;
        Equal(true, AssessmentQueries.CityTouchesRegion(f.World, new(0), 2));
    }

    private static void PowerRanksHistoryAndObjectives()
    {
        var f = new AssessmentFixture(); var world = f.World;
        world.Globals.CivsAlive = CivilizationSet.Only(1).With(2); world.Globals.Turn = 12;
        f.Civ.NumAdvances = 4; f.Civ.TotalCitySize = 2; f.Civ.Gold = 128; // 32, equal to civ 2.
        world.Civilizations[2].TotalCitySize = 4;
        f.Civ.UnitsActive[4] = 255; // Army strength is not added.
        new PowerRanking(world, f.Random).Publish("power");
        Equal((byte)7, world.Globals.PowerRank[1]); Equal((byte)6, world.Globals.PowerRank[2]);
        Equal((byte)5, world.Globals.PowerRank[3]); Equal((byte)0, world.Globals.CivByRank[5]);
        Equal((byte)7, world.Globals.BestHumanRank); Equal((byte)7, world.Globals.WorstHumanRank);
        Equal((byte)4, world.PowerHistory[3][1]); Equal((byte)0, world.PowerHistory[3][3]);
        f.Data.AddCity(0, 1, new(10, 10)); f.Data.AddCity(1, 2, new(16, 10));
        world.Entities.Cities.Storage[0].Flags.Objective = true;
        world.Globals.WonderCity[0] = world.Globals.WonderCity[27] = 0;
        world.Scenario.Flags.WonderObjectives = true;
        Equal(3, CityObjectives.Value(world, new(0))); // Independent of scenario/active-wonder state.
        world.Globals.Status.Scenario = world.Scenario.Flags.ObjectiveVictory = true;
        new PowerRanking(world, f.Random).Publish("objectives");
        Equal((byte)6, world.PowerHistory[6][1]); Equal((byte)0, world.PowerHistory[6][2]);
        world.Globals.Turn = 75; world.PowerHistory[37][1] = 88;
        new PowerRanking(world, f.Random).Publish("cutoff"); Equal((byte)88, world.PowerHistory[37][1]);
        world.Globals.Turn = -2; new PowerRanking(world, f.Random).Publish("negative-history");
        f.Random.RequireChoicesConsumed();
    }

    private static void PowerNegativeRanksAndCoordination()
    {
        var f = new AssessmentFixture(); var world = f.World;
        world.Globals.CivsAlive = CivilizationSet.Only(1); f.Civ.TotalCitySize = -2;
        world.Globals.PowerRank[1] = 255; world.Globals.HumanPlayers = CivilizationSet.Only(0);
        new PowerRanking(world, f.Random).Publish("negative");
        Equal((byte)1, world.Globals.PowerRank[0]); Equal((byte)255, world.Globals.PowerRank[1]);
        Equal((byte)7, world.Globals.BestHumanRank); Equal((byte)0, world.Globals.WorstHumanRank);
        var draw = Choice.Create(new("A01.coordination", "coordinate", 2), new SamplingLaw.PrimaryModulo(32), ChoiceCode.Primary(31));
        f = new(draw); world = f.World;
        world.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3).With(4);
        world.Globals.Turn = 201; world.Globals.Difficulty = Difficulty.Emperor;
        f.Civ.CityCount = 5; f.Civ.TotalCitySize = 100; f.Civ.Reputation = 10;
        for (int c = 2; c <= 4; ++c) world.Civilizations[c].CityCount = 2;
        world.Civilizations[2].Treaties[1].Peace = true; world.Civilizations[2].SpaceshipFlags.Launched = true;
        world.Civilizations[3].Treaties[1].Alliance = true; world.Civilizations[3].SpaceshipFlags.Launched = true;
        new PowerRanking(world, f.Random).Publish("coordinate");
        Equal(true, world.Globals.Status.AiCoordination);
        Equal(true, world.Civilizations[2].Treaties[1].HostileIntent); Equal(true, world.Civilizations[2].Treaties[1].Peace);
        Equal(true, world.Civilizations[3].Treaties[1].HostileIntent);
        Equal(true, world.Civilizations[4].Treaties[1].Contact);
        Equal(false, f.Civ.Treaties[4].Contact); Equal(false, world.Civilizations[2].Treaties[1].War);
        f.Random.RequireChoicesConsumed();
    }

    private static void GovernmentRatesAndScoreInputs()
    {
        var f = new AssessmentFixture(); f.Ai();
        f.Data.Process.Rules.Leaders[0].Civilize = -8; f.Data.Process.Rules.Leaders[0].Attack = -1;
        f.Civ.ScienceRate = f.Civ.TaxRate = 3;
        f.Policy.AfterCities(1, false, false); // L falls 4 -> 3; signed science -5 wraps before clamp.
        Equal((byte)6, f.Civ.ScienceRate); Equal((byte)1, f.Civ.TaxRate);
        f.Civ.Advances = default(AdvanceSet).With(AdvanceIds.FusionPower).With(AdvanceIds.Laser);
        foreach (int item in new[] { BuildingIds.Structural, BuildingIds.Component, BuildingIds.Module })
            f.Rules.Improvements[item] = f.Rules.Improvements[item] with { Prerequisite = AdvanceReference.None };
        f.Policy.AfterCities(1, true, true); Equal((byte)0, f.Civ.ScienceRate); Equal((byte)6, f.Civ.TaxRate);
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1); f.Civ.ScienceRate = 3; f.Civ.TaxRate = 4;
        f.World.Globals.BestHumanRank = 7; f.World.Globals.CivByRank[7] = 2;
        ref var target = ref f.World.Civilizations[7];
        target.SubversionCount = 2; target.UnitsActive[UnitTypeIds.Diplomat] = 3; target.UnitsActive[UnitTypeIds.Spy] = 1;
        target.Gold = 100; f.Civ.GovernmentScore[(int)Government.Democracy] = 5;
        f.World.Production.WonderNotices[1][0].Started = true; f.World.Globals.WonderCity[0] = 22;
        f.Policy.AfterCities(1, false, true);
        Equal((short)27, f.Civ.GovernmentScore[(int)Government.Democracy]);
        Equal((byte)3, f.Civ.ScienceRate); Equal((byte)4, f.Civ.TaxRate);
        Equal(false, f.World.Production.WonderNotices[1][0].Started); Equal((short)22, f.World.Globals.WonderCity[0]);
    }

    private static void GovernmentChoiceTransitionsAndDraws()
    {
        var f = new AssessmentFixture(); f.Ai();
        f.Civ.Advances = default(AdvanceSet).With(AdvanceIds.Monarchy).With(AdvanceIds.Republic).With(AdvanceIds.Democracy);
        f.World.Civilizations[2].Treaties[1].Vendetta = true;
        f.Policy.Choose(1, false, "ties");
        Equal(Government.Democracy, f.Civ.Government); Equal(true, f.Civ.Flags.GovernmentChanged);
        Equal(false, f.World.Civilizations[2].Treaties[1].Vendetta);
        f.World.Globals.Status.Scenario = f.World.Scenario.Flags.GovernmentLock = true;
        f.Policy.Choose(1, true, "locked"); f.Random.RequireChoicesConsumed();
        f = new(Choice.Create(new("A08.government-limit", "restricted"), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(0)));
        f.Ai(); f.Civ.Government = Government.Republic; f.Civ.GovernmentScore[0] = 1;
        f.Policy.Choose(1, true, "restricted"); Equal(Government.Despotism, f.Civ.Government);
        f.Random.RequireChoicesConsumed();
    }

    private static void AssessmentOrderedCensusAndAging()
    {
        var f = new AssessmentFixture(); f.Rule(0, RuleRole.Settle, 0, 1); f.Rule(4, RuleRole.Attack, 3, 2);
        f.Rule(46, RuleRole.Attack, 99, 0);
        f.Data.AddCity(0, 1, new(10, 10), 5); f.Data.AddCity(1, 1, new(20, 10), -1);
        f.World.Entities.Cities.Storage[0].Production = 4; f.World.Entities.Cities.Storage[1].Production = 99;
        f.World.Entities.Cities.Storage[0].Flags.FerryPort = true;
        f.World.Entities.Cities.Storage[1].Flags.FerryPort = f.World.Entities.Cities.Storage[1].Flags.FreshPort = true;
        f.Data.AddUnit(0, 1, 0, new(10, 10)); f.Data.Unit(0).Home = new(0);
        f.Data.AddUnit(1, 1, 4, new(12, 10)); f.Data.Unit(1).Orders = UnitOrder.Fortified;
        f.Data.AddUnit(2, 1, 46, new(14, 10));
        f.Civ.UnitsLost[4] = 6;
        f.Goal(0, new(16, 10), AiGoalRoles.FoundCity, 2);
        f.Run();
        Equal((byte)1, f.Civ.UnitsActive[46]); Equal((short)2, f.World.UnitTotals[1]);
        Equal((short)1, f.Civ.MilitaryCount); Equal((short)3, f.Civ.TotalAttack); Equal((ushort)6, f.Civ.MilitaryStrength);
        Equal((short)2, f.Civ.CityCount); Equal((short)4, f.Civ.TotalCitySize);
        Equal((byte)4, f.Civ.ContinentCitySize[1]); Equal((byte)1, f.Civ.UnitsInProduction[4]); Equal((byte)6, f.Civ.UnitsLost[4]);
        Equal(true, f.Data.Unit(0).Flags.Colonist); Equal(true, f.Data.Unit(1).Flags.Garrison);
        Equal(true, f.World.Entities.Cities.Storage[0].Flags.SettlerNearby);
        Equal(false, f.World.Entities.Cities.Storage[0].Flags.FerryPort);
        Equal(true, f.World.Entities.Cities.Storage[1].Flags.FerryPort); Equal(false, f.World.Entities.Cities.Storage[1].Flags.FreshPort);
        Equal(true, ((ReadOnlySpan<CivilizationGoal>)f.Civ.Goals).ToArray().Any(g => g.Role == AiGoalRoles.FoundCity && g.Priority == -2));
    }

    private static void AssessmentSettlersAndSleeping()
    {
        var f = new AssessmentFixture(); f.Ai(); f.Rule(0, RuleRole.Settle, 0, 1); f.Rule(4, RuleRole.Attack, 2, 1);
        f.Data.AddCity(0, 1, new(10, 10)); f.World.Entities.Cities.Storage[0].Production = 4;
        f.Data.AddUnit(0, 1, 0, new(10, 10)); f.Data.Unit(0).Home = new(0);
        f.Data.AddUnit(1, 1, 4, new(14, 10)); f.Data.Unit(1).Orders = UnitOrder.Sleeping;
        f.Data.AddUnit(2, 1, 4, new(18, 10)); f.World.Map.TileAt(new(18, 10)).Terrain = Terrain.Ocean;
        f.Data.AddUnit(3, 1, 4, new(22, 10)); f.Data.AddUnit(4, 2, 4, new(23, 11));
        f.Run();
        Equal(false, f.Data.Unit(0).Flags.Colonist); Equal(UnitOrder.None, f.Data.Unit(1).Orders);
        Equal(UnitOrder.Sleeping, f.Data.Unit(2).Orders); Equal(UnitOrder.AiIdle, f.Data.Unit(3).Orders);
        Equal((short)3, f.Civ.MilitaryCount); Equal((byte)1, f.Civ.ContinentCities[1]);
    }

    private static void AssessmentRegionalPoliciesAndProduction()
    {
        var f = new AssessmentFixture(); f.Ai(); f.Rule(59, RuleRole.Defend);
        f.Data.AddCity(0, 1, new(10, 10)); f.World.Entities.Cities.Storage[0].Production = 4;
        f.Civ.ContinentPolicy[1] = 0;
        f.Run(); // Policy change invokes actual C06/A02; no units selects the highest available defender.
        Equal((byte)5, f.Civ.ContinentPolicy[1]); Equal((sbyte)59, f.World.Entities.Cities.Storage[0].Production);
        Equal((byte)0, f.Civ.UnitsInProduction[4]); Equal((byte)1, f.Civ.UnitsInProduction[59]);
        f = new(); f.Data.AddCity(0, 1, new(10, 10)); f.Rule(4, RuleRole.Attack, 4, 1); f.Data.AddUnit(0, 1, 4, new(10, 10));
        f.Civ.Advances = default(AdvanceSet).With(AdvanceIds.MapMaking);
        f.World.Map.Regions[1] = new(100, 29, 0, default);
        f.World.Civilizations[2].ContinentMilitary[1] = 1; f.World.Civilizations[2].ContinentCities[1] = 1;
        f.Civ.Treaties[2].Alliance = true;
        f.Run(); // Own city is counted again; ally's city twice: (1+1+2)*7+2=30 >29.
        Equal((byte)4, f.Civ.ContinentPolicy[1]); Equal(default(RegionalPresence), f.Civ.ContinentFlags[1]);
        f.Civ.Advances = default; f.Run(); Equal((byte)5, f.Civ.ContinentPolicy[1]);
    }

    private static void AssessmentGoalsAndReverseAllocation()
    {
        var f = new AssessmentFixture(); f.Rule(4, RuleRole.Attack);
        for (int i = 0; i < 2; ++i) { f.Data.AddUnit(i, 1, 4, new(10, 10)); f.Data.Unit(i).Flags.Automated = true; }
        f.Goal(0, new(14, 10), (int)RuleRole.Attack, 2); f.Goal(1, new(6, 10), (int)RuleRole.Attack, 2);
        f.Run();
        Equal(new MapPoint(14, 10), f.Data.Unit(1).Destination);
        Equal(new MapPoint(14, 10), f.Data.Unit(0).Destination); // Attack allocation floors (6+1)/2 to the original weight.
        Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); Equal((byte)AiTaskIds.Goal, f.Data.Unit(0).AiTask);
        f = new(); f.Rule(4, RuleRole.Defend); f.Data.AddUnit(0, 1, 4, new(10, 10)); f.Data.Unit(0).Flags.Automated = true;
        f.Goal(0, new(10, 10), (int)RuleRole.Defend, 3); f.Run();
        Equal(UnitOrder.AiIdle, f.Data.Unit(0).Orders); Equal(true, f.Data.Unit(0).Flags.GoalReached);
    }

    private static void AssessmentRetirementAndUpgrade()
    {
        foreach (bool airbase in new[] { false, true })
        {
            var f = new AssessmentFixture(); f.Ai(); f.World.Multiplayer.LocalHuman = 2;
            f.Rule(4, RuleRole.Attack); f.Rule(5, RuleRole.Attack); f.Rule(0, RuleRole.Settle);
            f.Rules.Units[4] = f.Rules.Units[4] with { ObsoleteBy = (AdvanceReference)3 };
            f.Rules.Units[5] = f.Rules.Units[5] with { Prerequisite = (AdvanceReference)3 };
            f.Civ.Advances = default(AdvanceSet).With(3);
            f.Data.AddUnit(0, 1, 4, new(10, 10)); f.Data.Unit(0).Flags.Veteran = true;
            f.Data.AddUnit(1, 1, 0, new(20, 10));
            f.World.Map.TileAt(new(10, 10)).Features |= MapFeatures.Fortification | (airbase ? MapFeatures.CitySite : default);
            f.Run();
            Equal(!airbase, f.Data.Unit(0).Token.Occupied);
            if (!airbase) { Equal(new UnitType(5), f.Data.Unit(0).Type); Equal(false, f.Data.Unit(0).Flags.Veteran); }
            Equal((byte)(airbase ? 0 : 1), f.Civ.UnitsActive[4]); Equal((byte)0, f.Civ.UnitsActive[5]); // U01 deletion decrements; in-place upgrade does not repair counts.
        }
    }

    private static void AssessmentEliminationAndGovernment()
    {
        var f = new AssessmentFixture(); f.Ai(); f.World.Globals.Turn = 23;
        var restartRandom = CreationRandom(CreationAttitudes());
        f.Lifecycle.Recreate = owner => Creation(f.Data, owner, restartRandom);
        f.Run();
        restartRandom.RequireChoicesConsumed();
        Equal(1, f.Lifecycle.Destructions.Count); Equal(1, f.Lifecycle.Restarts);
        Equal(true, f.Civ.Flags.GovernmentChanged); // Assignment calls V02 even after actual V01's failed recreation.
        f = new(); f.Ai(); f.World.Globals.Turn = 23; f.Rule(0, RuleRole.Settle); f.Data.AddUnit(0, 1, 0, new(10, 10));
        f.Civ.Advances = default(AdvanceSet).With(AdvanceIds.Monarchy); f.Civ.GovernmentScore[2] = 10;
        f.Run(); Equal(Government.Monarchy, f.Civ.Government);
    }

    private static void AssessmentParatrooperStaging()
    {
        var f = new AssessmentFixture(); f.Ai(); f.Rule(UnitTypeIds.Paratroopers, RuleRole.Defend);
        f.Rules.Units[UnitTypeIds.Paratroopers] = f.Rules.Units[UnitTypeIds.Paratroopers] with { Abilities = new() { Paradrop = true } };
        f.Data.AddCity(0, 2, new(20, 10)); f.Data.AddCity(1, 1, new(14, 10)); f.Data.AddCity(2, 1, new(8, 10));
        f.Civ.Treaties[2].War = true; f.Civ.UnitsActive[UnitTypeIds.Paratroopers] = 1;
        f.Data.AddUnit(0, 1, UnitTypeIds.Paratroopers, new(8, 10)); f.Data.Unit(0).Visibility = CivilizationSet.Only(2);
        f.Run();
        Equal(new MapPoint(14, 10), f.Data.Unit(0).Position); Equal((byte)6, f.Data.Unit(0).MovesUsed);
        Equal(default(CivilizationSet), f.Data.Unit(0).Visibility);
        Equal(false, f.World.Map.TileAt(new(14, 10)).RevealedTo.Contains(1));
        Equal((byte)1, f.Civ.UnitsActive[UnitTypeIds.Paratroopers]);
    }

    private static void AssessmentThreatAndLateRegion()
    {
        var f = new AssessmentFixture(); f.Data.AddCity(0, 1, new(10, 10));
        f.Civ.Treaties[2].Peace = f.Civ.Treaties[2].HostileIntent = f.Civ.Treaties[2].War = true;
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
        f.World.Civilizations[2].ContinentMilitary[1] = 4;
        f.World.Civilizations[2].ContinentAttack[1] = 9;
        f.World.Civilizations[2].ContinentCities[1] = 1;
        f.Run();
        Equal((byte)1, f.Civ.ContinentPolicy[1]); Equal(true, f.Civ.ContinentFlags[1].StrongerEnemy);
        Equal(true, f.Civ.ContinentFlags[1].MilitaryWithPeace); Equal(true, f.Civ.ContinentFlags[1].CitiesWithPeace);
        Equal(true, f.Civ.Flags.AtWar); // Retained foreign census participates even when its owner is dead.
        f = new(); f.Data.AddCity(0, 1, new(10, 10)); f.World.Map.TileAt(new(10, 10)).Region = 63;
        f.Run(); Equal((byte)4, f.Civ.ContinentPolicy[63]); Equal((byte)5, f.Civ.ContinentPolicy[0]);
    }

    private static void AssessmentAirGoalsAndFailurePrefix()
    {
        foreach (bool zeroRoad in new[] { false, true })
        {
            var f = new AssessmentFixture(); f.Rule(30, RuleRole.AirSuperiority, 8, 1);
            f.Rules.Units[30] = f.Rules.Units[30] with { Domain = RuleDomain.Air, Movement = 30, Range = 1 };
            f.Rules.Units[UnitTypeIds.Bomber] = f.Rules.Units[30];
            f.Data.AddUnit(0, 1, 30, new(10, 10)); f.Data.Unit(0).Flags.Automated = true;
            f.World.Map.TileAt(new(14, 10)).Region = 2;
            f.Goal(0, new(18, 10), (int)RuleRole.AirSuperiority, 5);
            f.Goal(1, new(14, 10), (int)RuleRole.NavalSuperiority, 1);
            if (zeroRoad)
            {
                f.Rules.Cosmic = f.Rules.Cosmic with { RoadMovementMultiplier = 0 };
                Throws<DivideByZeroException>(f.Run);
                Equal((byte)1, f.Civ.UnitsActive[30]); Equal((byte)AiTaskIds.Unassigned, f.Data.Unit(0).AiTask);
                Equal((sbyte)5, f.Civ.Goals[0].Priority); // Census/task reset precede failure; tail aging does not run.
            }
            else
            {
                f.Run(); Equal(new MapPoint(14, 10), f.Data.Unit(0).Destination);
                Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders);
            }
        }
    }

    private static void AssessmentAntiSubmarineAndFortressGoals()
    {
        var f = new AssessmentFixture(); f.Rule(37, RuleRole.NavalSuperiority);
        f.Rules.Units[37] = f.Rules.Units[37] with { Abilities = new() { DetectSubmarines = true } };
        f.Data.AddUnit(0, 1, 37, new(10, 10)); f.Data.Unit(0).Flags.Automated = true;
        f.World.Map.TileAt(new(14, 10)).Region = 2;
        f.Goal(0, new(14, 10), AiGoalRoles.AntiSubmarine, 3); // Rejected region still changes the local role.
        f.Goal(1, new(12, 10), (int)RuleRole.NavalSuperiority, 3);
        f.Goal(2, new(16, 10), AiGoalRoles.AntiSubmarine, 3);
        f.Run(); Equal(new MapPoint(16, 10), f.Data.Unit(0).Destination);
        foreach (int priority in new[] { 2, 3 })
        {
            f = new(); f.Rule(4, RuleRole.Defend); f.Data.AddUnit(0, 1, 4, new(10, 10)); f.Data.Unit(0).Flags.Automated = true;
            f.World.Map.TileAt(new(10, 10)).Features |= MapFeatures.Fortification;
            f.Goal(0, new(14, 10), (int)RuleRole.Defend, priority);
            f.Run();
            Equal(priority == 3, f.Data.Unit(0).Orders == UnitOrder.GoTo);
            Equal(priority == 3 ? 2 : 0, SharedGoals.PriorityAt(f.Civ, new(10, 10), (sbyte)RuleRole.Defend));
        }
    }

    private static void AssessmentHistoricalRouteRestoresFlags()
    {
        var f = new AssessmentFixture(Choice.Create(new("A01.scenario-route", "historical", 0), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(2)));
        f.World.Map.Allocate(new(100, 100));
        foreach (ref var tile in f.World.Map.Tiles) { tile.Region = 1; tile.Terrain = Terrain.Grassland; }
        f.World.Globals.Status.Scenario = f.World.Scenario.Flags.HistoricalAi = true; f.World.Globals.Turn = 4;
        f.Rule(UnitTypeIds.Armor, RuleRole.Attack); f.Rule(0, RuleRole.Settle);
        f.Data.AddUnit(0, 3, UnitTypeIds.Armor, new(50, 50)); f.Data.Unit(0).Flags.Veteran = true;
        f.Data.AddUnit(1, 3, 0, new(80, 80));
        f.World.Map.TileAt(new(43, 53)).Owner = 3;
        f.World.Civilizations[3].Goals[0] = new(new(52, 50), (sbyte)RuleRole.Attack, 3);
        f.Assessment.Assess(3, "historical"); f.Random.RequireChoicesConsumed();
        Equal(new MapPoint(52, 50), f.Data.Unit(0).Destination); // Common goal replaces the temporary scenario route.
        Equal(false, f.Data.Unit(0).Flags.Scratch); Equal(false, f.Data.Unit(0).Flags.SpyMission); Equal(true, f.Data.Unit(0).Flags.Veteran);
        f.World.Globals.Turn = 1; f.Data.Unit(0).Flags.Scratch = f.Data.Unit(0).Flags.SpyMission = true;
        f.Assessment.Assess(3, "early");
        Equal(new MapPoint(43, 53), f.Data.Unit(0).Destination);
        Equal(false, f.Data.Unit(0).Flags.Scratch); Equal(false, f.Data.Unit(0).Flags.SpyMission);
    }
    private static void AssessmentInvalidRegionWritesAndReads()
    {
        var f = new AssessmentFixture(); f.Rule(0, RuleRole.Settle); f.Rule(4, RuleRole.Attack);
        f.Data.AddCity(0, 1, new(10, 10)); f.World.Entities.Cities.Storage[0].Production = 99;
        f.World.Map.TileAt(new(10, 10)).Region = 250;
        f.Data.AddUnit(0, 1, 0, new(10, 10)); f.Data.AddUnit(1, 1, 4, new(10, 10));
        f.Goal(0, new(10, 10), AiGoalRoles.FoundCity, 3);
        f.Run();
        Equal((short)1, f.Civ.CityCount); Equal((short)1, f.Civ.MilitaryCount);
        Equal(2, f.World.UnitTotals[1]); Equal((byte)1, f.Civ.UnitsActive[4]);
        Equal((ushort)0, f.Civ.ContinentMilitary[1]); Equal((byte)0, f.Civ.ContinentCities[1]);
        f.Data.Unit(0).Home = new(0);
        Throws<IndexOutOfRangeException>(f.Run); // Housed-settler quota needs the unbacked old regional count.
        Equal(1, f.World.UnitTotals[1]); Equal((byte)1, f.Civ.UnitsActive[0]);
        Equal((byte)0, f.Civ.UnitsActive[4]);
    }

    private static void LocalPlayAssessmentAndPower()
    {
        byte[]? direct = null;
        foreach (bool console in new[] { false, true })
        {
            using var game = new LocalGame();
            var stored = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            var civilizations = stored.Civilizations.ToArray();
            civilizations[1] = civilizations[1] with { TotalCitySize = 123, MilitaryCount = -5 };
            int oldPower = 3 * civilizations[1].NumAdvances + 8 * 123 + (civilizations[1].Gold >> 5);
            stored = stored with { Civilizations = new(civilizations) };
            Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(stored)));
            int military = game.Player.OwnUnits().Records.Count(u => u.Type.Value != 0);
            EndLocalTurn(game, console, 2);
            stored = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            Equal((short)0, stored.Civilizations[1].TotalCitySize);
            Equal((short)military, stored.Civilizations[1].MilitaryCount);
            Equal((byte)7, stored.Globals.PowerRank[1]); Equal((byte)7, stored.Globals.BestHumanRank);
            Equal((byte)(oldPower >> 3), stored.PowerHistory[0][1]); // Old census is published before this turn's assessment.
            var saved = game.SaveClassic(); Equal(LocalLoadResult.Loaded, game.LoadClassic(saved));
            EndLocalTurn(game, console, 3);
            var continued = game.SaveClassic();
            if (direct is null) direct = continued; else BytesEqual(direct, continued);
        }
    }

}
