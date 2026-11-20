using System.Runtime.CompilerServices;
using OpenCiv2.Contracts;
using SkiaSharp;

namespace OpenCiv2.MapRendering;

[Flags]
internal enum MapGpuFeatures
{
    None = 0,
    Atlas = 1,
    Offload = 2,
    Retain = 4,
    Direct = 8,
    Textures = 16,
    Terrain = 32,
    Encoded = 64,
    Commands = 128,
    Masks = 256,
    StableAtlas = 512,
    Sparse = 1024,
    PanCache = 2048,
    TierCache = 4096,
    GroundPlans = 8192,
    MaskBatch = 16384,
    PanPhase = 32768
}

/// <summary>Experimental GPU composition using the same P02 selectors, ordering and art as the raster path.</summary>
internal static class MapGpuComposition
{
    internal const MapGpuFeatures SupportedFeatures = MapGpuFeatures.Atlas | MapGpuFeatures.Direct
        | MapGpuFeatures.Textures
        | MapGpuFeatures.Encoded | MapGpuFeatures.Commands;
    internal static readonly bool Experiment = Environment.GetEnvironmentVariable("OPENCIV2_MAP_EXPERIMENT") == "1";
    internal static readonly bool Synchronize = Experiment
        && Environment.GetEnvironmentVariable("OPENCIV2_MAP_GPU_SYNC") == "1";
    // Only probes or explicitly enabled experiments read arbitrary feature flags.
    internal static MapGpuFeatures Features => Enum.Parse<MapGpuFeatures>(
        Environment.GetEnvironmentVariable("OPENCIV2_MAP_GPU_FEATURES") ?? SupportedFeatures.ToString(),
        ignoreCase: true);
    internal static long CacheLimitBytes => (long)Math.Clamp(
        int.TryParse(
            Environment.GetEnvironmentVariable("OPENCIV2_MAP_GPU_CACHE_MIB"), out int mib) ? mib : 512,
        128,
        1024) * 1024 * 1024;

    private sealed class Budget { public long Limit = long.MaxValue; }

    private static readonly ConditionalWeakTable<GRContext, Budget> Budgets = new();

    internal static void PrepareContext(GRContext context, long? limit = null)
    {
        // A context can serve multiple windows. Register the most conservative
        // requested ceiling once; windows cannot raise one another's budget.
        var budget = Budgets.GetOrCreateValue(context);
        lock (budget)
        {
            long requested = limit ?? CacheLimitBytes;
            if (requested >= budget.Limit)
            {
                return;
            }

            context.SetResourceCacheLimit(requested);
            budget.Limit = requested;
        }
    }

    public static MapDrawStats Draw(
        MapPainter painter,
        GRContext context,
        SKCanvas output,
        long epoch,
        MapCamera camera,
        double width,
        double height,
        double scaling,
        MapPick? selected,
        MapDisplay display = default,
        VisualFrame? visual = null,
        MapPick? inspected = null,
        MapGpuFeatures? features = null,
        long? cacheLimit = null)
    {
        PrepareContext(context, cacheLimit);
        var selectedFeatures = features ?? Features;
        if (selectedFeatures.HasFlag(MapGpuFeatures.Direct))
        {
            return painter.DrawCore(output, epoch, camera, width, height, scaling, selected, display, visual, inspected,
                gpuComposition: true, gpuFeatures: selectedFeatures) with
            { Composition = "gpu" };
        }
        // A physical-pixel target keeps the map's origin independent of the
        // host's fractional layout transform. All GPU objects created here are
        // released while the host context is current. Skia owns upload caches;
        // no GPU handles cross to scene publication or CPU preparation workers.
        var info = new SKImageInfo((int)Math.Ceiling(width * scaling), (int)Math.Ceiling(height * scaling),
            SKImageInfo.PlatformColorType, SKAlphaType.Opaque);
        using var target = SKSurface.Create(context, true, info) ??
            throw new MapGpuResourceException("Could not allocate GPU map target");
        target.Canvas.Scale((float)scaling);
        var result = painter.DrawCore(target.Canvas, epoch, camera, width, height, scaling, selected,
            display, visual, inspected, gpuComposition: true, gpuFeatures: selectedFeatures);
        using var image = target.Snapshot();
        if (!image.IsTextureBacked)
        {
            throw new MapGpuResourceException("Map target is not GPU backed");
        }

        output.DrawImage(image, SKRect.Create((float)(info.Width / scaling), (float)(info.Height / scaling)),
            new SKSamplingOptions(SKFilterMode.Nearest));
        return result with { Composition = "gpu" };
    }
}
