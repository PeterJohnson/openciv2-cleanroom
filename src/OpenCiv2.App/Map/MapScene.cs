using OpenCiv2.Contracts;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.App.Map;

internal enum MapPickKind { Tile, Unit, City }
internal readonly record struct MapPick(MapPoint Position, MapPickKind Kind);
internal readonly record struct TileConnections(int RiverMask, int RouteMask);
internal readonly record struct MapObjects(ObservedMapUnit? Unit, int Count, ObservedMapCity? City, string? Name);

/// <summary>View-side indexing of an owned, filtered observation. No world or command access.</summary>
internal sealed class MapScene
{
    private readonly MapObjects[] objects;
    private readonly int[] objectSlots;
    private readonly int[][] foregroundRows, labelRows;
    private readonly HashSet<MapPoint>? region;
    private readonly HashSet<MapPoint> cityRadius = [];
    private readonly Dictionary<UnitTarget, MapPoint> ownUnits = [];
    public MapAnnotations Annotations => Map.Annotations ?? MapAnnotations.Stock;
    public bool InRegion(MapPoint point) => region is null || region.Contains(point);
    public bool InCityRadius(MapPoint point) => cityRadius.Contains(point);
    public MapPoint? Neighbor(MapPoint point, int direction)
    {
        var offset = Directions[direction];
        int x = point.X + offset.X;
        if (!Map.Flat) x = MapCamera.CanonicalX(x, Map.Width);
        var candidate = new MapPoint(x, point.Y + offset.Y);
        return Contains(candidate) && candidate != point ? candidate : null;
    }
    // W01 §3, B direction order. Road and rail index 0 is isolated; indices
    // 1–8 use this order. The four even-index directions are river contacts.
    private static readonly MapPoint[] Directions = [new(1, -1), new(2, 0), new(1, 1), new(0, 2),
        new(-1, 1), new(-2, 0), new(-1, -1), new(0, -2)];
    // W01 §3 B[0..20], the ordinary workable radius.
    private static readonly MapPoint[] Workable = [.. Directions, new(2, -2), new(2, 2), new(-2, 2), new(-2, -2),
        new(1, -3), new(3, -1), new(3, 1), new(1, 3), new(-1, 3), new(-3, 1), new(-3, -1), new(-1, -3), new(0, 0)];

    public MapObservation Map { get; }
    public MapScene(MapObservation map)
    {
        Map = map;
        region = map.Annotations?.Region?.Tiles.ToHashSet();
        // Slot zero is the shared empty cell. A map needs a full tile lookup
        // but only occupied cells need to store the complete object record.
        objectSlots = new int[map.Tiles.Count];
        objects = new MapObjects[1 + (int)Math.Min(map.Tiles.Count, (long)map.Units.Count + map.Cities.Count)];
        int occupied = 0;
        foreach (var city in map.Cities)
        {
            if (!InRegion(city.Position)) continue;
            ref var cell = ref OccupiedCell(city.Position, ref occupied);
            cell = cell with { City = city, Name = CityName(city) };
            if (city.Details?.Knowledge != CityKnowledge.Own) continue;
            foreach (var offset in Workable)
            {
                int x = city.Position.X + offset.X;
                if (!map.Flat) x = MapCamera.CanonicalX(x, map.Width);
                var tile = new MapPoint(x, city.Position.Y + offset.Y);
                if (Contains(tile)) cityRadius.Add(tile);
            }
        }
        foreach (var unit in map.Units)
        {
            if (!InRegion(unit.Position)) continue;
            if (unit.Target is UnitTarget target) ownUnits[target] = unit.Position;
            ref var cell = ref OccupiedCell(unit.Position, ref occupied);
            cell = AddUnit(cell, unit);
        }
        foregroundRows = new int[map.Height][];
        labelRows = new int[map.Height][];
        var foreground = new List<int>();
        var labels = new List<int>();
        for (int y = 0; y < map.Height; y++)
        {
            int index = y * (map.Width / 2);
            for (int x = y & 1; x < map.Width; x += 2, index++)
            {
                var point = new MapPoint(x, y);
                if (!InRegion(point)) continue;
                ref readonly var cell = ref objects[objectSlots[index]];
                var tile = map.Tiles[index];
                if (cell.Unit is not null || cell.City is not null || RememberedCity(tile) ||
                    tile.Terrain is not null && (tile.Improvements & MapImprovements.Fortification) != 0)
                    foreground.Add(x);
                if (cell.City is not null && cell.Name is { Length: > 0 }) labels.Add(x);
            }
            foregroundRows[y] = foreground.ToArray();
            labelRows[y] = labels.ToArray();
            foreground.Clear();
            labels.Clear();
        }
    }

    private ref MapObjects OccupiedCell(MapPoint point, ref int occupied)
    {
        ref int slot = ref objectSlots[Index(point)];
        if (slot == 0) slot = ++occupied;
        return ref objects[slot];
    }

    public IEnumerable<(MapPoint Point, int DisplayX)> VisibleForeground(MapVisibleBounds bounds) => VisibleCells(bounds, foregroundRows);
    public IEnumerable<(MapPoint Point, int DisplayX)> VisibleLabels(MapVisibleBounds bounds) => VisibleCells(bounds, labelRows);

    private IEnumerable<(MapPoint Point, int DisplayX)> VisibleCells(MapVisibleBounds bounds, int[][] rows)
    {
        var (left, top, right, bottom) = bounds;
        if (left > right) yield break;
        int firstCopy = Map.Flat ? 0 : left - MapCamera.CanonicalX(left, Map.Width);
        int lastCopy = Map.Flat ? 0 : right - MapCamera.CanonicalX(right, Map.Width);
        // Preserve the dense traversal's depth order across repeated wrapped copies.
        for (int y = top; y <= bottom; y++)
        {
            var columns = rows[y];
            if (columns.Length == 0) continue;
            for (int copy = firstCopy; copy <= lastCopy; copy += Map.Width)
            {
                int start = Array.BinarySearch(columns, left - copy);
                for (int i = start < 0 ? ~start : start; i < columns.Length; i++)
                {
                    int projected = columns[i] + copy;
                    if (projected > right) break;
                    yield return (new(columns[i], y), projected);
                }
            }
        }
    }

    private static string CityName(ObservedMapCity city)
    {
        try { return ClassicText.DecodeWindows1252(city.Name.ToArray(), "map city name"); }
        catch (ContentException) { return "City"; }
    }

    private int Index(MapPoint position) => position.Y * (Map.Width / 2) + position.X / 2;
    public bool Contains(MapPoint point) => point.X >= 0 && point.X < Map.Width && point.Y >= 0 &&
        point.Y < Map.Height && ((point.X ^ point.Y) & 1) == 0;

    public ObservedTile Tile(MapPoint position) => Map.Tiles[Index(position)];
    public MapObjects Objects(MapPoint position) => objects[objectSlots[Index(position)]];
    public bool HasCity(MapPoint position) => objects[objectSlots[Index(position)]].City is not null;

    internal IEnumerable<int> ComparisonIndices(MapScene before, bool sparse, bool radius = false)
    {
        var delta = Map.Delta;
        return sparse && delta is not null && delta.Matches(before.Map, Map) && !delta.AnnotationsChanged &&
            !(radius && delta.CitiesChanged) ? delta.ChangedIndices : Enumerable.Range(0, Map.Tiles.Count);
    }

    public static MapImprovements Route(ObservedTile tile) =>
        (tile.Improvements & MapImprovements.Railroad) != 0 ? MapImprovements.Railroad : tile.Improvements & MapImprovements.Road;

    /// <summary>P02 map information. Presentation joins come only from the bound observer's tiles.
    /// Projected roads/rails join matching remembered improvements. River masks use explored land/water.</summary>
    public TileConnections Connections(MapPoint center, ObservedTile tile)
    {
        if (tile.Terrain is null) return default;
        var route = Route(tile);
        int riverMask = 0, routeMask = 0;
        for (int i = 0; i < Directions.Length; i++)
        {
            if (route == MapImprovements.None && (i & 1) != 0) continue;
            if (Neighbor(center, i) is not { } neighbor) continue;
            var observed = Tile(neighbor);
            if (observed.Terrain is null) continue;
            if (tile.River && (i & 1) == 0 && (observed.River || observed.Terrain == 10))
                riverMask |= 1 << (i / 2);
            if ((observed.Improvements & route) != 0)
                routeMask |= 1 << i;
        }
        return new(riverMask, routeMask);
    }

    public bool HasTarget(UnitTarget target) => ownUnits.ContainsKey(target);
    public MapPoint? Locate(UnitTarget target) => ownUnits.TryGetValue(target, out var point) ? point : null;

    public bool PermitsPath(MapPath path)
    {
        if (path.Target is not { } target) return path.Kind != MapPathKind.ActiveRoute;
        if (Locate(target) is not { } point || !InRegion(point) || Tile(point).Terrain is null) return false;
        return path.Kind != MapPathKind.ActiveRoute ||
            target == Annotations.ActiveUnit && path.Points.Count > 0 && path.Points[0] == point;
    }

    public Dictionary<MapPoint, MapObjects> StacksWithout(IReadOnlyList<UnitTarget> moving)
    {
        var remaining = new Dictionary<MapPoint, MapObjects>();
        foreach (var target in moving)
            if (Locate(target) is { } point)
                remaining.TryAdd(point, Objects(point) with { Unit = null, Count = 0 });
        foreach (var unit in Map.Units)
            if (remaining.TryGetValue(unit.Position, out var stack) && (unit.Target is not { } target || !moving.Contains(target)))
                remaining[unit.Position] = AddUnit(stack, unit);
        return remaining;
    }

    private static MapObjects AddUnit(MapObjects cell, ObservedMapUnit unit)
    {
        var top = cell.Unit;
        if (top is null || top.Value.Target is null && unit.Target is not null) top = unit;
        return cell with { Unit = top, Count = cell.Count + 1 };
    }

    public bool PermitsSegment(MapPoint a, MapPoint b, out int destinationX)
    {
        destinationX = b.X;
        if (!Contains(a) || !Contains(b)) return false;
        if (!Map.Flat)
        {
            int delta = b.X - a.X;
            if (delta > Map.Width / 2) destinationX -= Map.Width;
            else if (delta < -Map.Width / 2) destinationX += Map.Width;
        }
        // Rotate the diamond lattice to unit squares, then visit every square
        // crossed by the segment. Compare edge crossing times as rationals.
        int u = (a.X + a.Y) / 2, v = (a.X - a.Y) / 2;
        int endU = (destinationX + b.Y) / 2, endV = (destinationX - b.Y) / 2;
        int signU = Math.Sign(endU - u), signV = Math.Sign(endV - v);
        int countU = Math.Abs(endU - u), countV = Math.Abs(endV - v);
        long edgeU = 1, edgeV = 1;
        if (!Allowed(u, v)) return false;
        while (u != endU || v != endV)
        {
            long nextU = u == endU ? long.MaxValue : edgeU * countV;
            long nextV = v == endV ? long.MaxValue : edgeV * countU;
            if (nextU == nextV && (!Allowed(u + signU, v) || !Allowed(u, v + signV))) return false;
            if (nextU <= nextV) { u += signU; edgeU += 2; }
            if (nextV <= nextU) { v += signV; edgeV += 2; }
            if (!Allowed(u, v)) return false;
        }
        return true;

        bool Allowed(int axisU, int axisV)
        {
            int x = axisU + axisV;
            var point = new MapPoint(Map.Flat ? x : MapCamera.CanonicalX(x, Map.Width), axisU - axisV);
            return Contains(point) && InRegion(point) && Tile(point).Terrain is not null;
        }
    }

    // Body hit boxes use logical map coordinates, including the overhang above a tile.
    public const int UnitWidth = 52, UnitTop = -30;
    public const int CityWidth = 52, CityTop = -30;
    public static bool RememberedCity(ObservedTile tile) =>
        (tile.Improvements & (MapImprovements.CitySite | MapImprovements.Fortification)) == MapImprovements.CitySite;

    public MapPick? Pick(MapCamera camera, double x, double y, bool terrainOnly = false)
    {
        var tile = camera.Hit(x, y, Map);
        if (region is not null && (tile is not { } candidate || !InRegion(candidate))) return null;
        if (!terrainOnly && PickMarker(camera with { X = camera.X - x, Y = camera.Y - y }) is { } marker)
            return marker;
        return tile is { } point ? new(point, MapPickKind.Tile) : null;
    }

    public bool PermitsPick(MapPick pick) => Contains(pick.Position) && InRegion(pick.Position) && pick.Kind switch
    {
        MapPickKind.Tile => true,
        MapPickKind.Unit => Objects(pick.Position).Unit is not null,
        MapPickKind.City => Objects(pick.Position).City is not null || RememberedCity(Tile(pick.Position)),
        _ => false
    };

    private MapPick? PickMarker(MapCamera local)
    {
        MapPick? selected = null;
        foreach (var (point, display) in local.Visible(Map, 0, 0))
        {
            if (!InRegion(point)) continue;
            var cell = Objects(point);
            double x = -local.ScreenX(display) / local.Zoom, y = -local.ScreenY(point.Y) / local.Zoom;
            bool cityHit = cell.City is { } city
                ? city.Details?.Appearance.Value is not null
                    ? Math.Abs(x) <= CityWidth / 2 && y >= CityTop && y <= CityTop + CityWidth * 0.75
                    : Math.Abs(x) <= 4 && y is >= -8 and <= 0
                : RememberedCity(Tile(point)) && Math.Abs(x * local.Zoom) <= 4 && Math.Abs(y * local.Zoom) <= 4;
            if (cityHit)
                selected = new(point, MapPickKind.City);
            if (cell.Unit is not null && Math.Abs(x - (cell.City is null ? 0 : 14)) <= UnitWidth / 2 && y >= UnitTop && y <= UnitTop + UnitWidth * 0.75)
                selected = new(point, MapPickKind.Unit);
        }
        return selected;
    }
}
