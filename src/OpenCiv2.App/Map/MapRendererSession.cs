using SkiaSharp;

namespace OpenCiv2.App.Map;

internal sealed class MapGpuResourceException(string message) : Exception(message);

/// <summary>One window's renderer state. Reset work runs on the render thread inside its Skia lease.</summary>
internal sealed class MapRendererSession(MapRendererOptions options)
{
    private WeakReference<GRContext>? previous;
    private string? fallback;
    private int retry;
    internal void Retry() => Interlocked.Exchange(ref retry, 1);

    internal MapDrawStats Draw(GRContext? context, Func<bool, MapDrawStats> render, Action reset, bool strict = false)
    {
        if (Interlocked.Exchange(ref retry, 0) != 0)
        {
            reset();
            fallback = null;
        }
        if (previous is not null && (!previous.TryGetTarget(out var old) || !ReferenceEquals(old, context)))
        {
            reset();
            previous = null;
        }
        bool gpu = options.Mode == MapRendererMode.Gpu && fallback is null;
        if (gpu && (context is null || context.IsAbandoned))
            Fail(context is null ? "No GPU Skia context is available" : "The GPU context was lost");
        if (context is not null) previous = new(context);
        MapDrawStats result;
        try
        {
            if (gpu) MapGpuComposition.PrepareContext(context!, (long)options.GpuCacheMiB * 1024 * 1024);
            result = render(gpu);
            if (gpu && context!.IsAbandoned) throw new MapGpuResourceException("The GPU context was lost during drawing");
        }
        catch (MapGpuResourceException error) when (gpu)
        {
            Fail(error.Message);
            result = render(false);
        }
        return result with
        {
            RequestedRenderer = options.Mode == MapRendererMode.Gpu ? "gpu" : "cpu",
            Composition = gpu ? "gpu" : "cpu",
            RendererFallback = fallback
        };

        void Fail(string reason)
        {
            if (strict) throw new MapGpuResourceException(reason);
            reset();
            fallback = reason;
            gpu = false;
        }
    }
}
