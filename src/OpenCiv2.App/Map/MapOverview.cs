using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using OpenCiv2.Contracts;
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

internal static class MapOverviewGeometry
{
    public static SKRect Inner(MapObservation map, double width, double height)
    {
        double scale = Math.Max(0, Math.Min((width - 8) / (map.Width * 2), (height - 8) / map.Height));
        float w = (float)(map.Width * 2 * scale), h = (float)(map.Height * scale);
        return SKRect.Create(((float)width - w) / 2, ((float)height - h) / 2, w, h);
    }

    public static MapPoint? Target(MapObservation map, SKRect box, double x, double y)
    {
        if (box.Width <= 0 || box.Height <= 0 || x < box.Left || x >= box.Right || y < box.Top || y >= box.Bottom)
            return null;
        int row = Math.Min(map.Height - 1, (int)((y - box.Top) * map.Height / box.Height));
        int column = Math.Min(map.Width - 2, 2 * (int)((x - box.Left) * (map.Width / 2) / box.Width));
        return new(column + (row & 1), row);
    }

    public static SKPoint CenterOf(MapObservation map, SKRect box, MapPoint point) => new(
        box.Left + box.Width * (point.X / 2 + .5f) / (map.Width / 2),
        box.Top + box.Height * (point.Y + .5f) / map.Height);

    public static string? Describe(MapScene scene, MapPoint point)
    {
        if (!scene.Contains(point) || !scene.InRegion(point)) return null;
        if (scene.Objects(point).City?.Details is { } city) return $"Civilization {city.Owner.Value} city";
        var tile = scene.Tile(point);
        if (MapScene.RememberedCity(tile)) return "Remembered city site";
        return tile.Terrain switch { null => "Unexplored", 10 => "Ocean", _ => "Land" };
    }

    public static IEnumerable<SKRect> Footprint(MapObservation map, SKRect box, MapCamera camera, double width, double height)
    {
        double span = width / (camera.Zoom * MapCamera.HalfWidth);
        double left = -camera.X / (camera.Zoom * MapCamera.HalfWidth);
        double top = Math.Clamp(-camera.Y / (camera.Zoom * MapCamera.HalfHeight), 0, map.Height);
        double bottom = Math.Clamp((height - camera.Y) / (camera.Zoom * MapCamera.HalfHeight), 0, map.Height);
        if (span <= 0 || bottom <= top) yield break;
        if (map.Flat)
        {
            double right = Math.Clamp(left + span, 0, map.Width);
            left = Math.Clamp(left, 0, map.Width);
            if (right > left) yield return Box(left, right);
        }
        else if (span >= map.Width) yield return Box(0, map.Width);
        else
        {
            left %= map.Width;
            if (left < 0) left += map.Width;
            double right = left + span;
            yield return Box(left, Math.Min(right, map.Width));
            if (right > map.Width) yield return Box(0, right - map.Width);
        }

        SKRect Box(double x1, double x2) => new(box.Left + (float)(box.Width * x1 / map.Width),
            box.Top + (float)(box.Height * top / map.Height), box.Left + (float)(box.Width * x2 / map.Width),
            box.Top + (float)(box.Height * bottom / map.Height));
    }
}
