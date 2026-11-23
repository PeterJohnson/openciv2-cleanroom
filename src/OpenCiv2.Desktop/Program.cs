using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using OpenCiv2.App;
using OpenCiv2.Core.State;
using OpenCiv2.Formats.Content;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.IO.Native;
using OpenCiv2.Platform.Native;
using OpenCiv2.Session;
using OpenCiv2.ViewModels;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args) => DesktopHost.Run(args);
}

/// <summary>Common native launcher used by the ordinary desktop and optional automation entry points.</summary>
public static class DesktopHost
{
    public static int Run(string[] args, Func<LocalGame, SessionPanel, IDesktopAutomation>? automation = null,
        LocalReplayRecorder? replay = null, params Assembly[] optionalAssemblies)
    {
        try
        {
            string? storageDirectory = null;
            bool localGame = false;
            string? assets = null,
                fonts = null,
                observation = null,
                native = null,
                scenario = null,
                installation = null,
                showcase = null,
                coldView = null;
            string? renderer = null, gpuCache = null, rendererSettings = null;
            (int Width, int Height)? size = null;
            for (int i = 0; i < args.Length; i += 2)
            {
                if (i + 1 == args.Length)
                {
                    throw Usage();
                }

                switch (args[i])
                {
                    case "--assets" when assets is null:
                        assets = args[i + 1];
                        break;
                    case "--fonts" when fonts is null:
                        fonts = args[i + 1];
                        break;
                    case "--observe" when observation is null:
                        observation = args[i + 1];
                        break;
                    case "--map-art" when native is null:
                        native = args[i + 1];
                        break;
                    case "--scenario-art" when scenario is null:
                        scenario = args[i + 1];
                        break;
                    case "--installation-art" when installation is null:
                        installation = args[i + 1];
                        break;
                    case "--game" when !localGame && args[i + 1] == "alder-valley":
                        localGame = true;
                        break;
                    case "--showcase" when showcase is null:
                        showcase = args[i + 1];
                        break;
                    case "--cold-view" when coldView is null:
                        coldView = args[i + 1];
                        break;
                    case "--map-renderer" when renderer is null:
                        renderer = args[i + 1];
                        break;
                    case "--map-gpu-cache-mib" when gpuCache is null:
                        gpuCache = args[i + 1];
                        break;
                    case "--storage-directory" when storageDirectory is null:
                        storageDirectory = args[i + 1];
                        break;
                    case "--renderer-settings" when rendererSettings is null:
                        rendererSettings = args[i + 1];
                        break;
                    case "--window" when size is null:
                        string[] parts = args[i + 1].Split('x');
                        if (parts.Length != 2 || !int.TryParse(parts[0], out int width)
                            || !int.TryParse(parts[1], out int height)
                            ||
                            width is < 600 or > 5500 || height is < 500 or > 3200)
                        {
                            throw Usage();
                        }

                        size = (width, height);
                        break;
                    default:
                        throw Usage();
                }
            }

            if (Environment.GetEnvironmentVariable("OPENCIV2_MAP_EXPERIMENT") == "1")
            {
                renderer ??= Environment.GetEnvironmentVariable("OPENCIV2_MAP_COMPOSITION");
                gpuCache ??= Environment.GetEnvironmentVariable("OPENCIV2_MAP_GPU_CACHE_MIB");
            }
            SeamApp.ConfigureServicesAsync(new NativeByteStore(storageDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenCiv2", "storage")), media: new NativeAudio())
                .GetAwaiter().GetResult();
            SeamApp.ConfigureMapRenderer(RendererPreferences.Load(rendererSettings, renderer, gpuCache));
            bool overrides = native is not null || scenario is not null || installation is not null;
            if ((assets is null) != (fonts is null) || overrides && assets is null)
            {
                throw Usage();
            }

            if (localGame && showcase is not null || size is not null && showcase is null || coldView is not null &&
                (coldView is not ("full" or "half" or "overview") || showcase != "Mixed" || size is null
                    || observation is null
                    || assets is null))
            {
                throw Usage();
            }

            long admissionStart = 0, admissionEnd = 0;
            if (assets is not null && fonts is not null)
            {
                admissionStart = Stopwatch.GetTimestamp();
                var atlas = PinnedGraphics.LoadAtlas(new ContentDirectory(assets).OpenRead);
                var fontBytes = PinnedGraphics.LoadFonts(new ContentDirectory(fonts).OpenRead);
                var selected = overrides ? MapArtSelection.Load(
                    native is not null ? new ContentDirectory(native).OpenRead : null,
                    scenario is not null ? new ContentDirectory(scenario).OpenClassicLeaf : null,
                    installation is not null ? new ContentDirectory(installation).OpenClassicLeaf : null) : null;
                if (selected is not null)
                {
                    foreach (string warning in selected.Diagnostics)
                    {
                        System.Console.Error.WriteLine(warning);
                    }
                }

                SeamApp.ConfigureGraphics(atlas, fontBytes, selected);
                admissionEnd = Stopwatch.GetTimestamp();
            }

            if (localGame)
            {
                string preferenceDirectory = storageDirectory ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenCiv2", "storage");
                Directory.CreateDirectory(preferenceDirectory);
                var preferenceStorage = new ClassicStorage(preferenceDirectory);
                var preferenceState = new ProcessContext();
                if (PreferenceFiles.Load(preferenceState, new(1024, true), preferenceStorage) == PreferenceLoadStatus.Input)
                {
                    throw new IOException("Could not read gameplay preferences");
                }
                SeamApp.ConfigureLocalGame(automation, replay, new ClassicPreferenceWriter(preferenceStorage),
                    PreferenceFiles.Capture(preferenceState));
            }

            if (showcase is not null)
            {
                SeamApp.ConfigureShowcase(
                    showcase,
                    size,
                    coldView is null ? null : (coldView, admissionStart, admissionEnd));
            }

            // Explicit read-only test observation; ordinary launches perform no report-file writes.
            if (observation is not null)
            {
                string path = Path.GetFullPath(observation);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                void WriteIdentity() =>
                    File.WriteAllText(
                        path + ".identity.json",
                        ApplicationIdentity.Read(typeof(DesktopHost).Assembly,
                            [typeof(ContentDirectory).Assembly, typeof(NativeAudio).Assembly, .. optionalAssemblies]));
                if (coldView is null)
                {
                    WriteIdentity();
                }

                string? previous = null;
                SeamApp.Observe = value =>
                {
                    if (value == previous)
                    {
                        return;
                    }

                    if (previous is null && coldView is not null)
                    {
                        WriteIdentity();
                    }

                    File.WriteAllText(path + ".tmp", value);
                    File.Move(path + ".tmp", path, true);
                    previous = value;
                };
            }
            SeamApp.BeginDesktopMapPreparation();
            try
            {
                return AppBuilder.Configure<SeamApp>().UsePlatformDetect().WithInterFont()
                    .StartWithClassicDesktopLifetime(
                        []);
            }
            finally
            {
                SeamApp.ReleasePendingDesktopMapPreparation();
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException
            or UnauthorizedAccessException
            or ContentException
            or JsonException
            or FormatException)
        {
            System.Console.Error.WriteLine(e.Message);
            return 2;
        }
    }

    private static ArgumentException Usage() =>
        new(
            "Usage: OpenCiv2.Desktop [--assets ROOT --fonts ROOT] [--map-art ROOT] [--scenario-art ROOT] [--installation-art ROOT] [--game alder-valley] [--showcase CASE [--window WIDTHxHEIGHT]] [--observe PATH] [--cold-view full|half|overview] [--map-renderer cpu|gpu] [--map-gpu-cache-mib 128..1024] [--renderer-settings PATH] [--storage-directory PATH]");
}
