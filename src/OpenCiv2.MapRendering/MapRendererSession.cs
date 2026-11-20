using System.Diagnostics;
using SkiaSharp;

namespace OpenCiv2.MapRendering;

internal sealed class MapGpuResourceException(string message) : Exception(message);

/// <summary>Timing and diagnostics for a completed draw; timestamps use Stopwatch ticks.</summary>
public readonly record struct MapRenderResult(MapDrawStats Stats, double Milliseconds, long StartedAt);

/// <summary>One window's renderer state. Reset work runs on the render thread inside its Skia lease.</summary>
public sealed class MapRendererSession(MapRendererOptions options)
{
    private WeakReference<GRContext>? previous;
    private string? fallback;
    private int retry;
    public void Retry() => Interlocked.Exchange(ref retry, 1);

    /// <summary>Whether the host should capture input timestamps for the explicitly enabled latency experiment.</summary>
    public static bool TraceLatency { get; } = MapGpuComposition.Experiment &&
        Environment.GetEnvironmentVariable("OPENCIV2_MAP_LATENCY") == "1";

    /// <summary>Draws with the configured backend and fallback policy. The caller keeps the borrowed
    /// canvas/context current for this call, clips the viewport, and owns both resources. Use one
    /// session per painter/window; invoke drawing and resource reset on the render thread.</summary>
    public MapRenderResult Draw(MapPainter painter, GRContext? context, SKCanvas canvas,
        long epoch, MapCamera camera, double width, double height, double scaling, MapPick? selected,
        MapDisplay display = default, VisualFrame? visual = null, MapPick? inspected = null,
        int inputCode = 0, long inputAt = 0)
    {
        if ((MapGpuComposition.Synchronize || TraceLatency) && context is null)
            throw new MapGpuResourceException("GPU timing requires a hardware Skia context");
        // Explicit experiments apply the same ceiling to CPU upload
        // and GPU composition, including asynchronous timing runs.
        // Ordinary CPU sessions keep the host's existing policy.
        if ((MapGpuComposition.Experiment || TraceLatency) && context is not null)
            MapGpuComposition.PrepareContext(context!, (long)painter.Options.GpuCacheMiB * 1024 * 1024);
        if (MapGpuComposition.Synchronize) context!.Flush(submit: true, synchronous: true);
        long startedAt = Stopwatch.GetTimestamp();
        var stats = Draw(context, gpu => gpu
            ? MapGpuComposition.Draw(painter, context!, canvas, epoch, camera,
                width, height, scaling, selected, display, visual, inspected,
                MapGpuComposition.Experiment ? MapGpuComposition.Features : MapGpuComposition.SupportedFeatures,
                (long)painter.Options.GpuCacheMiB * 1024 * 1024)
            : painter.Draw(canvas, epoch, camera, width, height, scaling, selected, display, visual, inspected),
            painter.RecoverResources, strict: MapGpuComposition.Experiment);
        if (TraceLatency)
        {
            // Test-only optical stamp: pointer coordinate and its complement.
            // X11 readback observes the rendered window image, never a draw receipt.
            using var marker = new SKPaint();
            uint code = (uint)inputCode | (uint)(~inputCode & 0xffff) << 16;
            for (int bit = 0; bit < 32; bit++)
            {
                marker.Color = (code & (1u << bit)) == 0 ? SKColors.Black : SKColors.White;
                canvas.DrawRect(SKRect.Create((float)(bit * 2 / scaling), 0, (float)(2 / scaling), (float)(3 / scaling)), marker);
            }
        }
        if (MapGpuComposition.Synchronize)
        {
            long waiting = Stopwatch.GetTimestamp();
            context!.Flush(submit: true, synchronous: true);
            stats = stats with { GpuWaitMs = Stopwatch.GetElapsedTime(waiting).TotalMilliseconds, GpuSynchronized = true };
        }
        if (context is { IsAbandoned: false })
        {
            context!.GetResourceCacheUsage(out _, out long cacheBytes);
            stats = stats with { GpuCacheBytes = cacheBytes, GpuCacheLimitBytes = context.GetResourceCacheLimit() };
        }
        double elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        if (TraceLatency && inputAt != 0)
            stats = stats with { InputToDrawMs = Stopwatch.GetElapsedTime(inputAt).TotalMilliseconds, InputCode = inputCode };
        return new(stats, elapsed, startedAt);
    }

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
