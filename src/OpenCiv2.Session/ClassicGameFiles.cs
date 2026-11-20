using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Classic;
using OpenCiv2.IO;

namespace OpenCiv2.Session;

/// <summary>F06 adapter scratch, distinct from world state and client authority.</summary>
public sealed class ClassicFileState
{
    public TextDirectories Text { get; } = new("", "", "");
    public string Extension = ".SAV", OpenedPath = "", ScenarioDirectory = "";
    public string DefaultGameStem = "GAME", GameStem = "GAME", LoadedFilename = "", RememberedSubdirectory = "";
    public StartingPositions Starts = new();
    public MapPoint Cursor;
    public SavedLayout PackedLayout = new();
    public Rectangle CurrentScreen;
    public Dictionary<ulong, ImportedEventBytes> EventBytes { get; } = [];
    public RecordCompatibility? RecordBytes;

    /// <summary>Discard prior game-file scratch while retaining application directories, language and screen.</summary>
    public void PrepareNewWorld()
    {
        Text.Working = Text.Search = Text.Home;
        Extension = ".SAV";
        OpenedPath = ScenarioDirectory = LoadedFilename = RememberedSubdirectory = "";
        GameStem = DefaultGameStem;
        Starts = new();
        Cursor = default;
        PackedLayout = new();
        EventBytes.Clear();
        RecordBytes = null;
    }
}
public enum CoreLoadStatus { Success, Input, Unsupported, Data, Newer }
public enum CoreSaveStatus { Success, Output }
public sealed record CoreLoadServices(
    IFileStorage Storage,
    Action Flush,
    Action<string> ChangeDirectory,
    Func<string, string, bool> Exists,
    Func<string, string, bool> PathEqual,
    Func<LoadedContent> LoadContent,
    Action? ContentInstalled = null)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Storage);
        ArgumentNullException.ThrowIfNull(Flush);
        ArgumentNullException.ThrowIfNull(ChangeDirectory);
        ArgumentNullException.ThrowIfNull(Exists);
        ArgumentNullException.ThrowIfNull(PathEqual);
        ArgumentNullException.ThrowIfNull(LoadContent);
    }
}
/// <summary>One synchronous guarded load. Byte parsing and explicit close precede live content or world
/// installation.</summary>
public sealed class ClassicLoadOperation : IDisposable
{
    private readonly SessionRuntime runtime;
    private readonly ClassicFileState retained;
    private readonly CoreLoadServices services;
    private readonly SessionActivity activity;
    private bool started, disposed;

    public ClassicLoadOperation(SessionRuntime runtime, ClassicFileState retained, CoreLoadServices services)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(retained);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();
        _ = runtime.GameplayRandom;
        this.runtime = runtime;
        this.retained = retained;
        this.services = services;
        activity = runtime.BeginActivity();
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            activity.Dispose();
        }
    }

    private void Paths(string request)
    {
        int separator = request.LastIndexOf('\\');
        retained.ScenarioDirectory = request[..separator];
        retained.Text.Working = retained.ScenarioDirectory;
        services.ChangeDirectory(retained.Text.Working);
        retained.GameStem = services.Exists(retained.Text.Working, "GAME." + retained.Text.Extension)
            ? retained.ScenarioDirectory + "\\GAME"
            : retained.DefaultGameStem;
        retained.LoadedFilename = request[(separator + 1)..];
        string home = retained.Text.Home;
        if (request.Length >= home.Length && services.PathEqual(request[..home.Length], home)
            && separator > home.Length)
        {
            string relative = request[home.Length..separator];
            if (relative.StartsWith('\\'))
            {
                relative = relative[1..];
            }

            if (relative.Length != 0)
            {
                retained.RememberedSubdirectory = relative;
            }
        }
        retained.Text.Working = retained.Text.Home;
        services.ChangeDirectory(retained.Text.Working);
    }

    public CoreLoadStatus Load(string path, string passwordResetCall)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started)
        {
            throw new InvalidOperationException("Load operation already started");
        }

        ArgumentNullException.ThrowIfNull(path);
        if (path.Contains('\0') || !path.Contains('\\'))
        {
            throw new ArgumentException("Core load requires a qualified path without NUL", nameof(path));
        }

        started = true;
        retained.Extension = ClassicGameFiles.SelectExtension(
            false,
            runtime.World.Globals.Multiplayer,
            retained.Extension);
        retained.OpenedPath = path.Contains('.') ? path : path + retained.Extension;
        using var input = services.Storage.Read(retained.OpenedPath);
        if (input is null)
        {
            return CoreLoadStatus.Input;
        }

        try
        {
            var bytes = new List<byte>();
            for (int i = 0; i < OpenCiv2.Core.State.EliminationHistory.Capacity; ++i)
            {
                var value = input.Byte();
                if (value is null)
                {
                    input.Close();
                    return CoreLoadStatus.Input;
                }

                bytes.Add(value.Value);
            }
            GameHeader header;
            try
            {
                header = ClassicFiles.DecodeGameHeader(bytes.ToArray());
            }
            catch (DecodeException)
            {
                input.Close();
                return CoreLoadStatus.Input;
            }
            var status = ClassicFiles.GameVersionStatus(header.Version);
            if (status != VersionStatus.Supported)
            {
                input.Close();
                return status == VersionStatus.Newer ? CoreLoadStatus.Newer : CoreLoadStatus.Unsupported;
            }
            const int maximum = 256 * 1024 * 1024;
            for (; ; )
            {
                var part = input.Item(65536);
                if (part.Prefix.Count > 65536 || part.Complete && part.Prefix.Count != 65536)
                {
                    throw new ArgumentException("Inconsistent input transfer");
                }

                if (part.Prefix.Count > maximum - bytes.Count)
                {
                    throw new ArgumentException("Save exceeds admission limit");
                }

                bytes.AddRange(part.Prefix);
                if (!part.Complete)
                {
                    break;
                }
            }
            if (!input.Close())
            {
                return CoreLoadStatus.Input;
            }

            StoredSave imported;
            GameInput loaded;
            try
            {
                imported = ClassicFiles.DecodeStoredSave(bytes.ToArray());
                loaded = RestorationInput.Game(imported);
                GameInstallation.Validate(loaded);
            }
            catch (DecodeException)
            {
                return CoreLoadStatus.Data;
            }
            catch (ArgumentException)
            {
                return CoreLoadStatus.Data;
            }
            catch (IndexOutOfRangeException)
            {
                return CoreLoadStatus.Data;
            }
            Paths(path);
            var content = services.LoadContent();
            ArgumentNullException.ThrowIfNull(content);
            ArgumentNullException.ThrowIfNull(content.Rules);
            var rules = runtime.Process.Rules;
            content.Labels?.Install(rules.Labels);
            new RuleInstaller(content.Rules, rules).Load(true);
            services.ContentInstalled?.Invoke();
            GameInstallation.Install(loaded, runtime, content.Rules, retained.CurrentScreen, passwordResetCall);
            var world = runtime.World;
            var opaque = new RecordCompatibility(
                world.Lifetime)
            {
                Map = new(world.Map, imported.Map.Tiles, imported.Map.Regions),
                Globals = imported.Globals,
                Scenario = imported.Scenario,
                Multiplayer = imported.Multiplayer
            };
            imported.Civilizations.ToArray().CopyTo(opaque.Civilizations, 0);
            for (int i = 0; i < imported.Units.Count; ++i)
            {
                var token = world.Entities.Units.Storage[i].Token;
                if (token.Occupied)
                {
                    opaque.Units.Add(token, imported.Units[i]);
                }
            }
            for (int i = 0; i < imported.Cities.Count; ++i)
            {
                var token = world.Entities.Cities.Storage[i].Token;
                if (token.Occupied)
                {
                    opaque.Cities.Add(token, imported.Cities[i]);
                }
            }
            retained.RecordBytes = opaque;
            opaque.Attach(world.Lifetime);
            retained.EventBytes.Clear();
            if (imported.Events is { } events)
            {
                for (int i = 0; i < events.Count; ++i)
                {
                    var live = world.Events.Collection.At(i);
                    retained.EventBytes.Add(live.Identity, ImportedEventBytes.FromRecord(live, events[i].Record));
                }
            }

            retained.Starts = new();
            retained.Cursor = loaded.Cursor;
            retained.PackedLayout = StateConversion.ToStored(loaded.Layout);
            runtime.Publish();
            services.Flush();
            return CoreLoadStatus.Success;
        }
        catch
        {
            try
            {
                if (runtime.Active)
                {
                    runtime.Publish();
                }
            }
            catch { /* Preserve the original failure. */ }

            throw;
        }
    }
}
public static class ClassicGameFiles
{
    public static string SelectExtension(bool scenario, GameMode mode, string prior) => scenario ? ".SCN" : mode switch
    {
        GameMode.SinglePlayer => ".SAV",
        GameMode.Hotseat => ".HOT",
        GameMode.Email => ".EML",
        >= GameMode.Tcp and <= GameMode.Serial => ".NET",
        _ => prior
    };

    public static CoreLoadStatus Load(
        SessionRuntime runtime,
        ClassicFileState retained,
        string path,
        CoreLoadServices services,
        string passwordResetCall)
    {
        using var operation = new ClassicLoadOperation(runtime, retained, services);
        return operation.Load(path, passwordResetCall);
    }

    private static void PrepareScenario(bool scenario, GameState world)
    {
        ref var globals = ref world.Globals;
        if (!scenario)
        {
            globals.Status.ScenarioExport = false;
            return;
        }
        globals.Status.ScenarioExport = true;
        for (int advance = 0; advance < RuleLimits.Advances; ++advance)
        {
            globals.AdvanceKnownBy[advance] = default;
            int possessors = 0;
            byte first = 0;
            for (int civ = 1; civ < EntityLimits.Civilizations; ++civ)
            {
                if (CivilizationKnowledge.KnowsAdvance(world.Civilizations, civ, advance))
                {
                    globals.AdvanceKnownBy[advance] = globals.AdvanceKnownBy[advance].With(civ);
                    first = (byte)civ;
                    ++possessors;
                }
            }

            if (possessors != 0)
            {
                globals.FirstDiscoverer[advance] = possessors == 1 ? first : (byte)EntityLimits.Civilizations;
            }
        }
        for (int civ = 1; civ < EntityLimits.Civilizations; ++civ)
        {
            if (RecordCompatibility.Find(world.Lifetime) is { } opaque)
            {
                opaque.Civilizations[civ] = opaque.Civilizations[civ] with { Flags = (ushort)(opaque.Civilizations[civ].Flags & ~0x40) };
            }

            world.Civilizations[civ].Flags.Revolution = false;
            world.Civilizations[civ].Flags.GovernmentChanged = false;
            world.Civilizations[civ].Flags.PendingFreeAdvance = false;
        }
    }

    /// <summary>F06 production save: coherent encoding before open, retained preparation effects and truthful close.
    /// Providers do not admit mutation; storage owns replacement/commit policy.</summary>
    public static CoreSaveStatus Save(
        SessionRuntime runtime,
        ClassicFileState retained,
        string path,
        bool scenarioExport,
        IFileStorage storage)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(retained);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(path);
        if (!runtime.Active || runtime.HasPendingCommands)
        {
            throw new InvalidOperationException("Core save requires an active runtime with settled commands");
        }

        if (path.Contains('\0'))
        {
            throw new ArgumentException("Core path must not contain NUL", nameof(path));
        }

        using var pin = runtime.PinWorld();
        var world = runtime.World;
        ref var globals = ref world.Globals;
        if (globals.UnitEnd < 0 || globals.CityEnd < 0 || globals.UnitEnd != world.Entities.Units.End
            || globals.CityEnd != world.Entities.Cities.End)
        {
            throw new ArgumentException("Live save requires matching global and pool scan bounds");
        }

        retained.Extension = SelectExtension(scenarioExport, globals.Multiplayer, retained.Extension);
        retained.OpenedPath = path.Contains('.') ? path : path + retained.Extension;
        var bytes = new ByteWriter();
        byte[] image;
        try
        {
            globals.SavedCurrentCiv = unchecked((sbyte)world.Multiplayer.LocalHuman);
            PrepareScenario(scenarioExport, world);
            GameInstallation.PrepareScenarioNames(
                scenarioExport,
                globals,
                world.Civilizations,
                world.Names,
                runtime.Process.Rules);
            var exported = globals;
            if (exported.Multiplayer == GameMode.Hotseat)
            {
                exported.HumanPlayers = world.Multiplayer.HotseatParticipants;
            }

            bytes.Append(ClassicFiles.EncodeGameHeader());
            bool Append(ReadOnlySpan<byte> value)
            {
                bytes.Append(value);
                return true;
            }
            retained.RecordBytes = retained.RecordBytes?.For(world.Lifetime) ?? RecordCompatibility.Find(world.Lifetime);
            ClassicLive.WritePersistentItems(Append, exported, world.Names, world.Civilizations, retained.RecordBytes);
            ClassicLive.WriteMapItems(Append, MapStream.Embedded, world.Map, retained.Starts, retained.RecordBytes?.Map);
            ClassicLive.WriteMapSummaryItems(Append, world.Map, retained.RecordBytes?.Map);
            ClassicLive.WriteEntityItems(Append, world.Entities, retained.RecordBytes);
            ClassicLive.WriteLeaderItems(
                Append,
                exported,
                world.Civilizations,
                runtime.Process.Rules.Leaders,
                retained.Cursor);
            ClassicLive.WriteHistoryLayoutItems(
                Append,
                world.PowerHistory,
                retained.CurrentScreen,
                ref retained.PackedLayout,
                world.Layout,
                world.Views);
            ClassicLive.WriteScenarioHistoryItems(Append, scenarioExport, exported, world.Scenario, world.Eliminations, retained.RecordBytes);
            if (exported.Multiplayer != GameMode.SinglePlayer)
            {
                ClassicLive.WriteMultiplayerItem(
                    Append,
                    world.Multiplayer,
                    world.Multiplayer.NetworkTurnCiv,
                    retained.RecordBytes);
            }

            if (world.Events.Loaded && !scenarioExport)
            {
                ClassicLive.WriteEventItems(Append, Append, value =>
                {
                    bytes.U8(value);
                    return true;
                }, world.Events.Collection, retained.EventBytes);
            }

            image = bytes.ToArray();
        }
        catch
        {
            try
            {
                if (runtime.Active)
                {
                    runtime.Publish();
                }
            }
            catch { /* Preserve the original failure. */ }

            throw;
        }
        runtime.Publish();
        using var output = storage.Replace(retained.OpenedPath);
        if (output is null)
        {
            return CoreSaveStatus.Output;
        }

        bool written = output.Item(image);
        bool finalized = output.Close();
        return written && finalized ? CoreSaveStatus.Success : CoreSaveStatus.Output;
    }
}
