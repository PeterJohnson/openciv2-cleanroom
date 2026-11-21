using System.Diagnostics;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using SkiaSharp;

namespace OpenCiv2.MapRendering;

public enum MapRedraw { Full, Cached, Dirty, Scrolled, Direct, Error }
internal sealed class MapResourceException(string message) : Exception(message);
public readonly record struct MapDrawStats(
    MapRedraw Redraw,
    int Tiles,
    int Objects,
    int Batches,
    int GroundChunks = 0,
    int PreparedChunks = 0,
    int Labels = 0,
    string? Error = null,
    double GroundTier = 0,
    long DamagePixels = 0,
    int Effects = 0,
    int Moving = 0,
    int Notices = 0,
    long GroundDamagePixels = 0,
    double GroundBuildMs = 0,
    double GroundSelectorMs = 0,
    double TexturePrepareMs = 0,
    int ParallelGroundChunks = 0,
    long GroundCacheBytes = 0,
    long GroundCacheLimitBytes = 0,
    double TerrainMs = 0,
    double ForegroundMs = 0,
    double LabelLayoutMs = 0,
    double ViewportBlitMs = 0,
    int ForegroundBands = 0,
    int PreparedBodyTextures = 0,
    int OverlappedBodyTextures = 0,
    int GroundReplayBands = 0,
    int AlignedGroundChunks = 0,
    long TextureCacheBytes = 0,
    long DemandRasterLoads = 0,
    double TintPrepareMs = 0,
    double BandSetupMs = 0,
    double BandRecordMs = 0,
    double BandReplayMs = 0,
    double BandRepairMs = 0,
    double DeferredGroundInvalidateMs = 0,
    double EarlyBodyLeadMs = 0,
    int HigherBodyTextures = 0,
    double HigherBodyWorkMs = 0,
    string Composition = "cpu",
    double GpuWaitMs = 0,
    bool GpuSynchronized = false,
    long GpuCacheBytes = 0,
    long GpuCacheLimitBytes = 0,
    string GpuFeatures = "None",
    int SpriteCalls = 0,
    int AtlasBatches = 0,
    long AtlasBytes = 0,
    int RetainedPatches = 0,
    int InputCode = 0,
    double InputToDrawMs = 0,
    long EncodedSourceBytes = 0,
    string RequestedRenderer = "cpu",
    string? RendererFallback = null,
    double ForegroundPlanMs = 0,
    double AtlasPrepareMs = 0,
    double ForegroundSubmitMs = 0,
    int ForegroundTraversals = 0,
    int ForegroundCommands = 0,
    long DrawAllocatedBytes = 0,
    int AtlasAdmissions = 0,
    int AtlasEvictions = 0,
    int AtlasHits = 0,
    int GroundComparedTiles = 0,
    int DamageComparedTiles = 0,
    int DamageRegions = 0,
    int RegionBuilds = 0,
    int RegionHits = 0,
    int RegionEvictions = 0,
    long RegionBytes = 0,
    int TopologyHits = 0,
    int TopologyBuilds = 0,
    int TopologyEvictions = 0,
    long TopologyBytes = 0);

/// <summary>P02 read-only renderer. One batch per texture/layer, bounded viewport rasters,
/// and a transient selection overlay. Raster ownership and scene publication use separate locks.</summary>
public sealed partial class MapPainter : IDisposable
{
    private const int MaxFramePixels = 16_000_000;
    private const double HigherBodyPreparationStart = 1.35, HigherBodyTierBoundary = 2;
    private static readonly SKColor Background = new(0xff172b33);
    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear);
    private static readonly SKSamplingOptions CacheSampling = new(SKFilterMode.Nearest);
    private readonly object gate = new();
    private readonly object bindingGate = new();
    private readonly MapTextures textures;
    private readonly MapText text;
    private readonly Func<SKImageInfo, SKSurface?> allocateRaster;
    private readonly bool parallelForeground;
    private readonly bool packGroundTextures;
    private MapScene? scene;
    private SceneBinding binding = new(null, 0);
    private string? sceneError, backendError;
    private bool disposed;
    private bool gpuComposition;
    private bool gpuResources;
    private MapGpuFeatures gpuFeatures;
    private readonly MapGpuSpriteAtlas spriteAtlas = new();
    private int retainedPatches;
    private int groundComparedTiles, damageComparedTiles, damageRegions;

    private FrameKey key;
    private SKImage? baseImage;
    private MapScene? rasterScene;
    private List<CityLabel> rasterLabels = [];
    private SKSurface? viewportRaster;
    private SKImageInfo viewportInfo;
    private MapPick? highlighted;
    private MapDrawStats stats;
    private long terrainTicks, foregroundTicks, labelLayoutTicks, viewportBlitTicks;
    private (long Setup, long Record, long Replay, long Repair) bandTicks;
    private int preparedBodyTextures, overlappedBodyTextures;
    private (long Epoch, FrameKey Frame, long QueuedAt)? earlyBodies;

    private readonly record struct FrameKey(
        MapCamera Camera,
        double Width,
        double Height,
        double Scaling,
        MapDisplay Display,
        OwnedList<UnitTarget>? Moving);

    private sealed record SceneBinding(MapScene? Scene, long Epoch);

    public MapRendererOptions Options { get; }

    public MapPainter(
        AssetAtlas? atlas,
        PinnedFonts? fonts,
        MapArtSelection? selected = null,
        Func<SKImageInfo, SKSurface?>? allocateRaster = null,
        bool parallelForeground = true,
        bool packGroundTextures = true,
        MapRendererOptions? options = null)
    {
        Options = options ?? new();
        this.allocateRaster = allocateRaster ?? SKSurface.Create;
        this.parallelForeground = parallelForeground;
        this.packGroundTextures = packGroundTextures;
        textures = new(atlas, selected);
        try
        {
            text = new(fonts);
        }
        catch
        {
            textures.Dispose();
            throw;
        }
    }

    internal (int Count, long PixelBytes) TextureUsage => (textures.Count, textures.PixelBytes);
    internal (long Pages, long Sources) AtlasUsage => (spriteAtlas.Bytes, spriteAtlas.SourceBytes);
    internal (long Regions, long Topology) OptimizationUsage => (regionBytes, topologyBytes);

    public void BeginStartupGroundPreparation() => textures.BeginStartupGround();

    public StartupGroundWork? StartupGroundPreparation => textures.StartupGround;

    public bool TryPrepareFirstFrameBodies(long epoch, MapCamera camera, double width, double height,
        double scaling, MapDisplay display, VisualFrame? visual)
    {
        double pixels = Math.Ceiling(width * scaling) * Math.Ceiling(height * scaling);
        if (Options.Mode == MapRendererMode.Gpu || OperatingSystem.IsBrowser() || pixels < LargeBandPixels
            || pixels > MaxFramePixels
            ||
            !Monitor.TryEnter(gate))
        {
            return false;
        }

        try
        {
            var current = Volatile.Read(ref binding);
            if (disposed || baseImage is not null || earlyBodies is not null || epoch != current.Epoch ||
                current.Scene is not { } active || (sceneError ?? backendError) is not null)
            {
                return false;
            }

            var frame = FrameFor(camera, width, height, scaling, display, visual);
            if (!BeginColdBodyPreparation(active, frame))
            {
                return false;
            }

            earlyBodies = (epoch, frame, Stopwatch.GetTimestamp());
            return true;
        }
        catch (Exception error) when (error is MapResourceException or InvalidDataException)
        {
            // The draw reports unsupported art when it resolves the bindings.
            return false;
        }
        finally
        {
            Monitor.Exit(gate);
        }
    }

    internal (int Count, long PixelBytes) GroundUsage => (ground.Count, groundBytes + PackedGroundBytes);
    internal long ViewportBytes => (long)viewportInfo.Width * viewportInfo.Height * 4 *
        ((viewportRaster is null ? 0 : 1) + (terrainRaster is null ? 0 : 1) + (viewportScratch is null ? 0 : 1));

    public readonly record struct BindingTiming(double WaitMs, double InvalidateGroundMs, bool Deferred = false);

    public long SetScene(MapScene? value) => SetScene(value, measure: false, out _);

    public long SetScene(MapScene? value, bool measure, out BindingTiming timing)
    {
        timing = default;
        long requested = measure ? Stopwatch.GetTimestamp() : 0;
        if (!Monitor.TryEnter(gate))
        {
            // A frame may still be rasterizing. Publish same-layout changes
            // immediately and let the next draw apply them to the caches.
            // Observer and layout changes release old resources synchronously.
            lock (bindingGate)
            {
                if (disposed || ReferenceEquals(value, binding.Scene))
                {
                    return binding.Epoch;
                }

                if (value is not null && binding.Scene is { } current && SameLayout(current, value))
                {
                    if (measure)
                    {
                        timing = new(Stopwatch.GetElapsedTime(requested).TotalMilliseconds, 0, Deferred: true);
                    }

                    return PublishBinding(value);
                }
            }
            Monitor.Enter(gate);
        }
        try
        {
            long epoch;
            lock (bindingGate)
            {
                if (disposed)
                {
                    return binding.Epoch;
                }

                epoch = ReferenceEquals(value, binding.Scene) ? binding.Epoch : PublishBinding(value);
            }
            double wait = measure ? Stopwatch.GetElapsedTime(requested).TotalMilliseconds : 0;
            timing = new(wait, ApplyScene(value, measure));
            return epoch;
        }
        finally
        {
            Monitor.Exit(gate);
        }
    }

    // The binding lock serializes publishers. The raster lock still owns all
    // images, textures, scratch, and writes to the output canvas.
    private long PublishBinding(MapScene? value)
    {
        var next = new SceneBinding(value, binding.Epoch + 1);
        Volatile.Write(ref binding, next);
        return next.Epoch;
    }

    private double ApplyScene(MapScene? value, bool measure = false)
    {
        if (ReferenceEquals(value, scene))
        {
            return 0;
        }

        if (scene is not null && (value is null || scene.Map.Identity != value.Map.Identity))
        {
            ReleaseRasters();
        }

        if (settledUpdates is null)
        {
            ClearTerrainFrame();
        }

        long invalidate = measure ? Stopwatch.GetTimestamp() : 0;
        InvalidateGround(scene, value);
        double groundMs = measure ? Stopwatch.GetElapsedTime(invalidate).TotalMilliseconds : 0;
        if (scene is null || value is null || !SameLayout(scene, value))
        {
            DiscardEarlyBodies();
            textures.DiscardHigherBodies();
            ClearFrame();
            ClearViewportRasters();
            backendError = null;
        }
        scene = value;
        byte invalid = value?.Annotations.FactionRoles.FirstOrDefault(role => role >= Catalog.Factions.Count) ?? 0;
        sceneError = invalid == 0 ? null : $"Unsupported owner color role {invalid}";
        if (sceneError is not null)
        {
            ClearFrame();
        }

        ClearOverview();
        return groundMs;
    }

    public void InvalidateFrame()
    {
        lock (gate)
        {
            ClearFrame();
            ClearTerrainFrame();
            DiscardEarlyBodies();
        }
    }

    public long SelectArt(MapArtSelection? selection)
    {
        lock (gate)
        {
            if (disposed || !textures.SelectArt(selection))
            {
                return Volatile.Read(ref binding).Epoch;
            }

            ReleaseRasters();
            backendError = null;
            lock (bindingGate)
            {
                return PublishBinding(binding.Scene);
            }
        }
    }

    public void RecoverResources()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            ReleaseRasters();
            backendError = null;
        }
    }

    private void ReleaseRasters()
    {
        spriteAtlas.Clear();
        foregroundCommands.Reset();
        ClearGpuRegions();
        retainedPatches = 0;
        ClearFrame();
        ClearOverview();
        ClearGround();
        ClearViewportRasters();
        earlyBodies = null;
        textures.Clear();
    }

    private void DiscardEarlyBodies()
    {
        if (earlyBodies is null)
        {
            return;
        }

        earlyBodies = null;
        textures.CompleteBodyPreparation(admit: false);
    }

    /// <summary>Draws the CPU composition into a borrowed canvas. The caller retains canvas ownership.</summary>
    public MapDrawStats Draw(
        SKCanvas output,
        long epoch,
        MapCamera camera,
        double width,
        double height,
        double scaling,
        MapPick? selected,
        MapDisplay display = default,
        VisualFrame? visual = null,
        MapPick? inspected = null) =>
        DrawCore(output, epoch, camera, width, height, scaling, selected, display, visual, inspected);

    internal MapDrawStats DrawCore(
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
        bool gpuComposition = false,
        MapGpuFeatures gpuFeatures = MapGpuFeatures.None)
    {
        lock (gate)
        {
            var current = Volatile.Read(ref binding);
            if (disposed || epoch != current.Epoch || current.Scene is not { } active || width <= 0 || height <= 0)
            {
                return ClearStale(output);
            }

            double deferredGroundMs = ApplyScene(active, measure: true);
            var frame = FrameFor(camera, width, height, scaling, display, visual);
            double earlyLeadMs = 0;
            if (earlyBodies is { } scheduled)
            {
                if (scheduled.Epoch == epoch && scheduled.Frame == frame && (sceneError ?? backendError) is null)
                {
                    earlyBodies = null;
                    earlyLeadMs = Stopwatch.GetElapsedTime(scheduled.QueuedAt).TotalMilliseconds;
                }
                else
                {
                    DiscardEarlyBodies();
                }
            }
            if ((sceneError ?? backendError) is { } error)
            {
                return PresentFailure(output, epoch, width, height, error);
            }

            try
            {
                this.gpuComposition = gpuComposition;
                if (gpuResources != gpuComposition || this.gpuFeatures != gpuFeatures)
                {
                    ReleaseRasters();
                    gpuResources = gpuComposition;
                    this.gpuFeatures = gpuFeatures;
                }
                textures.GpuPreparation = gpuComposition
                    && (gpuFeatures & (MapGpuFeatures.Offload | MapGpuFeatures.Textures)) != 0;
                textures.EncodedSources = gpuFeatures.HasFlag(MapGpuFeatures.Encoded);
                spriteAtlas.ReuseBatchStorage = gpuFeatures.HasFlag(MapGpuFeatures.Commands);
                spriteAtlas.StableLayout = gpuFeatures.HasFlag(MapGpuFeatures.StableAtlas);
                spriteAtlas.ModulatedMasks = gpuFeatures.HasFlag(MapGpuFeatures.Masks | MapGpuFeatures.MaskBatch);
                spriteAtlas.BeginFrame();
                long allocationStart = GC.GetAllocatedBytesForCurrentThread();
                foregroundPlanTicks = atlasPrepareTicks = foregroundSubmitTicks = 0;
                foregroundTraversals = foregroundCommandCount = 0;
                regionHits = regionBuilds = regionEvictions = 0;
                topologyHits = topologyBuilds = topologyEvictions = 0;
                groundBuildTicks = groundSelectorTicks = 0;
                parallelGroundChunks = alignedGroundChunks = preparedBodyTextures = overlappedBodyTextures = 0;
                terrainTicks = foregroundTicks = labelLayoutTicks = viewportBlitTicks = 0;
                bandTicks = default;
                long textureTicks = textures.PreparationTicks;
                long tintTicks = textures.TintPreparationTicks;
                long demandLoads = textures.DemandRasterLoads;
                var (higherBodies, higherBodyWork) = textures.PublishHigherBodies();
                bool zoomingIn = !gpuComposition && parallelForeground && baseImage is not null
                    && ReferenceEquals(rasterScene, active)
                    &&
                    key.Camera.Zoom < camera.Zoom && !display.Bare && textures.CanPrepare &&
                    width * height * scaling * scaling >= LargeBandPixels &&
                    camera.Zoom * scaling is >= HigherBodyPreparationStart and <= HigherBodyTierBoundary;
                var drawn = DrawCurrent(output, active, epoch, frame, selected, visual, inspected, gpuComposition);
                spriteAtlas.Flush();
                if (zoomingIn && drawn.Error is null && CanPresent(epoch))
                {
                    BeginHigherBodyPreparation(active, frame);
                }

                return drawn with
                {
                    GroundBuildMs = Stopwatch.GetElapsedTime(0, groundBuildTicks).TotalMilliseconds,
                    GroundSelectorMs = Stopwatch.GetElapsedTime(0, groundSelectorTicks).TotalMilliseconds,
                    TexturePrepareMs = Stopwatch.GetElapsedTime(textureTicks, textures.PreparationTicks)
                        .TotalMilliseconds,
                    TintPrepareMs = Stopwatch.GetElapsedTime(tintTicks, textures.TintPreparationTicks)
                        .TotalMilliseconds,
                    TextureCacheBytes = textures.PixelBytes,
                    DemandRasterLoads = textures.DemandRasterLoads - demandLoads,
                    ParallelGroundChunks = parallelGroundChunks,
                    AlignedGroundChunks = alignedGroundChunks,
                    GroundCacheBytes = groundBytes + PackedGroundBytes,
                    GroundCacheLimitBytes = groundBudget,
                    TerrainMs = Stopwatch.GetElapsedTime(0, terrainTicks).TotalMilliseconds,
                    ForegroundMs = Stopwatch.GetElapsedTime(0, foregroundTicks).TotalMilliseconds,
                    BandSetupMs = Stopwatch.GetElapsedTime(0, bandTicks.Setup).TotalMilliseconds,
                    BandRecordMs = Stopwatch.GetElapsedTime(0, bandTicks.Record).TotalMilliseconds,
                    BandReplayMs = Stopwatch.GetElapsedTime(0, bandTicks.Replay).TotalMilliseconds,
                    BandRepairMs = Stopwatch.GetElapsedTime(0, bandTicks.Repair).TotalMilliseconds,
                    LabelLayoutMs = Stopwatch.GetElapsedTime(0, labelLayoutTicks).TotalMilliseconds,
                    ViewportBlitMs = Stopwatch.GetElapsedTime(0, viewportBlitTicks).TotalMilliseconds,
                    PreparedBodyTextures = preparedBodyTextures,
                    OverlappedBodyTextures = overlappedBodyTextures,
                    DeferredGroundInvalidateMs = deferredGroundMs,
                    EarlyBodyLeadMs = earlyLeadMs,
                    HigherBodyTextures = higherBodies,
                    HigherBodyWorkMs = higherBodyWork,
                    GpuFeatures = gpuFeatures.ToString(),
                    SpriteCalls = spriteAtlas.SpriteCalls,
                    AtlasBatches = spriteAtlas.Batches,
                    AtlasBytes = spriteAtlas.Bytes,
                    EncodedSourceBytes = textures.EncodedBytes,
                    RetainedPatches = retainedPatches,
                    ForegroundPlanMs = Stopwatch.GetElapsedTime(0, foregroundPlanTicks).TotalMilliseconds,
                    AtlasPrepareMs = Stopwatch.GetElapsedTime(0, atlasPrepareTicks).TotalMilliseconds,
                    ForegroundSubmitMs = Stopwatch.GetElapsedTime(0, foregroundSubmitTicks).TotalMilliseconds,
                    ForegroundTraversals = foregroundTraversals,
                    ForegroundCommands = foregroundCommandCount,
                    DrawAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart,
                    AtlasAdmissions = spriteAtlas.Admissions,
                    AtlasEvictions = spriteAtlas.Evictions,
                    AtlasHits = spriteAtlas.Hits,
                    GroundComparedTiles = groundComparedTiles,
                    DamageComparedTiles = damageComparedTiles,
                    DamageRegions = damageRegions,
                    RegionBuilds = regionBuilds,
                    RegionHits = regionHits,
                    RegionEvictions = regionEvictions,
                    RegionBytes = regionBytes,
                    TopologyHits = topologyHits,
                    TopologyBuilds = topologyBuilds,
                    TopologyEvictions = topologyEvictions,
                    TopologyBytes = topologyBytes
                };
            }
            catch (Exception failure) when (failure is MapResourceException or InvalidDataException)
            {
                ReleaseRasters();
                if (!CanPresent(epoch))
                {
                    return ClearStale(output);
                }

                backendError = failure.Message;
                return DrawFailure(output, width, height, backendError);
            }
            finally
            {
                groundComparedTiles = damageComparedTiles = damageRegions = 0;
                this.gpuComposition = false;
                textures.CompleteBodyPreparation(admit: false);
            }
        }
    }

    private void BeginHigherBodyPreparation(MapScene scene, FrameKey frame)
    {
        // Prefer bodies near the current view center, where an inward zoom is
        // likely to keep them visible. Required lookups still resolve any misses.
        double steps = Math.Log(HigherBodyTierBoundary / (frame.Camera.Zoom * frame.Scaling), 1.25);
        var nearer = frame.Camera.ZoomAt(frame.Width / 2, frame.Height / 2, steps);
        var bounds = nearer.VisibleBounds(scene.Map, frame.Width, frame.Height);
        var remaining = frame.Moving is { } moving ? scene.StacksWithout(moving) : null;
        textures.BeginHigherBodyPreparation(VisibleBodyBindings(scene, bounds, remaining));
    }

    private static MapDrawStats ClearStale(SKCanvas output)
    {
        output.DrawColor(Background, SKBlendMode.Src);
        return default;
    }

    private MapDrawStats PresentFailure(SKCanvas output, long epoch, double width, double height, string error) =>
        CanPresent(epoch) ? DrawFailure(output, width, height, error) : ClearStale(output);

    // The read commits this output while the raster lock serializes draws.
    // A later same-layout publication can arrive during submission. Its draw
    // follows the current submission. Observer and layout changes wait for it.
    private bool CanPresent(long epoch) => epoch == Volatile.Read(ref binding).Epoch;

    private MapDrawStats DrawFailure(SKCanvas output, double width, double height, string error)
    {
        output.DrawColor(Background, SKBlendMode.Src);
        using var ink = new SKPaint { IsAntialias = true };
        using var font = text.Font(13);
        if (width > 24 && height >= 36)
        {
            string message = $"Map graphics unavailable · {error}";
            float panelWidth = Math.Min(text.Measure(message, font, ink).Width + 18, (float)width - 24);
            Badge(output, ink, font, message, SKRect.Create(12, 12, panelWidth, 24), 13, HudBackground);
        }
        return new(MapRedraw.Error, 0, 0, 0, Error: error);
    }

    private static FrameKey FrameFor(
        MapCamera camera,
        double width,
        double height,
        double scaling,
        MapDisplay display,
        VisualFrame? visual) =>
        new(
            camera,
            width,
            height,
            scaling,
            display,
            !display.Bare && !display.ReducedMotion && visual?.Moving is { Count: > 0 } moving
            ? moving : null);

    private MapDrawStats DrawCurrent(SKCanvas output, MapScene scene, long epoch, FrameKey requested,
        MapPick? selected, VisualFrame? visual, MapPick? inspected, bool directComposition)
    {
        var (camera, width, height, scaling, display, moving) = requested;
        bool animate = !display.Bare && !display.ReducedMotion;
        int effects = animate ? visual?.Effects.Count ?? 0 : 0;
        if (selected is { } pick && !scene.PermitsPick(pick))
        {
            selected = null;
        }

        double pixelWidth = Math.Ceiling(width * scaling), pixelHeight = Math.Ceiling(height * scaling);
        SetGroundBudget(pixelWidth * pixelHeight);
        if (directComposition && gpuFeatures.HasFlag(MapGpuFeatures.PanCache))
        {
            return Present(() =>
            {
                var result = DrawGpuRegions(output, scene, requested, LayoutLabels());
                DrawSelection(output, scene, requested, selected);
                return Finish(result);
            });
        }

        if (directComposition && gpuFeatures.HasFlag(MapGpuFeatures.Retain))
        {
            return DrawRetainedGpu();
        }

        if (directComposition || pixelWidth * pixelHeight > MaxFramePixels)
        {
            ClearFrame();
            ClearViewportRasters();
            ClearRetainedGroundAtlas();
            return DrawDirect(LayoutLabels());
        }
        MapRedraw redraw = MapRedraw.Cached;
        bool paintedWorld = false;
        if (baseImage is null || key != requested || !ReferenceEquals(rasterScene, scene))
        {
            var labels = LayoutLabels();
            var damage = baseImage is not null && key == requested && rasterScene is { } former
                ? MeasureDamage(former, scene, requested, rasterLabels, labels, (int)pixelWidth, (int)pixelHeight)
                : null;
            // Local world changes repeatedly pack the same nearby art. A
            // camera change can need the full chunk budget for a new tier.
            bool settledView = baseImage is not null && key == requested;
            var info = new SKImageInfo(
                (int)pixelWidth,
                (int)pixelHeight,
                SKImageInfo.PlatformColorType,
                SKAlphaType.Opaque);
            bool settleGround = settledView
                && CanPaintForegroundBands(scene, requested, new(0, 0, info.Width, info.Height));
            bool reuseGroundAtlas = settledView && (settleGround || pixelWidth * pixelHeight >= LargeBandPixels);
            if (!reuseGroundAtlas)
            {
                ClearRetainedGroundAtlas();
            }
            // Compose large first frames directly into viewport bands. A
            // later pan can establish the separate terrain-scroll backing.
            bool composeFirstFrame = baseImage is null && ground.Count == 0 &&
                pixelWidth * pixelHeight >= LargeBandPixels &&
                CanPaintForegroundBands(scene, requested, new(0, 0, info.Width, info.Height));
            bool retainTerrain = !composeFirstFrame && damage is null && scene.Annotations.Region is null &&
                (baseImage is null || ReferenceEquals(rasterScene, scene) && key.Camera.Zoom == camera.Zoom);
            bool replayGround = composeFirstFrame || baseImage is not null && viewportInfo == info;
            if (damage is null)
            {
                ClearFrame();
            }

            key = requested;
            if (viewportRaster is null || viewportInfo != info)
            {
                ClearViewportRasters();
                viewportRaster = allocateRaster(info);
                viewportInfo = info;
            }
            if (viewportRaster is null)
            {
                return DrawDirect(labels);
            }

            if (damage is null || !damage.Value.IsEmpty)
            {
                // Release our snapshot before modifying the surface to avoid
                // copying the whole viewport for a small patch.
                baseImage?.Dispose();
                baseImage = null;
                if (damage is { } pixels
                    && TryPaintDamage(viewportRaster, scene, requested, labels, pixels, reuseGroundAtlas, out stats))
                {
                    ClearTerrainFrame();
                    redraw = MapRedraw.Dirty;
                }
                else
                {
                    var canvas = viewportRaster.Canvas;
                    using var restore = new SKAutoCanvasRestore(canvas, true);
                    if (damage is null)
                    {
                        BeginColdBodyPreparation(scene, requested);
                    }

                    long start = Stopwatch.GetTimestamp();
                    var terrain = settleGround ? PrepareSettledTerrain(scene, requested, info, reuseGroundAtlas) :
                        retainTerrain ? PrepareTerrainFrame(scene, requested, info) : null;
                    terrainTicks += Stopwatch.GetTimestamp() - start;
                    if (terrain is null && !retainTerrain)
                    {
                        ClearTerrainFrame();
                    }

                    canvas.Scale((float)scaling);
                    stats = DrawBase(canvas, scene, key, labels, terrain: terrain, raster: viewportRaster,
                        replayGround: replayGround, reuseGroundAtlas: reuseGroundAtlas) with
                    {
                        DamagePixels = (long)pixelWidth * (int)pixelHeight
                    };
                    redraw = terrain?.Scrolled == true ? MapRedraw.Scrolled : MapRedraw.Full;
                }
                baseImage = viewportRaster.Snapshot();
                paintedWorld = true;
            }
            rasterScene = scene;
            rasterLabels = labels;
        }
        return Present(() =>
        {
            if (selected != highlighted && redraw == MapRedraw.Cached)
            {
                redraw = MapRedraw.Dirty;
            }

            highlighted = selected;
            // Keep an exact pixel-to-DIP ratio even when the viewport rounds up a fraction of a pixel.
            long blit = Stopwatch.GetTimestamp();
            DrawRaster(output, baseImage!, scaling);
            viewportBlitTicks = Stopwatch.GetTimestamp() - blit;
            DrawSelection(output, scene, requested, selected);
            return Finish(paintedWorld ? stats with { Redraw = redraw }
                : stats with
                {
                    Redraw = redraw,
                    PreparedChunks = 0,
                    DamagePixels = 0,
                    GroundDamagePixels = 0,
                    ForegroundBands = 0,
                    GroundReplayBands = 0
                },
                baseImage);
        });

        MapDrawStats DrawDirect(List<CityLabel> labels) => Present(() =>
        {
            var direct = DrawBase(output, scene, requested, labels);
            DrawSelection(output, scene, requested, selected);
            return Finish(direct with { Redraw = MapRedraw.Direct });
        });

        MapDrawStats DrawRetainedGpu()
        {
            var labels = LayoutLabels();
            MapRedraw redraw = MapRedraw.Cached;
            if (baseImage is null || key != requested || !ReferenceEquals(rasterScene, scene))
            {
                // Deferred pictures let Skia retain GPU pixels without application-owned
                // context handles. Bound patch chains to four predecessors; a full
                // redraw then releases every superseded viewport reference.
                var damage = baseImage is not null && key == requested && rasterScene is { } former
                    && retainedPatches < 4
                    ? MeasureDamage(former, scene, requested, rasterLabels, labels, (int)pixelWidth, (int)pixelHeight)
                    : null;
                using var recorder = new SKPictureRecorder();
                var canvas = recorder.BeginRecording(SKRect.Create((float)pixelWidth, (float)pixelHeight));
                if (damage is { } patch)
                {
                    canvas.DrawImage(baseImage!, 0, 0, CacheSampling);
                    canvas.ClipRect(patch);
                    retainedPatches++;
                    redraw = MapRedraw.Dirty;
                }
                else
                {
                    retainedPatches = 0;
                    redraw = MapRedraw.Full;
                }
                canvas.Scale((float)scaling);
                stats = DrawBase(canvas, scene, requested, labels, damage);
                using var picture = recorder.EndRecording();
                var image = SKImage.FromPicture(picture, new((int)pixelWidth, (int)pixelHeight));
                baseImage?.Dispose();
                baseImage = image;
                key = requested;
                rasterScene = scene;
                rasterLabels = labels;
            }
            return Present(() =>
            {
                DrawRaster(output, baseImage!, scaling);
                DrawSelection(output, scene, requested, selected);
                return Finish(stats with { Redraw = redraw });
            });
        }

        MapDrawStats Present(Func<MapDrawStats> paint)
        {
            // A revision published during rasterization owns the next visible
            // frame. This draw only leaves its private raster.
            if (!CanPresent(epoch))
            {
                return ClearStale(output);
            }

            using var correction = display.Color switch
            {
                MapColorCorrection.Greyscale => SKColorFilter.CreateColorMatrix(Greyscale),
                MapColorCorrection.HighContrast => SKColorFilter.CreateColorMatrix(
                    [1.25f, 0, 0, 0, -.125f, 0, 1.25f, 0, 0, -.125f, 0, 0, 1.25f, 0, -.125f, 0, 0, 0, 1, 0]),
                _ => null
            };
            using var adjusted = correction is null ? null : new SKPaint { ColorFilter = correction };
            using var save = new SKAutoCanvasRestore(output, true);
            if (adjusted is not null)
            {
                output.SaveLayer(SKRect.Create((float)width, (float)height), adjusted);
            }

            return paint();
        }

        List<CityLabel> LayoutLabels()
        {
            long start = Stopwatch.GetTimestamp();
            var labels = PlanLabels(scene, requested);
            labelLayoutTicks = Stopwatch.GetTimestamp() - start;
            return labels;
        }

        MapDrawStats Finish(MapDrawStats result, SKImage? backdrop = null) => result with
        {
            Effects = effects,
            Moving = moving?.Count ?? 0,
            Notices = DrawTransient(output, scene, requested, visual, inspected, selected, backdrop)
        };
    }

    private static IEnumerable<SKPoint> SelectionCenters(MapObservation map, FrameKey frame, MapPoint selected)
    {
        var camera = frame.Camera;
        double period = map.Width * MapCamera.HalfWidth * camera.Zoom;
        double origin = camera.ScreenX(selected.X), y = camera.ScreenY(selected.Y);
        double radius = 35 * camera.Zoom + 2;
        if (y + radius < 0 || y - radius > frame.Height)
        {
            yield break;
        }

        int left = map.Flat ? 0 : (int)Math.Ceiling((-radius - origin) / period);
        int right = map.Flat ? 0 : (int)Math.Floor((frame.Width + radius - origin) / period);
        for (int i = left; i <= right; i++)
        {
            yield return new((float)(origin + i * period), (float)y);
        }
    }

    private void DrawSelection(SKCanvas canvas, MapScene scene, FrameKey frame, MapPick? selected)
    {
        if (selected is not { } target)
        {
            return;
        }

        using var clip = new SKAutoCanvasRestore(canvas, true);
        ClipRegion(canvas, scene, frame);
        float zoom = (float)frame.Camera.Zoom;
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = Color("selectionHighlight"),
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke
        };
        using var highlight = new SKPaint { Color = SKColors.White.WithAlpha(96) };
        using var builder = new SKPathBuilder();
        foreach (var center in SelectionCenters(scene.Map, frame, target.Position))
        {
            float x = center.X, y = center.Y;
            if (textures.HasStock && target.Kind == MapPickKind.Tile)
            {
                Sprite(
                    canvas,
                    Catalog.Overlays["selection"],
                    x - 32 * zoom,
                    y - 16 * zoom,
                    zoom,
                    zoom * frame.Scaling,
                    highlight);
                continue;
            }
            builder.MoveTo(x, y - 16 * zoom);
            builder.LineTo(x + 32 * zoom, y);
            builder.LineTo(x, y + 16 * zoom);
            builder.LineTo(x - 32 * zoom, y);
            builder.Close();
        }
        using var path = builder.Detach();
        canvas.DrawPath(path, paint);
    }

    private void ClearFrame()
    {
        baseImage?.Dispose();
        baseImage = null;
        rasterScene = null;
        rasterLabels = [];
        highlighted = null;
    }

    private void ClearViewportRasters()
    {
        viewportRaster?.Dispose();
        viewportRaster = null;
        ClearTerrainRasters();
    }

    public void Dispose()
    {
        lock (gate)
        {
            lock (bindingGate)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                PublishBinding(null);
            }
            scene = null;
            spriteAtlas.Clear();
            ClearFrame();
            ClearOverview();
            ClearGround();
            ClearViewportRasters();
            textures.Dispose();
            gpuNearest.Dispose();
            text.Dispose();
        }
    }
}
