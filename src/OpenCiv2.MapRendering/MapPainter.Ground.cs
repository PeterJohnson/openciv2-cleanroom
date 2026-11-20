using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenCiv2.Contracts;
using SkiaSharp;

namespace OpenCiv2.MapRendering;

public sealed partial class MapPainter
{
    private const int ChunkSize = 256, ChunkMargin = 2;
    private const int MinimumGroundBatch = 2, MinimumPackedGroundBatch = 4;
    private const int GroundBatchLimit = 24, GroundPlacementLimit = 96, GroundBatchBytes = 8 * 1024 * 1024;
    private static readonly ParallelOptions GroundWorkers = new()
    {
        MaxDegreeOfParallelism = Math.Min(12, Environment.ProcessorCount)
    };
    private const long StandardGroundBudget = 240L * 1024 * 1024;
    private readonly long machineGroundBudget = OperatingSystem.IsBrowser() ? StandardGroundBudget :
        Math.Max(StandardGroundBudget, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 16);
    private readonly Dictionary<GroundKey, LinkedListNode<GroundImage>> ground = [];
    private readonly LinkedList<GroundImage> groundRecency = [];
    private long groundBudget = StandardGroundBudget;
    private long groundBytes, groundBuildTicks, groundSelectorTicks;
    private int parallelGroundChunks, alignedGroundChunks;
    private bool GpuGround => gpuComposition && (gpuFeatures & (MapGpuFeatures.Offload | MapGpuFeatures.Terrain)) != 0;

    private readonly record struct GroundKey(int Column, int Row, double Tier, MapDetail Detail, bool Bare);

    private readonly record struct GroundPlacement(GroundKey Key, SKRect Target, int Preparation);

    private readonly record struct GroundChunk(int Width, int Height, SKImageInfo Backing, MapCamera Camera);

    private readonly record struct GroundPlan(
        GroundChunk Chunk,
        Dictionary<(TileArtLayer Layer, int Row, byte Piece, string Binding), GroundPositions> Groups,
        double PositionScale = 1);

    private readonly record struct TopologyKey(int Column, int Row, MapDetail Detail, bool Bare);

    private sealed record TopologyPlan(TopologyKey Key, GroundPlan Plan, long EstimatedBytes);

    private readonly Dictionary<TopologyKey, LinkedListNode<TopologyPlan>> topology = [];
    private readonly LinkedList<TopologyPlan> topologyRecency = [];
    private long topologyBytes;
    private int topologyHits, topologyBuilds, topologyEvictions;
    private const long TopologyBudget = 16L * 1024 * 1024;

    private struct GroundPositions
    {
        public SKPoint First;
        public List<SKPoint>? Additional;
        public readonly int Count => 1 + (Additional?.Count ?? 0);
        public readonly SKPoint this[int index] => index == 0 ? First : Additional![index - 1];
    }

    private sealed record GroundImage(GroundKey Key, SKImage Image, int Width, int Height, int Batches)
    {
        public long Bytes => (long)Image.Width * Image.Height * 4;
    }

    private sealed record GroundJob(GroundPlan Plan, SKSurface Surface, SKPicture? Picture) : IDisposable
    {
        public void Dispose()
        {
            Picture?.Dispose();
            Surface.Dispose();
        }
    }

    // A modest magnification of the nearer raster keeps the common dial
    // positions from requiring four times as many backing pixels. In particular,
    // a detail-band transition at 0.55 does not also inflate its backing.
    internal static double GroundRasterTier(double physicalZoom) => physicalZoom switch
    {
        <= .625 => .5,
        <= 1.25 => 1,
        <= 2.5 => 2,
        <= 5 => 4,
        _ => 8
    };

    // The world-aligned ground cache retains topology-dependent layers when the
    // camera changes. Rasterize with a gutter for sprites that overlap chunks.
    private readonly record struct GroundWork(int Used, int Prepared, int Batches, double Tier, long PaintedPixels = 0);

    private GroundWork DrawGround(
        SKCanvas output,
        MapScene scene,
        FrameKey frame,
        MapDetail detail,
        SKRect? damage,
        SKRegion? scrollDamage = null,
        SKRegion? uncovered = null,
        bool reuseAtlas = false,
        HashSet<MapPoint>? settledUpdates = null)
    {
        textures.CompleteStartupGround();
        var map = scene.Map;
        var camera = frame.Camera;
        double zoom = camera.Zoom;
        double tier = GroundRasterTier(zoom * frame.Scaling);
        int period = map.Width * 32;
        int mapWidth = period + (map.Flat ? 32 : 0), mapHeight = map.Height * 16 + 16;
        double viewLeft = ((damage?.Left - 1 ?? 0) - camera.X) / zoom + 32;
        double viewRight = ((damage?.Right + 1 ?? frame.Width) - camera.X) / zoom + 32;
        double viewTop = ((damage?.Top - 1 ?? 0) - camera.Y) / zoom + 16;
        double viewBottom = ((damage?.Bottom + 1 ?? frame.Height) - camera.Y) / zoom + 16;
        int top = Math.Max(0, (int)Math.Floor(viewTop / ChunkSize));
        int bottom = Math.Min((mapHeight - 1) / ChunkSize, (int)Math.Ceiling(viewBottom / ChunkSize) - 1);
        int firstCopy = map.Flat ? 0 : (int)Math.Floor(viewLeft / mapWidth);
        int lastCopy = map.Flat ? 0 : (int)Math.Ceiling(viewRight / mapWidth) - 1;
        int used = 0, prepared = 0, batches = 0;
        long painted = 0;
        int backingSide = (int)((ChunkSize + 2 * ChunkMargin) * tier);
        int batchLimit = Math.Min(GroundBatchLimit, GroundBatchBytes / (backingSide * backingSide * 4));
        // Two double-size chunks already repay packing when the aligned compositor is available.
        int minimumPacked = tier == 2 && AlignedGroundSupported ? MinimumGroundBatch : MinimumPackedGroundBatch;
        bool batchMisses = !GpuGround && batchLimit >= MinimumGroundBatch && !OperatingSystem.IsBrowser() &&
            GroundWorkers.MaxDegreeOfParallelism > 1;
        bool canPack = batchMisses && packGroundTextures;
        bool canPackHalf = canPack && tier == .5 && AlignedGroundSupported;
        bool retainPacked = reuseAtlas && canPack && ground.Count != 0;
        using var packed = canPack && !retainPacked ? new GroundAtlas() : null;
        using var halfPacked = canPackHalf && !retainPacked ? new GroundAtlas(halfSize: true) : null;
        List<GroundPlacement>? pending = null;
        List<GroundKey>? missing = null;
        for (int copy = firstCopy; copy <= lastCopy; copy++)
        {
            double shift = copy * period;
            int left = Math.Max(0, (int)Math.Floor((viewLeft - shift) / ChunkSize));
            int right = Math.Min((mapWidth - 1) / ChunkSize, (int)Math.Ceiling((viewRight - shift) / ChunkSize) - 1);
            for (int row = top; row <= bottom; row++)
            {
                for (int column = left; column <= right; column++)
                {
                    int width = Math.Min(ChunkSize, mapWidth - column * ChunkSize);
                    int height = Math.Min(ChunkSize, mapHeight - row * ChunkSize);
                    if (settledUpdates is not null
                        && !settledUpdates.Any(point => Touches(column, row, width, height, point, map)))
                    {
                        continue;
                    }

                    float x = (float)(camera.X + (column * ChunkSize + shift - 32) * zoom);
                    float y = (float)(camera.Y + (row * ChunkSize - 16) * zoom);
                    var box = SKRect.Create(x, y, (float)(width * zoom), (float)(height * zoom));
                    if (settledUpdates is not null)
                    {
                        var coverage = SKRectI.Intersect(
                            ToBacking(box, frame.Scaling),
                            SKRectI.Create(
                                (int)Math.Ceiling(frame.Width * frame.Scaling),
                                (int)Math.Ceiling(frame.Height * frame.Scaling)));
                        painted += (long)coverage.Width * coverage.Height;
                    }
                    if (scrollDamage is not null && !scrollDamage.Intersects(ToBacking(box, frame.Scaling)))
                    {
                        continue;
                    }

                    var key = new GroundKey(column, row, tier, detail, frame.Display.Bare);
                    if (!batchMisses || pending is not { Count: > 0 } && ground.ContainsKey(key))
                    {
                        DrawOne(key, box);
                        continue;
                    }
                    // After a miss, collect a bounded run of placements. A
                    // resident chunk between two misses does not split their
                    // preparation. Publication still happens in draw order.
                    pending ??= new(batchLimit);
                    missing ??= new(batchLimit);
                    int preparation = -1;
                    if (!ground.ContainsKey(key) && !missing.Contains(key))
                    {
                        preparation = missing.Count;
                        missing.Add(key);
                    }
                    pending.Add(new(key, box, preparation));
                    if (missing.Count == batchLimit || pending.Count == GroundPlacementLimit)
                    {
                        FlushPending();
                    }
                }
            }
        }
        FlushPending();
        return new(used, prepared, batches, tier, painted);

        void DrawOne(GroundKey key, SKRect box)
        {
            var entry = FindGround(scene, key, out bool created);
            if (created)
            {
                prepared++;
            }

            Paint(entry, box);
        }

        void Paint(GroundImage entry, SKRect box)
        {
            batches += entry.Batches;
            used++;
            float margin = (float)(ChunkMargin * tier);
            var source = SKRect.Create(margin, margin, (float)(entry.Width * tier), (float)(entry.Height * tier));
            // The raster backend selects the lower texel at an exact nearest-
            // neighbor tie. GPU normalized coordinates can round either way.
            // Bias only this experimental GPU lookup by 1/1000 of a source
            // texel, retaining the same extent, to give overview ties the same
            // direction. This is sampling policy, not a change to map geometry.
            if (gpuComposition)
            {
                source.Offset(-.001f, -.001f);
            }

            output.DrawImage(entry.Image, source, box, CacheSampling);
            if (uncovered is not null && entry.Image.AlphaType == SKAlphaType.Opaque)
            {
                float scale = (float)frame.Scaling;
                // Leave the edges to the clear even when a rounded Skia box
                // would cover them. Interior pixels are wholly within the draw.
                var covered = new SKRectI(
                    (int)MathF.Ceiling(box.Left * scale) + 1,
                    (int)MathF.Ceiling(box.Top * scale) + 1,
                    (int)MathF.Floor(box.Right * scale) - 1,
                    (int)MathF.Floor(box.Bottom * scale) - 1);
                if (covered.Width > 0 && covered.Height > 0)
                {
                    uncovered.Op(covered, SKRegionOperation.Difference);
                }
            }
        }

        void FlushPending()
        {
            if (pending is not { Count: > 0 } || missing is null)
            {
                return;
            }

            if (missing.Count < MinimumGroundBatch)
            {
                foreach (var item in pending)
                {
                    DrawOne(item.Key, item.Target);
                }

                pending.Clear();
                missing.Clear();
                return;
            }

            long starting = Stopwatch.GetTimestamp();
            var jobs = new List<GroundJob>(missing.Count);
            try
            {
                var plans = new GroundPlan[missing.Count];
                Parallel.For(0, plans.Length, GroundWorkers, i => plans[i] = PlanGround(scene, missing[i]));
                groundSelectorTicks += Stopwatch.GetTimestamp() - starting;
                var bindings = plans.SelectMany(plan => plan.Groups.Keys.Select(key => key.Binding)).Distinct()
                    .ToArray();
                textures.Prepare(bindings, tier);
                var borrowed = textures.BorrowPrepared(bindings, tier);
                GroundAtlas? readyAtlas = null;
                if (plans.Length >= minimumPacked && canPack && borrowed is not null &&
                    CanPackGroundTextures(borrowed, tier))
                {
                    if (canPackHalf && CanSampleGroundAtHalfSize(borrowed, plans))
                    {
                        readyAtlas = Pack(borrowed, halfSize: true);
                    }

                    readyAtlas ??= Pack(borrowed, halfSize: false);
                }
                foreach (var plan in plans)
                {
                    jobs.Add(AllocateGroundJob(plan, record: borrowed is null));
                }
                // Workers use separate CPU surfaces. A cache-constrained
                // batch can resolve textures during recording on the painter lane.
                Parallel.ForEach(jobs, GroundWorkers, job =>
                {
                    if (readyAtlas is not null)
                    {
                        PaintPackedGround(job.Surface, job.Plan, readyAtlas);
                    }
                    else if (borrowed is not null)
                    {
                        PaintGround(job.Surface.Canvas, job.Plan, borrowed);
                    }
                    else
                    {
                        job.Surface.Canvas.DrawPicture(job.Picture!);
                    }
                });
                groundBuildTicks += Stopwatch.GetTimestamp() - starting;
                parallelGroundChunks += jobs.Count;
                foreach (var item in pending)
                {
                    if (item.Preparation < 0)
                    {
                        DrawOne(item.Key, item.Target);
                        continue;
                    }
                    var job = jobs[item.Preparation];
                    long storing = Stopwatch.GetTimestamp();
                    int batches = readyAtlas is not null && job.Plan.Groups.Count > 0 ? 1 : job.Plan.Groups.Count;
                    var entry = StoreGround(item.Key, job.Plan.Chunk, job.Surface, batches);
                    groundBuildTicks += Stopwatch.GetTimestamp() - storing;
                    prepared++;
                    Paint(entry, item.Target);
                }
            }
            finally
            {
                foreach (var job in jobs)
                {
                    job.Dispose();
                }

                pending.Clear();
                missing.Clear();
            }
        }

        GroundAtlas? Pack(IReadOnlyDictionary<string, SKImage> images, bool halfSize)
        {
            if (retainPacked)
            {
                return PackRetainedGround(images, tier, halfSize);
            }

            var atlas = halfSize ? halfPacked! : packed!;
            return atlas.Include(images) ? atlas : null;
        }
    }

    private GroundImage FindGround(MapScene scene, GroundKey key, out bool created)
    {
        if (ground.TryGetValue(key, out var existing))
        {
            groundRecency.Remove(existing);
            groundRecency.AddLast(existing);
            created = false;
            return existing.Value;
        }

        long starting = Stopwatch.GetTimestamp();
        var plan = PlanGround(scene, key);
        groundSelectorTicks += Stopwatch.GetTimestamp() - starting;
        if (GpuGround)
        {
            using var recorder = new SKPictureRecorder();
            var canvas = recorder.BeginRecording(SKRect.Create(plan.Chunk.Backing.Width, plan.Chunk.Backing.Height));
            int groups = PaintGround(canvas, plan);
            using var picture = recorder.EndRecording();
            var deferred = SKImage.FromPicture(picture, new(plan.Chunk.Backing.Width, plan.Chunk.Backing.Height));
            var entry = new GroundImage(key, deferred, plan.Chunk.Width, plan.Chunk.Height, groups);
            TrimGround(entry.Bytes);
            ground.Add(key, groundRecency.AddLast(entry));
            groundBytes += entry.Bytes;
            groundBuildTicks += Stopwatch.GetTimestamp() - starting;
            created = true;
            return entry;
        }
        using var surface = AllocateGround(plan.Chunk);
        int batches = PaintGround(surface.Canvas, plan);
        var image = StoreGround(key, plan.Chunk, surface, batches);
        groundBuildTicks += Stopwatch.GetTimestamp() - starting;
        created = true;
        return image;
    }

    private GroundJob AllocateGroundJob(GroundPlan plan, bool record)
    {
        var chunk = plan.Chunk;
        var surface = AllocateGround(chunk);
        if (!record)
        {
            return new(plan, surface, null);
        }

        try
        {
            using var recorder = new SKPictureRecorder();
            var canvas = recorder.BeginRecording(SKRect.Create(chunk.Backing.Width, chunk.Backing.Height));
            PaintGround(canvas, plan);
            return new(plan, surface, recorder.EndRecording());
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }

    private static GroundChunk DescribeGround(MapObservation map, GroundKey key)
    {
        int left = key.Column * ChunkSize, top = key.Row * ChunkSize;
        int width = Math.Min(ChunkSize, map.Width * 32 + (map.Flat ? 32 : 0) - left);
        int height = Math.Min(ChunkSize, map.Height * 16 + 16 - top);
        var info = new SKImageInfo((int)((width + 2 * ChunkMargin) * key.Tier),
            (int)((height + 2 * ChunkMargin) * key.Tier), SKImageInfo.PlatformColorType, SKAlphaType.Opaque);
        return new(
            width,
            height,
            info,
            new((32 + ChunkMargin - left) * key.Tier, (16 + ChunkMargin - top) * key.Tier, key.Tier));
    }

    private SKSurface AllocateGround(GroundChunk chunk) => allocateRaster(chunk.Backing) ??
        throw new MapResourceException("Could not allocate map ground chunk");

    private GroundImage StoreGround(GroundKey key, GroundChunk chunk, SKSurface surface, int batches)
    {
        var image = new GroundImage(key, surface.Snapshot(), chunk.Width, chunk.Height, batches);
        TrimGround(image.Bytes);
        ground.Add(key, groundRecency.AddLast(image));
        groundBytes += image.Bytes;
        return image;
    }

    private void SetGroundBudget(double physicalPixels)
    {
        // Keep adjacent backing tiers resident across repeated 4K/5K zooms
        // when memory allows. Smaller viewports return to the normal cap.
        if (physicalPixels < MinimumBandPixels)
        {
            ClearRetainedGroundAtlas();
        }

        long viewportBudget = physicalPixels switch
        {
            >= 14_000_000 => 544L * 1024 * 1024,
            >= 8_000_000 => 352L * 1024 * 1024,
            _ => StandardGroundBudget
        };
        // GPU-backed deferred chunks share Skia's 512 MiB cache with sprites,
        // viewport targets and the host. Reserve headroom instead of retaining
        // all zoom tiers up to the software cache's 544 MiB allowance.
        if (GpuGround)
        {
            viewportBudget = Math.Min(viewportBudget, 192L * 1024 * 1024);
        }

        long limit = Math.Min(viewportBudget, machineGroundBudget);
        if (limit == groundBudget)
        {
            return;
        }

        groundBudget = limit;
        TrimGround(0);
    }

    private void TrimGround(long incomingBytes)
    {
        while (groundBytes + PackedGroundBytes + incomingBytes > groundBudget && groundRecency.First is { } victim)
        {
            DropGround(victim);
        }
    }

    private GroundPlan PlanGround(MapScene scene, GroundKey key)
    {
        if (!GpuGround || !gpuFeatures.HasFlag(MapGpuFeatures.GroundPlans))
        {
            return PlanGroundUncached(scene, key);
        }

        var id = new TopologyKey(key.Column, key.Row, key.Detail, key.Bare);
        if (topology.TryGetValue(id, out var resident))
        {
            topologyRecency.Remove(resident);
            topologyRecency.AddLast(resident);
            topologyHits++;
        }
        else
        {
            var plan = PlanGroundUncached(scene, key with { Tier = 1 });
            // A conservative managed-storage estimate, not texture residency.
            long bytes = 256 + plan.Groups.Sum(group => 160L + group.Value.Count * 16L);
            while (topologyBytes + bytes > TopologyBudget && topologyRecency.First is { } victim)
            {
                DropTopology(victim);
            }

            if (bytes > TopologyBudget)
            {
                return PlanGroundUncached(scene, key);
            }

            resident = topologyRecency.AddLast(new TopologyPlan(id, plan, bytes));
            topology.Add(id, resident);
            topologyBytes += bytes;
            topologyBuilds++;
        }
        return new(DescribeGround(scene.Map, key), resident.Value.Plan.Groups, key.Tier);
    }

    private GroundPlan PlanGroundUncached(MapScene scene, GroundKey key)
    {
        var chunk = DescribeGround(scene.Map, key);
        var camera = chunk.Camera;
        int width = chunk.Backing.Width, height = chunk.Backing.Height;
        float zoom = (float)camera.Zoom;
        // Neighboring rows can overlap. Tiles within one row occupy disjoint
        // 64-pixel tiles, so they can share a texture batch without changing
        // alpha order. Preserve multiple pieces within each tile as well.
        var groups = new Dictionary<(TileArtLayer Layer, int Row, byte Piece, string Binding), GroundPositions>();
        Span<byte> pieces = stackalloc byte[(int)TileArtLayer.Fog + 1];
        var selected = new List<TileSprite>(24);
        foreach (var (point, display) in camera.Visible(scene.Map, width, height))
        {
            float x = (float)camera.ScreenX(display) - 32 * zoom, y = (float)camera.ScreenY(point.Y) - 16 * zoom;
            if (x > width || y > height || x + 64 * zoom < 0 || y + 32 * zoom < 0)
            {
                continue;
            }

            MapTileArt.Select(selected, scene, point, key.Detail, key.Bare);
            pieces.Clear();
            foreach (var sprite in selected)
            {
                if (!textures.HasStock && sprite.Layer != TileArtLayer.Ground)
                {
                    continue;
                }

                var groupKey = (sprite.Layer, point.Y, pieces[(int)sprite.Layer]++, sprite.Key);
                var position = new SKPoint(x + sprite.Offset.X * zoom, y + sprite.Offset.Y * zoom);
                ref var group = ref CollectionsMarshal.GetValueRefOrAddDefault(groups, groupKey, out bool exists);
                if (exists)
                {
                    (group.Additional ??= new(2)).Add(position);
                }
                else
                {
                    group.First = position;
                }
            }
        }
        return new(chunk, groups);
    }

    private int PaintGround(SKCanvas canvas, GroundPlan plan, IReadOnlyDictionary<string, SKImage>? borrowed = null)
    {
        canvas.Clear(Background);
        float zoom = (float)plan.Chunk.Camera.Zoom;
        var buffers = new Dictionary<int, (SKRect[] Rectangles, SKRotationScaleMatrix[] Transforms)>();
        foreach (var (binding, instances) in plan.Groups.OrderBy(group => group.Key.Layer)
                     .ThenBy(group => group.Key.Row).ThenBy(group => group.Key.Piece))
        {
            var texture = borrowed is null ? textures.Get(binding.Binding, zoom) : borrowed[binding.Binding];
            var logical = textures.Logical(binding.Binding);
            float scale = zoom * logical.X / texture.Width;
            int count = instances.Count;
            if (!buffers.TryGetValue(count, out var batch))
            {
                buffers.Add(count, batch = (new SKRect[count], new SKRotationScaleMatrix[count]));
            }

            var (rects, transforms) = batch;
            Array.Fill(rects, SKRect.Create(texture.Width, texture.Height));
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i] = new(
                    scale,
                    0,
                    (float)(instances[i].X * plan.PositionScale),
                    (float)(instances[i].Y * plan.PositionScale));
            }
            // A low-resolution replacement can have hard indexed-alpha edges.
            // Preserve those pixels when enlarging the registered sprite.
            canvas.DrawAtlas(texture, rects, transforms, scale > 1 ? CacheSampling : Sampling);
        }
        return plan.Groups.Count;
    }

    private void DropGround(LinkedListNode<GroundImage> entry)
    {
        groundBytes -= entry.Value.Bytes;
        ground.Remove(entry.Value.Key);
        groundRecency.Remove(entry);
        entry.Value.Image.Dispose();
    }

    private void ClearGround()
    {
        ClearRetainedGroundAtlas();
        while (groundRecency.First is { } entry)
        {
            DropGround(entry);
        }

        while (topologyRecency.First is { } entry)
        {
            DropTopology(entry);
        }
    }

    private void DropTopology(LinkedListNode<TopologyPlan> entry)
    {
        topologyBytes -= entry.Value.EstimatedBytes;
        topology.Remove(entry.Value.Key);
        topologyRecency.Remove(entry);
        topologyEvictions++;
    }

    private void InvalidateGround(MapScene? previous, MapScene? replacement)
    {
        if (previous is null || replacement is null || !SameLayout(previous, replacement))
        {
            ClearGround();
            return;
        }
        if (ground.Count == 0 && topology.Count == 0 && settledUpdates is null)
        {
            return;
        }

        var map = replacement.Map;
        var touched = new List<MapPoint>();
        foreach (int i in replacement.ComparisonIndices(previous, gpuFeatures.HasFlag(MapGpuFeatures.Sparse)))
        {
            groundComparedTiles++;
            int row = i / (map.Width / 2);
            var point = new MapPoint(2 * (i % (map.Width / 2)) + (row & 1), row);
            if (SameGround(map.Tiles[i], previous.Map.Tiles[i]) &&
                previous.HasCity(point) == replacement.HasCity(point))
            {
                continue;
            }

            touched.Add(point);
            if (touched.Count <= 128)
            {
                continue;
            }

            ClearGround();
            ClearTerrainFrame();
            return;
        }
        if (settledUpdates is not null)
        {
            settledUpdates.UnionWith(touched);
            if (settledUpdates.Count > 128)
            {
                ClearTerrainFrame();
            }
        }
        for (var entry = groundRecency.First; entry is not null;)
        {
            var next = entry.Next;
            var chunk = entry.Value;
            if (touched.Any(point => Touches(chunk.Key.Column, chunk.Key.Row, chunk.Width, chunk.Height, point, map)))
            {
                DropGround(entry);
            }

            entry = next;
        }
        for (var entry = topologyRecency.First; entry is not null;)
        {
            var next = entry.Next;
            var plan = entry.Value;
            if (touched.Any(
                point => Touches(
                    plan.Key.Column,
                    plan.Key.Row,
                    plan.Plan.Chunk.Width,
                    plan.Plan.Chunk.Height,
                    point,
                    map)))
            {
                DropTopology(entry);
            }

            entry = next;
        }
    }

    private static bool Touches(int column, int row, int width, int height, MapPoint point, MapObservation map)
    {
        int top = row * ChunkSize, y = point.Y * 16 + 16;
        // A changed center affects all W01 B[0..7] neighbors and the sprite
        // extents of those neighbors, plus the sampling gutter.
        if (y + 48 + ChunkMargin < top || y - 48 - ChunkMargin > top + height)
        {
            return false;
        }

        int x = point.X * 32 + 32, left = column * ChunkSize, period = map.Width * 32;
        return Contains(x) || !map.Flat && (Contains(x - period) || Contains(x + period));

        bool Contains(int center) => center + 96 + ChunkMargin >= left && center - 96 - ChunkMargin <= left + width;
    }
}
