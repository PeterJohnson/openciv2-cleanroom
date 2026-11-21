using OpenCiv2.Contracts;

namespace OpenCiv2.Core;

internal sealed partial class MapGeneration
{
    internal void CreateLand()
    {
        minY = 0;
        maxY = shape.Height - 1;
        if (Pick("land.bounds", 2) == 0)
        {
            maxY = shape.Height - 5;
        }
        else
        {
            minY = 4;
        }
        int target = shape.Area / 400 * ((8 * landMass + 8 + 8 * Math.Max(form, 0)) * 5 + 80) * 8 / 10;
        do
        {
            Landmass(false);
        } while (Placed < target);
        MapRegions.Rebuild(map, flat);
        int occupied = 0;
        foreach (var region in map.Regions)
        {
            if (region.Land != 0)
            {
                ++occupied;
            }
        }
        for (int i = occupied; i < 63; ++i)
        {
            Landmass(true);
        }
        RepairContacts();
    }

    private bool WithinGeneration(MapPoint point) => point.Y >= minY && point.Y <= maxY
        && (!flat || (point.X >= 3 && point.X <= shape.Width - 3));

    private void Mark(MapPoint point)
    {
        point = new(shape.WrapX(point.X, flat), point.Y);
        if (WithinGeneration(point) && shape.TileIndex(point) is { } index)
        {
            marked[index] = true;
        }
        // Original out-of-map scratch writes have no land/terrain consumer. They do not become
        // logical tile ownership; finalization replaces the fallback with the first finished tile.
    }

    private void Cluster(MapPoint point)
    {
        Mark(point);
        Mark(new(point.X + 1, point.Y - 1));
        Mark(new(point.X + 1, point.Y + 1));
    }

    internal void Landmass(bool islet)
    {
        Array.Clear(marked);
        MapPoint start;
        do
        {
            int x = 8 + Pick("land.start-x", shape.Width - 16);
            int y = 4 + Pick("land.start-y", shape.Height - 8);
            start = new(x, y);
            if (!islet && Pick("land.overlap", form + 2) != 0)
            {
                break;
            }
        } while (Coverage[shape.TileIndex(start)!.Value] != 0);
        if (islet)
        {
            int repetitions = Pick("islet.walks", 10);
            Walk(start, true);
            if (repetitions >= 7)
            {
                Walk(start, true);
            }
            if (repetitions >= 9)
            {
                Walk(start, true);
            }
        }
        else
        {
            Walk(start, false);
        }
        for (int i = 0; i < marked.Length; ++i)
        {
            if (marked[i])
            {
                Coverage[i] = unchecked((byte)(Coverage[i] + 1));
                ++Placed;
            }
        }
    }

    private void Walk(MapPoint point, bool diagonal)
    {
        bool wide = !diagonal && form >= 1;
        int remaining = Pick(diagonal ? "islet.length" : "land.length", diagonal ? 16 : wide ? 48 : 64);
        if (!diagonal && Coverage[shape.TileIndex(point)!.Value] != 0)
        {
            remaining /= 2;
        }
        ++remaining;
        while (remaining != 0 && WithinGeneration(point))
        {
            if (diagonal)
            {
                Mark(point);
            }
            else
            {
                Cluster(point);
                if (wide)
                {
                    foreach (var offset in new MapPoint[] { new(2, 0), new(-2, 0), new(0, 2), new(0, -2) })
                    {
                        if (Pick("land.widen", 4) == 0)
                        {
                            Cluster(new(point.X + offset.X, point.Y + offset.Y));
                        }
                    }
                }
            }
            int d = 2 * Pick(diagonal ? "islet.direction" : "land.direction", 4) + (diagonal ? 0 : 1);
            var step = MapNeighborhoods.Directions[d];
            point = new(point.X + step.X, point.Y + step.Y);
            --remaining;
        }
    }

    internal void RepairContacts()
    {
        int Index(int x, int y) => shape.TileIndex(new(x, y))!.Value;
        for (int y = 1; y < shape.Height - 2; ++y)
        {
            for (int x = y % 2; x < shape.Width - 2; x += 2)
            {
                bool west = Coverage[Index(x, y)] != 0, south = Coverage[Index(x + 1, y + 1)] != 0;
                bool north = Coverage[Index(x + 1, y - 1)] != 0, east = Coverage[Index(x + 2, y)] != 0;
                if ((north && south && !west && !east) || (west && east && !north && !south))
                {
                    Coverage[Index(x + 1, y + 1)] = 1;
                    Coverage[Index(x + 1, y - 1)] = 1;
                    Coverage[Index(x + 2, y)] = 1;
                    if (x > 1 && y > 1)
                    {
                        --x;
                        --y;
                    }
                    else if (y > 1)
                    {
                        ++x;
                        --y;
                    }
                    else if (x > 1)
                    {
                        x -= 2;
                    }
                }
            }
        }
    }
}
