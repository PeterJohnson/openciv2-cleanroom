using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Content;
using OpenCiv2.Query;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void SemanticTextBoundaries()
    {
        var pools = new EntityPools(); pools.Cities.SetEnd(1);
        pools.Cities.Storage[0] = new() { Token = new(7), Owner = new(1), Name = "東京 🏙️", Position = new(8, 6) };
        var observation = new LiveObservation(new(1), new(1), 0, new(1));
        var snapshot = LiveProjection.OwnCities(pools, observation);
        var copy = pools.Cities.Storage[0]; copy.Name = "Renamed";
        Equal("東京 🏙️", snapshot.Records[0].Name);
        Equal("東京 🏙️", pools.Cities.Storage[0].Name);
        Throws<ContentException>(() => StateConversion.ToStored(pools.Cities.Storage[0]));

        var names = new NamingFixture();
        var catalog = new NameCatalog([new(new([new("tribes 0", new(["12345678901234🌍tail"]))]), null)],
            StringComparer.Ordinal.Equals);
        Equal(true, names.Run(catalog)); Equal("12345678901234🌍", names.City.Name);
        var bytes = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        BytesEqual(bytes, ClassicText.EncodeField(ClassicText.DecodeField(bytes)));
        Equal("€Œé\u0081", ClassicText.DecodeTerminated([0x80, 0x8c, 0xe9, 0x81, 0, 65]));
        var stored = new StoredCity { Name = new(new byte[] { 0x80, 0x8c, 0xe9, 0x81 }.Concat(new byte[12])) };
        var live = StateConversion.ToLive(stored); Equal("€Œé\u0081", live.Name);
        BytesEqual(stored.Name, StateConversion.ToStored(live).Name);
        live.Name = "Café €";
        BytesEqual([67, 97, 102, 0xe9, 32, 0x80, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], StateConversion.ToStored(live).Name);

        var passwords = new OwnedList<string>(["Café", "€", "", "", "", "", "", ""]);
        var image = PasswordConversion.Encode(passwords);
        Equal(passwords, PasswordConversion.Decode(image));
        var retained = PasswordConversion.Decode(new(Enumerable.Repeat((byte)0xa5, 256)));
        BytesEqual(Enumerable.Repeat((byte)0xa5, 256), PasswordConversion.Encode(retained));
        var changed = retained.ToArray(); changed[2] = "Changed";
        Equal("Changed", PasswordConversion.Decode(PasswordConversion.Encode(new(changed)))[2]);
        BytesEqual(Enumerable.Repeat((byte)0xa5, 256), PasswordConversion.Encode(retained));
    }

    private static void SemanticMapLocations()
    {
        var stored = new StoredUnit { X = -7, Y = 13, Orders = UnitOrder.Sleeping, DestinationX = 21, DestinationY = -9 };
        var unit = StateConversion.ToLive(stored);
        Equal(new MapPoint(-7, 13), unit.Position); Equal(new UnitSlot(21), unit.Carrier);
        unit.Position = new(10, 12); unit.Carrier = new UnitSlot(4);
        var exported = StateConversion.ToStored(unit);
        Equal((short)10, exported.X); Equal((short)12, exported.Y); Equal((short)4, exported.DestinationX);
        unit.Orders = UnitOrder.GoTo; unit.Destination = new(16, 18);
        exported = StateConversion.ToStored(unit);
        Equal((short)16, exported.DestinationX); Equal((short)18, exported.DestinationY);
        Equal((UnitSlot?)null, StateConversion.ToLive(exported).Carrier);
        foreach (short absent in new short[] { short.MinValue, -17, -1 })
        {
            var raw = stored with { Previous = absent, Next = absent, DestinationX = absent };
            var imported = StateConversion.ToLive(raw);
            Equal<UnitSlot?>(null, imported.Previous); Equal<UnitSlot?>(null, imported.Next);
            Equal<UnitSlot?>(null, imported.Carrier);
            var canonical = StateConversion.ToStored(imported);
            Equal((short)-1, canonical.Previous); Equal((short)-1, canonical.DestinationX);
            Equal(raw, StateConversion.PreserveOpaque(canonical, raw));
            imported.Previous = new(0); imported.Carrier = new(7);
            var edited = StateConversion.PreserveOpaque(StateConversion.ToStored(imported), raw);
            Equal((short)0, edited.Previous); Equal((short)7, edited.DestinationX); Equal(absent, edited.Next);
            var cityBytes = new StoredCity { RoutePartners = new([absent, 0, 254]) };
            var city = StateConversion.ToLive(cityBytes); Equal<CitySlot?>(null, city.RoutePartners[0]);
            Equal<CitySlot?>(new(0), city.RoutePartners[1]);
            Equal(cityBytes, StateConversion.PreserveOpaque(StateConversion.ToStored(city), cityBytes));
            var globalsBytes = new StoredGlobals { ActiveUnit = absent, MovingUnit = absent };
            var globals = StateConversion.ToLive(globalsBytes);
            Equal<UnitSlot?>(null, globals.ActiveUnit); Equal<UnitSlot?>(null, globals.MovingUnit);
            Equal(globalsBytes, StateConversion.PreserveOpaque(StateConversion.ToStored(globals), globalsBytes));
        }
        var values = new EventValues { MoveTo = new(int.MinValue, int.MaxValue) };
        values.MoveRectangle[3] = new(13, 15); values.CreateLocations[9] = new(17, 19);
        var record = StateConversion.ToStored(values);
        Equal(13, record.MoveRectangle[6]); Equal(15, record.MoveRectangle[7]);
        Equal(17, record.CreateLocations[18]); Equal(19, record.CreateLocations[19]);
        Equal(values, StateConversion.ToLive(record));
        var view = new MapView(true, true, new(-100, 37), -2);
        Equal(view, StateConversion.ToLive(StateConversion.ToStored(view)));
    }

    // F06 §2: known flags have independent meanings; the upper byte is adapter-owned payload.
    private static void SemanticGameStatus()
    {
        GameStatus[] meanings = [new() { AiCoordination = true }, new() { Won = true },
            new() { LargeMap = true }, new() { SmallMap = true }, new() { Cheated = true },
            new() { Scored = true }, new() { ScenarioExport = true }, new() { Scenario = true }];
        for (int i = 0; i < meanings.Length; ++i)
        {
            Equal(meanings[i], StateConversion.DecodeGameStatus((ushort)(1 << i)));
            Equal(meanings[i], StateConversion.DecodeGameStatus((ushort)(0xff00 | (1 << i))));
            Equal((ushort)(1 << i), StateConversion.EncodeGameStatus(meanings[i]));
        }
        for (int bits = 0; bits <= ushort.MaxValue; ++bits)
        {
            var stored = new StoredGlobals { GameFlags = (ushort)bits };
            var live = StateConversion.ToLive(stored);
            Equal((ushort)(bits & 0xff), StateConversion.ToStored(live).GameFlags);
            Equal(stored, StateConversion.PreserveOpaque(StateConversion.ToStored(live), stored));
        }
        var imported = new StoredGlobals { GameFlags = 0xa5ff };
        var changed = StateConversion.ToLive(imported);
        changed.Status = new() { Scenario = true };
        Equal((ushort)0xa580, StateConversion.PreserveOpaque(StateConversion.ToStored(changed), imported).GameFlags);
    }

    // W01 §§5–6 / Appendix A: semantic mutations and classic publication preserve separate contracts.
    private static void SemanticTileMutations()
    {
        var map = new MapStorage(new()); map.Allocate(new(4, 2));
        var position = new MapPoint(0, 0);
        var effects = new List<TileSyncEffect>();
        var controls = new TileSyncControls(3, true, true, false);
        var change = new TileChange.Features(position, MapFeatures.CitySite | MapFeatures.Fortification);
        map.Mutate(change, true, controls, effects.Add);
        Equal(true, map.TileAt(position).Airbase); Equal(false, map.TileAt(position).City);
        Equal(new TileSyncEffect(TileSyncRoute.ClientBatch, change), effects.Single());
        map.Mutate(change, true, controls, effects.Add); Equal(1, effects.Count);
        map.Mutate(new TileChange.Features(position, MapFeatures.Fortification, false));
        Equal(true, map.TileAt(position).City); Equal(false, map.TileAt(position).Airbase);
        map.Mutate(new TileChange.Revelation(position, new CivilizationSet().With(1).With(7)));
        map.Mutate(new TileChange.Revelation(position, new CivilizationSet().With(7), false));
        Equal(true, map.IsRevealedTo(position, 1)); Equal(false, map.IsRevealedTo(position, 7));
        Equal(true, map.IsRevealedTo(position, -1)); Equal(false, map.IsRevealedTo(position, 33));
        Equal(2U, ClassicTileMutations.Revelation(map, position, 33));
        Throws<ArgumentOutOfRangeException>(() => new CivilizationSet().With(8));
        map.Mutate(new TileChange.Terrain(position, Terrain.Hills, AddRiver: true, SuppressResource: true));
        map.Mutate(new TileChange.Terrain(position, Terrain.Ocean));
        Equal(true, map.TileAt(position).River); Equal(true, map.TileAt(position).ResourceSuppressed);
        Equal(Terrain.Ocean, map.TileAt(position).Terrain);
        map.Mutate(new TileChange.Owner(position, 3)); map.Mutate(new TileChange.Fertility(position, 12));
        map.Mutate(new TileChange.Region(position, 17)); map.Mutate(new TileChange.Claimant(position, 2));
        Equal((byte?)3, map.TileAt(position).Owner); Equal((byte)12, map.TileAt(position).Fertility);
        Equal((byte)17, map.TileAt(position).Region); Equal((byte)2, map.TileAt(position).Claimant);
    }

    // F06/W01: unknown terrain/radius bits survive edits, and follow only their original allocation.
    private static void OpaqueTileLifetimes()
    {
        var map = new MapStorage(new()); var geometry = new Geometry(4, 2); map.Allocate(geometry);
        var stored = ClassicRecords.DecodeTile([0xf4, 0xff, 0xbf, 17, 0x82, 0x3c]);
        map.Tiles.Fill(StateConversion.ToLive(stored));
        var compatibility = new MapCompatibility(map, Enumerable.Repeat(stored, 4));
        map.Tiles[0].River = false; map.Tiles[0].Features &= ~MapFeatures.Unit;
        map.Tiles[0].RevealedTo = map.Tiles[0].RevealedTo.With(7, false);
        BytesEqual([0x74, 0xfe, 0xbf, 17, 2, 0x3c], ClassicRecords.Encode(compatibility.Export(map, 0)));
        var starts = new StartingPositions(); var output = new ItemCapture();
        Equal(true, ClassicLive.WriteMapItems(output.Write, MapStream.Raw, map, starts, compatibility));
        Equal(compatibility.Export(map, 0), ClassicFiles.DecodeRawMap(output.Bytes).Tiles[0]);
        var other = new MapStorage(new()); other.SwapStorage(map);
        Equal(false, compatibility.BelongsTo(map)); Equal(true, compatibility.BelongsTo(other));
        other.Allocate(geometry); Equal(false, compatibility.BelongsTo(other));
        output = new(); ClassicLive.WriteMapItems(output.Write, MapStream.Raw, other, starts, compatibility);
        Equal((byte)0, ClassicFiles.DecodeRawMap(output.Bytes).Tiles[0].TerrainFlags);
        Throws<ArgumentException>(() => compatibility.Export(other, 0));
        compatibility = new(other); var effects = new List<ClassicTileSyncEffect>();
        var mutation = new ClassicTileMutation(ClassicTileField.Terrain, new(0, 0), 0x3a, Synchronize: true);
        other.ApplyClassic(mutation, new(3, false, false, true), effects.Add, compatibility);
        Equal(new ClassicTileSyncEffect(TileSyncRoute.Broadcast, mutation), effects.Single());
        Equal(new Tile(), other.Tiles[0]); Equal((byte)0x30, compatibility.Export(other, 0).TerrainFlags);
        other.ApplyClassic(mutation, new(3, false, false, true), effects.Add, compatibility); Equal(1, effects.Count);
        other.Release(); Equal(false, compatibility.BelongsTo(other));
    }
    private static void SemanticEntityFlags()
    {
        for (int bits = 0; bits <= ushort.MaxValue; ++bits)
            Equal((ushort)bits, StateConversion.EncodeUnitStatus(StateConversion.DecodeUnitStatus((ushort)bits)));
        var cityBytes = new StoredCity { Flags = uint.MaxValue, WorkedTiles = uint.MaxValue, SpecialistKinds = 0x55555555 };
        var city = StateConversion.ToLive(cityBytes);
        city.Flags.Disorder = false;
        city.WorkedTiles = city.WorkedTiles.With(20, false);
        city.SpecialistCount = 17;
        city.Specialists[0] = SpecialistKind.Scientist;
        var savedCity = StateConversion.PreserveOpaque(StateConversion.ToStored(city), cityBytes);
        Equal(0xfffffffeU, savedCity.Flags);
        Equal(0x47efffffU, savedCity.WorkedTiles);
        Equal(0x55555557U, savedCity.SpecialistKinds);
        Equal(city, StateConversion.ToLive(savedCity));
        var civBytes = new StoredCivilization
        {
            Flags = ushort.MaxValue,
            SpaceshipFlags = byte.MaxValue,
            Treaties = new(Enumerable.Repeat(uint.MaxValue, 8)),
            Advances = new(Enumerable.Repeat(byte.MaxValue, 13)),
            ContinentFlags = new(Enumerable.Repeat(byte.MaxValue, 64))
        };
        var civ = StateConversion.ToLive(civBytes);
        civ.Flags.FemaleLeader = false; civ.SpaceshipFlags.Launched = false;
        civ.Treaties[3].Alliance = false; civ.ContinentFlags[5].StrongerEnemy = false;
        civ.Advances = civ.Advances.With(99, false);
        var savedCiv = StateConversion.PreserveOpaque(StateConversion.ToStored(civ), civBytes);
        Equal((ushort)0xfdff, savedCiv.Flags); Equal((byte)0xfd, savedCiv.SpaceshipFlags);
        Equal(0xfffffff7U, savedCiv.Treaties[3]); Equal(uint.MaxValue, savedCiv.Treaties[2]);
        Equal((byte)0xef, savedCiv.ContinentFlags[5]); Equal((byte)0xf7, savedCiv.Advances[12]);
        Equal(civ, StateConversion.ToLive(savedCiv));
    }

    private static void SemanticMemberships()
    {
        for (int bits = 0; bits < 256; ++bits)
            Equal((byte)bits, StateConversion.EncodeCivilizations(StateConversion.DecodeCivilizations((byte)bits)));
        var members = CivilizationSet.Only(7).With(1);
        Equal(2, members.Count); Equal(false, members.Contains(-1)); Equal(false, members.Contains(8));
        Equal(CivilizationSet.Only(1), members.Except(CivilizationSet.Only(7)));
        var advances = new AdvanceSet().With(0).With(63).With(64).With(99);
        Equal(advances, StateConversion.DecodeAdvances(StateConversion.EncodeAdvances(advances)));
        Equal((byte)8, StateConversion.EncodeAdvances(advances)[12]);
        Throws<ArgumentOutOfRangeException>(() => advances.With(100));
        Throws<ArgumentOutOfRangeException>(() => new ImprovementSet().With(40));
        Throws<ArgumentOutOfRangeException>(() => new CityWorkSet().With(21));
        Throws<ArgumentOutOfRangeException>(() => StateConversion.EncodeCityWork(default, 64));
        var region = new StoredRegionSummary(1, 2, 3, 0x8000000000000001, new([0xa5, 0x5a]));
        var map = new MapStorage(new()); map.Allocate(new(4, 2)); map.Regions[0] = StateConversion.ToLive(region);
        var opaque = new MapCompatibility(map, importedRegions: Enumerable.Repeat(region, 64));
        map.Regions[0] = map.Regions[0] with { NeighboringWater = map.Regions[0].NeighboringWater.With(63, false) };
        Equal(region with { NeighboringWater = 1 }, opaque.ExportRegion(map, 0));
        map.Allocate(new(4, 2)); Equal(region with { NeighboringWater = 1 }, opaque.ExportRegion(map, 0));
        var raw = new MapInput { Width = 4, Height = 2, Tiles = new(Enumerable.Repeat(new Tile(), 4)) };
        raw.Install(map, false);
        var replacement = new MapCompatibility(map);
        Equal(region with { NeighboringWater = 1 }, replacement.ExportRegion(map, 0));
        var other = new MapStorage(new()); other.SwapStorage(map);
        Equal(true, replacement.HasRegionsFor(other)); Equal(false, replacement.HasRegionsFor(map));
        Throws<ArgumentException>(() => replacement.ExportRegion(map, 0));
    }

    private static void OpaqueOptionLifetimes()
    {
        var f = new PreferenceFixture();
        var stored = new StoredOrdinaryPreferences { Rules = 0x7fff, GameOptions = uint.MaxValue, DisabledMessages = ushort.MaxValue };
        f.Input = [.. ClassicPreferences.EncodeOrdinary(stored), .. ClassicPreferences.EncodeMultiplayer(new())];
        Equal(OpenCiv2.Session.PreferenceLoadStatus.Success, f.Load());
        using var runtime = new OpenCiv2.Session.SessionRuntime(f.Process, new(79));
        runtime.World.Globals = StateConversion.ToLive(new StoredGlobals { GameOptions = 0x80000008, MessageOptions = ushort.MaxValue });
        var records = new RecordCompatibility(runtime.World.Lifetime) { Globals = new() { GameOptions = 0x80000008, MessageOptions = ushort.MaxValue } };
        records.Attach(runtime.World.Lifetime);
        Equal(true, OpenCiv2.Session.Configuration.Game(runtime, GameChoices(_ => false), new(f.Storage, new ConfigurationMusic())));
        var saved = ClassicPreferences.DecodeOrdinary(f.Output.Take(72).ToArray());
        Equal(0x80000000U, saved.GameOptions); Equal(stored.Rules, saved.Rules); Equal(ushort.MaxValue, saved.DisabledMessages);
        f.Output.Clear();
        OpenCiv2.Session.Configuration.Messages(runtime, MessageChoices(_ => true), f.Storage);
        Equal((ushort)0, ClassicPreferences.DecodeOrdinary(f.Output.Take(72).ToArray()).DisabledMessages);
        Equal((ushort)0, StateConversion.PreserveOpaque(StateConversion.ToStored(runtime.World.Globals), records.Globals).MessageOptions);
        var other = new ProcessContext(); other.OrdinaryPreferences.SetDefaults(1000, true);
        f.Process.SwapContents(other); f.Output.Clear(); f.Save();
        Equal(0x003f3258U, ClassicPreferences.DecodeOrdinary(f.Output.Take(72).ToArray()).GameOptions);
        Equal(stored.Rules, OpenCiv2.Session.PreferenceFiles.Capture(other).ImportedOrdinary!.Rules);
        runtime.PrepareNewGame(); Equal(true, records.For(runtime.World.Lifetime) is null);
    }

    private static void OpaqueFlagsLiveSave()
    {
        using var f = new GameFileFixture();
        var stored = ClassicFiles.DecodeStoredSave(f.Bytes);
        stored = stored with
        {
            Globals = stored.Globals with { Rules = ushort.MaxValue, GameOptions = uint.MaxValue, MessageOptions = ushort.MaxValue },
            Civilizations = new(stored.Civilizations.Select(c => c with { Flags = ushort.MaxValue, SpaceshipFlags = byte.MaxValue })),
            Units = new(stored.Units.Select(u => u with { Flags = ushort.MaxValue })),
            Cities = new(stored.Cities.Select(c => c with { Flags = uint.MaxValue, WorkedTiles = uint.MaxValue })),
            Map = stored.Map with { Regions = new(stored.Map.Regions.Select(r => r with { Retained = new([0x5a, 0xa5]) })) }
        };
        f.Bytes = ClassicFiles.EncodeStoredSave(stored);
        Equal(OpenCiv2.Session.CoreLoadStatus.Success, f.Load());
        var world = f.Runtime.World;
        world.Globals.Rules.Bloodlust = false; world.Globals.GameOptions.Music = false;
        world.Civilizations[1].Flags.Revolution = false; world.Civilizations[1].SpaceshipFlags.Arrived = false;
        world.Entities.Units.Storage[0].Flags.Veteran = false;
        world.Entities.Cities.Storage[0].Flags.Disorder = false;
        world.Entities.Cities.Storage[0].WorkedTiles = world.Entities.Cities.Storage[0].WorkedTiles.With(0, false);
        Equal(OpenCiv2.Session.CoreSaveStatus.Success, f.Save());
        var saved = ClassicFiles.DecodeStoredSave(f.Output.ToArray());
        Equal((ushort)0xff7f, saved.Globals.Rules); Equal(0xfffffff7U, saved.Globals.GameOptions);
        Equal((ushort)0xfffe, saved.Civilizations[1].Flags); Equal((byte)0xfb, saved.Civilizations[1].SpaceshipFlags);
        Equal((ushort)0xdfff, saved.Units[0].Flags); Equal(0xfffffffeU, saved.Cities[0].Flags);
        Equal(0xfffffffeU, saved.Cities[0].WorkedTiles); BytesEqual([0x5a, 0xa5], saved.Map.Regions[0].Retained);
    }

    private static void OpaqueEventFlags()
    {
        var events = new EventCollection(); var entry = events.Append();
        var stored = new EventRecord { Triggers = 3, Actions = uint.MaxValue };
        entry.Values = StateConversion.ToLive(stored);
        Equal(EventTrigger.Unrecognized, entry.Values.Trigger);
        entry.Values.Actions.Text = false;
        var opaque = new Dictionary<ulong, ImportedEventBytes> { [entry.Identity] = ImportedEventBytes.FromRecord(entry, stored) };
        var output = new ItemCapture();
        ClassicLive.WriteEventItems(output.Write, _ => true, _ => true, events, opaque);
        var saved = ClassicRecords.DecodeEventRecord(output.Items[2]);
        Equal(3U, saved.Triggers); Equal(0xfffffffeU, saved.Actions);
        entry.Values.Trigger = EventTrigger.None; output = new();
        ClassicLive.WriteEventItems(output.Write, _ => true, _ => true, events, opaque);
        Equal(0U, ClassicRecords.DecodeEventRecord(output.Items[2]).Triggers);
        events.Reset(); entry = events.Append(); entry.Values.Actions.Text = true; output = new();
        ClassicLive.WriteEventItems(output.Write, _ => true, _ => true, events, opaque);
        Equal(1U, ClassicRecords.DecodeEventRecord(output.Items[2]).Actions);
    }
}
