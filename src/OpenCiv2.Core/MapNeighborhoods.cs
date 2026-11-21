using OpenCiv2.Contracts;

namespace OpenCiv2.Core;

/// <summary>W01 §3 ordered neighborhoods. Order participates in generation and path ties.</summary>
internal static class MapNeighborhoods
{
    internal static readonly MapPoint[] Directions =
        [new(1, -1), new(2, 0), new(1, 1), new(0, 2), new(-1, 1), new(-2, 0), new(-1, -1), new(0, -2)];
    internal static readonly MapPoint[] Coarse =
        [new(0, -1), new(1, -1), new(1, 0), new(1, 1), new(0, 1), new(-1, 1), new(-1, 0), new(-1, -1)];
    internal static readonly MapPoint[] City =
    [
        .. Directions,
        new(2, -2), new(2, 2), new(-2, 2), new(-2, -2),
        new(1, -3), new(3, -1), new(3, 1), new(1, 3),
        new(-1, 3), new(-3, 1), new(-3, -1), new(-1, -3), new(0, 0)
    ];

    internal static MapPoint Offset(Geometry shape, MapPoint point, MapPoint offset, bool flat) =>
        new(shape.WrapX(point.X + offset.X, flat), point.Y + offset.Y);
}
