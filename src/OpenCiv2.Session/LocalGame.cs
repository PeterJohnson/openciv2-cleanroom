using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats.Content;
using OpenCiv2.IO;

namespace OpenCiv2.Session;

public enum LocalLoadResult { Loaded, Busy, InvalidFile, UnsupportedVersion, UnsupportedGame }

/// <summary>Owns a transport-free Alder Valley game and its replaceable player session.</summary>
public sealed class LocalGame : IDisposable
{
    private const string SavePath = @"local\Alder Valley.SAV";
    public const ulong GameplaySeed = 17, PresentationSeed = 23;
    private static readonly Seeds SessionSeeds = new(GameplaySeed, PresentationSeed);
    private SessionRuntime runtime;
    private ClassicFileState files = NewFileState();
    private ulong nextRuntime = 1;
    private ParticipantId participant;
    private readonly LocalReplayRecorder? replay;
    private readonly IPreferenceWriter preferences;
    private readonly HallOfFame hall;
    private readonly RateDecisionPreferences rateChoices = new();
    private readonly PreferenceSnapshot? initialPreferences;

    public IPlayerClient Player { get; private set; }
    public bool Busy => runtime.HasPendingCommands;

    public LocalGame() : this(null) { }

    public LocalGame(LocalReplayRecorder? replay, IPreferenceWriter? preferences = null,
        PreferenceSnapshot? initialPreferences = null, IFileStorage? hallStorage = null)
    {
        this.replay = replay;
        hall = new(hallStorage ?? new BufferedHallStorage());
        this.preferences = preferences ?? new BufferedPreferenceWriter();
        this.initialPreferences = initialPreferences;
        runtime = NewRuntime();
        AuthorWorld(runtime);
        Player = BindPlayer();
    }

    /// <summary>Binds an additional controller to the current human seat.</summary>
    public IPlayerClient Bind(ControllerKind kind) => runtime.Bind(participant, kind);

    /// <summary>Releases an auxiliary binding in its original game lifetime.</summary>
    public void Revoke(ClientIdentity binding)
    {
        if (binding.Kind != ControllerKind.Frontend && binding.Runtime == Player.Identity.Runtime
            && binding.Session == Player.Identity.Session && binding.Participant == participant)
        {
            runtime.Revoke(binding.Controller);
        }
    }

    public bool AdvanceOne() => runtime.AdvanceOne();
    public bool RetireCommand(CommandId command) => runtime.RetireCommand(command);

    private IPlayerClient BindPlayer()
    {
        participant = runtime.AddParticipant(new(LocalGameRules.Owner));
        return Bind(ControllerKind.Frontend);
    }

    private SessionRuntime NewRuntime(ProcessTokens? previous = null)
    {
        var process = new ProcessContext();
        process.OrdinaryPreferences.SetDefaults(1024, true);
        process.MultiplayerPreferences.SetDefaults();
        var preferred = previous is not null ? PreferenceFiles.Capture(runtime.Process) : initialPreferences;
        if (preferred is { } snapshot)
        {
            process.OrdinaryPreferences = snapshot.Ordinary;
            process.MultiplayerPreferences = snapshot.Multiplayer;
            PreferenceFiles.ReplaceImported(process, snapshot.ImportedOrdinary);
        }
        if (previous is not null)
        {
            process.Tokens.NextUnit = previous.NextUnit;
            process.Tokens.NextCity = previous.NextCity;
        }

        var candidate = new SessionRuntime(process, new(nextRuntime++));
        candidate.EnableLocalPlay(replay);
        LocalPlayCommands.Register(candidate, preferences, rateChoices, hall);
        candidate.BeginClientSession(SessionSeeds);
        return candidate;
    }

    private static void AuthorWorld(SessionRuntime runtime)
    {
        var world = runtime.World;
        var rules = runtime.Process.Rules;
        new RuleInstaller(LocalGameRules.Input(), rules).Load(true);
        var map = world.Map;
        var geometry = new Geometry(LocalGameRules.Width, LocalGameRules.Height);
        map.Allocate(geometry);
        map.PatternSeed = LocalGameRules.Seed;
        // F06 stores the two retained coarse workspaces. No AI pass populates them in this profile.
        map.CoarseGrid(0).Fill(0);
        map.CoarseGrid(1).Fill(0);
        for (int i = 0; i < geometry.Area; i++)
        {
            var point = geometry.Coordinates(i);
            map.Tiles[i] = new()
            {
                Terrain = LocalGameRules.Landform(point),
                River = LocalGameRules.River(point),
                Features = LocalGameRules.Features(point),
                ResourceSuppressed = true,
                Owner = null
            };
        }

        MapRegions.Rebuild(map, true);
        const int owner = LocalGameRules.Owner;
        ref var globals = ref world.Globals;
        globals.Rules = GameRules.Normal with { FlatWorld = true };
        globals.Turn = 1;
        globals.FirstLaunchTurn = -1;
        globals.Year = (short)Calendar.Year(globals.Turn, globals, world.Scenario, GameplayContent.Builtin.Values.Calendar);
        globals.PlayerCiv = globals.TurnCiv = globals.ViewCiv = globals.SavedCurrentCiv = owner;
        globals.CivsAlive = globals.CivsAtStart = globals.HumanPlayers = CivilizationSet.Only(owner);
        globals.NextHistoryTurn = short.MaxValue;
        for (int i = 0; i < RuleLimits.Wonders; i++)
        {
            globals.WonderCity[i] = -1;
        }

        world.Multiplayer.LocalHuman = owner;
        ref var civ = ref world.Civilizations[owner];
        civ.Government = Government.Despotism;
        civ.Researching = -1;
        civ.ScienceRate = 6;
        civ.TaxRate = 4;
        for (int i = 0; i < EntityLimits.Civilizations; i++)
        {
            world.Views[i] = world.Views[i] with { Center = LocalGameRules.StartingTile };
        }

        globals.ActiveUnit = LocalPlayCommands.CreateUnit(runtime, LocalGameRules.Settler, LocalGameRules.StartingTile, null);
        LocalPlayCommands.CreateUnit(runtime, LocalGameRules.Rider, new(10, 8), null);
        EnterPlayerPhase(runtime, false);
    }

    private static void EnterPlayerPhase(SessionRuntime runtime, bool resumed)
    {
        var world = runtime.World;
        world.TurnLoopActive = world.PlayerPhaseActive = true;
        world.MovementMode = MovementMode.MoveUnits;
        if (resumed)
        {
            world.Globals.TurnCiv = (sbyte)world.Multiplayer.ResumeCiv;
            world.Globals.ViewCiv = (sbyte)world.Multiplayer.LocalHuman;
            foreach (var unit in world.Entities.Units.Scanned)
            {
                if (unit.Token.Occupied)
                {
                    ++world.UnitTotals[unit.Owner.Value];
                }
            }

            // F04 ordinary resume skips global reset and upkeep. Rebuild only unsaved city outputs.
            if (world.Entities.Cities.Live(new(0)))
            {
                LocalCityEconomy.Recalculate(world, runtime.Process.Rules, new(0));
            }

            LocalPlayCommands.SelectReadyUnit(world, runtime.Process.Rules.Definitions);
        }

        runtime.Publish();
    }

    /// <summary>Writes ordinary F06 save bytes at a settled player boundary.</summary>
    public byte[] SaveClassic()
    {
        if (Busy)
        {
            throw new InvalidOperationException("Finish the pending command before saving.");
        }

        var storage = new ClassicBuffer();
        if (ClassicGameFiles.Save(runtime, files, SavePath, false, storage) != CoreSaveStatus.Success)
        {
            throw new IOException("Classic save failed.");
        }

        byte[] bytes = storage.Bytes.ToArray();
        replay?.Record(new LocalReplayStep.Save(ContentIdentity.Sha256(bytes)));
        return bytes;
    }

    /// <summary>Stages an F06 load under the declared authored rules and installs it only if this mode can resume it.</summary>
    public LocalLoadResult LoadClassic(ReadOnlyMemory<byte> bytes)
    {
        var result = LoadClassicCore(bytes);
        replay?.RecordLoad(bytes, result);
        return result;
    }

    private LocalLoadResult LoadClassicCore(ReadOnlyMemory<byte> bytes)
    {
        if (Busy)
        {
            return LocalLoadResult.Busy;
        }

        if (bytes.Length > StoredDocument.MaximumBytes)
        {
            return LocalLoadResult.InvalidFile;
        }

        var staged = NewRuntime(runtime.Process.Tokens);
        try
        {
            var stagedFiles = NewFileState();
            var buffer = new ClassicBuffer { Bytes = bytes };
            var services = new CoreLoadServices(buffer, () => { }, _ => { }, (_, _) => false,
                (a, b) => a == b, () => new(LocalGameRules.Input()));
            var status = ClassicGameFiles.Load(staged, stagedFiles, SavePath, services, "alder-valley/load");
            if (status != CoreLoadStatus.Success)
            {
                return status is CoreLoadStatus.Unsupported or CoreLoadStatus.Newer
                    ? LocalLoadResult.UnsupportedVersion : LocalLoadResult.InvalidFile;
            }

            if (!CanResume(staged.World))
            {
                return LocalLoadResult.UnsupportedGame;
            }

            EnterPlayerPhase(staged, true);
            runtime.Dispose();
            runtime = staged;
            files = stagedFiles;
            Player = BindPlayer();
            return LocalLoadResult.Loaded;
        }
        finally
        {
            if (!ReferenceEquals(staged, runtime))
            {
                staged.Dispose();
            }
        }
    }

    private static bool CanResume(GameState world)
    {
        const int owner = LocalGameRules.Owner;
        var globals = world.Globals;
        var seat = CivilizationSet.Only(owner);
        var map = world.Map;
        var geometry = map.Geometry;
        var civ = world.Civilizations[owner];
        if (geometry.Width != LocalGameRules.Width || geometry.Height != LocalGameRules.Height
            || map.PatternSeed != LocalGameRules.Seed || globals.Multiplayer != GameMode.SinglePlayer
            || globals.Status.HasScenario || globals.RevealMap != 0 || !globals.Rules.FlatWorld
            || !globals.Rules.NormalCombat || globals.Difficulty != Difficulty.Chieftain
            || globals.HumanPlayers != seat || globals.CivsAlive != seat || globals.Turn < 1
            || world.Multiplayer.LocalHuman != owner || world.Multiplayer.ResumeCiv != owner
            || world.Events.Loaded || civ.Government is not (Government.Anarchy or Government.Despotism) || civ.Tribe != 0
            || civ.CityCount is < 0 or > 1 || !CivilizationRates.Allowed(world, owner, civ.TaxRate, civ.ScienceRate)
            || civ.Researching is < -1 or > 2 || globals.BestHumanRank >= EntityLimits.Civilizations
            || world.Entities.Cities.End != civ.CityCount)
        {
            return false;
        }

        var city = world.Entities.Cities.Storage[0];
        if (civ.CityCount == 1 && (!city.Token.Occupied || city.Owner.Value != owner
            || city.Size is < 1 or > 8 || !geometry.Canonical(city.Position) || map.TileAt(city.Position).Ocean
            || city.Production is not (LocalGameRules.Guard or LocalGameRules.Rider or LocalGameRules.Temple or LocalGameRules.Library)
            || !LocalGameRules.SupportedBuildings(city.Improvements)
            || city.RouteCount != 0 || city.SpecialistCount > city.Size || !HasSupportedSpecialists(city)
            || city.Flags.Disorder))
        {
            return false;
        }

        var counts = new int[geometry.Area];
        var units = world.Entities.Units;
        for (int i = 0; i < units.End; i++)
        {
            var unit = units.Storage[i];
            if (!unit.Token.Occupied)
            {
                continue;
            }

            if (unit.Owner.Value != owner || !geometry.Canonical(unit.Position) || unit.Damage != 0
                || map.TileAt(unit.Position).Ocean || unit.Flags.Automated || unit.Flags.Aboard
                || unit.Type.Value is not (LocalGameRules.Settler or LocalGameRules.Guard or LocalGameRules.Rider)
                || unit.Orders is not (UnitOrder.None or UnitOrder.Fortifying or UnitOrder.Fortified)
                    && !(unit.Type.Value == LocalGameRules.Settler && TerrainWorkOrders.IsWork(unit.Orders) && unit.Orders != UnitOrder.Transform)
                || unit.Home is { } home && (home.Value != 0 || civ.CityCount == 0)
                || unit.Type.Value == LocalGameRules.Settler && (unit.Home is not null || civ.CityCount != 0))
            {
                return false;
            }

            ++counts[geometry.TileIndex(unit.Position)!.Value];
        }

        for (int i = 0; i < geometry.Area; i++)
        {
            var point = geometry.Coordinates(i);
            var tile = map.Tiles[i];
            bool center = civ.CityCount == 1 && city.Position == point;
            var features = (center || tile.Airbase ? MapFeatures.CitySite : LocalGameRules.Features(point))
                | (counts[i] != 0 ? MapFeatures.Unit : MapFeatures.None);
            bool occupied = center || counts[i] != 0;
            const MapFeatures workFeatures = MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Road
                | MapFeatures.Railroad | MapFeatures.Fortification | MapFeatures.Pollution;
            if (tile.Terrain != LocalGameRules.Landform(point) || tile.River != LocalGameRules.River(point)
                || !tile.ResourceSuppressed || center && tile.Airbase
                || (tile.Features & ~workFeatures) != (features & ~workFeatures)
                || (occupied ? tile.Owner != owner : tile.Owner is not (owner or null)))
            {
                return false;
            }
        }

        // Loaded stacks must describe the same placement admitted by this profile.
        var stacks = new OpenCiv2.Core.Units.StackOperations(world, new(100000, 64));
        for (int i = 0; i < geometry.Area; i++)
        {
            if (counts[i] != 0)
            {
                var point = geometry.Coordinates(i);
                int first = stacks.FirstAt(point);
                if (first < 0 || stacks.RawForwardCount(first) != counts[i])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HasSupportedSpecialists(in City city)
    {
        // C03 counts the stored kinds across all slots, even beyond the active specialist count.
        for (int i = 0; i < CityLimits.Specialists; i++)
        {
            if (city.Specialists[i] is not (SpecialistKind.Unused or SpecialistKind.Entertainer))
            {
                return false;
            }
        }

        return true;
    }

    private static ClassicFileState NewFileState()
    {
        var retained = new ClassicFileState();
        retained.Text.Home = retained.Text.Working = retained.Text.Search = "local";
        return retained;
    }

    public void Dispose() => runtime.Dispose();

    private sealed class ClassicBuffer : IFileStorage
    {
        public ReadOnlyMemory<byte> Bytes { get; set; }
        public CoreInput Read(string path) => new Input(Bytes);
        public CoreOutput Replace(string path) => new Output(this);
        public bool Remove(string path) => false;

        private sealed class Input(ReadOnlyMemory<byte> bytes) : CoreInput
        {
            private int offset;

            protected override ReadResult ReadItem(int size)
            {
                int count = Math.Min(bytes.Length - offset, size);
                var part = bytes.Span.Slice(offset, count).ToArray();
                offset += count;
                return new(new(part), count == size);
            }

            protected override byte? ReadByte() => offset < bytes.Length ? bytes.Span[offset++] : null;
            protected override bool Finish() => true;
        }

        private sealed class Output(ClassicBuffer owner) : CoreOutput
        {
            protected override bool WriteItem(ReadOnlySpan<byte> bytes)
            {
                owner.Bytes = bytes.ToArray();
                return true;
            }

            protected override bool Finish() => true;
        }
    }
}
