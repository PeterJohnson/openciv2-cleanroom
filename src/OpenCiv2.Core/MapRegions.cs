using OpenCiv2.Contracts;

namespace OpenCiv2.Core;

/// <summary>W01 §7 region publication, including row carry, small seas and overflow counts.
/// Fertile counts survive this operation; neither connectivity nor fertility is rebuilt.</summary>
public static class MapRegions
{
    public static (bool LandOverflow, bool WaterOverflow) Rebuild(MapStorage map, bool flat)
    {
        ArgumentNullException.ThrowIfNull(map);
        for (int i = 0; i < MapLimits.Regions; ++i)
        {
            map.Regions[i] = map.Regions[i] with { Land = 0, Water = 0 };
        }
        foreach (ref var tile in map.Tiles)
        {
            tile.Region = 0;
        }
        bool water = Label(map, flat, true);
        bool land = Label(map, flat, false);
        RebuildBorders(map, flat);
        return (land, water);
    }

    private static bool Label(MapStorage map, bool flat, bool water)
    {
        var shape = map.Geometry;
        int width = shape.Width / 2;
        var labels = new int[shape.Area];
        // Slot 63 exists even on a small retained map.
        var counts = new int[Math.Max(shape.Area + 1, MapLimits.Regions)];
        int current = 0;
        bool run = false;
        for (int i = 0; i < shape.Area; ++i)
        {
            var point = shape.Coordinates(i);
            if ((flat && i % width == 0) || map.Tiles[i].Ocean != water)
            {
                run = false;
                current = 0;
            }
            if (map.Tiles[i].Ocean != water)
            {
                continue;
            }
            if (point.Y != 0)
            {
                foreach (int direction in new[] { 6, 7, 0 })
                {
                    var neighbor = MapNeighborhoods.Offset(shape, point, MapNeighborhoods.Directions[direction], flat);
                    if (shape.TileIndex(neighbor) is not { } index || labels[index] == 0)
                    {
                        continue;
                    }
                    int other = labels[index];
                    if (current != 0 && current != other)
                    {
                        int smaller = Math.Min(current, other), larger = Math.Max(current, other);
                        counts[smaller] += counts[larger];
                        counts[larger] = 0;
                        int end = (point.Y + 1) * width;
                        for (int j = 0; j < end; ++j)
                        {
                            if (labels[j] == larger)
                            {
                                labels[j] = smaller;
                            }
                        }
                        current = smaller;
                    }
                    else
                    {
                        current = other;
                    }
                }
            }
            if (current == 0 && !run)
            {
                current = FirstFree(counts);
            }
            labels[i] = current;
            ++counts[current];
            run = true;
        }
        if (water)
        {
            var small = new bool[MapLimits.Regions];
            for (int i = 0; i < 63; ++i)
            {
                if (counts[i] < 9)
                {
                    counts[63] += counts[i];
                    small[i] = true;
                    counts[i] = 0;
                }
            }
            for (int i = 0; i < labels.Length; ++i)
            {
                if (labels[i] < 63 && small[labels[i]])
                {
                    labels[i] = 63;
                }
            }
        }
        var remap = new Dictionary<int, int>();
        bool overflow = false;
        for (int i = 0; i < labels.Length; ++i)
        {
            int id = labels[i];
            if (id == 0)
            {
                continue;
            }
            if (id > 63)
            {
                if (remap.TryGetValue(id, out int mapped))
                {
                    id = mapped;
                }
                else if (counts[id] == 0)
                {
                    id = 0;
                }
                else
                {
                    int free = FirstFree(counts);
                    if (free < 64 && (!water || counts[id] > 16))
                    {
                        counts[free] = counts[id];
                        remap[id] = free;
                        id = free;
                    }
                    else
                    {
                        overflow = true;
                        id = 63;
                    }
                }
            }
            map.Tiles[i].Region = (byte)id;
        }
        for (int i = 0; i < MapLimits.Regions; ++i)
        {
            map.Regions[i] = water
                ? map.Regions[i] with { Water = (short)counts[i] }
                : map.Regions[i] with { Land = (short)counts[i] };
        }
        return overflow;
    }

    private static int FirstFree(int[] counts)
    {
        int id = 1;
        while (id < counts.Length && counts[id] != 0)
        {
            ++id;
        }
        return id;
    }

    public static void RebuildBorders(MapStorage map, bool flat)
    {
        for (int i = 0; i < MapLimits.Regions; ++i)
        {
            map.Regions[i] = map.Regions[i] with { NeighboringWater = default };
        }
        var shape = map.Geometry;
        for (int i = 0; i < shape.Area; ++i)
        {
            var tile = map.Tiles[i];
            if (tile.Ocean)
            {
                continue;
            }
            var point = shape.Coordinates(i);
            for (int d = 0; d < 8; ++d)
            {
                var neighbor = MapNeighborhoods.Offset(shape, point, MapNeighborhoods.Directions[d], flat);
                if (shape.InBounds(neighbor) && map.TileAt(neighbor).Ocean)
                {
                    var region = map.Regions[tile.Region];
                    map.Regions[tile.Region] = region with
                    {
                        NeighboringWater = region.NeighboringWater.With(map.TileAt(neighbor).Region)
                    };
                    d += 2 - d % 2;
                }
            }
        }
    }
}
