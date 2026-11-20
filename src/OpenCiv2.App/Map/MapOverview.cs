using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using OpenCiv2.Contracts;
using OpenCiv2.MapRendering;
using SkiaSharp;

namespace OpenCiv2.App.Map;

/// <summary>MV23 overview uses only admitted terrain and city marks from the same binding.</summary>
internal sealed class MapOverview : Control
{
    private readonly MapControl map;
    private readonly MapPainter painter;
    private MapPoint? keyboardPoint;
    internal MapPoint? KeyboardPoint => keyboardPoint;

    public MapOverview(MapControl map, MapPainter painter)
    {
        (this.map, this.painter) = (map, painter);
        Focusable = true;
        ClipToBounds = true;
        map.CameraChanged += (_, _) => InvalidateVisual();
        map.SizeChanged += (_, _) => InvalidateVisual();
        map.SelectionChanged += _ => InvalidateVisual();
        map.ObservationChanged += (_, _) => { keyboardPoint = null; ToolTip.SetTip(this, null); InvalidateVisual(); };
        map.MapResourcesChanged += (_, _) => InvalidateVisual();
        LostFocus += (_, _) => { keyboardPoint = null; InvalidateVisual(); };
        AddHandler(KeyDownEvent, OverviewKey, RoutingStrategies.Tunnel, handledEventsToo: true);
        AutomationProperties.SetAutomationId(this, "map-overview");
        AutomationProperties.SetName(this, "Map overview. Use arrows and Enter to recenter.");
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.Custom(new OverviewDraw(painter, map.Epoch, new Rect(Bounds.Size), map.Camera,
            map.Bounds.Size, keyboardPoint ?? map.Selection?.Position));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (map.Scene is not { } scene || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var point = e.GetPosition(this);
        var box = MapOverviewGeometry.Inner(scene.Map, Bounds.Width, Bounds.Height);
        if (MapOverviewGeometry.Target(scene.Map, box, point.X, point.Y) is not { } target || !scene.InRegion(target)) return;
        Focus(NavigationMethod.Pointer);
        keyboardPoint = target;
        map.Recenter(target);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (map.Scene is not { } scene) return;
        var point = e.GetPosition(this);
        var box = MapOverviewGeometry.Inner(scene.Map, Bounds.Width, Bounds.Height);
        var target = MapOverviewGeometry.Target(scene.Map, box, point.X, point.Y);
        ToolTip.SetTip(this, target is { } tile ? MapOverviewGeometry.Describe(scene, tile) : null);
    }

    private void OverviewKey(object? sender, KeyEventArgs e)
    {
        if (map.Scene is not { } scene || e.KeyModifiers != KeyModifiers.None) return;
        var point = keyboardPoint ?? map.Selection?.Position ?? new MapPoint(0, 0);
        int direction = e.Key switch { Key.Right => 1, Key.Down => 3, Key.Left => 5, Key.Up => 7, _ => -1 };
        if (direction >= 0)
        {
            var next = scene.Neighbor(point, direction) ?? point;
            if (scene.InRegion(next)) keyboardPoint = next;
        }
        else if (e.Key == Key.Enter) map.Recenter(point);
        else return;
        InvalidateVisual();
        e.Handled = true;
    }

    private sealed class OverviewDraw(MapPainter painter, long epoch, Rect bounds,
        MapCamera camera, Size viewport, MapPoint? selected) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;
        public bool HitTest(Point point) => bounds.Contains(point);
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null) return;
            using var lease = feature.Lease();
            painter.DrawOverview(lease.SkCanvas, epoch, bounds.Width, bounds.Height, camera, viewport.Width, viewport.Height, selected);
        }
    }
}
