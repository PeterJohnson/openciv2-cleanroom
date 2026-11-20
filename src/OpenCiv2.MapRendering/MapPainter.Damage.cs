using OpenCiv2.Contracts;
using SkiaSharp;

namespace OpenCiv2.MapRendering;

public sealed partial class MapPainter
{
    private const int DamageGuard = 8;

    internal (IReadOnlyList<SKRectI>? Regions, int ComparedTiles) InspectDamage(MapScene before, MapScene after,
        MapCamera camera, int width, int height, double density, bool sparse)
    {
        lock (gate)
        {
            var frame = new FrameKey(camera, width / density, height / density, density, default, null);
            int compared = damageComparedTiles;
            var regions = MeasureDamageRegions(
                before,
                after,
                frame,
                PlanLabels(before, frame),
                PlanLabels(after, frame),
                width,
                height,
                sparseOverride: sparse);
            return (regions, damageComparedTiles - compared);
        }
    }

    private bool TryPaintDamage(
        SKSurface viewport,
        MapScene scene,
        FrameKey frame,
        List<CityLabel> labels,
        SKRectI patch,
        bool reuseGroundAtlas,
        out MapDrawStats result)
    {
        result = default;
        using var pixels = new SKPixmap();
        if (!viewport.PeekPixels(pixels))
        {
            return false;
        }

        var info = pixels.Info;
        var area = patch;
        foreach (var label in labels)
        {
            var box = ToBacking(label.Box, frame.Scaling);
            if (box.IntersectsWith(patch))
            {
                area.Union(box);
            }
        }
        var padded = new SKRectI(Math.Max(0, area.Left - DamageGuard), Math.Max(0, area.Top - DamageGuard),
            Math.Min(info.Width, area.Right + DamageGuard), Math.Min(info.Height, area.Bottom + DamageGuard));
        // Hard raster clips can change antialiasing in neighboring pixels. Keep
        // a narrow copy of the unchanged ring, paint with enough draw margin,
        // then restore the ring at the original pixel addresses.
        SKRectI[] ring =
        [
            new(padded.Left, padded.Top, padded.Right, patch.Top),
            new(padded.Left, patch.Bottom, padded.Right, padded.Bottom),
            new(padded.Left, patch.Top, patch.Left, patch.Bottom),
            new(patch.Right, patch.Top, padded.Right, patch.Bottom)
        ];
        var saved = new SKBitmap?[ring.Length];
        try
        {
            for (int i = 0; i < ring.Length; i++)
            {
                var part = ring[i];
                if (part.Width <= 0 || part.Height <= 0)
                {
                    continue;
                }

                var copy = saved[i] = new SKBitmap();
                if (!copy.TryAllocPixels(info.WithSize(part.Width, part.Height)) ||
                    !pixels.ReadPixels(copy.Info, copy.GetPixels(), copy.RowBytes, part.Left, part.Top))
                {
                    return false;
                }
            }

            var canvas = viewport.Canvas;
            using var restore = new SKAutoCanvasRestore(canvas, true);
            canvas.ClipRect(new(padded.Left, padded.Top, padded.Right, padded.Bottom));
            canvas.Scale((float)frame.Scaling);
            result = DrawBase(
                canvas,
                scene,
                frame,
                labels,
                padded,
                raster: viewport,
                reuseGroundAtlas: reuseGroundAtlas) with
            {
                DamagePixels = (long)padded.Width * padded.Height
            };
            // Painting can replace the backing store if the host still holds
            // a snapshot. Restore into the surface's current pixels.
            if (!viewport.PeekPixels(pixels))
            {
                throw new MapResourceException("Could not read map damage backing");
            }

            using var source = new SKPixmap();
            for (int i = 0; i < ring.Length; i++)
            {
                if (saved[i] is not { } copy)
                {
                    continue;
                }

                if (!copy.PeekPixels(source) || !source.ReadPixels(copy.Info,
                    pixels.GetPixels(ring[i].Left, ring[i].Top), pixels.RowBytes))
                {
                    throw new MapResourceException("Could not restore pixels around map damage");
                }
            }
            return true;
        }
        finally
        {
            foreach (var copy in saved)
            {
                copy?.Dispose();
            }
        }
    }

    private static bool SameLayout(MapScene before, MapScene after) => before.Map.Identity == after.Map.Identity &&
        before.Map.Width == after.Map.Width && before.Map.Height == after.Map.Height
        && before.Map.Flat == after.Map.Flat
        &&
        Equals(before.Annotations.Region, after.Annotations.Region);

    private static bool SameGround(ObservedTile before, ObservedTile after) => before.Terrain == after.Terrain &&
        (before.Terrain is null || (before.Improvements == after.Improvements && before.Resource == after.Resource &&
        before.River == after.River && before.GrasslandShield == after.GrasslandShield &&
        (before.Hut.Value == MapPresence.Present) == (after.Hut.Value == MapPresence.Present)));

    // A custom left shield anchor can extend a further five world
    // pixels outward when the secondary shield and its shadow are drawn.
    private static SKRect BodyBounds(float x, float y, float zoom) =>
        new(x - 56 * zoom - 3, y - 52 * zoom - 3, x + 76 * zoom + 5, y + 44 * zoom + 12);

    // Null selects a full repaint. An empty rect means the new observation
    // leaves every displayed pixel unchanged. Damage is rounded in backing pixels.
    private SKRectI? MeasureDamage(MapScene before, MapScene after, FrameKey frame,
        List<CityLabel> oldLabels, List<CityLabel> newLabels, int width, int height) =>
        MeasureDamageRegions(before, after, frame, oldLabels, newLabels, width, height, multiple: false) is { } regions
            ? regions.Count == 0 ? SKRectI.Empty : regions[0] : null;

    private List<SKRectI>? MeasureDamageRegions(
        MapScene before,
        MapScene after,
        FrameKey frame,
        List<CityLabel> oldLabels,
        List<CityLabel> newLabels,
        int width,
        int height,
        bool multiple = true,
        bool? sparseOverride = null)
    {
        if (!SameLayout(before, after))
        {
            return null;
        }

        var paths = frame.Display.Bare ? null : after.Annotations.Paths;
        if (!frame.Display.Bare && !Equals(before.Annotations.Paths, paths))
        {
            return null;
        }

        bool pathsPresent = paths is { Count: > 0 };
        if (pathsPresent && paths!.Any(path => before.PermitsPath(path) != after.PermitsPath(path)))
        {
            return null;
        }

        int recolored = 0;
        if (!Equals(before.Annotations.FactionRoles, after.Annotations.FactionRoles))
        {
            for (byte slot = 0; slot < Catalog.Factions.Count; slot++)
            {
                if (before.Annotations.ColorRole(new(slot)) != after.Annotations.ColorRole(new(slot)))
                {
                    recolored |= 1 << slot;
                }
            }
        }

        bool recoloredRadius = frame.Display.CityRadius && ChangedColor(after.Map.Identity.Observer);

        var camera = frame.Camera;
        float zoom = (float)camera.Zoom;
        var area = SKRect.Create((float)frame.Width, (float)frame.Height);
        var dirty = new List<SKRect>();
        var map = after.Map;
        int columns = map.Width / 2;
        double period = map.Width * MapCamera.HalfWidth * camera.Zoom;
        int changes = 0;
        foreach (int i in after.ComparisonIndices(
            before,
            sparseOverride ?? gpuFeatures.HasFlag(MapGpuFeatures.Sparse),
            frame.Display.CityRadius))
        {
            damageComparedTiles++;
            var old = before.Map.Tiles[i];
            var next = map.Tiles[i];
            var point = new MapPoint(2 * (i % columns) + (i / columns & 1), i / columns);
            if (!after.InRegion(point))
            {
                continue;
            }

            if (pathsPresent && (old.Terrain is null) != (next.Terrain is null))
            {
                return null;
            }

            var previous = before.Objects(point);
            var current = after.Objects(point);
            bool ground = !SameGround(old, next) || (previous.City is null) != (current.City is null);
            bool radius = frame.Display.CityRadius && after.InCityRadius(point);
            bool overlay = frame.Display.Ownership
                && (old.Ownership != next.Ownership || ChangedColor(next.Ownership.Value?.Owner))
                ||
                frame.Display.CityRadius && (radius != before.InCityRadius(point) || radius && recoloredRadius);
            bool body = !frame.Display.Bare && (previous != current || old.AirbaseOccupant != next.AirbaseOccupant ||
                ChangedColor(current.Unit?.Owner) || ChangedColor(current.City?.Details?.Owner)
                || ChangedColor(next.AirbaseOccupant.Value?.Owner));
            if (!ground && !overlay && !body)
            {
                continue;
            }

            if (++changes > 128)
            {
                return null;
            }

            double x = camera.ScreenX(point.X);
            float y = (float)camera.ScreenY(point.Y);
            double reach = 96 * zoom + 5;
            int first = map.Flat ? 0 : (int)Math.Ceiling((-reach - x) / period);
            int last = map.Flat ? 0 : (int)Math.Floor((frame.Width + reach - x) / period);
            for (int copy = first; copy <= last; copy++)
            {
                float center = (float)(x + copy * period);
                if (ground)
                {
                    Add(new(center - 96 * zoom - 4, y - 48 * zoom - 4, center + 96 * zoom + 4, y + 48 * zoom + 4));
                }

                if (body || overlay)
                {
                    Add(BodyBounds(center, y, zoom));
                }
            }
        }

        if (!oldLabels.SequenceEqual(newLabels))
        {
            // Reflow can displace a label that belongs to a different city.
            var changed = oldLabels.ToHashSet();
            changed.SymmetricExceptWith(newLabels);
            foreach (var label in changed)
            {
                Add(new(label.Box.Left - 2, label.Box.Top - 2, label.Box.Right + 2, label.Box.Bottom + 2));
            }
        }
        double scale = frame.Scaling;
        var pixels = dirty.Select(
            box => new SKRectI(
                (int)Math.Floor(box.Left * scale),
                (int)Math.Floor(box.Top * scale),
                Math.Min(width, (int)Math.Ceiling(box.Right * scale)),
                Math.Min(height, (int)Math.Ceiling(box.Bottom * scale))))
            .ToList();
        damageRegions += pixels.Count;
        return pixels.Sum(box => (long)box.Width * box.Height) > (long)width * height / 2 ? null : pixels;

        void Add(SKRect box)
        {
            if (!area.IntersectsWith(box))
            {
                return;
            }

            box = SKRect.Intersect(area, box);
            if (!multiple)
            {
                // Preserve the established single-rectangle raster path, whose
                // union starts at the empty origin rectangle. Its larger clip
                // also preserves antialiasing at fractional detail boundaries.
                var union = dirty.Count == 0 ? SKRect.Empty : dirty[0];
                union.Union(box);
                if (dirty.Count == 0)
                {
                    dirty.Add(union);
                }
                else
                {
                    dirty[0] = union;
                }

                return;
            }
            // Merge only overlapping regions, including transitive overlaps.
            // Distant edits never pay for the rectangle between them.
            for (int i = 0; i < dirty.Count;)
            {
                if (box.IntersectsWith(dirty[i]))
                {
                    box.Union(dirty[i]);
                    dirty.RemoveAt(i);
                    i = 0;
                }
                else
                {
                    i++;
                }
            }
            dirty.Add(box);
        }

        bool ChangedColor(CivilizationSlot? slot) => slot is { } owner && (recolored & (1 << owner.Value)) != 0;
    }
}
