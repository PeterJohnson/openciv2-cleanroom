using OpenCiv2.Base;

namespace OpenCiv2.Core;

public readonly record struct Position(int X, int Y);

/// <summary>W01 doubled coordinates and admitted classic dimensions.</summary>
public sealed class Geometry
{
    public int Width { get; }
    public int Height { get; }
    public int Area { get; }
    public int CoarseWidth => (Width + 3) / 4;
    public int CoarseHeight => (Height + 3) / 4;
    public Geometry(int width, int height)
    {
        if (width <= 0 || width > 32767 || (width & 1) != 0 || height <= 0 || height > 32767)
            throw new ArgumentException("Map dimensions require positive even i16 width and positive i16 height");
        int area = width / 2 * height;
        if (area >= 32768) throw new ArgumentException("Map area must be in 1..32767");
        Width = width; Height = height; Area = area;
    }
    public bool InBounds(Position p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;
    public bool Canonical(Position p) => InBounds(p) && (p.X & 1) == (p.Y & 1);
    public int? TileIndex(Position p) => InBounds(p) ? p.Y * (Width / 2) + p.X / 2 : null;
    public Position Coordinates(int index)
    {
        if (index < 0 || index >= Area) throw new ArgumentOutOfRangeException(nameof(index));
        int y = index / (Width / 2);
        return new(index % (Width / 2) * 2 + (y & 1), y);
    }
    private static int Abs32(int value) => value <= 0 ? unchecked(-value) : value;
    private static int SingleWrap(int x, int width, bool flat) => flat ? x : x < 0
        ? unchecked(x + width) : x >= width ? unchecked(x - width) : x;
    public int WrapX(int x, bool flat) => SingleWrap(x, Width, flat);
    public int CoarseWrapX(int x, bool flat) => SingleWrap(x, CoarseWidth, flat);
    public int WrappedDx(int x1, int x2, bool flat)
    {
        int delta = Abs32(unchecked(x1 - x2));
        return !flat && delta > Width / 2 ? unchecked(Width - delta) : delta;
    }
    public int StepDistance(Position first, Position second, bool flat) =>
        Arithmetic.ShiftSigned(unchecked(WrappedDx(first.X, second.X, flat) + Abs32(unchecked(first.Y - second.Y))), -1);
    public static int WeightedFromDeltas(Position delta)
    {
        int dx = Abs32(delta.X), dy = Abs32(delta.Y);
        int half = Arithmetic.ShiftSigned(unchecked(dx + dy), -1);
        int correction = Arithmetic.ShiftSigned(unchecked(half - Math.Min(dx, dy) + 1), -1);
        return unchecked(Math.Max(dx, dy) - correction);
    }
    public int WeightedDistance(Position first, Position second, bool flat) =>
        WeightedFromDeltas(new(WrappedDx(first.X, second.X, flat), Abs32(unchecked(first.Y - second.Y))));
    public bool ContainsX(int x, int left, int length, bool flat)
    {
        int end = unchecked(left + length);
        if (!flat)
        {
            if (x < left) x = unchecked(x + Width);
            if (x >= end) x = unchecked(x - Width);
        }
        return x >= left && unchecked(end - x) >= 1;
    }
    public bool ContainsRectangle(Position p, Position origin, Position extent, bool flat) =>
        ContainsX(p.X, origin.X, extent.X, flat) && p.Y >= origin.Y && p.Y < unchecked(origin.Y + extent.Y);
    public OwnedList<Position> Neighbors(Position center, IEnumerable<Position> offsets, bool flat)
    {
        var result = new List<Position>();
        foreach (var offset in offsets)
        {
            var p = new Position(WrapX(unchecked(center.X + offset.X), flat), unchecked(center.Y + offset.Y));
            if (InBounds(p)) result.Add(p);
        }
        return new(result);
    }
    public static byte DirectionOf(Position delta, IReadOnlyList<Position> directions)
    {
        if (directions.Count < 8) throw new ArgumentException("Eight direction entries are required.");
        byte result = 8;
        for (byte i = 0; i < 8; ++i)
            if (Math.Sign(delta.X) == Math.Sign(directions[i].X) && Math.Sign(delta.Y) == Math.Sign(directions[i].Y)) result = i;
        return result;
    }
    public static bool GrasslandShield(Position p)
    {
        int half = Arithmetic.ShiftSigned(unchecked(p.X + p.Y), -1);
        return (unchecked((uint)(-half - p.X)) & 3) < 2;
    }
}
