using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // S02 §9 specification cases; original-game execution is separate evidence.
    private sealed class TerrainFixture
    {
        public EliminationFixture Life { get; }
        public RevelationFixture Data => Life.Data;
        public GameState World => Data.World;
        public IncidentFixture Incidents { get; }
        public MapCompatibility Compatibility { get; }
        public ScenarioTerrain Service { get; }
        public Event Entry { get; }
        public Func<int, CivilizationCreation>? Create;
        public List<int> Resets { get; } = [];
        public List<GameplayRandom> Randoms { get; } = [];
        public List<MapPoint> Tiles { get; } = [];
        public List<(int Owner, int Mode)> Maps { get; } = [];
        public List<CityTarget> Removed { get; } = [];
        public bool FailDraw;
        public TerrainFixture(RevelationFixture? data = null)
        {
            Life = new(data: data); Incidents = new(Life.Random, Data);
            Compatibility = new(World.Map);
            for (int owner = 0; owner < 8; ++owner)
            { Data.AddCity(owner, owner, new(32, 2 + 2 * owner)); World.Civilizations[owner].CityCount = 1; }
            Service = new(World, Data.Stacks, new(World, Data.Rules, Data.Stacks, Life.Loss, Removed.Add),
                Life.Elimination, Incidents.Incidents, owner =>
                {
                    Resets.Add(owner);
                    if (Create is not null) { return Create(owner); }
                    var random = CreationRandom(owner == 0 || World.Globals.HumanPlayers.Contains(owner) ? [] : CreationAttitudes());
                    Randoms.Add(random); return Creation(Data, owner, random);
                }, new ScenarioTerrainEncoding(World.Map, Compatibility),
                point => { Tiles.Add(point); if (FailDraw) { throw new IOException("View unavailable"); } },
                (owner, mode) => { Maps.Add((owner, mode)); if (FailDraw) { throw new IOException("View unavailable"); } });
            Entry = World.Events.Collection.Append(); Rectangle(new(10, 8), new(10, 8));
        }
        public void Rectangle(MapPoint first, MapPoint last)
        {
            Entry.Values.TerrainRectangle[0] = first;
            Entry.Values.TerrainRectangle[1] = new(last.X, -100);
            Entry.Values.TerrainRectangle[2] = new(-100, -100);
            Entry.Values.TerrainRectangle[3] = new(-100, last.Y);
        }
        public void Empty() => Rectangle(new(2, 0), new(0, 0));
        public void NoCity(int owner) { World.Entities.Cities.Storage[owner].Token = default; World.Civilizations[owner].CityCount = 0; }
        public void Finish(ScenarioTerrain.Operation op, int expected)
        {
            for (int i = 0; i < 50 && op.Result is null; ++i) { Equal(null, op.Resume()); }
            Equal(expected, op.Result); Life.Random.RequireChoicesConsumed();
            foreach (var random in Randoms) { random.RequireChoicesConsumed(); }
        }
    }
    private static void ScenarioTerrainReplacementAndRectangle()
    {
        var f = new TerrainFixture(); f.Rectangle(new(10, 8), new(12, 10));
        f.Entry.Values.TerrainType = 0x17f;
        foreach (ref var tile in f.World.Map.Tiles)
        { tile.River = true; tile.ResourceSuppressed = true; tile.Features = MapFeatures.Road | MapFeatures.Pollution; tile.Claimant = 6; tile.Fertility = 13; tile.RevealedTo = CivilizationSet.Only(3); }
        // Restore city features; their radii are well outside this rectangle.
        for (int owner = 0; owner < 8; ++owner) { f.World.Map.TileAt(new(32, 2 + 2 * owner)).Features = MapFeatures.CitySite; }
        for (int owner = 1; owner < 8; ++owner) { f.World.Map.RememberedLayer(owner).Fill(MapFeatures.Road); }
        f.World.Map.RememberedBarbarian = Enumerable.Repeat(MapFeatures.Road, f.World.Map.Tiles.Length).ToArray();
        using var op = f.Service.Begin(f.Entry, "terrain"); f.Finish(op, 9);
        Sequence(new[] { new MapPoint(10, 8), new(10, 9), new(10, 10), new(11, 8), new(11, 9), new(11, 10), new(12, 8), new(12, 9), new(12, 10) }, f.Tiles);
        foreach (var point in f.Tiles)
        {
            var tile = f.World.Map.TileAt(point);
            Equal((Terrain)15, tile.Terrain); Equal(false, tile.River); Equal(true, tile.ResourceSuppressed);
            Equal(MapFeatures.None, tile.Features); Equal((byte)0, tile.Claimant); Equal((byte)0, tile.Region);
            Equal((byte)13, tile.Fertility); Equal(CivilizationSet.Only(3), tile.RevealedTo);
            Equal((byte)0x70, f.Compatibility.Export(f.World.Map, f.World.Map.Geometry.TileIndex(point)!.Value).TerrainFlags);
            for (int owner = 1; owner < 8; ++owner) { Equal(MapFeatures.None, f.World.Map.Remembered(owner, point)); }
            Equal(MapFeatures.Road, f.World.Map.Remembered(0, point));
        }
        Equal(true, f.World.Map.TileAt(new(14, 8)).River); Equal(0, f.Resets.Count);
        // Replacement clears old opaque/river/resource bits, rather than OR-ing them.
        f.Entry.Values.TerrainType = (int)Terrain.Plains;
        using var clear = f.Service.Begin(f.Entry, "clear"); f.Finish(clear, 9);
        Equal((byte)0, f.Compatibility.Export(f.World.Map, f.World.Map.Geometry.TileIndex(new(10, 8))!.Value).TerrainFlags);
        Equal(false, f.World.Map.TileAt(new(10, 8)).ResourceSuppressed);
    }
    private static void ScenarioTerrainDeletionOrder()
    {
        var f = new TerrainFixture(); var point = new MapPoint(10, 8);
        f.Data.AddCity(8, 1, point); f.World.Civilizations[1].CityCount = 2;
        f.Data.AddUnit(0, 1, 4, point); f.Data.AddUnit(1, 1, 4, point);
        f.Data.AddUnit(2, 1, 4, new(14, 8)); f.Data.Unit(2).Home = new(8);
        f.Entry.Values.TerrainType = (int)Terrain.Ocean;
        using var op = f.Service.Begin(f.Entry, "terrain"); f.Finish(op, 1);
        Equal(false, f.World.Entities.Cities.Storage[8].Token.Occupied); Equal(new CitySlot(8), f.Removed.Single().Slot);
        Equal(false, f.Data.Unit(2).Token.Occupied); Equal(false, f.Data.Unit(1).Token.Occupied);
        Equal(true, f.Data.Unit(0).Token.Occupied); // Next is queried after deletion clears the link.
        Equal(point, f.Data.Unit(0).Position); Equal(MapFeatures.None, f.World.Map.TileAt(point).Features);
        Equal(Terrain.Ocean, f.World.Map.TileAt(point).Terrain); Equal(0, f.Resets.Count);
        Sequence(new[] { "draw", "unit:10" }, f.Life.Order);
        f = new(); f.Rectangle(new(30, 2), new(30, 2)); f.Entry.Values.TerrainType = (int)Terrain.Ocean;
        using var radius = f.Service.Begin(f.Entry, "radius"); f.Finish(radius, 1);
        Equal((byte)1, f.World.Map.TileAt(new(30, 2)).Claimant); // Live cities reapply claims even on ocean.
    }
    private static void ScenarioTerrainCitylessEmptyRectangle()
    {
        var f = new TerrainFixture(); f.Empty();
        for (int owner = 0; owner < 8; ++owner) { f.NoCity(owner); }
        f.Data.AddUnit(0, 2, 4, new(10, 8)); f.Data.AddUnit(1, 2, 4, new(12, 8));
        f.World.Globals.CivsAlive = default;
        foreach (ref var tile in f.World.Map.Tiles) { tile.RevealedTo = CivilizationSet.Only(0).With(1).With(2).With(3).With(4).With(5).With(6).With(7); }
        f.World.Map.TileAt(new(-1, 0)).RevealedTo = CivilizationSet.Only(0).With(1).With(2).With(3).With(4).With(5).With(6).With(7); // Shared W01 fallback, rectangular miss.
        using var op = f.Service.Begin(f.Entry, "empty"); f.Finish(op, 0);
        Sequence(Enumerable.Range(0, 8), f.Resets); Equal(8, f.Maps.Count);
        Equal(true, f.Maps.All(x => x == (1, 1))); Equal(EndReason.Defeat, f.World.PendingEndReason);
        Equal(true, f.World.Globals.HumanPlayers.Contains(1)); Equal(0, f.World.Entities.Units.End);
        Sequence(new[] { "unit:12", "unit:10" }, f.Life.Order);
        Equal(true, f.World.Map.Tiles.ToArray().All(t => t.RevealedTo.Count == 0));
        Equal(8, f.World.Map.TileAt(new(-1, 0)).RevealedTo.Count); Equal(0, f.Tiles.Count);
    }
    private static void ScenarioTerrainRetainedReset()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = new TerrainFixture(); var grants = new RestartGrantFixture(f.Data);
            // The Library replaces slot zero; retain a separate barbarian city.
            f.Data.AddCity(8, 0, new(32, 0)); f.NoCity(2); f.Empty();
            f.Create = owner => { Equal(2, owner); return grants.Create(); };
            f.Data.AddUnit(0, 2, 4, new(10, 8));
            using var op = f.Service.Begin(f.Entry, "retained"); grants.Pending(op.Resume());
            Equal(false, f.Data.Unit(0).Token.Occupied); Equal(0, f.Maps.Count); Equal(null, op.Result);
            Equal(op.Pending, op.Resume());
            if (dispose) { op.Dispose(); Throws<ObjectDisposedException>(() => op.Resume(new(1))); Equal(0, f.Maps.Count); }
            else { Equal(null, op.Resume(new(1))); Equal(0, op.Result); Equal(1, f.Maps.Count); Equal(Government.Anarchy, f.World.Civilizations[1].Government); }
            grants.Random.RequireChoicesConsumed();
        }
    }
    private static void ScenarioTerrainRetainedDeletion()
    {
        var f = new TerrainFixture(); var grants = new RestartGrantFixture(f.Data);
        f.Data.AddCity(8, 0, new(32, 0)); f.NoCity(2);
        f.Life.Recreate = owner => { Equal(2, owner); return grants.Create(); };
        f.Data.AddUnit(0, 2, 0, new(10, 8)); f.World.Civilizations[2].UnitsActive[0] = 1;
        f.Entry.Values.TerrainType = (int)Terrain.Ocean;
        using var op = f.Service.Begin(f.Entry, "delete"); grants.Pending(op.Resume());
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal(0, f.Tiles.Count);
        Equal(Terrain.Grassland, f.World.Map.TileAt(new(10, 8)).Terrain);
        Equal(0, f.Resets.Count); Equal(1, f.Life.Restarts); Equal(op.Pending, op.Resume());
        op.Dispose(); Throws<ObjectDisposedException>(() => op.Resume(new(1)));
        Equal(0, f.Tiles.Count); grants.Random.RequireChoicesConsumed();
    }
    private static void ScenarioTerrainBoundsAndFailures()
    {
        var f = new TerrainFixture(); f.Rectangle(new(int.MaxValue, 0), new(int.MaxValue, 0));
        using var endless = f.Service.Begin(f.Entry, "wrap"); Equal(null, endless.Resume()); Equal(null, endless.Result);
        Equal(0, f.Tiles.Count); endless.Dispose(); Throws<ObjectDisposedException>(() => endless.Resume());
        f = new(); f.Rectangle(new(int.MaxValue, 1), new(int.MaxValue, 0));
        using var emptyRows = f.Service.Begin(f.Entry, "wrap-empty"); Equal(null, emptyRows.Resume()); Equal(null, emptyRows.Result);
        Equal(0, f.Tiles.Count); emptyRows.Dispose(); Throws<ObjectDisposedException>(() => emptyRows.Resume());
        f = new(); f.World.Globals.Multiplayer = GameMode.Tcp;
        using var remote = f.Service.Begin(f.Entry, "remote"); Throws<InvalidOperationException>(() => remote.Resume()); Equal(0, f.Tiles.Count);
        f = new(); f.FailDraw = true;
        using var cosmetic = f.Service.Begin(f.Entry, "cosmetic"); f.Finish(cosmetic, 1); Equal(1, cosmetic.PresentationFailures.Count);
        f = new(); f.Data.AddUnit(0, 1, 4, new(10, 8));
        f.World.Map.Allocate(new(40, 24)); f.World.Map.TileAt(new(10, 8)).Terrain = Terrain.Grassland;
        using var stale = f.Service.Begin(f.Entry, "stale"); Throws<ArgumentException>(() => stale.Resume());
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal(Terrain.Grassland, f.World.Map.TileAt(new(10, 8)).Terrain);
        Throws<InvalidOperationException>(() => stale.Resume());
    }
    private static void ScenarioTerrainPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(255)); var f = new TerrainFixture(new(runtime.Process, runtime.World));
        var grants = new RestartGrantFixture(f.Data); f.Data.AddCity(8, 0, new(32, 0)); f.NoCity(2); f.Empty();
        f.Create = owner => { Equal(2, owner); return grants.Create(); }; ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.terrain", "Change terrain", AccessClass.Player, new([]), new([new("result", ValueKind.Integer)])),
            (_, _, _) => new(), _ => new ScenarioTerrainOperation(f.Service.Begin(f.Entry, "terrain"), _ => recipient)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 1); recipient = human.Identity.Participant;
        var command = actor.Submit(new("test.terrain", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(human, command); Equal(false, actor.Reply(prompt, 1).Accepted); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(true, human.Reply(prompt, 1).Accepted); runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
        Equal(false, human.Reply(prompt, 1).Accepted); Equal(1, f.Maps.Count); grants.Random.RequireChoicesConsumed();
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var cancel = new TerrainFixture(); var research = new RestartGrantFixture(cancel.Data);
            cancel.Data.AddCity(8, 0, new(32, 0)); cancel.NoCity(2); cancel.Empty();
            cancel.Create = _ => research.Create(); cancel.FailDraw = true;
            var context = new OperationContext(cancel.World, cancel.Data.Process, research.Random, new PresentationRandom(1, "terrain"), new(default, new(1)), default, (_, _) => { }, () => { });
            using var adapter = new ScenarioTerrainOperation(cancel.Service.Begin(cancel.Entry, "cancel"), c => new((ulong)c));
            Equal(CommandStatus.AwaitingInteraction, adapter.Resume(context, new()).Status);
            var result = adapter.Resume(context, new(input)); Equal(CommandStatus.Failed, result.Status);
            Equal("scenario_terrain_presentation_failed", result.Code); Equal(1, cancel.Maps.Count);
            Equal(Government.Despotism, cancel.World.Civilizations[1].Government);
            Equal(result, adapter.Resume(context, new(input))); research.Random.RequireChoicesConsumed();
        }
    }
}
