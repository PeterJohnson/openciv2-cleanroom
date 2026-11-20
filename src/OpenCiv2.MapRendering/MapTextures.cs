using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using OpenCiv2.Formats.Graphics;
using SkiaSharp;

namespace OpenCiv2.MapRendering;

internal sealed class MapCommandCapacityException : Exception;
public sealed record StartupGroundWork(double WorkMs, double JoinMs, int Textures, long TextureBytes);

/// <summary>P03 immutable registered images. The renderer owns these CPU raster textures;
/// the current Skia backend manages any uploads. Only the painter's lane uses this cache.</summary>
internal sealed class MapTextures : IDisposable
{
    private const long BudgetBytes = 96L * 1024 * 1024;
    private const long OverlappingBodyBytes = 16L * 1024 * 1024;
    private const long StartupGroundBytes = 8L * 1024 * 1024;
    private const int HigherBodyBatchCount = 4;
    private const long HigherBodyBatchBytes = 1024 * 1024;
    private static readonly ParallelOptions PreparationWorkers = new()
    {
        MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 6)
    };
    private readonly AssetAtlas? atlas;
    private MapArtSelection? selected;
    private readonly MapArtCatalog catalog = MapArtCatalog.Stock;
    private readonly Dictionary<TextureKey, LinkedListNode<CachedImage>> images = [];
    private readonly LinkedList<CachedImage> recency = [];
    private TexturePreparation? pendingStartup, pendingBodies, pendingHigherBodies;
    private StartupGroundWork? startupGroundWork;
    private bool disposed;
    private bool commandRead;
    private readonly List<SKImage> retiredImages = [];
    private long retiredBytes;
    public long CommandRetainedPeakBytes { get; private set; }
    internal void BeginCommandRead() => commandRead = true;
    internal void EndCommandRead()
    {
        commandRead = false;
        foreach (var image in retiredImages) image.Dispose();
        retiredImages.Clear();
        retiredBytes = 0;
    }
    private void Retire(SKImage image)
    {
        if (commandRead)
        {
            retiredImages.Add(image);
            retiredBytes += 4L * image.Width * image.Height;
            CommandRetainedPeakBytes = Math.Max(CommandRetainedPeakBytes, retiredBytes);
        }
        else image.Dispose();
    }
    public bool GpuPreparation { get; set; }
    public bool EncodedSources { get; set; }
    private readonly Dictionary<string, (SKImage Image, long Bytes)> encodedSources = [];
    public long EncodedBytes { get; private set; }

    private readonly record struct TextureKey(string Binding, int Tier, SKColor? Tint = null);
    private readonly record struct TextureRequest(TextureKey Key, int Width, int Height, RgbaImage? Replacement, ArtPoint Logical, int OtherTier)
    {
        public long Bytes => 4L * (Width * Height + Logical.X * Logical.Y * OtherTier * OtherTier);
        public TextureKey OtherKey => new(Key.Binding, OtherTier);
    }

    private readonly record struct PreparedTexture(SKImage Image, SKImage? Other);
    private sealed record TexturePreparation(TextureRequest[] Requests, PreparedTexture?[] Images, long Bytes, Task<long> Work);
    private readonly record struct PreparationResult(int Count, long WorkTicks, long Bytes = 0);
    private sealed record CachedImage(TextureKey Key, SKImage Image)
    {
        public long Bytes => (long)Image.Width * Image.Height * 4;
    }

    private static readonly uint[] TerrainColors = [0xffbe9c64, 0xffa1a65c, 0xff639754, 0xff387458, 0xff8c8757, 0xff8a8c87,
        0xff969f8a, 0xffcad5dc, 0xff607d61, 0xff386d4b, 0xff3f7492];

    public MapTextures(AssetAtlas? atlas, MapArtSelection? selected = null)
    {
        this.atlas = atlas;
        this.selected = selected;
        if (atlas is not null) catalog.Verify(atlas);
    }

    public bool HasStock => atlas is not null;
    public bool SelectArt(MapArtSelection? selection)
    {
        if (ReferenceEquals(selected, selection)) return false;
        selected = selection;
        return true;
    }
    public int Count => images.Count;
    public long PixelBytes { get; private set; }
    public long PreparationTicks { get; private set; }
    public long TintPreparationTicks { get; private set; }
    public long DemandRasterLoads { get; private set; }
    public StartupGroundWork? StartupGround => Volatile.Read(ref startupGroundWork);
    public ArtPoint Logical(string key) => catalog.Images[key].Logical;
    public ArtPoint Marker(string key, ArtPoint fallback) => selected?.Image(key)?.Marker ?? fallback;
    public ArtPoint Flag(string key, ArtPoint fallback) => selected?.Image(key)?.Flag ?? fallback;
    public uint Color(int role) => selected?.Color(role, catalog.Factions[role].Rgb) ?? catalog.Factions[role].Rgb;
    public (uint Primary, uint Secondary) ShieldColors(int role)
    {
        uint stock = catalog.Factions[role].Rgb;
        return selected?.ShieldColors(role, stock) ?? (stock, stock);
    }

    public SKImage OwnershipFill(double pixelsPerWorldUnit, SKColor color)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        int tier = RasterTier(pixelsPerWorldUnit);
        var key = new TextureKey("ownership", tier, color);
        if (Find(key) is { } cached) return cached;
        long starting = Stopwatch.GetTimestamp();
        // Rasterize the entire diamond once so an integer damage clip cannot
        // change the antialiasing of an unchanged neighboring tile.
        var image = Add(key, CreateDiamond(64 * tier, 32 * tier, color));
        PreparationTicks += Stopwatch.GetTimestamp() - starting;
        return image;
    }

    public SKImage Get(string key, double pixelsPerWorldUnit = 1, SKColor? tint = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var request = Describe(key, pixelsPerWorldUnit, tint);
        if (Find(request.Key) is { } cached) return cached;
        long starting = Stopwatch.GetTimestamp(), previous = PreparationTicks;
        try
        {
            if (tint is SKColor color)
            {
                var mask = Get(key, pixelsPerWorldUnit);
                long tintStart = Stopwatch.GetTimestamp();
                var image = Add(request.Key, GpuPreparation ? DeferredTint(mask, color) : TintMask(key, mask, color));
                TintPreparationTicks += Stopwatch.GetTimestamp() - tintStart;
                return image;
            }
            if (atlas is null) return Add(request.Key, CreatePreview(key, request.Logical));
            if (GpuPreparation && EncodedSources && request.Key.Tier == 0)
                return EncodedSource(key, request.Replacement);
            if (GpuPreparation && request.Key.Tier != 0)
            {
                var source = Get(key, double.MaxValue);
                using var recorder = new SKPictureRecorder();
                var canvas = recorder.BeginRecording(SKRect.Create(request.Width, request.Height));
                canvas.Clear(SKColors.Transparent);
                canvas.DrawImage(source, SKRect.Create(request.Width, request.Height),
                    new SKSamplingOptions(new SKCubicResampler(1f / 3, 1f / 3)));
                using var picture = recorder.EndRecording();
                return Add(request.Key, SKImage.FromPicture(picture, new(request.Width, request.Height)) ?? throw new MapGpuResourceException("Could not prepare GPU sprite"));
            }
            DemandRasterLoads++;
            request = IncludeAdjacentTier(request);
            return Publish(request, Prepare(atlas, request));
        }
        finally
        {
            // A tint may prepare its source in a nested lookup. The outer
            // sample covers both operations.
            PreparationTicks = previous + Stopwatch.GetTimestamp() - starting;
        }
    }

    public bool CanPrepare => !GpuPreparation && atlas is not null && !OperatingSystem.IsBrowser() &&
        PreparationWorkers.MaxDegreeOfParallelism > 1 && PixelBytes < BudgetBytes;

    public void BeginStartupGround()
    {
        // These are scene-independent layers. The first two texture tiers
        // cover both the main desktop map and common high-density views.
        string[] bindings =
        [
            ..catalog.Terrain.SelectMany(terrain => terrain.Edge.Prepend(terrain.Body)),
            ..catalog.Coasts.SelectMany(coast => coast.Variants),
            ..catalog.Relief.OrderBy(entry => entry.Key).SelectMany(entry => entry.Value),
            ..catalog.Rivers, ..catalog.Mouths, ..catalog.Roads, ..catalog.Rails,
            ..catalog.Terrain.SelectMany(terrain => new[] { terrain.FirstResource, terrain.SecondResource }),
            catalog.GrasslandShield, ..catalog.Improvements.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => entry.Value),
            catalog.Fog, ..catalog.FogEdges
        ];
        pendingStartup ??= BeginPreparation(bindings, 1, 1, StartupGroundBytes);
    }

    public void CompleteStartupGround()
    {
        if (pendingStartup is null) return;
        // Join on the painter lane, where the configured theme and the
        // cache budget still agree with the completed texture requests.
        long joinedAt = Stopwatch.GetTimestamp();
        var completed = CompletePreparation(ref pendingStartup, admit: true, optional: true);
        Volatile.Write(ref startupGroundWork, new(Stopwatch.GetElapsedTime(0, completed.WorkTicks).TotalMilliseconds,
            Stopwatch.GetElapsedTime(joinedAt).TotalMilliseconds, completed.Count, completed.Bytes));
    }

    public int Prepare(IEnumerable<string> bindings, double scale)
    {
        if (atlas is null || !CanPrepare) return 0;
        long starting = Stopwatch.GetTimestamp();
        var missing = Missing(bindings, scale);
        // Do not evict a usable raster to speculate on a later lookup. Painting
        // can load misses in order if the set does not fit.
        if (missing.Length == 0 || missing.Sum(request => request.Bytes) > BudgetBytes - PixelBytes) return 0;
        // Only active workers hold decoded source pixels. Completed output
        // fits within the cache headroom checked above. Publish in request order.
        var prepared = new PreparedTexture?[missing.Length];
        try
        {
            PrepareBatch(atlas, missing, prepared);
            PublishBatch(missing, prepared);
        }
        finally
        {
            DisposeBatch(prepared);
        }
        PreparationTicks += Stopwatch.GetTimestamp() - starting;
        return missing.Length;
    }

    private static SKImage DeferredTint(SKImage mask, SKColor color)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SKRect.Create(mask.Width, mask.Height));
        using var filter = SKColorFilter.CreateBlendMode(color, SKBlendMode.SrcIn);
        using var paint = new SKPaint { ColorFilter = filter };
        canvas.Clear(SKColors.Transparent);
        canvas.DrawImage(mask, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), paint);
        using var picture = recorder.EndRecording();
        return SKImage.FromPicture(picture, new(mask.Width, mask.Height)) ?? throw new MapGpuResourceException("Could not prepare GPU tint");
    }

    private SKImage EncodedSource(string key, RgbaImage? replacement)
    {
        if (encodedSources.TryGetValue(key, out var existing)) return existing.Image;
        SKImage image;
        long bytes;
        if (replacement is not null)
        {
            image = ReadPixels(key, replacement);
            bytes = (long)image.Width * image.Height * 4;
        }
        else
        {
            using var input = atlas!.OpenPng(key);
            using var data = SKData.Create(input);
            bytes = data.Size;
            image = SKImage.FromEncodedData(data) ?? throw new MapResourceException($"Could not prepare encoded sprite '{key}'");
        }
        // Bounded source index. Pictures hold immutable references needed by
        // their own lifetime; Skia owns decoded pixel/texture residency.
        if (EncodedBytes + bytes > 64L * 1024 * 1024)
        {
            if (commandRead) { image.Dispose(); throw new MapCommandCapacityException(); }
            ClearEncoded();
        }
        encodedSources.Add(key, (image, bytes));
        EncodedBytes += bytes;
        DemandRasterLoads++;
        return image;
    }

    private void ClearEncoded()
    {
        foreach (var entry in encodedSources.Values) Retire(entry.Image);
        encodedSources.Clear();
        EncodedBytes = 0;
    }

    public bool BeginBodyPreparation(IEnumerable<string> bindings, double scale)
    {
        if (pendingBodies is not null) return false;
        pendingBodies = BeginPreparation(bindings, scale, PreparationWorkers.MaxDegreeOfParallelism, OverlappingBodyBytes);
        return pendingBodies is not null;
    }

    public void BeginHigherBodyPreparation(IEnumerable<string> bindings)
    {
        // Keep one small speculative batch in flight. Its result is useful
        // when the painter crosses from the 2x body tier to the 4x tier.
        pendingHigherBodies ??= BeginPreparation(bindings, 4, 1, HigherBodyBatchBytes, HigherBodyBatchCount);
    }

    public (int Count, double WorkMs) PublishHigherBodies()
    {
        // A missed prediction never blocks a draw on speculative work.
        if (pendingHigherBodies?.Work.IsCompleted != true) return default;
        var prepared = CompletePreparation(ref pendingHigherBodies, admit: true, optional: true);
        return (prepared.Count, Stopwatch.GetElapsedTime(0, prepared.WorkTicks).TotalMilliseconds);
    }

    public void DiscardHigherBodies() => CompletePreparation(ref pendingHigherBodies, admit: false);

    private TexturePreparation? BeginPreparation(IEnumerable<string> bindings, double scale, int minimum, long maximumBytes,
        int maximumCount = int.MaxValue)
    {
        if (atlas is null || !CanPrepare) return null;
        long starting = Stopwatch.GetTimestamp();
        var missing = Missing(bindings, scale, maximumCount);
        long bytes = missing.Sum(request => request.Bytes);
        if (missing.Length < minimum || bytes > Math.Min(maximumBytes, BudgetBytes - PixelBytes)) return null;
        var prepared = new PreparedTexture?[missing.Length];
        // Workers own their decoded rasters. The painter can continue using
        // its texture cache while they work on these immutable requests.
        var work = Task.Run(() =>
        {
            long start = Stopwatch.GetTimestamp();
            PrepareBatch(atlas, missing, prepared);
            return Stopwatch.GetTimestamp() - start;
        });
        PreparationTicks += Stopwatch.GetTimestamp() - starting;
        return new(missing, prepared, bytes, work);
    }

    public int CompleteBodyPreparation(bool admit)
    {
        var result = CompletePreparation(ref pendingBodies, admit);
        if (admit) PreparationTicks += result.WorkTicks;
        return result.Count;
    }

    private PreparationResult CompletePreparation(ref TexturePreparation? queued, bool admit, bool optional = false)
    {
        var pending = queued;
        if (pending is null) return default;
        queued = null;
        try
        {
            long work = pending.Work.GetAwaiter().GetResult();
            if (!admit) return default;
            if (pending.Bytes > BudgetBytes - PixelBytes ||
                pending.Requests.Any(request => images.ContainsKey(request.Key) ||
                    request.OtherTier != 0 && images.ContainsKey(request.OtherKey)))
                return new(0, work);
            PublishBatch(pending.Requests, pending.Images);
            return new(pending.Requests.Length, work, pending.Bytes);
        }
        catch (Exception error) when ((!admit || optional) && error is MapResourceException or InvalidDataException)
        {
            // Discard failed speculative work. A required binding is still
            // loaded and checked by the painter when it is used.
            return default;
        }
        finally
        {
            DisposeBatch(pending.Images);
        }
    }

    private TextureRequest[] Missing(IEnumerable<string> bindings, double scale, int maximumCount = int.MaxValue) =>
        bindings.Distinct().Select(key => Describe(key, scale, null))
            .Where(request => !images.ContainsKey(request.Key)).Take(maximumCount).Select(IncludeAdjacentTier).ToArray();

    private static void PrepareBatch(AssetAtlas atlas, TextureRequest[] requests, PreparedTexture?[] prepared)
    {
        try
        {
            if (requests.Length == 1) prepared[0] = Prepare(atlas, requests[0]);
            else Parallel.For(0, requests.Length, PreparationWorkers, i => prepared[i] = Prepare(atlas, requests[i]));
        }
        catch (AggregateException error) when (error.InnerExceptions.All(failure => failure is MapResourceException or InvalidDataException))
        {
            throw new MapResourceException(error.InnerExceptions[0].Message);
        }
    }

    private void PublishBatch(TextureRequest[] requests, PreparedTexture?[] prepared)
    {
        for (int i = 0; i < requests.Length; i++)
        {
            Publish(requests[i], prepared[i]!.Value);
            prepared[i] = null;
        }
    }

    private static void DisposeBatch(PreparedTexture?[] prepared)
    {
        foreach (var image in prepared)
        {
            image?.Other?.Dispose();
            image?.Image.Dispose();
        }
    }

    public IReadOnlyDictionary<string, SKImage>? BorrowPrepared(IReadOnlyList<string> bindings, double scale)
    {
        // The painter joins all borrowers before it can mutate or release
        // the texture cache again.
        var borrowed = new Dictionary<string, SKImage>(bindings.Count, StringComparer.Ordinal);
        foreach (string binding in bindings)
        {
            var key = Describe(binding, scale, null).Key;
            if (Find(key) is not { } image) return null;
            borrowed.Add(binding, image);
        }
        return borrowed;
    }

    private TextureRequest Describe(string key, double pixelsPerWorldUnit, SKColor? tint)
    {
        var binding = catalog.Images[key];
        var replacement = selected?.Image(key)?.Pixels;
        ArtPoint physical = replacement is null ? binding.Physical : new(replacement.Width, replacement.Height);
        int tier = RasterTier(pixelsPerWorldUnit);
        int width = Math.Min(physical.X, binding.Logical.X * tier);
        int height = Math.Min(physical.Y, binding.Logical.Y * tier);
        if (atlas is null || width == physical.X && height == physical.Y) tier = 0;
        int otherTier = tier is 1 or 2 && physical.X > binding.Logical.X * 2 && physical.Y > binding.Logical.Y * 2 ? 3 - tier : 0;
        return new(new(key, tier, tint), width, height, replacement, binding.Logical, otherTier);
    }

    private TextureRequest IncludeAdjacentTier(TextureRequest request)
    {
        // Navigation crosses the 1x/2x backing threshold frequently. Prepare
        // both small rasters from the same PNG decode while its pixels are in hand.
        return request.OtherTier != 0 && images.ContainsKey(request.OtherKey) ? request with { OtherTier = 0 } : request;
    }

    private static PreparedTexture Prepare(AssetAtlas atlas, TextureRequest request)
    {
        string key = request.Key.Binding;
        var source = ReadPixels(key, request.Replacement ?? atlas.Decode(key));
        if (request.Key.Tier == 0) return new(source, null);
        using (source)
        {
            var image = Scale(key, source, request.Width, request.Height);
            if (request.OtherTier == 0) return new(image, null);
            try
            {
                return new(image, Scale(key, source, request.Logical.X * request.OtherTier, request.Logical.Y * request.OtherTier));
            }
            catch
            {
                image.Dispose();
                throw;
            }
        }
    }

    private SKImage Publish(TextureRequest request, PreparedTexture prepared)
    {
        if (prepared.Other is { } other) Add(request.OtherKey, other);
        return Add(request.Key, prepared.Image);
    }

    private static int RasterTier(double scale) => scale <= 1 ? 1 : scale <= 2 ? 2 : scale <= 4 ? 4 : 8;

    private SKImage? Find(TextureKey key)
    {
        if (!images.TryGetValue(key, out var cached)) return null;
        recency.Remove(cached);
        recency.AddLast(cached);
        return cached.Value.Image;
    }

    private SKImage Add(TextureKey cacheKey, SKImage image)
    {
        var entry = new CachedImage(cacheKey, image);
        // Preserve at most one additional cache-sized frame working set. An
        // oversized command plan falls back before submission to the baseline
        // traversal, whose immediate draws do not require pinning all sources.
        if (commandRead && retiredBytes + Math.Max(0, PixelBytes + entry.Bytes - BudgetBytes) > BudgetBytes)
        {
            image.Dispose();
            throw new MapCommandCapacityException();
        }
        while (PixelBytes + entry.Bytes > BudgetBytes && recency.First is { } victim)
        {
            PixelBytes -= victim.Value.Bytes;
            Retire(victim.Value.Image);
            images.Remove(victim.Value.Key);
            recency.RemoveFirst();
        }
        images.Add(cacheKey, recency.AddLast(entry));
        PixelBytes += entry.Bytes;
        return image;
    }

    private static SKImage ReadPixels(string key, RgbaImage decoded)
    {
        return SKImage.FromPixelCopy(new SKImageInfo(decoded.Width, decoded.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul), decoded.Pixels)
            ?? throw new MapResourceException($"Could not create Skia image '{key}'");
    }

    private static SKImage TintMask(string key, SKImage mask, SKColor color)
    {
        using var source = mask.PeekPixels() ??
            throw new MapResourceException($"Could not read alpha mask '{key}'");
        var info = new SKImageInfo(mask.Width, mask.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap();
        if (!bitmap.TryAllocPixels(info, info.RowBytes))
            throw new MapResourceException($"Could not allocate tinted map texture '{key}'");
        using var target = bitmap.PeekPixels() ??
            throw new MapResourceException($"Could not write tinted map texture '{key}'");
        var output = target.GetPixelSpan<byte>();
        bool packed = source.ColorType is SKColorType.Rgba8888 or SKColorType.Bgra8888;
        if (packed && Sse2.IsSupported && BitConverter.IsLittleEndian && source.RowBytes % sizeof(uint) == 0)
            TintPacked(source, MemoryMarshal.Cast<byte, uint>(output), color);
        else
        {
            var pixels = source.GetPixelSpan();
            for (int y = 0; y < info.Height; y++)
                for (int x = 0; x < info.Width; x++)
                {
                    int i = y * info.RowBytes + x * 4;
                    int alpha = packed ? pixels[y * source.RowBytes + x * 4 + 3] : source.GetPixelColor(x, y).Alpha;
                    output[i] = color.Red;
                    output[i + 1] = color.Green;
                    output[i + 2] = color.Blue;
                    output[i + 3] = (byte)((alpha * color.Alpha + 127) / 255);
                }
        }
        // Keep straight color through resampling. Baking premultiplied RGB
        // into the mask would round color twice at partially covered edges.
        // The immutable bitmap lets Skia share the finished pixels with the cache image.
        bitmap.SetImmutable();
        return SKImage.FromBitmap(bitmap) ??
            throw new MapResourceException($"Could not prepare tinted map texture '{key}'");
    }

    private static void TintPacked(SKPixmap source, Span<uint> output, SKColor color)
    {
        int width = source.Width, stride = source.RowBytes / sizeof(uint);
        var pixels = source.GetPixelSpan<uint>();
        uint rgb = (uint)color.Red | (uint)color.Green << 8 | (uint)color.Blue << 16;
        for (int y = 0; y < source.Height; y++)
        {
            var input = pixels.Slice(y * stride, width);
            var target = output.Slice(y * width, width);
            int x = Avx2.IsSupported ? TintWide(input, target, rgb, color.Alpha) : TintBaseline(input, target, rgb, color.Alpha);
            for (; x < width; x++)
                target[x] = rgb | (((input[x] >> 24) * color.Alpha + 127) / 255 << 24);
        }
    }

    private static int TintBaseline(ReadOnlySpan<uint> input, Span<uint> target, uint rgb, uint opacity)
    {
        var tint = Vector128.Create(rgb);
        var multiplier = Vector128.Create(opacity).AsUInt16();
        var bias = Vector128.Create(127u).AsUInt16();
        var one = Vector128.Create(1u).AsUInt16();
        ref uint from = ref MemoryMarshal.GetReference(input);
        ref uint to = ref MemoryMarshal.GetReference(target);
        int x = 0;
        for (; x <= input.Length - Vector128<uint>.Count; x += Vector128<uint>.Count)
        {
            // Just the low half of each lane holds alpha. SSE2 can multiply it
            // without spilling into the next pixel since alpha is at most 255.
            var alpha = (Vector128.LoadUnsafe(ref from, (nuint)x) >> 24).AsUInt16();
            var scaled = Sse2.MultiplyLow(alpha, multiplier) + bias;
            var rounded = (scaled + one + (scaled >> 8)) >> 8;
            (tint | (rounded.AsUInt32() << 24)).StoreUnsafe(ref to, (nuint)x);
        }
        return x;
    }

    private static int TintWide(ReadOnlySpan<uint> input, Span<uint> target, uint rgb, uint opacity)
    {
        var tint = Vector256.Create(rgb);
        var multiplier = Vector256.Create(opacity);
        var bias = Vector256.Create(127u);
        var one = Vector256.Create(1u);
        ref uint from = ref MemoryMarshal.GetReference(input);
        ref uint to = ref MemoryMarshal.GetReference(target);
        int x = 0;
        for (; x <= input.Length - Vector256<uint>.Count; x += Vector256<uint>.Count)
        {
            var scaled = (Vector256.LoadUnsafe(ref from, (nuint)x) >> 24) * multiplier + bias;
            // Exact unsigned division by 255 over the 8-bit alpha product plus rounding bias.
            var alpha = (scaled + one + (scaled >> 8)) >> 8;
            (tint | (alpha << 24)).StoreUnsafe(ref to, (nuint)x);
        }
        return x;
    }

    private static SKImage Scale(string key, SKImage source, int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height)) ??
            throw new MapResourceException($"Could not prepare map texture '{key}'");
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawImage(source, SKRect.Create(width, height),
            new SKSamplingOptions(new SKCubicResampler(1f / 3, 1f / 3)));
        return surface.Snapshot();
    }

    private static SKImage CreatePreview(string key, ArtPoint logical)
    {
        var color = key.StartsWith("terrain/", StringComparison.Ordinal)
            ? TerrainColors[int.Parse(key.AsSpan(8), System.Globalization.CultureInfo.InvariantCulture)] : 0xff233b42;
        return CreateDiamond(logical.X, logical.Y, new(color));
    }

    private static SKImage CreateDiamond(int width, int height, SKColor color)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height)) ??
            throw new MapResourceException("Could not prepare map tile overlay");
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        using var builder = new SKPathBuilder();
        builder.MoveTo(width / 2f, 0);
        builder.LineTo(width, height / 2f);
        builder.LineTo(width / 2f, height);
        builder.LineTo(0, height / 2f);
        builder.Close();
        surface.Canvas.Clear(SKColors.Transparent);
        using var path = builder.Detach();
        surface.Canvas.DrawPath(path, paint);
        return surface.Snapshot();
    }

    public void Clear()
    {
        EndCommandRead();
        ClearEncoded();
        CompletePreparation(ref pendingStartup, admit: false);
        CompleteBodyPreparation(admit: false);
        DiscardHigherBodies();
        foreach (var item in recency) item.Image.Dispose();
        recency.Clear();
        images.Clear();
        PixelBytes = 0;
    }

    public void Dispose()
    {
        if (disposed) return;
        Clear();
        disposed = true;
    }
}
