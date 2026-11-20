using SkiaSharp;

namespace OpenCiv2.MapRendering;

/// <summary>Experimental immutable, picture-backed atlas pages. Skia owns GPU residency.
/// Consecutive sprites are batched only until an explicit ordering boundary.</summary>
internal sealed partial class MapGpuSpriteAtlas
{
    private const int Side = 2048, Gutter = 2, MaxLayouts = 16;
    private const long Budget = 64L * 1024 * 1024;

    private sealed record Entry(SKImage Page, SKRect Source);

    internal readonly record struct Key(string Binding, int Width, int Height, uint? Tint);

    private sealed record Pending(Key Id, int Width, int Height, SKPicture Picture, SKImage? Pixels, SKAlphaType Alpha);

    private sealed record Layout(HashSet<Key> Keys, Dictionary<Key, Entry> Entries, List<SKImage> Pages, long Bytes);

    private readonly LinkedList<Layout> layouts = [];
    private readonly Dictionary<HashSet<Key>, LinkedListNode<Layout>> history = new(HashSet<Key>.CreateSetComparer());
    private Dictionary<Key, Entry> entries = [];
    private readonly Dictionary<Key, Pending> pending = [];
    private List<SKImage> pages = [];
    private bool building;
    private readonly HashSet<Key> current = [];
    private HashSet<Key> layout = [];
    private long currentBytes;
    private readonly List<SKRect> rectangles = [];
    private readonly List<SKRotationScaleMatrix> transforms = [];
    private readonly List<SKColor> colors = [];
    private readonly Dictionary<int, SKColor[]> colorStorage = [];
    private bool activeTinted;
    // The pinned SkiaSharp API takes exact-length arrays. Cache common batch
    // lengths instead of allocating two ToArray results at every cell boundary.
    private readonly Dictionary<int, (SKRect[] Rectangles, SKRotationScaleMatrix[] Transforms)> batchStorage = [];
    internal bool ReuseBatchStorage { get; set; }
    internal bool StableLayout { get; set; }
    internal bool ModulatedMasks { get; set; }
    internal int Admissions { get; private set; }
    internal int Evictions { get; private set; }
    internal int Hits { get; private set; }
    private SKImage? activePage;
    private SKCanvas? activeCanvas;
    public long Bytes { get; private set; }
    internal int LayoutCount => StableLayout ? stableRecency.Count : layouts.Count;
    internal long SourceBytes => pending.Values.Sum(item => 4L * item.Width * item.Height);
    public int SpriteCalls { get; private set; }
    public int Batches { get; private set; }

    public void BeginFrame() => SpriteCalls = Batches = Admissions = Evictions = Hits = 0;

    public void BeginPreparation()
    {
        Flush();
        current.Clear();
        currentBytes = 0;
    }

    public void Collect(Key key, SKImage image)
    {
        if (current.Contains(key) || image.Width + 2 * Gutter > Side || image.Height + 2 * Gutter > Side)
        {
            return;
        }

        long cost = 4L * (image.Width + 2 * Gutter) * (image.Height + 2 * Gutter);
        if (currentBytes + cost > Budget)
        {
            return;
        }

        current.Add(key);
        currentBytes += cost;
        if (StableLayout && stableNodes.TryGetValue(key, out var resident))
        {
            stableRecency.Remove(resident);
            stableRecency.AddLast(resident);
            Hits++;
            return;
        }
        if (pending.ContainsKey(key))
        {
            return;
        }

        using var recorder = new SKPictureRecorder();
        recorder.BeginRecording(SKRect.Create(image.Width, image.Height))
            .DrawImage(
                image,
                0,
                0,
                new SKSamplingOptions(SKFilterMode.Nearest));
        using var pixels = image.PeekPixels();
        pending.Add(key, new(key, image.Width, image.Height, recorder.EndRecording(),
            pixels is null ? null : SKImage.FromPixelCopy(pixels), image.AlphaType));
    }

    public static bool CanBatch(SKImage image, SKRect destination, SKPaint? paint) =>
        paint?.ColorFilter is null && paint?.Shader is null &&
        (paint is null || paint.Color.Alpha == 255 && paint.BlendMode == SKBlendMode.SrcOver) &&
        Math.Abs(destination.Height - image.Height * (destination.Width / image.Width)) < .001f;

    public void Draw(
        SKCanvas canvas,
        Key key,
        SKImage image,
        SKRect destination,
        SKSamplingOptions sampling,
        SKPaint? paint,
        SKColor? tint = null)
    {
        SpriteCalls++;
        float scale = destination.Width / image.Width;
        if (!CanBatch(image, destination, paint) || !entries.TryGetValue(key, out var entry))
        {
            Flush();
            if (tint is { } color)
            {
                using var filter = SKColorFilter.CreateBlendMode(color, SKBlendMode.SrcIn);
                using var tinted = paint?.Clone() ?? new SKPaint();
                tinted.ColorFilter = filter;
                canvas.DrawImage(image, destination, sampling, tinted);
            }
            else
            {
                canvas.DrawImage(image, destination, sampling, paint);
            }

            return;
        }
        bool colored = ModulatedMasks || tint is not null;
        if (activePage != entry.Page || activeCanvas != canvas || activeTinted != colored)
        {
            Flush();
        }

        activePage = entry.Page;
        activeCanvas = canvas;
        activeTinted = colored;
        rectangles.Add(entry.Source);
        if (colored)
        {
            colors.Add(tint ?? SKColors.White);
        }
        // Atlas transforms apply to each sprite's local (0,0), not its page offset.
        transforms.Add(new(scale, 0, destination.Left, destination.Top));
    }

    public void Flush()
    {
        if (rectangles.Count == 0)
        {
            return;
        }

        if (ReuseBatchStorage && rectangles.Count <= 64)
        {
            if (!batchStorage.TryGetValue(rectangles.Count, out var storage))
            {
                batchStorage.Add(
                    rectangles.Count,
                    storage = (new SKRect[rectangles.Count], new SKRotationScaleMatrix[rectangles.Count]));
            }

            rectangles.CopyTo(storage.Rectangles);
            transforms.CopyTo(storage.Transforms);
            if (activeTinted)
            {
                if (!colorStorage.TryGetValue(colors.Count, out var colorArray))
                {
                    colorStorage.Add(colors.Count, colorArray = new SKColor[colors.Count]);
                }

                colors.CopyTo(colorArray);
                activeCanvas!.DrawAtlas(
                    activePage!,
                    storage.Rectangles,
                    storage.Transforms,
                    colorArray,
                    ModulatedMasks ? SKBlendMode.Modulate : SKBlendMode.DstIn,
                    new SKSamplingOptions(SKFilterMode.Linear));
            }
            else
            {
                activeCanvas!.DrawAtlas(
                    activePage!,
                    storage.Rectangles,
                    storage.Transforms,
                    new SKSamplingOptions(SKFilterMode.Linear));
            }
        }
        else if (activeTinted)
        {
            activeCanvas!.DrawAtlas(
                activePage!,
                rectangles.ToArray(),
                transforms.ToArray(),
                colors.ToArray(),
                ModulatedMasks ? SKBlendMode.Modulate : SKBlendMode.DstIn,
                new SKSamplingOptions(SKFilterMode.Linear));
        }
        else
        {
            activeCanvas!.DrawAtlas(
                activePage!,
                rectangles.ToArray(),
                transforms.ToArray(),
                new SKSamplingOptions(SKFilterMode.Linear));
        }

        Batches++;
        rectangles.Clear();
        transforms.Clear();
        colors.Clear();
        activePage = null;
        activeCanvas = null;
    }

    public void FinishPreparation()
    {
        Flush();
        if (StableLayout)
        {
            FinishStablePreparation();
            return;
        }
        foreach (var key in pending.Keys.Where(key => !current.Contains(key)).ToArray())
        {
            var obsolete = pending[key];
            obsolete.Picture.Dispose();
            obsolete.Pixels?.Dispose();
            pending.Remove(key);
        }
        if (layout.SetEquals(current))
        {
            return;
        }

        if (history.TryGetValue(current, out var cached))
        {
            layouts.Remove(cached);
            layouts.AddLast(cached);
            layout = cached.Value.Keys;
            entries = cached.Value.Entries;
            pages = cached.Value.Pages;
            return;
        }
        if (layouts.Count >= MaxLayouts)
        {
            EvictOldest();
        }

        entries = [];
        pages = [];
        layout = new(current);
        building = true;
        long frameBytes = 0;
        // The current frame alone determines page placement. Cached image IDs,
        // previous cameras and eviction order cannot change texture coordinates.
        var remaining = pending.Values.OrderByDescending(item => item.Height).ThenByDescending(item => item.Width)
            .ThenBy(item => item.Id.Binding, StringComparer.Ordinal).ThenBy(item => item.Id.Tint).ToList();
        while (remaining.Count > 0 && frameBytes < Budget)
        {
            using var recorder = new SKPictureRecorder();
            var canvas = recorder.BeginRecording(SKRect.Create(Side, Side));
            canvas.Clear(SKColors.Transparent);
            int x = 0, y = 0, rowHeight = 0, width = 0;
            var placed = new List<(Key Id, SKRect Source)>();
            var candidates = remaining.Where(
                item => item.Alpha == remaining[0].Alpha && (item.Pixels is null) == (remaining[0].Pixels is null))
                .ToArray();
            foreach (var item in candidates)
            {
                if (x + item.Width + 2 * Gutter > Side)
                {
                    x = 0;
                    y += rowHeight;
                    rowHeight = 0;
                }
                if (y + item.Height + 2 * Gutter > Side)
                {
                    break;
                }

                var box = SKRect.Create(x + Gutter, y + Gutter, item.Width, item.Height);
                canvas.Save();
                canvas.Translate(box.Left, box.Top);
                canvas.DrawPicture(item.Picture);
                canvas.Restore();
                // DrawImage clamps filtering at a sprite's edges. Duplicate its
                // outer texels into the atlas gutter to preserve that behavior.
                for (int gy = -1; gy <= 1; gy++)
                {
                    for (int gx = -1; gx <= 1; gx++)
                    {
                        if (gx == 0 && gy == 0)
                        {
                            continue;
                        }

                        float sx = gx < 0 ? 0 : gx > 0 ? item.Width - 1 : 0;
                        float sy = gy < 0 ? 0 : gy > 0 ? item.Height - 1 : 0;
                        float dx = gx < 0 ? box.Left - Gutter : gx > 0 ? box.Right : box.Left;
                        float dy = gy < 0 ? box.Top - Gutter : gy > 0 ? box.Bottom : box.Top;
                        float scaleX = gx == 0 ? 1 : Gutter, scaleY = gy == 0 ? 1 : Gutter;
                        canvas.Save();
                        canvas.ClipRect(
                            SKRect.Create(dx, dy, gx == 0 ? item.Width : Gutter, gy == 0 ? item.Height : Gutter));
                        canvas.Translate(dx - sx * scaleX, dy - sy * scaleY);
                        canvas.Scale(scaleX, scaleY);
                        canvas.DrawPicture(item.Picture);
                        canvas.Restore();
                    }
                }

                placed.Add((item.Id, box));
                x += item.Width + 2 * Gutter;
                width = Math.Max(width, x);
                rowHeight = Math.Max(rowHeight, item.Height + 2 * Gutter);
            }
            using var picture = recorder.EndRecording();
            int height = y + rowHeight;
            long pageBytes = 4L * width * height;
            if (frameBytes + pageBytes > Budget)
            {
                break;
            }

            while (Bytes + pageBytes > Budget && layouts.First is not null)
            {
                EvictOldest();
            }

            SKImage page;
            if (candidates[0].Pixels is not null)
            {
                // Preserve straight-alpha sources as straight-alpha atlas texels.
                // Rendering them into a premultiplied page changes interpolation.
                using var bitmap = new SKBitmap(
                    new SKImageInfo(width, height, SKColorType.Rgba8888, candidates[0].Alpha));
                bitmap.Erase(SKColors.Transparent);
                using var pixels = bitmap.PeekPixels();
                var words = pixels.GetPixelSpan<uint>();
                int stride = pixels.RowBytes / 4;
                foreach (var (id, source) in placed)
                {
                    var item = pending[id];
                    int px = (int)source.Left, py = (int)source.Top;
                    if (!item.Pixels!.ReadPixels(
                        bitmap.Info.WithSize(item.Width, item.Height),
                        pixels.GetPixels(px, py),
                        pixels.RowBytes,
                        0,
                        0))
                    {
                        throw new MapGpuResourceException("Could not pack sprite atlas pixels");
                    }

                    for (int row = py; row < py + item.Height; row++)
                    {
                        words.Slice(row * stride + px - Gutter, Gutter).Fill(words[row * stride + px]);
                        words.Slice(row * stride + px + item.Width, Gutter)
                            .Fill(
                                words[row * stride + px + item.Width - 1]);
                    }
                    for (int gutter = 1; gutter <= Gutter; gutter++)
                    {
                        words.Slice(py * stride + px - Gutter, item.Width + 2 * Gutter)
                            .CopyTo(
                                words.Slice((py - gutter) * stride + px - Gutter));
                        words.Slice((py + item.Height - 1) * stride + px - Gutter, item.Width + 2 * Gutter)
                            .CopyTo(
                                words.Slice((py + item.Height - 1 + gutter) * stride + px - Gutter));
                    }
                }
                bitmap.SetImmutable();
                page = SKImage.FromBitmap(bitmap);
            }
            else
            {
                page = SKImage.FromPicture(picture, new(width, height))
                    ?? throw new MapGpuResourceException("Could not allocate GPU sprite atlas");
            }

            pages.Add(page);
            Admissions++;
            Bytes += pageBytes;
            frameBytes += pageBytes;
            foreach (var item in placed)
            {
                entries.Add(item.Id, new(page, item.Source));
            }

            var ids = placed.Select(item => item.Id).ToHashSet();
            remaining.RemoveAll(item => ids.Contains(item.Id));
        }
        history.Add(layout, layouts.AddLast(new Layout(layout, entries, pages, frameBytes)));
        building = false;
    }

    private void EvictOldest()
    {
        var oldest = layouts.First!;
        Evictions += oldest.Value.Pages.Count;
        foreach (var image in oldest.Value.Pages)
        {
            image.Dispose();
        }

        Bytes -= oldest.Value.Bytes;
        history.Remove(oldest.Value.Keys);
        layouts.RemoveFirst();
    }

    private void ClearPages()
    {
        foreach (var cached in layouts)
        {
            foreach (var page in cached.Pages)
            {
                page.Dispose();
            }
        }

        if (building)
        {
            foreach (var page in pages)
            {
                page.Dispose();
            }
        }

        building = false;
        layouts.Clear();
        history.Clear();
        pages = [];
        entries = [];
        Bytes = 0;
    }

    public void Clear()
    {
        ClearStablePages();
        rectangles.Clear();
        transforms.Clear();
        colors.Clear();
        activePage = null;
        activeCanvas = null;
        foreach (var item in pending.Values)
        {
            item.Picture.Dispose();
            item.Pixels?.Dispose();
        }
        pending.Clear();
        current.Clear();
        layout.Clear();
        currentBytes = 0;
        ClearPages();
    }
}
