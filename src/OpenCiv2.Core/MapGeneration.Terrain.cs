using OpenCiv2.Contracts;

namespace OpenCiv2.Core;

internal sealed partial class MapGeneration
{
    internal void InitialTerrain()
    {
        int bandSize = shape.Height / (flat ? 8 : 12);
        if (bandSize < 3)
        {
            bandSize = 2;
        }
        for (int i = 0; i < shape.Area; ++i)
        {
            Terrain terrain;
            int count = Coverage[i];
            if (count != 1)
            {
                terrain = count == 0 ? Terrain.Ocean : count == 2 ? Terrain.Hills : Terrain.Mountains;
            }
            else
            {
                int y = shape.Coordinates(i).Y;
                int center = shape.Height / 2 + 8;
                bool negative = center - (y + Pick("terrain.sign", 16) + 1) <= 0;
                int latitude = center - (y + Pick("terrain.latitude", 16) + 1);
                if (negative)
                {
                    latitude = -latitude;
                }
                latitude = Math.Max(0, latitude - (flat ? 6 : 3) * temperature);
                int band = latitude / bandSize;
                terrain = band switch
                {
                    0 => Terrain.Desert,
                    <= 3 => Terrain.Plains,
                    4 => Pick("terrain.tundra", 2) == 0 ? Terrain.Plains : Terrain.Tundra,
                    <= 6 => Terrain.Tundra,
                    _ => Terrain.Glacier
                };
            }
            Replace(i, terrain);
        }
    }

    internal void Rainfall()
    {
        int forward = 0, reverse = shape.Area - 1;
        for (int row = 0; row < shape.Height; ++row)
        {
            int latitude = Math.Abs(shape.Height / 2 - row);
            int cap = 4 * climate + 4 + Math.Abs(shape.Height / 4 - latitude);
            int rain = cap > 0 ? Pick("rain.forward-start", cap + 1) : 0;
            for (int x = 0; x < shape.Width / 2; ++x, ++forward)
            {
                var tile = map.Tiles[forward];
                if (Plain(tile, Terrain.Ocean))
                {
                    if (rain < cap)
                    {
                        ++rain;
                    }
                }
                else if (rain > 0)
                {
                    rain -= Pick("rain.forward-loss", 7 - 2 * climate);
                    if (tile.Terrain == Terrain.Mountains)
                    {
                        rain -= 3;
                    }
                    Replace(forward, tile.Terrain switch
                    {
                        Terrain.Desert => Terrain.Plains,
                        Terrain.Plains => Terrain.Grassland,
                        Terrain.Hills or Terrain.Tundra => Terrain.Forest,
                        _ => tile.Terrain
                    });
                }
            }
            rain = 0;
            for (int x = 0; x < shape.Width / 2; ++x, --reverse)
            {
                var tile = map.Tiles[reverse];
                if (Plain(tile, Terrain.Ocean))
                {
                    if (rain < latitude / 2 + climate + 1)
                    {
                        ++rain;
                    }
                }
                else if (rain > 0)
                {
                    rain -= Pick("rain.reverse-loss", 5 - 2 * climate);
                    if (tile.Terrain == Terrain.Mountains)
                    {
                        rain -= 3;
                    }
                    else if (tile.Terrain == Terrain.Grassland)
                    {
                        rain -= 2;
                    }
                    Replace(reverse, tile.Terrain switch
                    {
                        Terrain.Desert or Terrain.Tundra => Terrain.Plains,
                        Terrain.Plains => Terrain.Grassland,
                        Terrain.Grassland => latitude < 3 * shape.Height / 10
                            ? latitude < 10 ? Terrain.Jungle : Terrain.Swamp : Terrain.Forest,
                        Terrain.Hills or Terrain.Swamp => Terrain.Forest,
                        _ => tile.Terrain
                    });
                }
            }
        }
    }

    private MapPoint PairedSelection(string phase, int iteration, MapPoint previous)
    {
        if (iteration % 2 == 0)
        {
            int y = Pick(phase + ".y", shape.Height);
            return new(2 * Pick(phase + ".x", shape.Width / 2) + y % 2, y);
        }
        return Neighbor(previous, MapNeighborhoods.Directions[Pick(phase + ".neighbor", 8)]);
    }

    internal void Weathering()
    {
        MapPoint point = default;
        for (int iteration = 0; iteration < (5 * age + 10) * 160; ++iteration)
        {
            point = PairedSelection("weather", iteration, point);
            if (shape.InBounds(point))
            {
                Weather(point);
            }
        }
    }

    internal void Weather(MapPoint point)
    {
        var tile = map.TileAt(point);
        if (!Plain(tile, tile.Terrain))
        {
            return;
        }
        if (tile.Terrain == Terrain.Mountains)
        {
            if (point.X >= 2 && point.X < shape.Width - 2 && point.Y >= 2 && point.Y < shape.Height - 2
                && !map.TileAt(new(point.X - 2, point.Y)).Ocean && !map.TileAt(new(point.X + 2, point.Y)).Ocean
                && !map.TileAt(new(point.X, point.Y - 2)).Ocean && !map.TileAt(new(point.X, point.Y + 2)).Ocean)
            {
                Replace(point, Terrain.Ocean);
            }
            return;
        }
        Replace(point, tile.Terrain switch
        {
            Terrain.Desert => Terrain.Plains,
            Terrain.Plains or Terrain.Tundra => Terrain.Hills,
            Terrain.Grassland => Terrain.Forest,
            Terrain.Forest => Math.Abs(shape.Height / 2 - point.Y) < 3 * shape.Height / 10
                ? Terrain.Jungle : Terrain.Plains,
            Terrain.Hills or Terrain.Glacier => Terrain.Mountains,
            Terrain.Swamp => Terrain.Grassland,
            Terrain.Jungle => Terrain.Swamp,
            _ => tile.Terrain
        });
    }

    private static bool High(Terrain terrain) => terrain is >= Terrain.Forest and <= Terrain.Jungle;

    internal void Voting()
    {
        MapPoint point = default;
        Terrain? last = null;
        for (int iteration = 0; iteration < (1 - age) * 800; ++iteration)
        {
            point = PairedSelection("vote", iteration, point);
            if (iteration % 2 == 0)
            {
                last = null;
            }
            Vote(point, ref last);
        }
    }

    internal void Vote(MapPoint point, ref Terrain? last)
    {
        if (!shape.InBounds(point) || map.TileAt(point).Ocean || map.TileAt(point).Terrain == last)
        {
            return;
        }
        bool high = High(map.TileAt(point).Terrain);
        Span<int> votes = stackalloc int[11];
        votes.Clear();
        if (climate == -1)
        {
            ++votes[(int)Terrain.Desert];
            ++votes[(int)Terrain.Plains];
            --votes[(int)Terrain.Swamp];
        }
        else if (climate == 1)
        {
            ++votes[(int)Terrain.Grassland];
            ++votes[(int)Terrain.Swamp];
            if (temperature >= 0)
            {
                ++votes[(int)Terrain.Jungle];
            }
        }
        if (temperature == -1)
        {
            ++votes[(int)Terrain.Tundra];
            ++votes[(int)Terrain.Forest];
            --votes[(int)Terrain.Desert];
        }
        else if (temperature == 1)
        {
            ++votes[(int)Terrain.Plains];
            --votes[(int)Terrain.Tundra];
            --votes[(int)Terrain.Glacier];
            if (climate == -1)
            {
                --votes[(int)Terrain.Grassland];
                --votes[(int)Terrain.Jungle];
            }
        }
        for (int d = 0; d < 8; ++d)
        {
            var neighbor = Neighbor(point, MapNeighborhoods.Directions[d]);
            if (!shape.InBounds(neighbor) || map.TileAt(neighbor).Ocean)
            {
                continue;
            }
            var terrain = map.TileAt(neighbor).Terrain;
            ref int vote = ref votes[(int)terrain];
            if (High(terrain) && high && vote != 0)
            {
                ++vote;
            }
            vote += d % 2 == 0 ? 2 : 1;
        }
        int best = 0;
        for (int t = 0; t < votes.Length; ++t)
        {
            if (votes[t] > best)
            {
                best = votes[t];
                Replace(point, (Terrain)t);
                last = (Terrain)t;
            }
        }
    }
}
