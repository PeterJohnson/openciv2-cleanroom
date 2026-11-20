using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using SkiaSharp;

namespace OpenCiv2.App.Map;

internal sealed class MapControl : Control
{
    private const string MissingRenderer = "Skia map renderer unavailable";
    private readonly MapPainter painter;
    private readonly MapRendererSession renderer;
    internal event Action? RendererChanged;
    private readonly bool tracePresentation;
    private readonly bool traceFirstDraw;
    private MapScene? scene;
    private MapObservation? boundObservation;
    private MapCamera camera = MapCamera.Initial;
    private long epoch;
    private IPointer? pointer;
    private Point origin, last;
    private bool dragged, orderKeyDown, zoomGesture;
    private DrawReceipt? receipt, changedReceipt, firstReceipt;
    private long drawSequence;
    private static bool TraceLatency => MapRendererSession.TraceLatency;
    private int inputCode;
    private long inputAt;
    private TopLevel? host;

    internal sealed record DrawReceipt(long Sequence, double Milliseconds, long Epoch, MapCamera Camera, Size Viewport,
        double Scaling, MapDrawStats Stats, VisualFrame? Presentation, long StartedAt, long CompletedAt);

    public DrawReceipt? LastDraw => Volatile.Read(ref receipt);
    public DrawReceipt? LastChangedDraw => Volatile.Read(ref changedReceipt);
    public DrawReceipt? FirstDraw => Volatile.Read(ref firstReceipt);

    private void Drawn(long version, MapCamera camera, Size viewport, double scaling, MapDrawStats stats, double ms,
        VisualFrame visual, long startedAt)
    {
        if (version != Volatile.Read(ref epoch))
        {
            return;
        }

        long completedAt = traceFirstDraw && firstReceipt is null && stats.Tiles > 0 && stats.Error is null
            ? Stopwatch.GetTimestamp() : 0;
        var draw = new DrawReceipt(
            Interlocked.Increment(ref drawSequence),
            ms,
            version,
            camera,
            viewport,
            scaling,
            stats,
            tracePresentation ? visual : null,
            completedAt == 0 ? 0 : startedAt,
            completedAt);
        if (completedAt != 0)
        {
            Interlocked.CompareExchange(ref firstReceipt, draw, null);
        }

        if (stats.Tiles > 0
            && (stats.PreparedChunks > 0 || changedReceipt is not { } last || last.Epoch != version
                || last.Camera != camera
                ||
            last.Viewport != viewport || last.Scaling != scaling))
        {
            Volatile.Write(ref changedReceipt, draw);
        }

        var prior = Interlocked.Exchange(ref receipt, draw);
        if (prior?.Stats.Composition != stats.Composition || prior?.Stats.RendererFallback != stats.RendererFallback)
        {
            Dispatcher.UIThread.Post(() =>
            {
                RendererChanged?.Invoke();
                InvalidateVisual();
            });
        }

        if ((prior?.Stats.Error == MissingRenderer) != (stats.Error == MissingRenderer))
        {
            Dispatcher.UIThread.Post(InvalidateVisual);
        }
    }

    private MapPick? selected, hovered;
    private MapDisplay display;
    private readonly MapVisualTimeline timeline = new();
    private VisualFrame visual = VisualFrame.Empty;

    internal void AdvancePresentation(long now)
    {
        var next = timeline.Sample(scene, now, display.ReducedMotion);
        if (visual == next)
        {
            return;
        }

        visual = next;
        InvalidateVisual();
    }

    internal bool Queue(ulong sequence, string message, int durationMs) =>
        scene is not null
            && timeline.Queue(
                scene.Map.Identity,
                new MapNotice(sequence, message, durationMs),
                Environment.TickCount64);

    internal bool Queue(MapEffect effect) =>
        scene is not null && timeline.Queue(scene.Map.Identity, effect, Environment.TickCount64);

    internal void CancelPresentation()
    {
        timeline.CancelAll();
        AdvancePresentation(Environment.TickCount64);
    }

    public long Epoch => epoch;
    public MapPick? Selection => selected;

    public MapDisplay Display
    {
        get => display;
        set
        {
            if (display == value)
            {
                return;
            }

            display = value;
            AdvancePresentation(Environment.TickCount64);
            TrackIfNeeded();
            InvalidateVisual();
        }
    }

    public event Action<MapPick?>? HoverChanged;
    public event EventHandler? CameraChanged;
    public event EventHandler? ObservationChanged;
    public event EventHandler? MapResourcesChanged;

    public MapControl(MapPainter painter, bool tracePresentation = false, bool traceFirstDraw = false)
    {
        this.painter = painter;
        renderer = new(painter.Options);
        this.tracePresentation = tracePresentation;
        this.traceFirstDraw = traceFirstDraw;
        Focusable = true;
        ClipToBounds = true;
        LostFocus += (_, _) => orderKeyDown = false;
        SizeChanged += (_, _) =>
        {
            ClampToViewport();
            TrackIfNeeded();
        };
        AutomationProperties.SetAutomationId(this, "world-map");
        AutomationProperties.SetName(this, "World map");
    }

    public event Action<MapPick?>? SelectionChanged;
    public event Action? OrderRequested;
    public MapCamera Camera => camera;
    public MapScene? Scene => scene;
    internal (double IndexMs, long IndexAllocatedBytes, MapPainter.BindingTiming Renderer) LastBindingWork { get; private set; }

    public void Show(MapObservation? map)
    {
        if (ReferenceEquals(map, boundObservation))
        {
            return;
        }

        bool newBinding = map?.Identity != scene?.Map.Identity;
        long allocated = tracePresentation ? GC.GetAllocatedBytesForCurrentThread() : 0;
        long indexing = tracePresentation ? Stopwatch.GetTimestamp() : 0;
        boundObservation = map;
        scene = map is null ? null : MapScene.Create(map, scene);
        double indexMs = tracePresentation ? Stopwatch.GetElapsedTime(indexing).TotalMilliseconds : 0;
        long indexAllocatedBytes = tracePresentation ? GC.GetAllocatedBytesForCurrentThread() - allocated : 0;
        Hover(null);
        timeline.Bind(map?.Identity);
        AdvancePresentation(Environment.TickCount64);
        Volatile.Write(ref epoch, painter.SetScene(scene, tracePresentation, out var binding));
        if (tracePresentation)
        {
            LastBindingWork = (indexMs, indexAllocatedBytes, binding);
        }

        if (newBinding)
        {
            ChangeCamera(MapCamera.Initial);
            ReleasePointer();
            Select(null, notify: true);
        }
        else if (selected is { } pick && (scene is null || !scene.PermitsPick(pick)))
        {
            Select(null, notify: true);
        }

        ClampToViewport();
        TrackIfNeeded();
        InvalidateVisual();
        ObservationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Select(MapPick? value, bool notify = false)
    {
        if (selected == value)
        {
            if (notify)
            {
                SelectionChanged?.Invoke(value);
            }

            return;
        }
        selected = value;
        if (notify)
        {
            SelectionChanged?.Invoke(value);
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (LastDraw is { } last && last.Epoch == epoch && last.Stats.Error == MissingRenderer)
        {
            context.DrawRectangle(Brushes.Black, null, new Rect(Bounds.Size));
            context.DrawText(new FormattedText(MissingRenderer, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                Typeface.Default, 13, Brushes.White), new Point(12, 12));
        }
        context.Custom(new MapDraw(painter, renderer, epoch, camera, new(Bounds.Size),
            host?.RenderScaling ?? 1, selected, display, visual, hovered, inputCode, inputAt, Drawn));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        host = TopLevel.GetTopLevel(this);
        if (host is not null)
        {
            host.ScalingChanged += BackingScaleChanged;
        }

        LayoutUpdated += PrepareFirstLayout;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= PrepareFirstLayout;
        if (host is not null)
        {
            host.ScalingChanged -= BackingScaleChanged;
        }

        host = null;
        ReleasePointer();
        painter.InvalidateFrame();
        base.OnDetachedFromVisualTree(e);
    }

    private void BackingScaleChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void PrepareFirstLayout(object? sender, EventArgs e)
    {
        if (scene is null || host is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        LayoutUpdated -= PrepareFirstLayout;
        // Layout has supplied the actual camera, viewport and backing scale.
        painter.TryPrepareFirstFrameBodies(
            epoch,
            camera,
            Bounds.Width,
            Bounds.Height,
            host.RenderScaling,
            display,
            visual);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (scene is null || pointer is not null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();
        origin = last = e.GetPosition(this);
        dragged = false;
        zoomGesture = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        pointer = e.Pointer;
        pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (pointer != e.Pointer)
        {
            Hover(scene?.Pick(camera, point.X, point.Y, terrainOnly: display.Bare));
            return;
        }
        if (!dragged && Math.Max(Math.Abs(point.X - origin.X), Math.Abs(point.Y - origin.Y)) < 5)
        {
            return;
        }

        dragged = true;
        if (TraceLatency)
        {
            inputCode = (int)Math.Round((zoomGesture ? point.Y : point.X) * (host?.RenderScaling ?? 1)) & 0xffff;
            inputAt = Stopwatch.GetTimestamp();
        }
        if (zoomGesture)
        {
            Zoom(origin, (last.Y - point.Y) / 80);
        }
        else
        {
            Pan(point.X - last.X, point.Y - last.Y);
        }

        last = point;
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (pointer != e.Pointer || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        var point = e.GetPosition(this);
        bool pick = !dragged && new Rect(Bounds.Size).Contains(point);
        ReleasePointer();
        if (pick)
        {
            var chosen = scene?.Pick(camera, point.X, point.Y, terrainOnly: display.Bare);
            Select(chosen, notify: true);
        }
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (pointer == e.Pointer)
        {
            pointer = null;
        }
    }

    private void ReleasePointer()
    {
        var captured = pointer;
        pointer = null;
        captured?.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (scene is null || e.Delta.Y == 0)
        {
            return;
        }

        var point = e.GetPosition(this);
        Zoom(point, e.Delta.Y);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (scene is null)
        {
            return;
        }

        if (e.KeyModifiers == KeyModifiers.Control && e.Key is Key.Right or Key.Down or Key.Left or Key.Up)
        {
            int direction = e.Key switch { Key.Right => 1, Key.Down => 3, Key.Left => 5, _ => 7 };
            var first = hovered?.Position ?? selected?.Position ?? new MapPoint(0, 0);
            var next = scene.Neighbor(first, direction) ?? first;
            if (scene.InRegion(next))
            {
                var unit = new MapPick(next, MapPickKind.Unit);
                var city = new MapPick(next, MapPickKind.City);
                Hover(display.Bare ? new(next, MapPickKind.Tile) :
                    scene.ShowsUnit(next) ? unit : scene.PermitsPick(city) ? city : new(next, MapPickKind.Tile));
            }
            e.Handled = true;
            return;
        }
        if (e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Left:
                Pan(48, 0);
                break;
            case Key.Right:
                Pan(-48, 0);
                break;
            case Key.Up:
                Pan(0, 48);
                break;
            case Key.Down:
                Pan(0, -48);
                break;
            case Key.Add:
            case Key.OemPlus:
                Zoom(new(Bounds.Width / 2, Bounds.Height / 2), 1);
                break;
            case Key.Subtract:
            case Key.OemMinus:
                Zoom(new(Bounds.Width / 2, Bounds.Height / 2), -1);
                break;
            case Key.Home:
                Preset(1);
                break;
            case Key.PageUp:
                Preset(1.5);
                break;
            case Key.PageDown:
                Preset(0.35);
                break;
            case Key.Escape:
                Select(null, notify: true);
                Hover(null);
                break;
            case Key.Enter:
                orderKeyDown = true;
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key != Key.Enter)
        {
            return;
        }

        bool submit = orderKeyDown && scene is not null && e.KeyModifiers == KeyModifiers.None;
        orderKeyDown = false;
        if (!submit)
        {
            return;
        }

        OrderRequested?.Invoke();
        e.Handled = true;
    }

    private void Pan(double x, double y)
    {
        if (scene is null)
        {
            return;
        }

        ChangeCamera(camera.Pan(x, y, scene.Map, Bounds.Width, Bounds.Height));
    }

    private void ClampToViewport()
    {
        if (scene?.Map.Flat == true && Bounds.Width > 0 && Bounds.Height > 0)
        {
            Pan(0, 0);
        }
    }

    private void Zoom(Point anchor, double delta)
    {
        if (scene is null)
        {
            return;
        }

        ChangeCamera(camera.ZoomAt(anchor.X, anchor.Y, delta).Pan(0, 0, scene.Map, Bounds.Width, Bounds.Height));
    }

    private void Preset(double zoom) =>
        Zoom(new(Bounds.Width / 2, Bounds.Height / 2), Math.Log(zoom / camera.Zoom, 1.25));

    public void Recenter(MapPoint tile) => Recenter(tile, camera.Zoom);

    internal void Recenter(MapPoint tile, double zoom)
    {
        if (scene is null || !scene.Contains(tile) || !scene.InRegion(tile))
        {
            return;
        }

        var position = new MapCamera(Bounds.Width / 2 - tile.X * MapCamera.HalfWidth * zoom,
            Bounds.Height / 2 - tile.Y * MapCamera.HalfHeight * zoom, zoom);
        ChangeCamera(position.Pan(0, 0, scene.Map, Bounds.Width, Bounds.Height));
    }

    private void TrackIfNeeded()
    {
        if (!display.FollowActive || scene is not { } known || known.Annotations.ActiveUnit is not UnitTarget target ||
            known.Locate(target) is not { } point || !known.InRegion(point) || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        double x = camera.ScreenX(point.X), y = camera.ScreenY(point.Y);
        double margin = Math.Min(64 * camera.Zoom, Math.Min(Bounds.Width, Bounds.Height) / 4);
        if (!known.Map.Flat)
        {
            double period = known.Map.Width * MapCamera.HalfWidth * camera.Zoom;
            x += Math.Round((Bounds.Width / 2 - x) / period) * period;
        }
        if (x >= margin && x <= Bounds.Width - margin && y >= margin && y <= Bounds.Height - margin)
        {
            return;
        }

        Recenter(point);
    }

    private void ChangeCamera(MapCamera value)
    {
        if (camera == value)
        {
            return;
        }

        camera = value;
        InvalidateVisual();
        CameraChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Hover(MapPick? value)
    {
        if (hovered == value)
        {
            return;
        }

        hovered = value;
        HoverChanged?.Invoke(value);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Hover(null);
    }

    public void RecoverResources()
    {
        renderer.Retry();
        InvalidateVisual();
        MapResourcesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectArt(MapArtSelection? selection)
    {
        long next = painter.SelectArt(selection);
        if (epoch == next)
        {
            return;
        }

        Volatile.Write(ref epoch, next);
        InvalidateVisual();
        MapResourcesChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class MapDraw(
        MapPainter painter,
        MapRendererSession renderer,
        long epoch,
        MapCamera camera,
        Rect bounds,
        double scaling,
        MapPick? selected,
        MapDisplay display,
        VisualFrame visual,
        MapPick? hovered,
        int inputCode,
        long inputAt,
        Action<long, MapCamera, Size, double, MapDrawStats, double, VisualFrame, long> drawn) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;

        public bool HitTest(Point p) => bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null)
            {
                drawn(
                    epoch,
                    camera,
                    bounds.Size,
                    scaling,
                    new(MapRedraw.Error, 0, 0, 0, Error: MissingRenderer),
                    0,
                    visual,
                    0);
                return;
            }
            MapDrawStats stats;
            double elapsed;
            long startedAt;
            using (var lease = feature.Lease())
            {
                var canvas = lease.SkCanvas;
                canvas.Save();
                try
                {
                    canvas.ClipRect(new SKRect(0, 0, (float)bounds.Width, (float)bounds.Height));
                    var result = renderer.Draw(painter, lease.GrContext, canvas, epoch, camera,
                        bounds.Width, bounds.Height, scaling, selected, display, visual, hovered, inputCode, inputAt);
                    stats = result.Stats;
                    elapsed = result.Milliseconds;
                    startedAt = result.StartedAt;
                }
                finally
                {
                    canvas.Restore();
                }
            }
            drawn(epoch, camera, bounds.Size, scaling, stats, elapsed, visual, startedAt);
        }
    }
}
