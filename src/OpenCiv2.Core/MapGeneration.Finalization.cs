using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Core;

internal sealed partial class MapGeneration
{
    internal void Finalize(RuleTables rules, GenerationPathWorkspace paths, bool retained, bool wideLayout)
    {
        if (!flat)
        {
            for (int x = 0; x < shape.Width; x += 2)
            {
                if (!map.TileAt(new(x + 1, 1)).Ocean)
                {
                    Replace(new MapPoint(x, 0), Terrain.Glacier);
                }
                if (!map.TileAt(new(x, shape.Height - 2)).Ocean)
                {
                    Replace(new MapPoint(x + 1, shape.Height - 1), Terrain.Glacier);
                }
            }
        }
        MapRegions.Rebuild(map, flat);
        GenerationConnectivity.Rebuild(map, flat, rules, paths);
        Fertility(rules);
        if (!flat)
        {
            for (int x = 0; x < shape.Width; x += 2)
            {
                Replace(new MapPoint(x, 0), Terrain.Glacier);
                Replace(new MapPoint(x + 1, shape.Height - 1), Terrain.Glacier);
            }
            if (!retained)
            {
                for (int i = 0; i < shape.Width / 8; ++i)
                {
                    Replace(new MapPoint(2 * Pick("poles.north", shape.Width / 2), 0), Terrain.Tundra);
                    Replace(new MapPoint(2 * Pick("poles.north-inner", shape.Width / 2) + 1, 1), Terrain.Tundra);
                    Replace(new MapPoint(2 * Pick("poles.south", shape.Width / 2) + 1, shape.Height - 1),
                        Terrain.Tundra);
                    Replace(new MapPoint(2 * Pick("poles.south-inner", shape.Width / 2), shape.Height - 2),
                        Terrain.Tundra);
                }
            }
        }
        foreach (ref var tile in map.Tiles)
        {
            tile.Features = MapFeatures.None;
        }
        if (!wideLayout)
        {
            SuppressResources();
        }
        map.TileAt(new(-1, -1)) = map.Tiles[0];
        markers?.CopyFirstTileToFallback();
    }

    private void SuppressResources()
    {
        for (int i = 0; i < shape.Area; ++i)
        {
            var point = shape.Coordinates(i);
            if (!Plain(map.Tiles[i], Terrain.Ocean) || (markers?.HasMarkers(i) ?? false)
                || map.SpecialResource(point) == 0)
            {
                continue;
            }
            bool nearLand = false;
            for (int n = 0; n < 20; ++n)
            {
                var neighbor = Neighbor(point, MapNeighborhoods.City[n]);
                if (shape.InBounds(neighbor) && !map.TileAt(neighbor).Ocean)
                {
                    nearLand = true;
                    break;
                }
            }
            if (!nearLand)
            {
                map.Tiles[i].ResourceSuppressed = true;
            }
        }
    }

    internal void Fertility(RuleTables rules)
    {
        for (int i = 0; i < MapLimits.Regions; ++i)
        {
            map.Regions[i] = map.Regions[i] with { Fertile = 0 };
        }
        var scores = new int[22];
        for (int variant = 0; variant < 2; ++variant)
        {
            for (int t = 0; t < 11; ++t)
            {
                var rule = rules.Terrain[variant * 11 + t];
                int score = 3 * unchecked((sbyte)rule.Food) + unchecked((sbyte)rule.Trade);
                if (t != (int)Terrain.Grassland)
                {
                    score += 2 * unchecked((sbyte)rule.Shields);
                }
                var work = rules.Terrain[t].Work;
                if (work?.Mining.Result == TerrainResult.ImproveInPlace)
                {
                    score += t == (int)Terrain.Hills ? 3 : 1;
                }
                else if (work?.Irrigation.Result == TerrainResult.ImproveInPlace)
                {
                    score += 2;
                }
                scores[variant * 11 + t] = score;
            }
        }
        for (int i = 0; i < shape.Area; ++i)
        {
            ref var center = ref map.Tiles[i];
            center.Owner = null;
            center.Fertility = 0;
            if (center.Terrain is not (Terrain.Plains or Terrain.Grassland))
            {
                continue;
            }
            var point = shape.Coordinates(i);
            int total = 0;
            for (int n = 0; n <= 20; ++n)
            {
                var neighbor = Neighbor(point, MapNeighborhoods.City[n]);
                if (!shape.InBounds(neighbor))
                {
                    continue;
                }
                var tile = map.TileAt(neighbor);
                int value = tile.Terrain == Terrain.Grassland && Geometry.GrasslandShield(neighbor) ? 2 : 0;
                int variant = n != 20 && map.SpecialResource(neighbor) != 0 ? 1 : 0;
                value += scores[variant * 11 + (int)tile.Terrain];
                if (tile.River)
                {
                    ++value;
                }
                if (n < 8)
                {
                    value *= 2;
                }
                else if (n == 20)
                {
                    value *= 4;
                    if (tile.River)
                    {
                        value += value / 2;
                    }
                }
                total += value;
            }
            if (center.Terrain == Terrain.Grassland && !Geometry.GrasslandShield(point))
            {
                total -= 16;
            }
            center.Fertility = FertilityFromTotal(total);
            var region = map.Regions[center.Region];
            map.Regions[center.Region] = region with { Fertile = checked((short)(region.Fertile + 1)) };
        }
    }

    internal static byte FertilityFromTotal(int total) => (byte)(Math.Clamp((total - 120) >> 3, 1, 15) / 2 + 8);
}
