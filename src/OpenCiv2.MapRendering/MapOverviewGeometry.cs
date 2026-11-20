using OpenCiv2.Contracts;
using SkiaSharp;

namespace OpenCiv2.MapRendering;

/// <summary>Shared minimap projection, picking and viewport footprint geometry.</summary>
public static class MapOverviewGeometry
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
        {
            return null;
        }

        int row = Math.Min(map.Height - 1, (int)((y - box.Top) * map.Height / box.Height));
        int column = Math.Min(map.Width - 2, 2 * (int)((x - box.Left) * (map.Width / 2) / box.Width));
        return new(column + (row & 1), row);
    }

    public static SKPoint CenterOf(MapObservation map, SKRect box, MapPoint point) => new(
        box.Left + box.Width * (point.X / 2 + .5f) / (map.Width / 2),
        box.Top + box.Height * (point.Y + .5f) / map.Height);

    public static string? Describe(MapScene scene, MapPoint point)
    {
        if (!scene.Contains(point) || !scene.InRegion(point))
        {
            return null;
        }

        if (scene.Objects(point).City?.Details is { } city)
        {
            return $"Civilization {city.Owner.Value} city";
        }

        var tile = scene.Tile(point);
        if (MapScene.RememberedCity(tile))
        {
            return "Remembered city site";
        }

        return tile.Terrain switch { null => "Unexplored", 10 => "Ocean", _ => "Land" };
    }

    public static IEnumerable<SKRect> Footprint(
        MapObservation map,
        SKRect box,
        MapCamera camera,
        double width,
        double height)
    {
        double span = width / (camera.Zoom * MapCamera.HalfWidth);
        double left = -camera.X / (camera.Zoom * MapCamera.HalfWidth);
        double top = Math.Clamp(-camera.Y / (camera.Zoom * MapCamera.HalfHeight), 0, map.Height);
        double bottom = Math.Clamp((height - camera.Y) / (camera.Zoom * MapCamera.HalfHeight), 0, map.Height);
        if (span <= 0 || bottom <= top)
        {
            yield break;
        }

        if (map.Flat)
        {
            double right = Math.Clamp(left + span, 0, map.Width);
            left = Math.Clamp(left, 0, map.Width);
            if (right > left)
            {
                yield return Box(left, right);
            }
        }
        else if (span >= map.Width)
        {
            yield return Box(0, map.Width);
        }
        else
        {
            left %= map.Width;
            if (left < 0)
            {
                left += map.Width;
            }

            double right = left + span;
            yield return Box(left, Math.Min(right, map.Width));
            if (right > map.Width)
            {
                yield return Box(0, right - map.Width);
            }
        }

        SKRect Box(double x1, double x2) => new(box.Left + (float)(box.Width * x1 / map.Width),
            box.Top + (float)(box.Height * top / map.Height), box.Left + (float)(box.Width * x2 / map.Width),
            box.Top + (float)(box.Height * bottom / map.Height));
    }
}
