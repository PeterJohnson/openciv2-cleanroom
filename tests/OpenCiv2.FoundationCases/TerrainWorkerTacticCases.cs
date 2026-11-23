using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class WorkerFixture
    {
        public WorkFixture Work { get; } = new();
        public RevelationFixture Data => Work.Data;
        public GameState World => Data.World;
        public RuleTables Rules => Data.Rules;
        public MapPoint Point => Work.Point;
        public SharedGoals Goals { get; }
        public TerrainWorkerTactics Policy { get; }
        public WorkerFixture(MapPoint? home = null)
        {
            World.Globals.HumanPlayers = CivilizationSet.Only(2); World.Multiplayer.LocalHuman = 2;
            World.Globals.Turn = 2; World.Civilizations[1].Government = Government.Monarchy;
            foreach (ref var tile in World.Map.Tiles) tile.ResourceSuppressed = true;
            SharedGoals.ResetStanding(ref World.Civilizations[1]);
            Data.AddCity(0, 1, home ?? new(13, 11)); World.Civilizations[1].CityCount = 1;
            World.Civilizations[1].ContinentCities[0] = 1;
            Data.Unit(0).Home = new(0); Data.Unit(0).AiTask = 7; Data.Unit(0).Counter = 2; Data.Unit(0).Orders = UnitOrder.None;
            World.Map.TileAt(Point).River = true;
            Rule(Terrain.Grassland, 1, 0);
            Goals = new(World, Rules, new MovementGoalQueries(World, Rules));
            Policy = new(World, Data.Process.Rules, Data.Stacks, Goals);
        }
        public void Rule(Terrain t, byte irrigation, byte mining, byte turns = 3, TerrainResult result = TerrainResult.ImproveInPlace)
        { Rules.Terrain[(int)t] = Rules.Terrain[(int)t] with { Work = new(new(result, 1, turns, irrigation), new(result, 1, 50, mining)) }; }
        public UnitTacticsContext Context(bool colonist = false)
        {
            var c = new UnitTacticsContext(World, Rules, new(0));
            c.SettlerType = true; c.SettlerKind = !colonist && c.Role == (int)RuleRole.Settle; if (colonist) c.Role = 21; return c;
        }
        public SpecialUnitChoice Choose(UnitTacticsContext? c = null) => Policy.Choose(c ?? Context());
        public void Know(params int[] values) => Work.Know(values);
        public MapPoint At(int d) => MapNeighborhoods.Offset(World.Map.Geometry, Point, MapNeighborhoods.Directions[d], World.Globals.Rules.FlatWorld);
        public void WorkResult(UnitOrder order, byte task)
        { Equal(order, Data.Unit(0).Orders); Equal(task, Data.Unit(0).AiTask); Equal((byte)2, Data.Unit(0).Counter); Equal(new CitySlot(0), Data.Unit(0).Home!.Value); Equal((byte)0, Data.Unit(0).MovesUsed); }
    }

    private static void WorkerAdmissionAndGotoCity()
    {
        foreach (int variant in Enumerable.Range(0, 5))
        {
            var f = new WorkerFixture(); if (variant == 3) f.World.Map.TileAt(f.Point).Terrain = Terrain.Ocean;
            if (variant == 4) f.Data.AddCity(1, 1, f.Point);
            var c = f.Context(); if (variant == 0) c.SettlerType = false; if (variant == 1) c.ForceAttack = true; if (variant == 2) c.GotoCity = true;
            Equal(SpecialUnitRoute.Continue, f.Choose(c).Route); Equal(0, f.Data.Priority(1, f.Point, RuleRole.Defend));
            f.WorkResult(UnitOrder.None, 7);
        }
        var city = new WorkerFixture(); city.Data.Unit(0).Orders = UnitOrder.GoTo; city.Data.Unit(0).Destination = new(13, 11);
        var cc = city.Context(); Equal(SpecialUnitRoute.Continue, city.Choose(cc).Route); Equal(true, cc.GotoCity);
        city.Data.Unit(0).Orders = UnitOrder.None; Equal(SpecialUnitRoute.Continue, city.Choose(cc).Route); Equal(true, cc.GotoCity);
        var retained = new WorkerFixture(); retained.World.Map.TileAt(retained.Point).Features |= MapFeatures.Pollution;
        var rc = retained.Context(); rc.SettlerType = rc.SettlerKind = false;
        Equal(SpecialUnitRoute.Continue, retained.Choose(rc).Route); Equal(UnitOrder.None, retained.Data.Unit(0).Orders);
    }

    private static void WorkerPollutionGoalsAndCompany()
    {
        foreach (int company in Enumerable.Range(0, 4))
        {
            var f = new WorkerFixture(); f.World.Map.TileAt(f.Point).Features |= MapFeatures.Pollution;
            f.Goals.AddStanding(1, f.Point, 21, 5); f.Data.Unit(0).Flags.GoalReached = true;
            if (company != 0) f.Data.AddUnit(1, company == 3 ? 2 : 1, 2, company == 1 ? f.Point : f.At(0));
            var c = f.Context(); c.FortressNeed = 2;
            Equal(SpecialUnitRoute.Finish, f.Choose(c).Route); f.WorkResult(UnitOrder.Clean, AiTaskIds.LocalCleanup);
            Equal(company == 0 ? 2 : 0, f.Data.Priority(1, f.Point, RuleRole.Defend)); Equal(true, f.Data.Unit(0).Flags.GoalReached);
            Equal(false, ((ReadOnlySpan<CivilizationGoal>)f.World.Civilizations[1].StandingGoals).ToArray().Any(g => g.Role == 21));
        }
    }

    private static void WorkerHumanPollutedGotoAndNearbyFlag()
    {
        var f = new WorkerFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
        f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(20, 10);
        f.World.Map.TileAt(new(20, 10)).Features |= MapFeatures.Pollution;
        var c = f.Context(); c.FortressNeed = 2; Equal(SpecialUnitRoute.Continue, f.Choose(c).Route); f.WorkResult(UnitOrder.GoTo, 7);
        Equal(2, f.Data.Priority(1, f.Point, RuleRole.Defend));
        f.Data.Unit(0).Destination = new(-2, 10); Equal(SpecialUnitRoute.Finish, f.Choose(c).Route); f.WorkResult(UnitOrder.Irrigate, AiTaskIds.LocalIrrigation);
        var nearby = new WorkerFixture(); nearby.Data.Unit(0).Orders = UnitOrder.GoTo; nearby.Data.Unit(0).Destination = new(20, 10);
        nearby.World.Entities.Cities.At(new(0)).Flags.SettlerNearby = true;
        Equal(SpecialUnitRoute.Continue, nearby.Choose().Route); nearby.WorkResult(UnitOrder.GoTo, 7);
        var worker = nearby.Context(true); Equal(SpecialUnitRoute.Finish, nearby.Choose(worker).Route); nearby.WorkResult(UnitOrder.Irrigate, AiTaskIds.LocalIrrigation);
    }

    private static void WorkerFortressPriorityAndSafety()
    {
        foreach (int variant in Enumerable.Range(0, 5))
        {
            var f = new WorkerFixture(); var c = f.Context(); c.FortressNeed = variant == 1 ? 1 : 2;
            if (variant is 2 or 3) f.Data.AddUnit(1, 2, 0, f.At(0));
            if (variant == 3) f.Data.AddUnit(2, 1, 2, f.Point);
            if (variant == 4) f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
            Equal(SpecialUnitRoute.Finish, f.Choose(c).Route);
            bool fort = variant is 0 or 3; f.WorkResult(fort ? UnitOrder.Fortress : UnitOrder.Irrigate, fort ? AiTaskIds.FortressWork : AiTaskIds.LocalIrrigation);
        }
        var late = new WorkerFixture(); late.Rule(Terrain.Grassland, 0, 0); var lc = late.Context(); lc.FortressNeed = 1;
        Equal(SpecialUnitRoute.Finish, late.Choose(lc).Route); late.WorkResult(UnitOrder.Fortress, AiTaskIds.FortressWork);
    }

    private static void WorkerSignedTimeAndRecommendations()
    {
        foreach (var (turns, irrigation, mining, selected) in new (byte, byte, byte, bool)[] { (3, 1, 0, true), (50, 0, 1, false), (255, 0, 1, true) })
        {
            var f = new WorkerFixture(); f.Rule(Terrain.Grassland, irrigation, mining, turns);
            Equal(selected ? SpecialUnitRoute.Finish : SpecialUnitRoute.Continue, f.Choose().Route);
            if (selected) f.WorkResult(irrigation != 0 ? UnitOrder.Irrigate : UnitOrder.Mine, irrigation != 0 ? AiTaskIds.LocalIrrigation : AiTaskIds.LocalMine);
        }
        var resource = new WorkerFixture(); resource.Rule(Terrain.Forest, 0, 1, 50); resource.World.Map.TileAt(resource.Point).Terrain = Terrain.Forest;
        resource.World.Map.TileAt(resource.Point).ResourceSuppressed = false;
        for (ushort seed = 0; seed < 64 && resource.World.Map.SpecialResource(resource.Point) == 0; ++seed) resource.World.Map.PatternSeed = seed;
        Equal(true, resource.World.Map.SpecialResource(resource.Point) != 0);
        Equal(SpecialUnitRoute.Finish, resource.Choose().Route); resource.WorkResult(UnitOrder.Mine, AiTaskIds.LocalMine);
    }

    private static void WorkerRoadRailAndCapacityPriority()
    {
        var goal = new WorkerFixture(); goal.Rule(Terrain.Grassland, 0, 0); goal.World.Map.TileAt(goal.Point).Features |= MapFeatures.Road;
        goal.Data.Unit(0).Flags.GoalReached = true; goal.Know(AdvanceIds.Railroad);
        Equal(SpecialUnitRoute.Finish, goal.Choose().Route); goal.WorkResult(UnitOrder.Road, AiTaskIds.RailroadWork);
        foreach (byte shields in new byte[] { 2, 255 })
        {
            var f = new WorkerFixture(); f.Rule(Terrain.Grassland, 0, 0); f.World.Civilizations[1].ContinentPolicy[0] = 4;
            f.World.Map.TileAt(f.Point).Features |= MapFeatures.Road; f.Rules.Terrain[2] = f.Rules.Terrain[2] with { Shields = shields }; f.Know(AdvanceIds.Railroad);
            Equal(shields == 2 ? SpecialUnitRoute.Finish : SpecialUnitRoute.Continue, f.Choose().Route);
            if (shields == 2) f.WorkResult(UnitOrder.Road, AiTaskIds.RailroadWork);
        }
        var bridge = new WorkerFixture(); bridge.Rule(Terrain.Grassland, 0, 0); bridge.Data.Unit(0).Flags.GoalReached = true;
        Equal(SpecialUnitRoute.Continue, bridge.Choose().Route); bridge.Know(AdvanceIds.BridgeBuilding);
        Equal(SpecialUnitRoute.Finish, bridge.Choose().Route); bridge.WorkResult(UnitOrder.Road, AiTaskIds.RoadWork);
    }

    private static void WorkerGotoCapacityAndFallback()
    {
        foreach (var (fertile, colonist, keep) in new (short, bool, bool)[] { (17, false, true), (16, false, false), (-1, false, false), (17, true, false) })
        {
            var f = new WorkerFixture(); f.Rule(Terrain.Grassland, 0, 0); f.World.Map.TileAt(f.Point).Features |= MapFeatures.Road;
            f.World.Map.Regions[0] = new(0, fertile, 0, default); f.Data.Process.Rules.Leaders[0].Expand = 0;
            f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(20, 10); f.Data.Unit(0).Flags.Colonist = colonist;
            var c = f.Context(); Equal(keep ? SpecialUnitRoute.Finish : SpecialUnitRoute.Continue, f.Choose(c).Route);
            Equal(c.HomeCity, c.CitySelector); f.WorkResult(UnitOrder.GoTo, 7);
        }
        var road = new WorkerFixture(); road.Rule(Terrain.Grassland, 0, 0); road.World.Map.TileAt(road.Point).River = false;
        Equal(SpecialUnitRoute.Finish, road.Choose().Route); road.WorkResult(UnitOrder.Road, AiTaskIds.RoadWork);
    }

    private static void WorkerRotatedMovementAndStacks()
    {
        foreach (int blocked in Enumerable.Range(0, 3))
        {
            var f = new WorkerFixture(new(11, 9)); f.World.Map.TileAt(f.Point).Features |= MapFeatures.Mine | MapFeatures.Road;
            f.World.Map.TileAt(f.At(2)).River = f.World.Map.TileAt(f.At(3)).River = true;
            f.World.Map.TileAt(f.At(2)).Region = 7;
            f.Rules.Units[4] = f.Rules.Units[0] with { Role = RuleRole.Attack };
            f.Data.AddUnit(1, blocked == 2 ? 2 : 1, blocked == 1 ? 0 : 4, f.At(2));
            var c = f.Context(); int d = blocked == 0 ? 2 : 3;
            Equal(new SpecialUnitChoice(SpecialUnitRoute.Direction, d), f.Choose(c)); f.WorkResult(UnitOrder.None, AiTaskIds.MoveToWork);
            TacticOrders(f.Data).Direction(c, d); Equal(f.At(d), f.Data.Unit(0).Destination); Equal(UnitOrder.AiGoTo, f.Data.Unit(0).Orders);
        }
    }

    private static void ColonistHomeAndRoadBeforeRetreat()
    {
        var f = new WorkerFixture(); f.Data.AddCity(200, 2, new(30, 20)); f.Data.Unit(0).Home = new(200);
        var c = f.Context(true); c.SettlerType = false;
        Equal(SpecialUnitRoute.Finish, f.Choose(c).Route); Equal(new MapPoint(30, 20), f.Data.Unit(0).Destination); Equal(AiTaskIds.ReturnHome, f.Data.Unit(0).AiTask);
        f.Data.Unit(0).Orders = UnitOrder.None; f.World.Map.TileAt(new(30, 20)).Region = 9; f.World.Map.TileAt(f.Point).River = false; c.EnemyZone = true;
        Equal(SpecialUnitRoute.Finish, f.Choose(c).Route); Equal(UnitOrder.Road, f.Data.Unit(0).Orders); Equal(AiTaskIds.RoadWork, f.Data.Unit(0).AiTask);
        f.World.Map.TileAt(f.Point).Features |= MapFeatures.Road;
        Equal(SpecialUnitRoute.Finish, f.Choose(c).Route); Equal(new MapPoint(13, 11), f.Data.Unit(0).Destination); Equal(AiTaskIds.ReturnHome, f.Data.Unit(0).AiTask);
    }

    private static void ColonistThresholdsAndTaskRetention()
    {
        foreach (var (irrigation, mining, job) in new (byte, byte, UnitOrder)[] { (6, 5, UnitOrder.Mine), (0, 5, UnitOrder.Clean), (0, 255, UnitOrder.Mine), (255, 0, UnitOrder.Irrigate) })
        {
            var f = new WorkerFixture(); f.Rule(Terrain.Grassland, irrigation, mining, result: TerrainResult.NoResult);
            f.World.Civilizations[1].Government = Government.Anarchy; f.World.Map.TileAt(f.Point).River = false;
            f.World.Map.TileAt(f.Point).Features |= MapFeatures.Road | MapFeatures.Pollution;
            Equal(false, TerrainSources.Irrigation(f.World, f.Point));
            var c = f.Context(true); c.SettlerType = false;
            Equal(SpecialUnitRoute.Finish, f.Choose(c).Route); f.WorkResult(job, 7);
        }
        var earlier = new WorkerFixture(); earlier.World.Map.TileAt(earlier.Point).Features |= MapFeatures.Pollution;
        Equal(SpecialUnitRoute.Finish, earlier.Choose(earlier.Context(true)).Route); earlier.WorkResult(UnitOrder.Clean, AiTaskIds.LocalCleanup);
    }

    private static void ColonistRadiusNoHumanAdmissionGates()
    {
        foreach (bool mining in new[] { false, true })
        {
            var f = new WorkerFixture(); f.World.Map.TileAt(f.Point).Features |= MapFeatures.Irrigation | MapFeatures.Road | MapFeatures.Railroad;
            var target = new MapPoint(14, 10); f.World.Map.TileAt(target).Terrain = Terrain.Ocean;
            f.Rule(Terrain.Ocean, 6, mining ? (byte)5 : (byte)0, result: TerrainResult.NoResult); f.Data.AddUnit(1, 2, 0, target);
            var c = f.Context(true); c.SettlerType = false;
            Equal(SpecialUnitRoute.Finish, f.Choose(c).Route); Equal(target, f.Data.Unit(0).Destination);
            Equal(mining ? AiTaskIds.MineDestination : AiTaskIds.IrrigationDestination, f.Data.Unit(0).AiTask); Equal(c.HomeCity, c.CitySelector);
        }
        var road = new WorkerFixture(); road.Rule(Terrain.Grassland, 0, 0); road.World.Map.TileAt(road.Point).Features |= MapFeatures.Irrigation | MapFeatures.Road | MapFeatures.Railroad;
        var rc = road.Context(true); rc.SettlerType = false; Equal(SpecialUnitRoute.Finish, road.Choose(rc).Route);
        Equal(new MapPoint(14, 10), road.Data.Unit(0).Destination); Equal(AiTaskIds.RoadDestination, road.Data.Unit(0).AiTask);
    }

    private static void ColonistRailroadWithoutRoadAndFarHome()
    {
        foreach (bool far in new[] { false, true })
        {
            var f = new WorkerFixture(far ? new(16, 10) : null); f.Know(AdvanceIds.Railroad);
            f.World.Map.TileAt(f.Point).Features |= MapFeatures.Irrigation; // River without Bridge prevents the earlier road branch.
            var c = f.Context(true); c.SettlerType = false;
            Equal(SpecialUnitRoute.Finish, f.Choose(c).Route); f.WorkResult(UnitOrder.Road, AiTaskIds.RoadWork);
        }
    }

    private static void WorkerActualOrderExecution()
    {
        var f = new OrderFixture(); var d = f.Data;
        d.Rules.Units[0] = d.Rules.Units[0] with { Role = RuleRole.Settle };
        d.Unit(0).Orders = UnitOrder.None; d.Unit(0).Counter = 3;
        d.World.Map.TileAt(d.Unit(0).Position).Features |= MapFeatures.Pollution;
        var c = new UnitTacticsContext(d.World, d.Rules, new(0)) { SettlerType = true, SettlerKind = true };
        var work = new TerrainWorkerTactics(d.World, d.Process.Rules, d.Stacks, new(d.World, d.Rules, new MovementGoalQueries(d.World, d.Rules)));
        Equal(SpecialUnitRoute.Finish, work.Choose(c).Route); Equal(0, TacticOrders(d).Finish(c.Unit, c.Domain)); Equal((byte)3, d.Unit(0).Counter);
        using var op = f.Begin(); f.Complete(op); Equal(false, d.World.Map.TileAt(d.Unit(0).Position).Features.HasFlag(MapFeatures.Pollution));
        Equal(UnitOrder.None, d.Unit(0).Orders); Equal((byte)0, d.Unit(0).Counter); Equal((byte)6, d.Unit(0).MovesUsed);
    }

    private static void WorkerMissingRulesRetainGoals()
    {
        var f = new WorkerFixture(); f.Goals.AddStanding(1, f.Point, 21, 5);
        f.Rules.Terrain[(int)Terrain.Grassland] = f.Rules.Terrain[(int)Terrain.Grassland] with { Work = null };
        Throws<InvalidOperationException>(() => f.Choose()); Equal(2, f.Data.Priority(1, f.Point, RuleRole.Defend));
        Equal(false, ((ReadOnlySpan<CivilizationGoal>)f.World.Civilizations[1].StandingGoals).ToArray().Any(g => g.Role == 21));
        f.WorkResult(UnitOrder.None, 7);
        f.Data.AddCity(200, 1, new(30, 20)); f.Data.Unit(0).Home = new(200);
        var c = f.Context(true); c.SettlerType = false; Equal(SpecialUnitRoute.Finish, f.Choose(c).Route);
        Equal(new MapPoint(30, 20), f.Data.Unit(0).Destination); // Earlier travel does not require unused terrain-work definitions.
    }
}
