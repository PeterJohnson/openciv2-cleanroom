using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Session;
using OpenCiv2.ViewModels;

namespace OpenCiv2.App;

/// <summary>Composition of a bounded authored W01/W03 map. The only admitted action uses the existing U01
/// command.</summary>
internal sealed class SeamSession : IDisposable
{
    internal SessionRuntime Runtime { get; } = new(new(), new(1));
    internal IPlayerClient Player { get; }
    public SessionPanel Panel { get; } = new(observeMap: true);

    public SeamSession()
    {
        Runtime.RegisterCommand(LocalUnitCommands.StackBottom());
        Runtime.BeginClientSession(new(17, 23));
        AuthorWorld(Runtime.World);
        Player = Runtime.Bind(Runtime.AddParticipant(new(1)), ControllerKind.Frontend);
        Panel.Bind(Player);
    }

    private static void AuthorWorld(GameState world)
    {
        var shape = new Geometry(56, 32);
        var map = world.Map;
        map.Allocate(shape);
        map.PatternSeed = 47;
        var memory = map.RememberedLayer(1);
        for (int i = 0; i < shape.Area; i++)
        {
            var (x, y) = shape.Coordinates(i);
            // Explicit fixture regions. This recipe is never called by a game setup or RNG path.
            int west = Math.Min(x + 7, shape.Width - x - 2);
            bool gulf = west < 4 + (y % 7) / 2 || y >= 26 - west % 5;
            bool valley = x < 16 && y is >= 1 and <= 10;
            var terrain = gulf ? Terrain.Ocean :
                valley
                    ? (x + y) % 5 == 0 ? Terrain.Forest : (x - y + 48) % 5 == 1 ? Terrain.Plains : Terrain.Grassland
                    :
                y <= 2 ? Terrain.Tundra : ((x + 7 * y) % 19) switch
                {
                    0 or 1 => Terrain.Mountains,
                    2 or 3 => Terrain.Hills,
                    4 or 5 or 6 or 7 => Terrain.Forest,
                    8 => Terrain.Swamp,
                    9 => Terrain.Desert,
                    10 or 11 => Terrain.Plains,
                    _ => Terrain.Grassland
                };
            var tile = new Tile
            {
                Terrain = terrain,
                Revelation = (byte)(y < 19 && (x < 23 - y / 2 || x > 51) ? 2 : 4),
                Owner = null
            };
            if (y == 4 && x is >= 4 and <= 16)
            {
                tile.Features = (byte)(x is 6 or 8 ? 0x14 : 0x10);
            }

            memory[i] = tile.Features;
            map.Tiles[i] = tile;
        }
        // W01 diagonal contacts form a continuous river. Its last land tile
        // meets the western gulf across the horizontal seam at (55, 11).
        AddRiver([(14, 2), (13, 3), (12, 4), (11, 5), (10, 6), (9, 7),
            (8, 8), (7, 9), (6, 8), (5, 7), (4, 8), (3, 9), (2, 10), (1, 11), (0, 10)]);
        AddRiver([(5, 3), (6, 4), (7, 5), (8, 6)]);

        world.Entities.Units.SetEnd(7);
        world.Globals.UnitEnd = 7;
        world.Globals.ActiveUnit = new(1);
        for (int i = 0; i < 3; i++)
        {
            AddUnit(i, 1, 2, 2, i + 1, i - 1, i >= 2 ? -1 : i + 1, 0);
        }

        AddUnit(3, 2, 2, 2, 0, -1, -1, 0); // Concealed on an explored tile.
        AddUnit(4, 2, 12, 6, 5, -1, -1, 2); // Independently seen by the observer.
        AddUnit(5, 2, 29, 9, 4, -1, -1, 2); // Seen bit alone cannot expose an unexplored tile.
        AddUnit(6, 2, 54, 4, 2, -1, -1, 0); // Concealed across the wrapped edge.
        map.TileAt(new(2, 2)).Terrain = Terrain.Grassland;
        map.TileAt(new(2, 2)).Features = 1;
        map.TileAt(new(2, 2)).Owner = 1;
        world.Entities.Cities.SetEnd(2);
        world.Globals.CityEnd = 2;
        world.Entities.Cities.Storage[0] = new()
        {
            Token = new(200),
            Owner = new(1),
            X = 6,
            Y = 4,
            Size = 3,
            Name = new("Riverbend"u8.ToArray())
        };
        world.Entities.Cities.Storage[1] = new()
        {
            Token = new(201),
            Owner = new(2),
            X = 12,
            Y = 8,
            Size = 5,
            Name = new("Concealed"u8.ToArray())
        };
        map.TileAt(new(6, 4)).Features |= 2;
        map.TileAt(new(6, 4)).Owner = 1;
        map.TileAt(new(12, 8)).Features |= 2;
        map.TileAt(new(12, 8)).Owner = 2;
        map.Remembered(1, new(12, 8)) |= 2;

        void AddRiver(ReadOnlySpan<(int X, int Y)> course)
        {
            foreach (var (x, y) in course)
            {
                map.TileAt(new(x, y)).TerrainFlags |= 0x80;
            }
        }

        void AddUnit(int slot, byte owner, short x, short y, int type, int previous, int next, byte seen)
        {
            world.Entities.Units.Storage[slot] = new()
            {
                Token = new((uint)slot + 100),
                Owner = new(owner),
                Type = new((byte)type),
                X = x,
                Y = y,
                Visibility = seen,
                Previous = new((short)previous),
                Next = new((short)next)
            };
        }
    }

    public void Tick()
    {
        if (Runtime.AdvanceOne())
        {
            Panel.Refresh();
        }

        if (Panel.View.Command is { } command && command.Status.Terminal())
        {
            Runtime.RetireCommand(command.Correlation.Command);
        }
    }

    public void Dispose()
    {
        Panel.Bind(null);
        Runtime.Dispose();
    }
}
