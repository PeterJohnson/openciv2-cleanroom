using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Formats;
using OpenCiv2.Session;
using OpenCiv2.ViewModels;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // W01 §§2,8, W03 §1, P01/P02. Authored knowledge vectors with deliberately independent channels.
    private static void MapObserverPrivacy()
    {
        MapDeltaSafety();
        using var f = new LocalUnitCommandFixture();
        var world = f.Runtime.World;
        Equal(true, f.Client.ObserveMap()!.Tiles.All(t => t == default));
        var a = new MapPoint(2, 2);
        var b = new MapPoint(4, 2);
        world.Map.TileAt(a) = new() { Terrain = Terrain.Grassland, River = true, RevealedTo = new CivilizationSet().With(1), Owner = 2, Features = MapFeatures.Unit | MapFeatures.Mine | MapFeatures.Road | MapFeatures.Pollution };
        world.Map.TileAt(b) = new() { Terrain = Terrain.Hills, RevealedTo = new CivilizationSet().With(2) };
        world.Map.Remembered(1, a) = MapFeatures.Unit | MapFeatures.CitySite | MapFeatures.Road;
        world.Map.Remembered(1, b) = MapFeatures.Unit | MapFeatures.CitySite | MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Road | MapFeatures.Railroad | MapFeatures.Fortification | MapFeatures.Pollution;
        f.Unit(3).Visibility = StateConversion.DecodeCivilizations(2);
        world.Entities.Cities.SetEnd(2);
        world.Entities.Cities.Storage[0] = new() { Token = new(8), Owner = new(1), Size = 5, Name = "Blue", Position = new(2, 2) };
        world.Entities.Cities.Storage[1] = new() { Token = new(9), Owner = new(2), Size = 7, Name = "Red", KnownTo = StateConversion.DecodeCivilizations(2), Position = new(4, 2) };
        var first = f.Client.ObserveMap()!;
        var second = f.Other.ObserveMap()!;
        int left = world.Map.Geometry.TileIndex(a)!.Value, right = world.Map.Geometry.TileIndex(b)!.Value;
        Equal((byte)1, first.Identity.Observer.Value);
        Equal((byte)2, second.Identity.Observer.Value);
        Equal(new ObservedTile(2, MapFeatures.Road | MapFeatures.CitySite, true, 0, Geometry.GrasslandShield(a),
            MapFact<MapPresence>.Known(MapPresence.Absent), MapFact<MapTileOwnership>.Known(new(new(1)))), first.Tiles[left]);
        Equal(default(ObservedTile), first.Tiles[right]);
        Equal(default(ObservedTile), second.Tiles[left]);
        Equal((byte)Terrain.Hills, second.Tiles[right].Terrain!.Value);
        Equal(4, first.Units.Count);
        Equal(3, first.Units.Count(u => u.Target is not null));
        Equal(1, second.Units.Count);
        Equal("Blue", first.Cities.Single().Name);
        Equal("Red", second.Cities.Single().Name);

        // Changing live foreign city facts and improvements cannot update retained tile memory.
        world.Entities.Cities.Storage[1].Name = "Change";
        world.Map.TileAt(a).Features = MapFeatures.None;
        f.Unit(3).Position = f.Unit(3).Position with { X = 4 };
        world.Globals.Rules = StateConversion.DecodeGameRules(0x8000);
        f.Runtime.Publish();
        var next = f.Client.ObserveMap()!;
        Equal(first.Tiles, next.Tiles);
        Equal(3, next.Units.Count);
        Equal(4, first.Units.Count);
        Equal(true, next.Flat);
        Equal(false, first.Flat);
        Equal(true, next.Revision > first.Revision);

        var site = new MapPoint(0, 0);
        world.Map.PatternSeed = 24;
        world.Map.TileAt(site) = new() { Terrain = Terrain.Desert, ResourceSuppressed = true, RevealedTo = new CivilizationSet().With(1), Owner = null };
        f.Runtime.Publish();
        var random = f.Runtime.GameplayRandom.Capture();
        Equal(true, world.Map.Hut(site));
        var eligible = f.Client.ObserveMap()!.Tiles[0];
        Equal(MapFact<MapPresence>.Withheld, eligible.Hut);
        Equal((byte)0, eligible.Resource);
        world.Map.TileAt(site).Owner = 2;
        f.Runtime.Publish();
        Equal(false, world.Map.Hut(site));
        Equal(eligible, f.Client.ObserveMap()!.Tiles[0]);
        world.Map.PatternSeed = 16;
        world.Map.TileAt(site).ResourceSuppressed = false;
        f.Runtime.Publish();
        Equal((byte)2, f.Client.ObserveMap()!.Tiles[0].Resource);
        world.Map.TileAt(site).Terrain = Terrain.Grassland;
        f.Runtime.Publish();
        var grass = f.Client.ObserveMap()!.Tiles[0];
        Equal((byte)0, grass.Resource);
        Equal(true, grass.GrasslandShield);
        Equal(random, f.Runtime.GameplayRandom.Capture());

        f.Runtime.Revoke(f.Client.Identity.Controller);
        Throws<AccessException>(() => f.Client.ObserveMap());
        world.Map.Release();
        Equal(true, f.Other.ObserveMap() is null);
    }

    private static void MapDeltaSafety()
    {
        using var fixture = new LocalUnitCommandFixture();
        var before = fixture.Client.ObserveMap()!;
        // A live change outside this observer's revelation must leave both the
        // disclosed values and their sparse metadata unchanged.
        fixture.Runtime.World.Map.TileAt(new(0, 0)).Terrain = Terrain.Hills;
        fixture.Runtime.Publish();
        var hidden = MapObservationDelta.Attach(before, fixture.Client.ObserveMap()!);
        Equal(true, hidden.Delta!.Matches(before, hidden));
        Equal(0, hidden.Delta.ChangedIndices.Count);
        var tiles = hidden.Tiles.ToArray();
        tiles[0] = tiles[0] with { Terrain = 2 };
        tiles[^1] = tiles[^1] with { Terrain = 3 };
        var changed = MapObservationDelta.Attach(hidden, hidden with { Revision = hidden.Revision + 1, Tiles = new(tiles) });
        Equal(true, changed.Delta!.Matches(hidden, changed));
        Equal(new OwnedList<int>([0, tiles.Length - 1]), changed.Delta.ChangedIndices);
        Equal(false, changed.Delta.Matches(hidden, changed with { Tiles = new(changed.Tiles) }));
        Equal(false, changed.Delta.Matches(hidden, changed with { Identity = fixture.Other.Identity }));
        Equal(true, MapObservationDelta.Attach(hidden, changed with { Revision = hidden.Revision + 2 }).Delta is null);
        Equal(true, MapObservationDelta.Attach(hidden, changed with { Identity = fixture.Other.Identity }).Delta is null);
    }

    // P01/P05: the map target is an owned slot/token; normal request validation owns stale rejection.
    private static void MapTargetCommand()
    {
        using var f = new LocalUnitCommandFixture();
        var panel = new SessionPanel(observeMap: true);
        panel.Bind(f.Client);
        var initial = panel.View.Map!;
        var random = f.Runtime.GameplayRandom.Capture();
        var cosmetic = f.Runtime.PresentationRandom.Capture();
        var target = initial.Units[0].Target!.Value;
        panel.Refresh();
        Equal(true, ReferenceEquals(initial, panel.View.Map));
        Equal(true, panel.Submit(f.Request(target.Slot.Value, target.Token.Value)));
        f.Runtime.AdvanceOne();
        panel.Refresh();
        Equal(true, panel.Cancel());
        f.Runtime.AdvanceOne();
        panel.Refresh();
        Equal((int)CommandStatus.Cancelled, (int)panel.View.Command!.Status);
        Equal(initial.Units, panel.View.Map!.Units);

        Equal(true, panel.Submit(f.Request(target.Slot.Value, target.Token.Value)));
        f.Runtime.AdvanceOne();
        panel.Refresh();
        Equal(true, panel.Reply(1));
        Equal(false, panel.Reply(1));
        f.Runtime.AdvanceOne();
        panel.Refresh();
        Equal((ushort)1, panel.View.Map!.Units[0].Target!.Value.Slot.Value);
        Equal(target, initial.Units[0].Target!.Value);
        Equal(1, panel.View.Command!.Committed.Count);

        Equal(true, panel.Submit(f.Request(1, 101)));
        f.Runtime.AdvanceOne();
        panel.Refresh();
        f.Unit(1).Token = new(300);
        f.Runtime.Publish();
        Equal(true, panel.Reply(1));
        f.Runtime.AdvanceOne();
        panel.Refresh();
        Equal((int)CommandStatus.Failed, (int)panel.View.Command!.Status);
        Equal(0, panel.View.Command!.Committed.Count);
        Equal(random, f.Runtime.GameplayRandom.Capture());
        Equal(cosmetic, f.Runtime.PresentationRandom.Capture());

        panel.Bind(f.Other);
        Equal(f.Other.Identity, panel.View.Map!.Identity);
        Equal(1, panel.View.Map.Units.Count);
        f.Runtime.Revoke(f.Other.Identity.Controller);
        panel.Refresh();
        Equal(true, panel.View.Map is null);
    }
}
