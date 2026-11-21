using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using OpenCiv2.App.Map;
using OpenCiv2.Base;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.IO;
using OpenCiv2.MapRendering;

namespace OpenCiv2.App;

public sealed class SeamApp : Application
{
    internal static IByteStore? Storage { get; private set; }
    internal static IFileTransfer? FileTransfer { get; private set; }
    internal static ShellPreferences? DisplayPreferences { get; private set; }
    internal static IPlatformClock Clock { get; private set; } = new SystemPlatformClock();
    internal static IPresentationMedia Media { get; private set; } = new SilentMedia();
    public static async Task ConfigureServicesAsync(IByteStore storage, IFileTransfer? transfer = null,
        IPlatformClock? clock = null, IPresentationMedia? media = null)
    {
        Storage = storage;
        FileTransfer = transfer;
        Clock = clock ?? new SystemPlatformClock();
        Media = media ?? new SilentMedia();
        DisplayPreferences = new(storage);
        await DisplayPreferences.LoadAsync();
    }

    private SeamSession? owner;
    private MapPainter? painter;
    private DispatcherTimer? timer;
    private GraphicsFonts? fonts;
    private static (AssetAtlas Atlas, PinnedFonts Fonts, MapArtSelection? Selected)? content;
    private static Task<PreparedPainter>? desktopPreparation;
    internal static MapRendererPreferences RendererPreferences { get; private set; } = new(new(), new());

    public static void ConfigureMapRenderer(MapRendererPreferences preferences) => RendererPreferences = preferences;

    private sealed record PreparedPainter(MapPainter Painter, long Started, long Completed);

    private static string? showcase;
    private static (int Width, int Height)? showcaseSize;
    private static (string Detail, long AdmissionStart, long AdmissionEnd)? coldView;

    public static void ConfigureShowcase(string id, (int Width, int Height)? size = null,
        (string Detail, long AdmissionStart, long AdmissionEnd)? startup = null)
    {
        if (!MapGallery.Cases.Contains(id))
        {
            throw new ArgumentException($"Unknown showcase case '{id}'");
        }

        showcase = id;
        showcaseSize = size;
        coldView = startup;
    }

    public static Action<string>? Observe { get; set; }
    public static SeamView? CurrentView { get; private set; }
    internal static MapGalleryView? CurrentGallery { get; private set; }

    public static void ConfigureGraphics(AssetAtlas atlas, PinnedFonts fonts, MapArtSelection? selected = null) =>
        content = (atlas, fonts, selected);

    public static void BeginDesktopMapPreparation()
    {
        if (content is not { } assets || desktopPreparation is not null || Environment.ProcessorCount < 8)
        {
            return;
        }
        // Content has been admitted. CPU images can be prepared while the
        // desktop thread initializes Avalonia and constructs the first window.
        desktopPreparation = Task.Run(() =>
        {
            long started = Stopwatch.GetTimestamp();
            var renderer = new MapPainter(
                assets.Atlas,
                assets.Fonts,
                assets.Selected,
                options: RendererPreferences.Startup);
            try
            {
                renderer.BeginStartupGroundPreparation();
                return new PreparedPainter(renderer, started, Stopwatch.GetTimestamp());
            }
            catch
            {
                renderer.Dispose();
                throw;
            }
        });
    }

    public static void ReleasePendingDesktopMapPreparation()
    {
        var pending = desktopPreparation;
        desktopPreparation = null;
        if (pending is null)
        {
            return;
        }

        try
        {
            pending.GetAwaiter().GetResult().Painter.Dispose();
        }
        catch (InvalidDataException) { } // Startup exited before consuming the renderer.
    }

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        long frameworkReady = coldView is not null ? Stopwatch.GetTimestamp() : 0;
        var selected = content;
        content = null;
        if (selected is { } assets)
        {
            fonts = new GraphicsFonts(assets.Fonts);
            FontManager.Current.AddFontCollection(fonts);
            GraphicsFonts.VerifyRoles();
        }
        var pending = desktopPreparation;
        desktopPreparation = null;
        MapStartupPreparation preparation = default;
        if (pending is null)
        {
            painter = new(selected?.Atlas, selected?.Fonts, selected?.Selected, options: RendererPreferences.Startup);
        }
        else
        {
            long waiting = Stopwatch.GetTimestamp();
            var ready = pending.GetAwaiter().GetResult();
            painter = ready.Painter;
            preparation = new(Stopwatch.GetElapsedTime(ready.Started, ready.Completed).TotalMilliseconds,
                Stopwatch.GetElapsedTime(waiting).TotalMilliseconds);
        }
        if (showcase is { } caseId && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime galleryDesktop)
        {
            showcase = null;
            StartShowcase(galleryDesktop, painter, caseId, selected, frameworkReady, preparation);
            base.OnFrameworkInitializationCompleted();
            return;
        }
        if (showcase is { } browserCase && ApplicationLifetime is ISingleViewApplicationLifetime galleryBrowser)
        {
            showcase = null;
            var gallery = new MapGalleryView(painter, browserCase, selection: selected?.Selected);
            CurrentGallery = gallery;
            galleryBrowser.MainView = gallery;
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
            timer.Tick += (_, _) =>
            {
                gallery.Tick();
                if (gallery.CanObserve)
                {
                    Observe?.Invoke(gallery.Snapshot().ToJsonString());
                }
            };
            timer.Start();
            base.OnFrameworkInitializationCompleted();
            return;
        }
        owner = new(Clock);
        var view = new SeamView(owner.Panel, painter, selected is not null, selected?.Selected);
        CurrentView = view;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "OpenCiv2 · Riverbend",
                Width = 1020,
                Height = 720,
                MinWidth = 640,
                MinHeight = 640,
                Content = view
            };
            view.AttachMenu(desktop.MainWindow);
            desktop.Exit += (_, _) => Stop();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            single.MainView = view;
        }

        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
        timer.Tick += (_, _) =>
        {
            owner.Tick();
            view.Refresh();
            view.AdvancePresentation();
            Observe?.Invoke(view.Snapshot());
        };
        timer.Start();
        base.OnFrameworkInitializationCompleted();
    }

    private void StartShowcase(
        IClassicDesktopStyleApplicationLifetime desktop,
        MapPainter primaryPainter,
        string caseId,
        (AssetAtlas Atlas, PinnedFonts Fonts, MapArtSelection? Selected)? assets,
        long frameworkReady,
        MapStartupPreparation preparation)
    {
        Window? companion = null;
        MapGalleryView? companionView = null;
        var size = showcaseSize ?? (1150, 780);
        var cold = coldView;
        showcaseSize = null;
        coldView = null;
        var gallery = new MapGalleryView(
            primaryPainter,
            caseId,
            OpenCompanion,
            assets?.Selected,
            cold is { } start
                ? (start.Detail, size.Height, start.AdmissionStart, start.AdmissionEnd, frameworkReady, preparation)
                : null);
        var main = CreateWindow(gallery, "Map showcase", size);
        if (cold is not null)
        {
            main.ClearValue(Window.HeightProperty);
            main.SizeToContent = SizeToContent.Height;
        }
        main.Closed += (_, _) => companion?.Close();
        desktop.MainWindow = main;
        desktop.Exit += (_, _) => Stop();
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
        timer.Tick += (_, _) =>
        {
            gallery.Tick();
            companionView?.Tick();
            if (Observe is { } observe && gallery.CanObserve)
            {
                var snapshot = gallery.Snapshot();
                snapshot["companion"] = companionView?.Snapshot();
                observe(snapshot.ToJsonString());
            }
        };
        timer.Start();

        void OpenCompanion()
        {
            if (companion is not null)
            {
                companion.Activate();
                return;
            }
            var renderer = new MapPainter(
                assets?.Atlas,
                assets?.Fonts,
                assets?.Selected,
                options: RendererPreferences.Startup);
            companionView = new MapGalleryView(renderer, "MV23", selection: assets?.Selected);
            var window = CreateWindow(companionView, "Map companion", (840, 620));
            window.Closed += (_, _) =>
            {
                companion = null;
                companionView = null;
                renderer.Dispose();
            };
            companion = window;
            window.Show();
        }

        static Window CreateWindow(MapGalleryView view, string title, (int Width, int Height) size) => new()
        {
            Title = $"OpenCiv2 · {title}",
            Width = size.Width,
            Height = size.Height,
            MinWidth = 600,
            MinHeight = 500,
            Content = view
        };
    }

    private void Stop()
    {
        timer?.Stop();
        CurrentView?.StopServices();
        owner?.Dispose();
        owner = null;
        painter?.Dispose();
        painter = null;
        if (fonts is not null)
        {
            FontManager.Current.RemoveFontCollection(fonts.Key);
        }

        fonts = null;
        CurrentView = null;
        CurrentGallery = null;
    }
}
