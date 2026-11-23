using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class WorkFixture : ITerrainWorkEffects
    {
        public RevelationFixture Data { get; } = new();
        public GameState World => Data.World;
        public RuleTables Rules => Data.Rules;
        public MapPoint Point { get; } = new(10, 10);
        public TerrainWorkOrders Work { get; }
        public List<TerrainWorkProblem> Notices { get; } = [];
        public List<string> Effects { get; } = [];
        public CitySlot? CityToRefresh { get; set; }
        public Action<MapPoint, int>? OnRedraw;
        public WorkFixture()
        {
            Rules.Units[0] = Rules.Units[0] with { Role = RuleRole.Settle };
            Rules.Units[1] = Rules.Units[0];
            Rules.Cosmic = Rules.Cosmic with { TransformationWork = 2, FoodPerCitizen = 2, ShieldBoxRows = 10 };
            for (int t = 0; t < RuleLimits.Terrains; ++t)
                Rules.Terrain[t] = new()
                {
                    MovementCost = 2,
                    Food = 3,
                    Shields = 2,
                    Trade = 1,
                    Work = new(new(TerrainResult.ImproveInPlace, 1, 3), new(TerrainResult.ImproveInPlace, 1, 4), (TerrainResult)Terrain.Plains)
                };
            Data.AddUnit(0, 1, 0, Point);
            Work = new(World, Data.Process.Rules, Data.Stacks, this);
        }
        public void Notice(UnitSlot unit, UnitOrder job, TerrainWorkProblem problem) { Notices.Add(problem); Effects.Add("notice"); }
        public void Redraw(MapPoint point, int radius) { Effects.Add("redraw:" + radius); OnRedraw?.Invoke(point, radius); }
        public void RedrawStatus() => Effects.Add("status");
        public void UnitChanged(UnitSlot unit) => Effects.Add("unit:" + unit.Value);
        public void CityChanged(CitySlot city) => Effects.Add("city:" + city.Value);
        public void Know(params int[] advances)
        { foreach (int a in advances) World.Civilizations[1].Advances = World.Civilizations[1].Advances.With(a); }
        public TerrainWorkProblem? Check(UnitOrder job) => TerrainWorkOrders.Check(World, Rules, new(0), job);
    }

    private static void TerrainWorkCommandAdmission()
    {
        var f = new WorkFixture();
        Equal((TerrainWorkProblem?)TerrainWorkProblem.RadioRequired, f.Check(UnitOrder.Airbase));
        Equal((TerrainWorkProblem?)TerrainWorkProblem.ConstructionRequired, f.Check(UnitOrder.Fortress));
        f.Know(AdvanceIds.Radio, AdvanceIds.Construction);
        f.World.Map.TileAt(f.Point).Features |= MapFeatures.Fortification;
        Equal((TerrainWorkProblem?)TerrainWorkProblem.CannotImprove, f.Check(UnitOrder.Airbase));
        Equal((TerrainWorkProblem?)TerrainWorkProblem.CannotImprove, f.Check(UnitOrder.Fortress));
        f.World.Map.TileAt(f.Point).Features |= MapFeatures.CitySite;
        Equal((TerrainWorkProblem?)TerrainWorkProblem.AlreadyAirbase, f.Check(UnitOrder.Airbase));
        Equal((TerrainWorkProblem?)null, f.Check(UnitOrder.Fortress));
        f.World.Map.TileAt(f.Point).Features = MapFeatures.Road | MapFeatures.Railroad;
        f.World.Map.TileAt(f.Point).River = true;
        Equal((TerrainWorkProblem?)TerrainWorkProblem.BridgeRequired, f.Check(UnitOrder.Road));
        f.Know(AdvanceIds.BridgeBuilding);
        Equal((TerrainWorkProblem?)TerrainWorkProblem.AlreadyRailroad, f.Check(UnitOrder.Road));
        f.World.Map.TileAt(f.Point).Features = MapFeatures.Road;
        Equal((TerrainWorkProblem?)TerrainWorkProblem.RailroadRequired, f.Check(UnitOrder.Road));
        f.Know(AdvanceIds.Railroad); Equal((TerrainWorkProblem?)null, f.Check(UnitOrder.Road));
        f.Data.Unit(0).Type = new(2);
        Equal(false, f.Work.Request(new(0), UnitOrder.Road));
        Equal(TerrainWorkProblem.OnlySettlers, f.Notices.Single());
        Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal(UnitOrder.None, f.Data.Unit(0).Orders);
    }

    private static void TerrainWorkWaterAndConversionAdmission()
    {
        var f = new WorkFixture();
        Equal((TerrainWorkProblem?)TerrainWorkProblem.NoWater, f.Check(UnitOrder.Irrigate));
        f.World.Map.TileAt(f.Point).Features |= MapFeatures.Irrigation;
        Equal((TerrainWorkProblem?)TerrainWorkProblem.RefrigerationRequired, f.Check(UnitOrder.Irrigate)); // Center is a water source.
        f.Know(AdvanceIds.Refrigeration); Equal((TerrainWorkProblem?)null, f.Check(UnitOrder.Irrigate));
        f.World.Map.TileAt(f.Point).Features |= MapFeatures.Mine;
        Equal((TerrainWorkProblem?)TerrainWorkProblem.AlreadyFarmland, f.Check(UnitOrder.Irrigate));
        Equal((TerrainWorkProblem?)null, f.Check(UnitOrder.Mine));
        f.World.Map.TileAt(f.Point).Features = MapFeatures.Mine;
        Equal((TerrainWorkProblem?)TerrainWorkProblem.AlreadyMining, f.Check(UnitOrder.Mine));
        f.Data.AddCity(0, 1, f.Point);
        f.Rules.Terrain[2] = f.Rules.Terrain[2] with { Work = new(new((TerrainResult)Terrain.Plains, WorkTurns: 1), new()) };
        Equal(true, f.Work.Request(new(0), UnitOrder.Irrigate));
        Equal(Terrain.Plains, f.World.Map.TileAt(f.Point).Terrain); // Conversion bypasses city/water checks.
        f = new(); Equal((TerrainWorkProblem?)TerrainWorkProblem.EngineersRequired, f.Check(UnitOrder.Transform));
        f.Data.Unit(0).Type = new(1); Equal((TerrainWorkProblem?)null, f.Check(UnitOrder.Transform));
        f.World.Map.TileAt(f.Point).Terrain = Terrain.Ocean;
        Equal((TerrainWorkProblem?)TerrainWorkProblem.CannotImprove, f.Check(UnitOrder.Transform));
        Equal((TerrainWorkProblem?)TerrainWorkProblem.NoPollution, f.Check(UnitOrder.Clean));
    }

    private static void TerrainWorkThresholdsAndProgress()
    {
        foreach (var (job, required) in new[] { (UnitOrder.Fortress, 4), (UnitOrder.Road, 4),
            (UnitOrder.Irrigate, 3), (UnitOrder.Mine, 4), (UnitOrder.Transform, 4), (UnitOrder.Clean, 4), (UnitOrder.Airbase, 4) })
        {
            var f = new WorkFixture(); f.Data.Unit(0).Counter = (byte)(required - 2);
            Equal(false, f.Work.Execute(new(0), job));
            Equal((byte)6, f.Data.Unit(0).MovesUsed); Equal(job, f.Data.Unit(0).Orders);
            Sequence(new[] { "redraw:0", "unit:0" }, f.Effects);
            Equal(true, f.Work.Execute(new(0), job));
            Equal((byte)0, f.Data.Unit(0).Counter); Equal(UnitOrder.None, f.Data.Unit(0).Orders);
            f = new(); f.Data.Unit(0).Type = new(1); f.Data.Unit(0).Counter = (byte)(required - 2);
            Equal(true, f.Work.Execute(new(0), job)); // Fixed Engineers slot adds two even without command checks.
        }
        var road = new WorkFixture(); road.World.Map.TileAt(road.Point).Features |= MapFeatures.Road;
        road.World.Map.TileAt(road.Point).River = true; road.Data.Unit(0).Counter = 8;
        Equal(false, road.Work.Execute(new(0), UnitOrder.Road)); Equal((byte)9, road.Data.Unit(0).Counter);
        Equal(true, road.Work.Execute(new(0), UnitOrder.Road)); // 2*4 + 2, no railroad advance: completes without adding rail.
        Equal(false, road.World.Map.TileAt(road.Point).Features.HasFlag(MapFeatures.Railroad));
    }

    private static void TerrainWorkDonorAndStackCompletion()
    {
        var f = new WorkFixture();
        f.Data.AddUnit(1, 1, 0, f.Point); f.Data.Unit(1).Orders = UnitOrder.Mine; f.Data.Unit(1).Counter = 9;
        f.Data.AddUnit(2, 1, 0, f.Point); f.Data.Unit(2).Owner = new(2); // Retained mixed-owner physical links, not a fresh owner-filtered attachment.
        f.Data.Unit(2).Orders = UnitOrder.Mine; f.Data.Unit(2).Counter = 0;
        Equal(false, f.Work.Execute(new(0), UnitOrder.Mine));
        Equal((byte)1, f.Data.Unit(0).Counter); Equal((byte)9, f.Data.Unit(1).Counter); // First zero-progress donor stops search.
        f.Data.Unit(2).Counter = 2;
        Equal(true, f.Work.Execute(new(0), UnitOrder.Mine));
        for (int i = 0; i < 3; ++i)
        { Equal((byte)0, f.Data.Unit(i).Counter); Equal(UnitOrder.None, f.Data.Unit(i).Orders); }
        Equal((byte)0, f.Data.Unit(1).MovesUsed); Equal((byte)0, f.Data.Unit(2).MovesUsed);
        Equal(true, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Mine));
    }

    private static void TerrainWorkSignedAndNonsettler()
    {
        var f = new WorkFixture(); f.Data.Unit(0).Counter = 127;
        Equal(false, f.Work.Execute(new(0), UnitOrder.Irrigate)); Equal((byte)128, f.Data.Unit(0).Counter);
        f.Data.Unit(0).Counter = 255;
        Equal(false, f.Work.Execute(new(0), UnitOrder.Irrigate)); Equal((byte)0, f.Data.Unit(0).Counter);
        f.Rules.Terrain[2] = f.Rules.Terrain[2] with { Work = new(new(TerrainResult.NoResult, WorkTurns: 255), new()) };
        f.Data.Unit(0).Type = new(2); f.Data.Unit(0).Orders = UnitOrder.Fortified;
        Equal(true, f.Work.Execute(new(0), UnitOrder.Irrigate));
        Equal((byte)1, f.Data.Unit(0).Counter); Equal(UnitOrder.Irrigate, f.Data.Unit(0).Orders);
        Equal(true, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Irrigation)); // Direct negative-result in-place work bypasses admission.
        f.Rules.Units[1] = f.Rules.Units[2]; f.Data.Unit(0).Type = new(1); f.Data.Unit(0).Counter = 0;
        Equal(true, f.Work.Execute(new(0), UnitOrder.Irrigate)); Equal((byte)2, f.Data.Unit(0).Counter);
    }

    private static void TerrainWorkFeaturesAndKnowledge()
    {
        var f = new WorkFixture(); f.Know(AdvanceIds.Refrigeration, AdvanceIds.Railroad);
        f.World.Map.TileAt(f.Point).Features |= MapFeatures.Mine; f.Data.Unit(0).Counter = 2;
        Equal(true, f.Work.Execute(new(0), UnitOrder.Irrigate));
        Equal(false, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Mine));
        f.Data.Unit(0).Counter = 2; f.Work.Execute(new(0), UnitOrder.Irrigate);
        Equal(true, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Mine | MapFeatures.Irrigation));
        f.Data.Unit(0).Counter = 3; f.Work.Execute(new(0), UnitOrder.Mine);
        Equal(false, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Irrigation));
        f.World.Map.TileAt(f.Point).Features |= MapFeatures.Pollution; f.World.Globals.PollutedTiles = short.MinValue;
        f.World.Map.Remembered(2, f.Point) = MapFeatures.Pollution;
        f.Data.Unit(0).Counter = 3; f.Work.Execute(new(0), UnitOrder.Clean);
        Equal(short.MaxValue, f.World.Globals.PollutedTiles);
        Equal(f.World.Map.TileAt(f.Point).Features, f.World.Map.Remembered(1, f.Point));
        Equal(MapFeatures.Pollution, f.World.Map.Remembered(2, f.Point));
        Equal(false, f.World.Map.TileAt(f.Point).RevealedTo.Contains(1));
        f.Data.Unit(0).Counter = 3; f.Work.Execute(new(0), UnitOrder.Clean); Equal(short.MaxValue, f.World.Globals.PollutedTiles);
    }

    private static void TerrainWorkConversionCleanupAndFailure()
    {
        var f = new WorkFixture();
        f.World.Map.TileAt(f.Point).Features |= MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Road | MapFeatures.Pollution;
        f.Rules.Terrain[1] = f.Rules.Terrain[1] with { Work = new(new(TerrainResult.ImproveInPlace), new(TerrainResult.NoResult)) };
        f.Data.Unit(0).Counter = 3; f.Work.Execute(new(0), UnitOrder.Transform);
        Equal(Terrain.Plains, f.World.Map.TileAt(f.Point).Terrain);
        Equal(true, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Irrigation | MapFeatures.Mine)); // Farmland survives lone-mine cleanup.
        f.Rules.Terrain[1] = f.Rules.Terrain[1] with { Work = new(new(), new(), (TerrainResult)Terrain.Grassland) };
        f.Rules.Terrain[2] = f.Rules.Terrain[2] with { Work = new(new(), new()) };
        f.Data.Unit(0).Counter = 3; f.Work.Execute(new(0), UnitOrder.Transform);
        Equal(false, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Irrigation | MapFeatures.Mine));
        Equal(true, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Road | MapFeatures.Pollution));
        f.Data.Unit(0).Counter = 3;
        var before = f.World.Map.Remembered(1, f.Point);
        Throws<IndexOutOfRangeException>(() => f.Work.Execute(new(0), UnitOrder.Transform));
        Equal((Terrain)15, f.World.Map.TileAt(f.Point).Terrain); Equal(true, f.World.Map.TileAt(f.Point).River);
        Equal(true, f.World.Map.TileAt(f.Point).ResourceSuppressed);
        Equal(UnitOrder.None, f.Data.Unit(0).Orders); Equal((byte)0, f.Data.Unit(0).Counter);
        Equal(before, f.World.Map.Remembered(1, f.Point)); // Required negative destination read stops before common completion tail.
        f.Data.Unit(0).Counter = 3;
        Equal(true, f.Work.Execute(new(0), UnitOrder.Clean)); // Fixed-cost jobs need no rule for the retained terrain kind 15.
    }

    private static void TerrainWorkFortressAirbaseAndCity()
    {
        var f = new WorkFixture(); f.Data.Unit(0).Counter = 3;
        f.Work.Execute(new(0), UnitOrder.Airbase); Equal(true, f.World.Map.TileAt(f.Point).Airbase);
        f.Data.Unit(0).Counter = 3; f.Work.Execute(new(0), UnitOrder.Fortress);
        Equal(false, f.World.Map.TileAt(f.Point).Airbase); Equal(true, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Fortification));
        f = new(); f.Data.AddCity(0, 1, f.Point); f.Data.Unit(0).Counter = 3;
        f.Work.Execute(new(0), UnitOrder.Airbase); Equal(true, f.World.Map.TileAt(f.Point).City);
        Equal(false, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Fortification));
        f.Know(AdvanceIds.Railroad); f.Data.Unit(0).Counter = 3;
        f.Work.Execute(new(0), UnitOrder.Road); Equal(true, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Road | MapFeatures.Railroad));
    }

    private static void TerrainWorkRefreshAndRetainedCommand()
    {
        var f = new WorkFixture(); f.Data.AddCity(0, 1, new(12, 10));
        f.CityToRefresh = new(0); f.Data.Unit(0).Counter = 3;
        f.Work.Execute(new(0), UnitOrder.Mine);
        Equal((CitySlot?)new CitySlot(0), f.World.Mood.City); Equal("city:0", f.Effects.Last());
        f.CityToRefresh = new(1); f.Data.AddCity(1, 1, new(18, 10)); f.Data.Unit(0).Counter = 3;
        f.Work.Execute(new(0), UnitOrder.Mine); Equal((CitySlot?)new CitySlot(0), f.World.Mood.City);
        f = new(); f.Data.Unit(0).Counter = 2; f.Data.Unit(0).Orders = UnitOrder.Road;
        f.Data.Stacks.Relocate(0, new(14, 10), false);
        Equal(true, f.Work.Request(new(0), UnitOrder.Mine));
        Equal(UnitOrder.Mine, f.Data.Unit(0).Orders); Equal((byte)3, f.Data.Unit(0).Counter);
        Equal(false, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Mine));
        f.OnRedraw = (_, _) => throw new InvalidOperationException("display stopped");
        Throws<InvalidOperationException>(() => f.Work.Execute(new(0), UnitOrder.Mine));
        Equal(UnitOrder.None, f.Data.Unit(0).Orders); Equal(true, f.World.Map.TileAt(new(14, 10)).Features.HasFlag(MapFeatures.Mine));
        Equal(f.World.Map.TileAt(new(14, 10)).Features, f.World.Map.Remembered(1, new(14, 10)));
    }

    private static void FortificationHomeAndRefreshOrder()
    {
        foreach (int home in new[] { 0, 127, 128, 254 })
        {
            var f = new WorkFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
            f.Data.AddCity(0, 1, new(18, 10), 4); f.Data.AddCity(1, 2, f.Point, 3);
            f.Data.Unit(0).Home = new((ushort)home); f.Data.Unit(0).MovesUsed = 1;
            List<string> calls = [];
            new UnitFortification(f.World, c => calls.Add("home:" + c.Value), _ => calls.Add("draw"), _ => calls.Add("unit")).Apply(new(0));
            Equal((CitySlot?)new CitySlot((ushort)(home < 128 ? 1 : home)), f.Data.Unit(0).Home);
            Sequence(home < 128 ? new[] { "home:" + home, "home:1", "draw", "unit" } : new[] { "draw", "unit" }, calls);
            Equal((byte)1, f.Data.Unit(0).MovesUsed); Equal(UnitOrder.Fortified, f.Data.Unit(0).Orders);
        }
        foreach (bool airbase in new[] { false, true })
        {
            var f = new WorkFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
            f.Data.AddCity(0, 1, new(18, 10), 4); f.Data.AddCity(1, 1, new(12, 10), 3); f.Data.Unit(0).Home = new(0);
            f.World.Map.TileAt(f.Point).Features |= MapFeatures.Fortification | (airbase ? MapFeatures.CitySite : default);
            new UnitFortification(f.World, _ => { }, _ => { }, _ => { }).Apply(new(0));
            Equal((CitySlot?)new CitySlot((ushort)(airbase ? 0 : 1)), f.Data.Unit(0).Home);
        }
    }

    public static (byte[] Initial, byte[] Working, byte[] Completed) LocalTerrainWorkReference() => PlayTerrainWork(false, true);

    private static (byte[] Initial, byte[] Working, byte[] Completed) PlayTerrainWork(bool console, bool loadInitial)
    {
        using var game = new LocalGame();
        var initial = game.SaveClassic();
        if (loadInitial)
        {
            Equal(LocalLoadResult.Loaded, game.LoadClassic(initial));
        }
        var worker = game.Player.ObserveLocalPlay()!.Units.Single(u => u.CanFound);
        Equal(true, worker.Work.Any(job => job.Id == (int)UnitOrder.Irrigate));
        Equal(false, worker.Work.Any(job => job.Id == (int)UnitOrder.Road)); // River bridge requirement.
        var id = $"{worker.Target.Slot.Value}:{worker.Target.Token.Value}";
        RunLocal(game, console, $"unit.work unit={id} job=6", LocalPlayCommands.Work,
            new("unit", new CommandValue.Unit(worker.Target)), new("job", new CommandValue.Integer(6)));
        var working = game.SaveClassic();
        var stored = ClassicFiles.DecodeStoredSave(working);
        Equal(UnitOrder.Irrigate, StateConversion.ToLive(stored.Units[0]).Orders);
        Equal((byte)1, StateConversion.ToLive(stored.Units[0]).Counter);
        Equal(LocalLoadResult.Loaded, game.LoadClassic(working));
        EndLocalTurn(game, console, 2);
        stored = ClassicFiles.DecodeStoredSave(game.SaveClassic());
        Equal((byte)2, StateConversion.ToLive(stored.Units[0]).Counter);
        EndLocalTurn(game, console, 3);
        var completed = game.SaveClassic();
        stored = ClassicFiles.DecodeStoredSave(completed);
        Equal(UnitOrder.None, StateConversion.ToLive(stored.Units[0]).Orders);
        Equal((byte)0, StateConversion.ToLive(stored.Units[0]).Counter);
        Equal(LocalLoadResult.Loaded, game.LoadClassic(completed));
        var map = game.Player.ObserveMap()!;
        int index = new Geometry(map.Width, map.Height).TileIndex(new(8, 8))!.Value;
        Equal(true, map.Tiles[index].Improvements.HasFlag(MapFeatures.Irrigation));
        return (initial, working, completed);
    }

    private static void LocalPlayTerrainWorkContinuation()
    {
        foreach (bool loadInitial in new[] { false, true })
        {
            var direct = PlayTerrainWork(false, loadInitial); var console = PlayTerrainWork(true, loadInitial);
            BytesEqual(direct.Initial, console.Initial); BytesEqual(direct.Working, console.Working); BytesEqual(direct.Completed, console.Completed);
        }
        using var game = new LocalGame();
        var units = game.Player.OwnUnits().Records;
        var rider = units.Single(u => u.Type.Value == 4); var pioneer = units.Single(u => u.Type.Value == 0);
        RunLocal(game, false, "fortify rider", LocalPlayCommands.Fortify, new NamedValue("unit", new CommandValue.Unit(new(rider.Slot, rider.Token))));
        EndLocalTurn(game, false, 2);
        Equal(UnitOrder.Fortifying, game.Player.OwnUnits().Records.Single(u => u.Slot == rider.Slot).Orders);
        RunLocal(game, false, "fortify pioneer", LocalPlayCommands.Fortify, new NamedValue("unit", new CommandValue.Unit(new(pioneer.Slot, pioneer.Token))));
        Equal(UnitOrder.Fortified, game.Player.OwnUnits().Records.Single(u => u.Slot == rider.Slot).Orders);
        Equal(true, game.Player.ObserveLocalPlay()!.Units.Single(u => u.Target.Slot == rider.Slot).MovesLeft > 0);
    }
}
