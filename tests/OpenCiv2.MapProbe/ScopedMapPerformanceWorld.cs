using OpenCiv2.App;
using OpenCiv2.App.Map;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

// Shared authored workload, projected through the production scoped client.
internal sealed class ScopedMapPerformanceWorld : IDisposable
{
    private static MapFeatures FeaturesFor(MapFeatures improvements)
    {
        var result = MapFeatures.None;
        if (improvements.HasFlag(MapFeatures.CitySite)) result |= MapFeatures.CitySite;
        if (improvements.HasFlag(MapFeatures.Irrigation)) result |= MapFeatures.Irrigation;
        if (improvements.HasFlag(MapFeatures.Mine)) result |= MapFeatures.Mine;
        if (improvements.HasFlag(MapFeatures.Road)) result |= MapFeatures.Road;
        if (improvements.HasFlag(MapFeatures.Railroad)) result |= MapFeatures.Railroad;
        if (improvements.HasFlag(MapFeatures.Fortification)) result |= MapFeatures.Fortification;
        if (improvements.HasFlag(MapFeatures.Pollution)) result |= MapFeatures.Pollution;
        return result;
    }

    private readonly SeamSession session = new();
    private readonly MapObservation author = MapGallery.Open("Mixed").Map;
    private readonly IPlayerClient second;
    private bool other;

    public ScopedMapPerformanceWorld()
    {
        second = session.Runtime.Bind(session.Runtime.AddParticipant(new(2)), ControllerKind.Frontend);
        Restart();
    }

    public void Restart()
    {
        var world = session.Runtime.World;
        var map = world.Map;
        map.Allocate(new(252, 252));
        map.PatternSeed = 47;
        var memoryA = map.RememberedLayer(1);
        var memoryB = map.RememberedLayer(2);
        for (int i = 0; i < map.Geometry.Area; i++)
        {
            var source = author.Tiles[i];
            map.Tiles[i] = new Tile
            {
                Terrain = (Terrain)(source.Terrain ?? 2),
                RevealedTo = new CivilizationSet().With(2).With(1, source.Terrain is not null),
                River = source.River,
                Features = FeaturesFor(source.Improvements),
                Owner = null
            };
            memoryA[i] = memoryB[i] = FeaturesFor(source.Improvements);
        }
        world.Entities.Units.SetEnd(2048);
        world.Entities.Cities.SetEnd(256);
        var rules = session.Runtime.Process.Rules;
        for (int i = 0; i < rules.Definitions.Units.Length; i++)
            rules.Definitions.Units[i] = new()
            {
                Identity = new("authored", RuleText.Empty),
                HitPoints = 30,
                Role = i < 2 ? RuleRole.Settle : RuleRole.Attack,
                Domain = i % 4 == 0 ? RuleDomain.Air : RuleDomain.Land
            };
        world.Globals.Rules = GameRules.Normal;
        for (int i = 0; i < 2048; i++)
        {
            var p = map.Geometry.Coordinates((64 + i * 101) % map.Geometry.Area);
            world.Entities.Units.Storage[i] = new()
            {
                Token = new((uint)i + 1),
                Owner = new((byte)(i % 8)),
                Type = new((byte)(i % 63)),
                Position = p,
                Visibility = new CivilizationSet().With(1).With(2),
                Damage = (byte)(i % 31),
                Orders = i % 5 == 0 ? UnitOrder.Fortified : i % 7 == 0 ? UnitOrder.Sleeping : UnitOrder.None
            };
        }
        for (int i = 0; i < 256; i++)
        {
            var p = map.Geometry.Coordinates((96 + i * 173) % map.Geometry.Area);
            world.Entities.Cities.Storage[i] = new()
            {
                Token = new((uint)i + 1),
                Owner = new((byte)(i % 8)),
                Position = p,
                Size = (sbyte)(i % 23),
                Name = $"Harbor {i + 1}"
            };
        }
        world.Globals.ActiveUnit = new(1);
        other = false;
        session.Runtime.Publish();
    }

    public MapObservation Capture()
    {
        var observed = (other ? second : session.Player).ObserveMap()!;
        var tiles = observed.Tiles.ToArray();
        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i].Terrain is null || author.Tiles[i].Terrain is null) continue;
            tiles[i] = tiles[i] with { Hut = author.Tiles[i].Hut, AirbaseOccupant = author.Tiles[i].AirbaseOccupant };
        }
        // A named authored provider supplies independently published remembered
        // city appearance and routes. Both are filtered against this view's tiles.
        var remembered = author.Cities.Where(city => city.Details?.Owner != observed.Identity.Observer &&
            tiles[city.Position.Y * (observed.Width / 2) + city.Position.X / 2].Terrain is not null);
        return observed with
        {
            Tiles = new(tiles),
            Cities = new(observed.Cities.Concat(remembered)),
            Annotations = observed.Annotations! with { Paths = author.Annotations!.Paths }
        };
    }

    public void Switch(int step)
    {
        other = (step & 1) != 0;
        var world = session.Runtime.World;
        world.Globals.Rules = GameRules.Normal with { FlatWorld = step % 3 == 0 };
        session.Runtime.Publish();
    }

    public void Change(int iteration)
    {
        var world = session.Runtime.World;
        int x = (iteration & 1) == 0 ? 62 : 64;
        ref var tile = ref world.Map.TileAt(new(x, 108));
        tile.Terrain = (Terrain)(((int)tile.Terrain + 1) % 11);
        ref var memory = ref world.Map.Remembered(1, new(x, 108));
        memory ^= MapFeatures.Road;
        ref var adjacent = ref world.Map.Remembered(1, new(x + 2, 108));
        adjacent ^= MapFeatures.Railroad;
        ref var unit = ref world.Entities.Units.Storage[1];
        unit.Position = new(x, 108);
        unit.Damage = (byte)(iteration % 31);
        unit.Orders = (iteration & 1) == 0 ? UnitOrder.Sleeping : UnitOrder.GoTo;
        ref var city = ref world.Entities.Cities.Storage[1];
        city.Position = new(66, 110);
        city.Size = (sbyte)(iteration % 23);
        city.Improvements = city.Improvements.With(8, !city.Improvements.Contains(8));
        city.Name = $"Harbor {iteration}";
        session.Runtime.Process.Rules.Leaders[0].Color = (short)(iteration % 8);
        session.Runtime.Publish();
    }

    public void Dispose() => session.Dispose();
}
