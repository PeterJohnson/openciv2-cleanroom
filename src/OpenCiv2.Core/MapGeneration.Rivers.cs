using OpenCiv2.Contracts;

namespace OpenCiv2.Core;

internal sealed partial class MapGeneration
{
    internal void Rivers()
    {
        int accepted = 0, attempts = 0;
        do
        {
            ++attempts;
            if (RiverAttempt(attempts).Accepted)
            {
                ++accepted;
            }
        } while (attempts < 1024 && accepted < 2 * landMass + 2 * climate + 12);
    }

    internal (bool Accepted, int Length) RiverAttempt(int attempt)
    {
        // New-map rivers run before any non-terrain tile data changes. Restoring the owned
        // snapshot therefore restores precisely the changed terrain, without callbacks.
        var backup = map.Tiles.ToArray();
        int length = 0;
        MapPoint point;
        Tile terrain;
        do
        {
            int y = Pick("river.start-y", shape.Height);
            point = new(2 * Pick("river.start-x", shape.Width / 2) + y % 2, y);
            terrain = map.TileAt(point);
        } while (Plain(terrain, Terrain.Mountains) || Plain(terrain, Terrain.Ocean));
        int direction = Pick("river.direction", 4);
        bool reachedOcean;
        do
        {
            Terrain kind = terrain.Terrain;
            if (Plain(terrain, kind))
            {
                kind = kind switch
                {
                    Terrain.Desert => Pick("river.desert", 10) >= 2 || length > 3
                        ? Terrain.Grassland : Terrain.Plains,
                    Terrain.Plains => Pick("river.plains", 10) >= 4 ? Terrain.Grassland : Terrain.Plains,
                    Terrain.Forest => Pick("river.forest", 10) >= 3 ? Terrain.Grassland : Terrain.Forest,
                    Terrain.Hills or Terrain.Mountains => Terrain.Grassland,
                    Terrain.Tundra => Terrain.Plains,
                    Terrain.Glacier => Terrain.Tundra,
                    Terrain.Swamp => Pick("river.swamp", 10) >= 4 ? Terrain.Jungle : Terrain.Swamp,
                    _ => kind
                };
                terrain.Terrain = kind;
            }
            terrain.River = true;
            ref var current = ref map.TileAt(point);
            current.Terrain = terrain.Terrain;
            current.River = terrain.River;
            current.ResourceSuppressed = terrain.ResourceSuppressed;
            ++length;
            reachedOcean = false;
            for (int d = 0; d < 8; d += 2)
            {
                var neighbor = Neighbor(point, MapNeighborhoods.Directions[d]);
                if (shape.InBounds(neighbor) && map.TileAt(neighbor).Ocean)
                {
                    reachedOcean = true;
                }
            }
            direction = (direction + Pick("river.turn", 2) - length % 2 + 4) % 4;
            point = Neighbor(point, MapNeighborhoods.Directions[2 * direction]);
            if (shape.TileIndex(point) is { } next)
            {
                terrain = backup[next];
            }
        } while (!reachedOcean && shape.InBounds(point) && !terrain.River);
        bool accepted = (reachedOcean || terrain.River) && length >= 5 - attempt / 800;
        if (!accepted)
        {
            backup.CopyTo(map.Tiles);
        }
        return (accepted, length);
    }
}
