using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
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

    private readonly IPlatformClock clock;

    public SeamSession(IPlatformClock? clock = null)
    {
        this.clock = clock ?? new SystemPlatformClock();
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
                RevealedTo = new CivilizationSet().With(y < 19 && (x < 23 - y / 2 || x > 51) ? 1 : 2),
                Owner = null
            };
            if (y == 4 && x is >= 4 and <= 16)
            {
                tile.Features = MapFeatures.Road | (x is 6 or 8 ? MapFeatures.Irrigation : MapFeatures.None);
            }

            memory[i] = tile.Features;
            map.Tiles[i] = tile;
        }
        // W01 diagonal contacts form a continuous river. Its last land tile
        // meets the western gulf across the horizontal seam at (55, 11).
        AddRiver([new(14, 2), new(13, 3), new(12, 4), new(11, 5), new(10, 6), new(9, 7),
            new(8, 8), new(7, 9), new(6, 8), new(5, 7), new(4, 8), new(3, 9), new(2, 10), new(1, 11), new(0, 10)]);
        AddRiver([new(5, 3), new(6, 4), new(7, 5), new(8, 6)]);

        world.Entities.Units.SetEnd(7);
        world.Globals.UnitEnd = 7;
        world.Globals.ActiveUnit = new UnitSlot(1);
        for (int i = 0; i < 3; i++)
        {
            AddUnit(i, 1, new(2, 2), i + 1, i - 1, i >= 2 ? -1 : i + 1, false);
        }

        AddUnit(3, 2, new(2, 2), 0, -1, -1, false); // Concealed on an explored tile.
        AddUnit(4, 2, new(12, 6), 5, -1, -1, true); // Independently seen by the observer.
        AddUnit(5, 2, new(29, 9), 4, -1, -1, true); // Seen bit alone cannot expose an unexplored tile.
        AddUnit(6, 2, new(54, 4), 2, -1, -1, false); // Concealed across the wrapped edge.
        map.TileAt(new(2, 2)).Terrain = Terrain.Grassland;
        map.TileAt(new(2, 2)).Features = MapFeatures.Unit;
        map.TileAt(new(2, 2)).Owner = 1;
        world.Entities.Cities.SetEnd(2);
        world.Globals.CityEnd = 2;
        world.Entities.Cities.Storage[0] = new()
        { Token = new(200), Owner = new(1), Size = 3, Name = "Riverbend", Position = new(6, 4) };
        world.Entities.Cities.Storage[1] = new()
        { Token = new(201), Owner = new(2), Size = 5, Name = "Concealed", Position = new(12, 8) };
        map.TileAt(new(6, 4)).Features |= MapFeatures.CitySite;
        map.TileAt(new(6, 4)).Owner = 1;
        map.TileAt(new(12, 8)).Features |= MapFeatures.CitySite;
        map.TileAt(new(12, 8)).Owner = 2;
        map.Remembered(1, new(12, 8)) |= MapFeatures.CitySite;

        void AddRiver(ReadOnlySpan<MapPoint> course)
        {
            foreach (var point in course)
            {
                map.TileAt(point).River = true;
            }
        }

        void AddUnit(int slot, byte owner, MapPoint position, int type, int previous, int next, bool seen)
        {
            world.Entities.Units.Storage[slot] = new()
            { Token = new((uint)slot + 100), Owner = new(owner), Type = new((byte)type), Visibility = new CivilizationSet().With(1, seen), Previous = (((short)previous) < 0 ? (UnitSlot?)null : new UnitSlot(unchecked((ushort)((short)previous)))), Next = (((short)next) < 0 ? (UnitSlot?)null : new UnitSlot(unchecked((ushort)((short)next)))), Position = position };
        }
    }

    public void Tick()
    {
        Runtime.ExpireInteractions(clock.Milliseconds);
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
