using System.Diagnostics;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;

namespace OpenCiv2.App.Map;

internal readonly record struct MapStartupPreparation(double WorkMs, double WaitMs);

/// <summary>Native showcase for the same MapControl and painter as the scoped application.
/// All cases are clearly authored and this host exposes no gameplay command lane.</summary>
internal sealed class MapGalleryView : UserControl
{
    private readonly MapControl map;
    private readonly MapOverview overview;
    private readonly TextBlock details = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock title = new() { FontWeight = FontWeight.Bold };
    private GalleryBoard board;
    private readonly ComboBox selector;
    private readonly Button play, clear, change, flip, recover, companion, rendererButton;
    private readonly MapRendererSettingsView rendererSettings;
    private readonly CheckBox bare, follow, reducedMotion, flat, suppliedArt;
    private OpenCiv2.Contracts.MapPoint? lastUpdate;
    private (ulong Revision, double AuthoredMs, double BindMs)? updateWork;
    private ulong sequence;

    private sealed record StartupTiming(
        string Detail,
        long AdmissionStart,
        long AdmissionEnd,
        long FrameworkReady,
        long ViewStart,
        long ViewReady,
        double BoardMs,
        double BindMs,
        MapStartupPreparation Preparation,
        MapPainter Painter);

    private readonly StartupTiming? startup;

    public MapGalleryView(
        MapPainter painter,
        string initial,
        Action? openCompanion = null,
        MapArtSelection? selection = null,
        (string Detail, int Height, long AdmissionStart, long AdmissionEnd, long FrameworkReady, MapStartupPreparation Preparation)? coldView = null)
    {
        long boardStart = Stopwatch.GetTimestamp();
        board = MapGallery.Open(initial);
        if (coldView is not null)
        {
            board = board with { Display = board.Display with { ReducedMotion = true } };
        }

        double boardMs = Stopwatch.GetElapsedTime(boardStart).TotalMilliseconds;
        map = new(painter, tracePresentation: SeamApp.Observe is not null, traceFirstDraw: coldView is not null);
        overview = new(map, painter);
        selector = new ComboBox { ItemsSource = MapGallery.Cases, SelectedItem = initial, MinWidth = 110 };
        AutomationProperties.SetAutomationId(selector, "gallery-case");
        selector.SelectionChanged += (_, _) =>
        {
            if (selector.SelectedItem is not string id || id == board.Id)
            {
                return;
            }

            board = MapGallery.Open(id);
            Show();
        };
        play = new Button { Content = "Play authored effects" };
        AutomationProperties.SetAutomationId(play, "play-effects");
        play.Click += (_, _) =>
        {
            if (board.Id == "MV27")
            {
                map.Queue(++sequence, "Survey of Château du Vent ⤀ ⌒", 8000);
                map.AdvancePresentation(Environment.TickCount64);
                return;
            }
            map.Queue(++sequence, "Authored preview · movement and combat", 8000);
            map.Queue(++sequence, "No gameplay command is attached", 6000);
            foreach (var effect in MapGallery.Effects(board.Map))
            {
                map.Queue(effect with { Id = ++sequence, Milliseconds = effect.Milliseconds * 3 });
            }

            map.AdvancePresentation(Environment.TickCount64);
        };
        clear = new Button { Content = "Clear presentation" };
        clear.Click += (_, _) => map.CancelPresentation();
        change = new Button { Content = "Change observed world" };
        AutomationProperties.SetAutomationId(change, "change-world");
        change.Click += (_, _) =>
        {
            long changeStarted = SeamApp.Observe is null ? 0 : Stopwatch.GetTimestamp();
            var focus = board.Id is "Mixed" or "MV29"
                ? map.Camera.Hit(map.Bounds.Width * .48, map.Bounds.Height * .52, board.Map) : null;
            if (focus is { } tile && map.Scene?.Tile(tile).Terrain is null)
            {
                focus = null;
            }

            board = board with
            {
                Map = board.Id switch
                {
                    "MV09" => MapGallery.ChangeKnowledgeRegion(board.Map),
                    "MV17" => MapGallery.ChangeOwnerColors(board.Map),
                    "MV30" => MapGallery.ChangeColorSupport(board.Map),
                    _ => MapGallery.Changed(board.Map, focus)
                }
            };
            lastUpdate = focus;
            long bindStarted = changeStarted == 0 ? 0 : Stopwatch.GetTimestamp();
            map.Show(board.Map);
            if (bindStarted != 0)
            {
                updateWork = (
                    board.Map.Revision,
                    Stopwatch.GetElapsedTime(changeStarted, bindStarted).TotalMilliseconds,
                    Stopwatch.GetElapsedTime(bindStarted).TotalMilliseconds);
            }

            if (focus is { } center)
            {
                map.Queue(new MapEffect(++sequence, MapEffectKind.Explosion, center, center, 250));
                map.AdvancePresentation(Environment.TickCount64);
            }
        };
        flip = new Button { Content = "Change display modes" };
        AutomationProperties.SetAutomationId(flip, "change-display");
        flip.Click += (_, _) => map.Display = board.Id == "MV30" ? map.Display with
        {
            Color = (MapColorCorrection)(((int)map.Display.Color + 1) % 3)
        } : map.Display with
        {
            Grid = !map.Display.Grid,
            Ownership = !map.Display.Ownership,
            CityRadius = !map.Display.CityRadius,
            Color = map.Display.Color == MapColorCorrection.Normal
                ? MapColorCorrection.HighContrast
                : MapColorCorrection.Normal
        };
        recover = new Button { Content = "Reload textures" };
        recover.Click += (_, _) => map.RecoverResources();
        bare = new CheckBox { Content = "Bare terrain" };
        bare.IsCheckedChanged += (_, _) => map.Display = map.Display with { Bare = bare.IsChecked == true };
        suppliedArt = new CheckBox
        {
            Content = "Use supplied art",
            IsVisible = selection is not null,
            IsChecked = selection is not null
        };
        suppliedArt.IsCheckedChanged += (_, _) => map.SelectArt(suppliedArt.IsChecked == true ? selection : null);
        follow = new CheckBox { Content = "Follow active unit" };
        follow.IsCheckedChanged += (_, _) => map.Display = map.Display with { FollowActive = follow.IsChecked == true };
        reducedMotion = new CheckBox { Content = "Reduced motion" };
        reducedMotion.IsCheckedChanged += (_, _) => map.Display = map.Display with
        {
            ReducedMotion = reducedMotion.IsChecked == true
        };
        flat = new CheckBox { Content = "Flat map" };
        flat.IsCheckedChanged += (_, _) =>
        {
            bool enabled = flat.IsChecked == true;
            if (board.Map.Flat == enabled)
            {
                return;
            }

            board = board with { Map = board.Map with { Flat = enabled, Revision = board.Map.Revision + 1 } };
            map.Show(board.Map);
        };
        companion = new Button { Content = "Open companion map" };
        companion.Click += (_, _) => openCompanion?.Invoke();
        companion.IsEnabled = openCompanion is not null;
        var header = new WrapPanel { Margin = new Thickness(10, 6), ItemSpacing = 8, LineSpacing = 3 };
        foreach (Control control in new Control[] {
            selector,
            title,
            play,
            change,
            flip,
            recover,
            bare,
            suppliedArt,
            follow,
            clear,
            reducedMotion,
            flat,
            companion
            })
        {
            header.Children.Add(control);
        }

        rendererSettings = new(map, SeamApp.RendererPreferences);
        rendererButton = new Button { Content = "Renderer", Flyout = new Flyout { Content = rendererSettings } };
        header.Children.Add(rendererButton);
        var inner = new Grid();
        if (coldView is { } view)
        {
            inner.Height = view.Height;
            map.SizeChanged += PlaceColdCamera;
        }
        inner.Children.Add(map);
        inner.Children.Add(new Border
        {
            Width = 192,
            Height = 106,
            Margin = new Thickness(8),
            Child = overview,
            BorderBrush = Brush.Parse("#a98d65"),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        });
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), Background = Brush.Parse("#ede9df") };
        layout.Children.Add(header);
        Grid.SetRow(inner, 1);
        layout.Children.Add(inner);
        details.Margin = new Thickness(10, 3);
        Grid.SetRow(details, 2);
        layout.Children.Add(details);
        Content = layout;

        void Inspect(MapPick? pick) => details.Text = pick is { } selected && map.Scene is { } scene
            ? MapInspection.Describe(scene, selected)
            : "Authored map. Drag to pan, use the wheel to zoom and click the overview to recenter.";
        map.SelectionChanged += Inspect;
        map.HoverChanged += Inspect;
        long bindStart = Stopwatch.GetTimestamp();
        Show();
        if (coldView is { } setup)
        {
            long ready = Stopwatch.GetTimestamp();
            startup = new(
                setup.Detail,
                setup.AdmissionStart,
                setup.AdmissionEnd,
                setup.FrameworkReady,
                boardStart,
                ready,
                boardMs,
                Stopwatch.GetElapsedTime(bindStart, ready).TotalMilliseconds,
                setup.Preparation,
                painter);
        }

        void PlaceColdCamera(object? sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0 || coldView is not { } setup)
            {
                return;
            }

            map.SizeChanged -= PlaceColdCamera;
            var world = board.Map;
            double zoom = setup.Detail switch
            {
                "half" => Math.Clamp(Math.Sqrt(e.NewSize.Width * e.NewSize.Height /
                    (world.Width * world.Height * MapCamera.HalfWidth * MapCamera.HalfHeight * .5)), .25, 3),
                "overview" => .25,
                _ => 1.25
            };
            map.Recenter(new(world.Width / 4 & ~1, world.Height / 2 & ~1), zoom);
        }
    }

    private void Show()
    {
        sequence = 0;
        lastUpdate = null;
        updateWork = null;
        title.Text = $"{board.Id} · {board.Title} · Authored";
        play.Content = board.Id == "MV27" ? "Preview map text" : "Play authored effects";
        change.Content = board.Id == "MV30" ? "Toggle invalid color fixture" : "Change observed world";
        flip.Content = board.Id == "MV30" ? "Change color correction" : "Change display modes";
        map.Show(board.Map);
        map.Display = board.Display;
        bare.IsChecked = board.Display.Bare;
        follow.IsVisible = board.Id == "MV25";
        follow.IsChecked = board.Display.FollowActive;
        flat.IsVisible = board.Id is "MV23" or "MV25" or "Mixed";
        flat.IsChecked = board.Map.Flat;
        companion.IsVisible = companion.IsEnabled && board.Id == "MV29";
        reducedMotion.IsVisible = clear.IsVisible = board.Id is "MV22" or "MV24";
        reducedMotion.IsChecked = board.Display.ReducedMotion;
        details.Text = "Authored map. Drag to pan, use the wheel to zoom and click the overview to recenter.";
    }

    public void Tick() => map.AdvancePresentation(Environment.TickCount64);

    public bool CanObserve => startup is null || map.FirstDraw is not null || map.LastDraw?.Stats.Error is not null;

    public JsonObject Snapshot()
    {
        JsonObject BoundsFor(Control control)
        {
            var position = control.TranslatePoint(default, this) ?? default;
            return new()
            {
                ["x"] = position.X,
                ["y"] = position.Y,
                ["width"] = control.Bounds.Width,
                ["height"] = control.Bounds.Height
            };
        }
        JsonObject? RendererChoiceBounds()
        {
            var choice = rendererSettings.Choice;
            if (rendererButton.Flyout?.IsOpen != true || TopLevel.GetTopLevel(choice) is null
                || choice.Bounds.Width <= 0)
            {
                return null;
            }

            var origin = this.PointToScreen(default);
            var point = choice.PointToScreen(default);
            double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            return new()
            {
                ["x"] = (point.X - origin.X) / scale,
                ["y"] = (point.Y - origin.Y) / scale,
                ["width"] = choice.Bounds.Width,
                ["height"] = choice.Bounds.Height
            };
        }
        static JsonObject? Draw(MapControl.DrawReceipt? draw) => draw is null ? null : new()
        {
            ["sequence"] = draw.Sequence,
            ["epoch"] = draw.Epoch,
            ["ms"] = draw.Milliseconds,
            ["composition"] = draw.Stats.Composition,
            ["requestedRenderer"] = draw.Stats.RequestedRenderer,
            ["rendererFallback"] = draw.Stats.RendererFallback,
            ["gpuWaitMs"] = draw.Stats.GpuWaitMs,
            ["gpuSynchronized"] = draw.Stats.GpuSynchronized,
            ["gpuCacheBytes"] = draw.Stats.GpuCacheBytes,
            ["gpuCacheLimitBytes"] = draw.Stats.GpuCacheLimitBytes,
            ["gpuFeatures"] = draw.Stats.GpuFeatures,
            ["spriteCalls"] = draw.Stats.SpriteCalls,
            ["atlasBatches"] = draw.Stats.AtlasBatches,
            ["atlasBytes"] = draw.Stats.AtlasBytes,
            ["atlasAdmissions"] = draw.Stats.AtlasAdmissions,
            ["atlasEvictions"] = draw.Stats.AtlasEvictions,
            ["atlasHits"] = draw.Stats.AtlasHits,
            ["encodedSourceBytes"] = draw.Stats.EncodedSourceBytes,
            ["retainedPatches"] = draw.Stats.RetainedPatches,
            ["inputCode"] = draw.Stats.InputCode,
            ["inputToDrawMs"] = draw.Stats.InputToDrawMs,
            ["x"] = draw.Camera.X,
            ["y"] = draw.Camera.Y,
            ["zoom"] = draw.Camera.Zoom,
            ["scaling"] = draw.Scaling,
            ["width"] = draw.Viewport.Width,
            ["height"] = draw.Viewport.Height,
            ["tiles"] = draw.Stats.Tiles,
            ["objects"] = draw.Stats.Objects,
            ["batches"] = draw.Stats.Batches,
            ["chunks"] = draw.Stats.GroundChunks,
            ["preparedChunks"] = draw.Stats.PreparedChunks,
            ["parallelGroundChunks"] = draw.Stats.ParallelGroundChunks,
            ["alignedGroundChunks"] = draw.Stats.AlignedGroundChunks,
            ["groundCacheBytes"] = draw.Stats.GroundCacheBytes,
            ["groundCacheLimitBytes"] = draw.Stats.GroundCacheLimitBytes,
            ["groundTier"] = draw.Stats.GroundTier,
            ["groundBuildMs"] = draw.Stats.GroundBuildMs,
            ["groundSelectorMs"] = draw.Stats.GroundSelectorMs,
            ["texturePrepareMs"] = draw.Stats.TexturePrepareMs,
            ["tintPrepareMs"] = draw.Stats.TintPrepareMs,
            ["textureCacheBytes"] = draw.Stats.TextureCacheBytes,
            ["demandRasterLoads"] = draw.Stats.DemandRasterLoads,
            ["preparedBodyTextures"] = draw.Stats.PreparedBodyTextures,
            ["overlappedBodyTextures"] = draw.Stats.OverlappedBodyTextures,
            ["earlyBodyLeadMs"] = draw.Stats.EarlyBodyLeadMs,
            ["higherBodyTextures"] = draw.Stats.HigherBodyTextures,
            ["higherBodyWorkMs"] = draw.Stats.HigherBodyWorkMs,
            ["terrainMs"] = draw.Stats.TerrainMs,
            ["foregroundMs"] = draw.Stats.ForegroundMs,
            ["foregroundPlanMs"] = draw.Stats.ForegroundPlanMs,
            ["atlasPrepareMs"] = draw.Stats.AtlasPrepareMs,
            ["foregroundSubmitMs"] = draw.Stats.ForegroundSubmitMs,
            ["foregroundTraversals"] = draw.Stats.ForegroundTraversals,
            ["foregroundCommands"] = draw.Stats.ForegroundCommands,
            ["drawAllocatedBytes"] = draw.Stats.DrawAllocatedBytes,
            ["regionBuilds"] = draw.Stats.RegionBuilds,
            ["regionHits"] = draw.Stats.RegionHits,
            ["regionBytes"] = draw.Stats.RegionBytes,
            ["regionEvictions"] = draw.Stats.RegionEvictions,
            ["groundComparedTiles"] = draw.Stats.GroundComparedTiles,
            ["damageComparedTiles"] = draw.Stats.DamageComparedTiles,
            ["damageRegions"] = draw.Stats.DamageRegions,
            ["topologyHits"] = draw.Stats.TopologyHits,
            ["topologyBuilds"] = draw.Stats.TopologyBuilds,
            ["topologyEvictions"] = draw.Stats.TopologyEvictions,
            ["topologyBytes"] = draw.Stats.TopologyBytes,
            ["bandSetupMs"] = draw.Stats.BandSetupMs,
            ["bandRecordMs"] = draw.Stats.BandRecordMs,
            ["bandReplayMs"] = draw.Stats.BandReplayMs,
            ["bandRepairMs"] = draw.Stats.BandRepairMs,
            ["deferredGroundInvalidateMs"] = draw.Stats.DeferredGroundInvalidateMs,
            ["foregroundBands"] = draw.Stats.ForegroundBands,
            ["groundReplayBands"] = draw.Stats.GroundReplayBands,
            ["labelLayoutMs"] = draw.Stats.LabelLayoutMs,
            ["viewportBlitMs"] = draw.Stats.ViewportBlitMs,
            ["damagePixels"] = draw.Stats.DamagePixels,
            ["groundDamagePixels"] = draw.Stats.GroundDamagePixels,
            ["effects"] = draw.Stats.Effects,
            ["presentation"] = draw.Presentation is { } visual ? new JsonObject
            {
                ["pulse"] = visual.Pulse,
                ["explosions"] = new JsonArray(visual.Effects.Where(row => row.Effect.Kind == MapEffectKind.Explosion)
                    .Select(row => (JsonNode?)new JsonObject
                    {
                        ["id"] = row.Effect.Id,
                        ["x"] = row.Effect.To.X,
                        ["y"] = row.Effect.To.Y,
                        ["wrappedX"] = row.WrappedDestinationX,
                        ["progress"] = row.Progress
                    }).ToArray())
            } : null,
            ["moving"] = draw.Stats.Moving,
            ["notices"] = draw.Stats.Notices,
            ["labels"] = draw.Stats.Labels,
            ["error"] = draw.Stats.Error,
            ["redraw"] = draw.Stats.Redraw.ToString()
        };
        var camera = map.Camera;
        var active = map.Scene is { } scene && scene.Annotations.ActiveUnit is { } target ? scene.Locate(target) : null;
        var startupGround = startup?.Painter.StartupGroundPreparation;
        return new JsonObject
        {
            ["case"] = board.Id,
            ["x"] = camera.X,
            ["y"] = camera.Y,
            ["zoom"] = camera.Zoom,
            ["revision"] = board.Map.Revision,
            ["selectionX"] = map.Selection?.Position.X,
            ["selectionY"] = map.Selection?.Position.Y,
            ["selectionKind"] = map.Selection?.Kind.ToString(),
            ["scaling"] = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1,
            ["viewport"] = BoundsFor(map),
            ["overview"] = BoundsFor(overview),
            ["overviewFocused"] = overview.IsFocused,
            ["overviewX"] = overview.KeyboardPoint?.X,
            ["overviewY"] = overview.KeyboardPoint?.Y,
            ["selector"] = BoundsFor(selector),
            ["rendererButton"] = BoundsFor(rendererButton),
            ["rendererChoice"] = RendererChoiceBounds(),
            ["rendererMessage"] = rendererSettings.Message,
            ["savedRenderer"] = SeamApp.RendererPreferences.Saved.Mode.ToString().ToLowerInvariant(),
            ["play"] = BoundsFor(play),
            ["clear"] = BoundsFor(clear),
            ["change"] = BoundsFor(change),
            ["display"] = BoundsFor(flip),
            ["recover"] = BoundsFor(recover),
            ["bareToggle"] = BoundsFor(bare),
            ["bare"] = map.Display.Bare,
            ["artToggle"] = BoundsFor(suppliedArt),
            ["suppliedArt"] = suppliedArt.IsChecked == true,
            ["colorMode"] = map.Display.Color.ToString(),
            ["followToggle"] = BoundsFor(follow),
            ["follow"] = map.Display.FollowActive,
            ["flat"] = board.Map.Flat,
            ["flatToggle"] = BoundsFor(flat),
            ["companionButton"] = BoundsFor(companion),
            ["motionToggle"] = BoundsFor(reducedMotion),
            ["reducedMotion"] = map.Display.ReducedMotion,
            ["activeX"] = active?.X,
            ["activeY"] = active?.Y,
            ["worldWidth"] = board.Map.Width,
            ["worldHeight"] = board.Map.Height,
            ["updateX"] = lastUpdate?.X,
            ["updateY"] = lastUpdate?.Y,
            ["updateWork"] = updateWork is { } update && update.Revision == board.Map.Revision ? new JsonObject
            {
                ["revision"] = update.Revision,
                ["authoredMs"] = update.AuthoredMs,
                ["bindMs"] = update.BindMs,
                ["sceneIndexMs"] = map.LastBindingWork.IndexMs,
                ["sceneIndexAllocatedBytes"] = map.LastBindingWork.IndexAllocatedBytes,
                ["rendererWaitMs"] = map.LastBindingWork.Renderer.WaitMs,
                ["groundInvalidateMs"] = map.LastBindingWork.Renderer.InvalidateGroundMs,
                ["bindingDeferred"] = map.LastBindingWork.Renderer.Deferred
            } : null,
            ["inspection"] = details.Text,
            ["draw"] = Draw(map.LastDraw),
            ["changedDraw"] = Draw(map.LastChangedDraw),
            ["coldStartup"] = startup is { } start && map.FirstDraw is { } first ? new JsonObject
            {
                ["detail"] = start.Detail,
                ["admissionMs"] = Stopwatch.GetElapsedTime(start.AdmissionStart, start.AdmissionEnd).TotalMilliseconds,
                ["afterAdmissionMs"] = Stopwatch.GetElapsedTime(start.AdmissionEnd, first.CompletedAt)
                    .TotalMilliseconds,
                ["admissionStartToDrawMs"] = Stopwatch.GetElapsedTime(start.AdmissionStart, first.CompletedAt)
                    .TotalMilliseconds,
                ["frameworkSetupMs"] = Stopwatch.GetElapsedTime(start.AdmissionEnd, start.FrameworkReady)
                    .TotalMilliseconds,
                ["rendererSetupMs"] = Stopwatch.GetElapsedTime(start.FrameworkReady, start.ViewStart).TotalMilliseconds,
                ["viewSetupMs"] = Stopwatch.GetElapsedTime(start.ViewStart, start.ViewReady).TotalMilliseconds,
                ["windowAndDrawQueueMs"] = Stopwatch.GetElapsedTime(start.ViewReady, first.StartedAt).TotalMilliseconds,
                ["drawCallbackMs"] = Stopwatch.GetElapsedTime(first.StartedAt, first.CompletedAt).TotalMilliseconds,
                ["startupPreparationMs"] = start.Preparation.WorkMs,
                ["startupPreparationWaitMs"] = start.Preparation.WaitMs,
                ["startupGroundPreparationMs"] = startupGround?.WorkMs ?? 0,
                ["startupGroundJoinMs"] = startupGround?.JoinMs ?? 0,
                ["startupPreparedTextures"] = startupGround?.Textures ?? 0,
                ["startupPreparedTextureBytes"] = startupGround?.TextureBytes ?? 0,
                ["boardMs"] = start.BoardMs,
                ["bindMs"] = start.BindMs,
                ["firstDraw"] = Draw(first)
            } : null
        };
    }
}
