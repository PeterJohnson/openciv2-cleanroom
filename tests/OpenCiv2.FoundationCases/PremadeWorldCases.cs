using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.IO;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F05 §3/F06 §4/W01: authored bytes and specification expectations, no capture/harness inputs.
    private static byte[] PremadeBytes(ushort seed = 24, ushort topology = 0xfffe, bool hints = true)
    {
        var w = new ByteWriter(); w.Append(ClassicRecords.Encode(new MapHeader { Width = 4, Height = 2, Area = 99, Topology = topology, Seed = seed, CoarseWidth = 77, CoarseHeight = 88 }));
        for (int i = 0; i < 21; ++i) w.I16((short)(hints && i == 0 ? 2 : hints && i == 20 ? 300 : -1));
        for (int i = 0; i < 21; ++i) w.I16((short)(hints && i == 0 ? 1 : hints && i == 20 ? -7 : -1));
        w.Append([0xc2, 0xff, 0xff, 61, 0x55, 0x73, 0x8a, 0x42, 0x45, 62, 0xaa, 0xf7, 0x19, 0x81, 0x20, 63, 0xff, 0xe9, 0xf0, 0x55, 0x9b, 3, 1, 0x21]); return w.ToArray();
    }
    private static OwnedList<byte> PremadeTiles(MapStorage map, MapCompatibility? compatibility = null) => new(
        Enumerable.Range(0, map.Tiles.Length).Select(i => compatibility is not null && compatibility.BelongsTo(map)
            ? compatibility.Export(map, i) : StateConversion.ToStored(map.Tiles[i])).SelectMany(ClassicRecords.Encode));
    private sealed class PremadeSource(Func<string, PremadeWorldInput?> load) : IPremadeWorldSource { public PremadeWorldInput? Load(string path) => load(path); }
    private sealed class PremadeFixture : IDisposable
    {
        public readonly SessionRuntime Runtime = new(new(), new(93));
        public StartingPositions Starts = new();
        public byte[] Bytes = PremadeBytes();
        public bool Open = true, CloseOk = true;
        public int Position, Closes, SeedResponse = 1, HintResponse = 1;
        public uint Tick = 0x12345678;
        public Action<string>? Observe;
        public List<string> Trace = [];
        public CallbackStorage Storage = new();
        public PremadeWorldServices Services;
        public PremadeFixture()
        {
            var map = Runtime.World.Map; map.Allocate(new(8, 4)); map.PatternSeed = 55; map.Regions[3] = new(10, 20, 30, StateConversion.DecodeRegions(0x12345678)); map.RememberedBarbarian = [MapFeatures.Unit | MapFeatures.CitySite | MapFeatures.Irrigation, MapFeatures.Mine, MapFeatures.Unit | MapFeatures.Mine]; map.Tiles[0].Region = 17; map.Tiles[0].Features = MapFeatures.CitySite | MapFeatures.Fortification;
            for (int civ = 1; civ < 8; ++civ) map.RememberedLayer(civ).Fill(MapFeatures.Unit | MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Fortification);
            for (int grid = 0; grid < 4; ++grid) map.CoarseGrid(grid).Fill(42);
            Runtime.Process.Fallback.Value = StateConversion.ToLive(ClassicRecords.DecodeTile([0x82, 0xff, 0xe1, 17, 15, 0x32]));
            for (int i = 0; i < 21; ++i) { Starts.Positions[i] = Starts.Positions[i] with { X = 0x1234 }; Starts.Positions[i] = Starts.Positions[i] with { Y = 0x5678 }; }
            Runtime.World.WorldSetup.Width = 99; Runtime.World.WorldSetup.Height = 88; for (int i = 0; i < 5; ++i) Runtime.World.WorldSetup.Parameters[i] = i + 1;
            Runtime.World.Globals.Status = new GameStatus { AiCoordination = true, Won = true, LargeMap = true, SmallMap = true, Cheated = true, Scored = true, ScenarioExport = true, Scenario = true }; Runtime.World.Globals.Rules = StateConversion.DecodeGameRules(0x1555);
            Storage.ReadFile = path =>
            {
                Equal("authored.mp", path); Event("open"); if (!Open) return null; Position = 0;
                return new CallbackInput(size => { Equal(65536, size); Event("read"); int n = Math.Min(size, Bytes.Length - Position); var part = Bytes.AsSpan(Position, n).ToArray(); Position += n; return new(new(part), n == size); }, () => { ++Closes; Event("close"); return CloseOk; });
            };
            Services = new(new ClassicPremadeWorldSource(Storage), () => Event("failure"), new(() => { Event("seed"); return SeedResponse; }, () => { Event("hints"); return HintResponse; }, () => { Event("ticks"); return Tick; }));
        }
        private void Event(string name) { Trace.Add(name); Observe?.Invoke(name); }
        public WorldSource? Load() => PremadeWorld.Load(Runtime, ref Starts, "authored.mp", Services);
        public void ReadWithoutPreparation()
        { var input = Services.Source.Load("authored.mp")!; input.Install(Runtime.World.Map); Starts = input.Starts; Trace.Clear(); }
        public void Dispose() => Runtime.Dispose();
    }
    private static void PremadeInstallationAndRetention()
    {
        using var f = new PremadeFixture(); var map = f.Runtime.World.Map; var fallback = f.Runtime.Process.Fallback.Value; var regions = new OwnedList<RegionSummary>(map.Regions.ToArray()); var preferences = f.Runtime.Process.OrdinaryPreferences;
        f.Bytes = [.. f.Bytes, 0xde, 0xad]; var original = new OwnedList<byte>(f.Bytes); Equal((int)WorldSource.PremadeFlat, (int)f.Load()!.Value);
        Equal(f.Bytes.Length, f.Position); Equal(1, f.Closes); Equal(new OwnedList<string>(["open", "read", "close", "seed", "hints"]), new(f.Trace)); Equal(4, map.Geometry.Width); Equal(2, map.Geometry.Height); Equal((ushort)24, map.PatternSeed); Equal((ushort)0xfffe, map.StoredTopology);
        Equal(new OwnedList<byte>([0xc2, 0, 0, 0, 0, 0xf0, 0x8a, 0, 0, 0, 0, 0xf0, 0x19, 0, 0, 0, 0, 0xf0, 0xf0, 0, 0, 0, 0, 0xf0]), PremadeTiles(map, ((ClassicPremadeWorldSource)f.Services.Source).MapBytes));
        Equal(regions, new(map.Regions.ToArray())); Equal(fallback, f.Runtime.Process.Fallback.Value); Equal(new OwnedList<MapFeatures>([MapFeatures.Unit | MapFeatures.CitySite | MapFeatures.Irrigation, MapFeatures.Mine, MapFeatures.Unit | MapFeatures.Mine]), new(map.RememberedBarbarian));
        for (int civ = 1; civ < 8; ++civ) Equal(true, map.RememberedLayer(civ).ToArray().All(v => v == 0));
        for (int grid = 0; grid < 4; ++grid) Equal(true, map.CoarseGrid(grid).ToArray().All(v => v is null));
        Equal((short)2, f.Starts.Positions[0].X); Equal((short)1, f.Starts.Positions[0].Y); Equal((short)300, f.Starts.Positions[20].X); Equal((short)-7, f.Starts.Positions[20].Y); Equal(StateConversion.DecodeGameRules((ushort)0x9555), f.Runtime.World.Globals.Rules); Equal(new GameStatus { AiCoordination = true, Won = true, LargeMap = true, SmallMap = true, Cheated = true, Scored = true, ScenarioExport = true, Scenario = true }, f.Runtime.World.Globals.Status); Equal((short)4, f.Runtime.World.WorldSetup.Width); Equal((short)2, f.Runtime.World.WorldSetup.Height); SetupParametersEqual([1, 2, 3, 4, 5], f.Runtime); Equal(preferences, f.Runtime.Process.OrdinaryPreferences); Equal(original, new(f.Bytes)); Equal(false, f.Runtime.Active);
        f.Runtime.World.Globals.Status = new GameStatus { Won = true, LargeMap = true }; f.Bytes = PremadeBytes(99, 0, false); Equal((int)WorldSource.PremadeCylindrical, (int)f.Load()!.Value); Equal(StateConversion.DecodeGameRules((ushort)0x1555), f.Runtime.World.Globals.Rules); Equal(new GameStatus { Won = true, LargeMap = true }, f.Runtime.World.Globals.Status); Equal(new StartingPositions(), f.Starts); SetupParametersEqual([1, 2, 3, 4, 5], f.Runtime); Equal(regions, new(map.Regions.ToArray()));
    }
    private static void PremadeNormalizationAndHintScan()
    {
        foreach (int slot in new[] { -1, 0, 20 })
        {
            using var f = new PremadeFixture(); f.ReadWithoutPreparation(); var map = f.Runtime.World.Map; var setup = f.Runtime.World.WorldSetup;
            for (int civ = 1; civ < 8; ++civ) map.RememberedLayer(civ).Fill(StateConversion.DecodeMapFeatures((byte)civ));
            for (int grid = 0; grid < 4; ++grid) map.CoarseGrid(grid)[0] = (byte)(grid + 10);
            for (int i = 0; i < 21; ++i) { f.Starts.Positions[i] = f.Starts.Positions[i] with { X = -2 }; f.Starts.Positions[i] = f.Starts.Positions[i] with { Y = 30000 }; }
            if (slot >= 0) f.Starts.Positions[slot] = f.Starts.Positions[slot] with { X = 0 }; var starts = f.Starts; f.HintResponse = 0;
            Equal((int)WorldSource.PremadeFlat, (int)PremadeWorld.Prepare(f.Runtime, ref f.Starts, f.Services.Choices)); Equal(slot < 0 ? starts : new StartingPositions(), f.Starts);
            Equal(slot < 0 ? new OwnedList<string>(["seed"]) : new(["seed", "hints"]), new(f.Trace));
            for (int civ = 1; civ < 8; ++civ) Equal(true, map.RememberedLayer(civ).ToArray().All(v => v == 0));
            for (int grid = 0; grid < 4; ++grid) Equal((byte)(grid + 10), map.CoarseGrid(grid)[0]!.Value);
            Equal(new OwnedList<MapFeatures>([MapFeatures.Unit | MapFeatures.CitySite | MapFeatures.Irrigation, MapFeatures.Mine, MapFeatures.Unit | MapFeatures.Mine]), new(map.RememberedBarbarian)); Equal(setup, f.Runtime.World.WorldSetup);
        }
    }
    private static void PremadeSeedAndResponseLaws()
    {
        foreach (ushort seed in new ushort[] { 0, 1 }) foreach (uint tick in new uint[] { 0, 32768, 32767, uint.MaxValue })
        {
            using var f = new PremadeFixture(); f.Runtime.BeginClientSession(new(17, 29)); var game = f.Runtime.GameplayRandom.Capture(); var presentation = f.Runtime.PresentationRandom.Capture(); f.Bytes = PremadeBytes(seed, 0, false); f.Tick = tick;
            f.Observe = e => { if (e is "seed" or "hints") throw new Exception("Unexpected prompt"); };
            Equal((int)WorldSource.PremadeCylindrical, (int)f.Load()!.Value); Equal((ushort)(tick is 0 or 32768 ? 1 : 32767), f.Runtime.World.Map.PatternSeed); Equal(1, f.Trace.Count(e => e == "ticks")); Equal(game, f.Runtime.GameplayRandom.Capture()); Equal(presentation, f.Runtime.PresentationRandom.Capture());
        }
        foreach (int response in new[] { -9, -1, 0, 1, 2 })
        {
            using var f = new PremadeFixture(); f.SeedResponse = f.HintResponse = response; f.Tick = 0x8002; f.Load(); Equal((ushort)(response == 0 ? 2 : 24), f.Runtime.World.Map.PatternSeed);
            if (response == 0) Equal(new StartingPositions(), f.Starts); else Equal((short)300, f.Starts.Positions[20].X); Equal(response == 0 ? 1 : 0, f.Trace.Count(e => e == "ticks"));
        }
    }
    private static void PremadeLiveChoicesAndFailures()
    {
        using (var f = new PremadeFixture())
        {
            f.Bytes = PremadeBytes(24, 0, false); f.SeedResponse = -1;
            f.Observe = e =>
            {
                if (e == "seed") { Equal(MapFeatures.None, f.Runtime.World.Map.Tiles[0].Features); Equal(true, f.Runtime.World.Map.Tiles[0].Owner is null); f.Runtime.World.Map.PatternSeed = 99; f.Starts.Positions[20] = f.Starts.Positions[20] with { X = 123 }; f.Runtime.World.Globals.Rules = StateConversion.DecodeGameRules(0x42); }
                if (e == "hints") { Equal((ushort)99, f.Runtime.World.Map.PatternSeed); Equal((short)123, f.Starts.Positions[20].X); f.Runtime.World.Map.StoredTopology = 7; f.Runtime.World.Globals.Rules.NoRestart = true; }
            };
            Equal((int)WorldSource.PremadeFlat, (int)f.Load()!.Value); Equal(StateConversion.DecodeGameRules((ushort)0x8142), f.Runtime.World.Globals.Rules); Equal((ushort)99, f.Runtime.World.Map.PatternSeed);
        }
        foreach (string stop in new[] { "seed", "ticks", "hints" })
        {
            using var f = new PremadeFixture(); f.SeedResponse = 0; f.Observe = e => { if (e == stop) throw new IOException(stop); };
            Throws<IOException>(() => f.Load()); Equal(MapFeatures.None, f.Runtime.World.Map.Tiles[0].Features); Equal(true, f.Runtime.World.Map.Tiles[0].Owner is null); Equal(StateConversion.DecodeGameRules((ushort)0x1555), f.Runtime.World.Globals.Rules); Equal((ushort)(stop == "hints" ? 0x5678 : 24), f.Runtime.World.Map.PatternSeed); Equal((short)300, f.Starts.Positions[20].X); Equal(1, f.Closes);
            f.Runtime.PrepareNewGame();
        }
    }
    private static void PremadeInputFailuresAndStaging()
    {
        for (int available = 0; available < 122; ++available)
        {
            using var f = new PremadeFixture(); f.Bytes = f.Bytes[..available]; var tiles = PremadeTiles(f.Runtime.World.Map); var starts = f.Starts; var setup = f.Runtime.World.WorldSetup; var globals = f.Runtime.World.Globals;
            Equal(true, f.Load() is null); Equal(tiles, PremadeTiles(f.Runtime.World.Map)); Equal(starts, f.Starts); Equal(setup, f.Runtime.World.WorldSetup); Equal(globals, f.Runtime.World.Globals); Equal(1, f.Closes); Equal("failure", f.Trace[^1]);
        }
        foreach (string failure in new[] { "open", "read", "close", "close-false", "missing", "bad-map" })
        {
            using var f = new PremadeFixture(); var tiles = PremadeTiles(f.Runtime.World.Map); var starts = f.Starts;
            f.Open = failure != "missing"; f.CloseOk = failure != "close-false";
            if (failure == "bad-map") f.Services = f.Services with { Source = new PremadeSource(_ => new(new MapInput { Width = 3, Height = 2 }, new())) };
            else f.Observe = e => { if (e == failure) throw new IOException(failure); };
            if (failure is "missing" or "close-false") Equal(true, f.Load() is null); else if (failure == "bad-map") Throws<ArgumentException>(() => f.Load()); else Throws<IOException>(() => f.Load());
            Equal(tiles, PremadeTiles(f.Runtime.World.Map)); Equal(starts, f.Starts); Equal(failure is "read" or "close" or "close-false" ? 1 : 0, f.Closes);
        }
    }
    private static void PremadeTransferOwnership()
    {
        using var f = new PremadeFixture(); f.Bytes = [.. f.Bytes, .. new byte[140000]]; Equal(true, f.Load() is not null); Equal(3, f.Trace.Count(e => e == "read")); Equal(1, f.Closes);
        foreach (var inconsistent in new[] { new ReadResult(new([1]), true), new ReadResult(new(new byte[65537]), false) })
        {
            int closes = 0; var storage = new CallbackStorage { ReadFile = _ => new CallbackInput(_ => inconsistent, () => { ++closes; return true; }) };
            Throws<ArgumentException>(() => new ClassicPremadeWorldSource(storage).Load("map")); Equal(1, closes);
        }
        int cleanup = 0; var error = new OutOfMemoryException("authored input failure");
        var failing = new CallbackStorage { ReadFile = _ => new CallbackInput(_ => throw error, () => { ++cleanup; throw new IOException("cleanup"); }) };
        try { new ClassicPremadeWorldSource(failing).Load("map"); throw new Exception("Expected source failure"); } catch (OutOfMemoryException caught) { Equal(true, ReferenceEquals(error, caught)); }
        Equal(1, cleanup);
    }
    private static void PremadeAdmissionAndPublication()
    {
        using var f = new PremadeFixture(); var before = PremadeTiles(f.Runtime.World.Map); Equal(true, PremadeWorld.Load(f.Runtime, ref f.Starts, null, null) is null); Equal(0, f.Trace.Count);
        Throws<ArgumentException>(() => PremadeWorld.Load(f.Runtime, ref f.Starts, "x\0y", null));
        foreach (var services in new[] { f.Services with { Source = null! }, f.Services with { LoadFailure = null! }, f.Services with { Choices = new(null!, () => 1, () => 1) }, f.Services with { Choices = new(() => 1, null!, () => 1) }, f.Services with { Choices = new(() => 1, () => 1, null!) } })
            Throws<ArgumentNullException>(() => PremadeWorld.Load(f.Runtime, ref f.Starts, "authored.mp", services));
        Equal(before, PremadeTiles(f.Runtime.World.Map)); Equal(0, f.Trace.Count);
        f.Runtime.BeginClientSession(new(1, 2)); f.Runtime.World.Entities.Units.SetEnd(1); f.Runtime.World.Entities.Units.Storage[0] = new() { Token = new(1), Owner = new(1), Position = new(7, 0) }; var client = SessionClient(f.Runtime); short next = 7;
        f.Observe = _ => { Equal(next, client.OwnUnits().Records[0].Position.X); f.Runtime.World.Entities.Units.Storage[0].Position = f.Runtime.World.Entities.Units.Storage[0].Position with { X = ++next }; Throws<InvalidOperationException>(f.Runtime.PrepareNewGame); };
        f.SeedResponse = 0; f.Load(); Equal(next, client.OwnUnits().Records[0].Position.X); f.Bytes = [1]; Equal(true, f.Load() is null); Equal(next, client.OwnUnits().Records[0].Position.X); f.Open = false; Equal(true, f.Load() is null); Equal(next, client.OwnUnits().Records[0].Position.X);
        f.Runtime.PrepareNewGame(); Throws<ArgumentException>(() => PremadeWorld.Prepare(f.Runtime, ref f.Starts, f.Services.Choices));
        f.Runtime.RegisterCommand(CommandDefinitionFor(_ => DecisionContinuation())); f.Runtime.BeginClientSession(new()); var pending = SessionClient(f.Runtime); pending.Submit(CommandRequestFor());
        int events = f.Trace.Count; Throws<InvalidOperationException>(() => f.Load()); Equal(events, f.Trace.Count); // Pending commands cannot be bypassed by a map load.
    }
    private static void PremadeParameterDispatch()
    {
        foreach (bool fail in new[] { false, true })
        {
            using var f = new PremadeFixture { Open = !fail }; var source = f.Load(); var before = f.Runtime.World.WorldSetup.Parameters; var random = WorldChoices(fail ? [] : [2, 2, 2, 2, 2]);
            Equal(!fail, WorldSetupOperations.SelectParameters(f.Runtime, random, "world/one", false, source.HasValue)); random.RequireChoicesConsumed(); if (fail) Equal(before, f.Runtime.World.WorldSetup.Parameters); else SetupParametersEqual([1, 0, 1, 1, 1], f.Runtime);
        }
    }
}
