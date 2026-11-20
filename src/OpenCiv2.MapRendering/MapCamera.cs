using OpenCiv2.Contracts;

namespace OpenCiv2.MapRendering;

public readonly record struct MapCamera(double X, double Y, double Zoom)
{
    public const int HalfWidth = 32, HalfHeight = 16;
    public static MapCamera Initial => new(80, 60, 1.25);

    public double ScreenX(int x) => X + x * HalfWidth * Zoom;

    public double ScreenY(int y) => Y + y * HalfHeight * Zoom;

    public MapCamera ZoomAt(double x, double y, double steps)
    {
        double zoom = Math.Clamp(Zoom * Math.Pow(1.25, steps), 0.25, 3);
        double ratio = zoom / Zoom;
        return new(x - (x - X) * ratio, y - (y - Y) * ratio, zoom);
    }

    public MapCamera Pan(double dx, double dy, MapObservation map, double width, double height)
    {
        double x = X + dx;
        double period = map.Width * HalfWidth * Zoom;
        if (map.Flat)
        {
            x = Math.Clamp(x, HalfWidth * Zoom - period, width - HalfWidth * Zoom);
        }
        else
        {
            x %= period;
        }

        double y = Math.Clamp(Y + dy, -(map.Height - 1) * HalfHeight * Zoom, Math.Max(0, height - HalfHeight * Zoom));
        return this with { X = x, Y = y };
    }

    // P01/W01: diamond coordinates are the two integer axes of this rotated lattice.
    // On an exactly shared edge choose an in-domain cell, preferring the lower y then x.
    public MapPoint? Hit(double x, double y, MapObservation map)
    {
        double dx = (x - X) / (Zoom * HalfWidth), dy = (y - Y) / (Zoom * HalfHeight);
        double u = (dx + dy) / 2, v = (dx - dy) / 2;
        if (!double.IsFinite(u) || !double.IsFinite(v) || Math.Abs(u) > 10000000 || Math.Abs(v) > 10000000)
        {
            return null;
        }

        MapPoint? best = null;
        int firstU = (int)Math.Floor(u), firstV = (int)Math.Floor(v);
        for (int a = firstU; a <= firstU + 1; a++)
        {
            for (int b = firstV; b <= firstV + 1; b++)
            {
                if (Math.Abs(a - u) > 0.500000001 || Math.Abs(b - v) > 0.500000001)
                {
                    continue;
                }

                int column = a + b, row = a - b;
                if (row < 0 || row >= map.Height || map.Flat && (column < 0 || column >= map.Width))
                {
                    continue;
                }

                if (best is not { } found || row < found.Y || row == found.Y && column < found.X)
                {
                    best = new(column, row);
                }
            }
        }
        return best is { } point ? point with { X = CanonicalX(point.X, map.Width) } : null;
    }

    public static int CanonicalX(int x, int width)
    {
        int reduced = x % width;
        return reduced < 0 ? reduced + width : reduced;
    }

    public IEnumerable<(MapPoint Point, int DisplayX)> Visible(MapObservation map, double width, double height)
    {
        var (left, top, right, bottom) = VisibleBounds(map, width, height);
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left + ((left ^ y) & 1); x <= right; x += 2)
            {
                yield return (new(CanonicalX(x, map.Width), y), x);
            }
        }
    }

    internal MapVisibleBounds VisibleBounds(MapObservation map, double width, double height)
    {
        // Include the full extent of the tile, sprites above it, and labels below it.
        int top = Math.Max(0, (int)Math.Ceiling(-Y / (Zoom * HalfHeight) - 2));
        int bottom = Math.Min(map.Height - 1, (int)Math.Floor((height - Y) / (Zoom * HalfHeight) + 3));
        int left = (int)Math.Ceiling(-X / (Zoom * HalfWidth) - 2);
        int right = (int)Math.Floor((width - X) / (Zoom * HalfWidth) + 2);
        if (map.Flat)
        {
            left = Math.Max(0, left);
            right = Math.Min(map.Width - 1, right);
        }
        return new(left, top, right, bottom);
    }
}

internal readonly record struct MapVisibleBounds(int Left, int Top, int Right, int Bottom)
{
    public int TileCount()
    {
        int columns = Math.Max(0, Right - Left + 1), rows = Math.Max(0, Bottom - Top + 1);
        return columns / 2 * rows + (columns % 2 == 0 ? 0 : (rows + (1 - ((Left ^ Top) & 1))) / 2);
    }
}
