using SkiaSharp;

namespace OpenCiv2.MapRendering;

internal sealed partial class MapGpuSpriteAtlas
{
    // Experimental one-content pages trade cross-image batching for immutable,
    // history-independent placement and admission without copying old residents.
    // Each page has exactly two clamped gutter texels, independent of visible set.
    // These are CPU/picture images; only Skia's context owns GPU residency.
    private readonly LinkedList<Key> stableRecency = [];
    private readonly Dictionary<Key, LinkedListNode<Key>> stableNodes = [];

    private void FinishStablePreparation()
    {
        foreach (var item in pending.Values)
        {
            if (!current.Contains(item.Id) || entries.ContainsKey(item.Id)) continue;
            int width = item.Width + 2 * Gutter, height = item.Height + 2 * Gutter;
            long bytes = 4L * width * height;
            while (Bytes + bytes > Budget)
            {
                var victim = stableRecency.First;
                while (victim is not null && current.Contains(victim.Value)) victim = victim.Next;
                if (victim is null) break;
                var page = entries[victim.Value].Page;
                Bytes -= 4L * page.Width * page.Height;
                page.Dispose();
                entries.Remove(victim.Value);
                stableNodes.Remove(victim.Value);
                stableRecency.Remove(victim);
                Evictions++;
            }
            if (Bytes + bytes > Budget) continue;
            var source = SKRect.Create(Gutter, Gutter, item.Width, item.Height);
            SKImage image;
            if (item.Pixels is not null)
            {
                using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, item.Alpha));
                using var pixels = bitmap.PeekPixels();
                if (!item.Pixels.ReadPixels(bitmap.Info.WithSize(item.Width, item.Height), pixels.GetPixels(Gutter, Gutter), pixels.RowBytes, 0, 0))
                    throw new MapGpuResourceException("Could not pack stable sprite pixels");
                var words = pixels.GetPixelSpan<uint>();
                int stride = pixels.RowBytes / 4;
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        if (x < Gutter || y < Gutter || x >= width - Gutter || y >= height - Gutter)
                            words[y * stride + x] = words[Math.Clamp(y, Gutter, height - Gutter - 1) * stride + Math.Clamp(x, Gutter, width - Gutter - 1)];
                bitmap.SetImmutable();
                image = SKImage.FromBitmap(bitmap);
            }
            else
            {
                using var recorder = new SKPictureRecorder();
                var canvas = recorder.BeginRecording(SKRect.Create(width, height));
                canvas.Clear(SKColors.Transparent);
                for (int gy = -1; gy <= 1; gy++)
                    for (int gx = -1; gx <= 1; gx++)
                    {
                        float sx = gx > 0 ? item.Width - 1 : 0, sy = gy > 0 ? item.Height - 1 : 0;
                        float dx = gx < 0 ? 0 : gx > 0 ? source.Right : source.Left;
                        float dy = gy < 0 ? 0 : gy > 0 ? source.Bottom : source.Top;
                        float scaleX = gx == 0 ? 1 : Gutter, scaleY = gy == 0 ? 1 : Gutter;
                        canvas.Save();
                        canvas.ClipRect(SKRect.Create(dx, dy, gx == 0 ? item.Width : Gutter, gy == 0 ? item.Height : Gutter));
                        canvas.Translate(dx - sx * scaleX, dy - sy * scaleY);
                        canvas.Scale(scaleX, scaleY);
                        canvas.DrawPicture(item.Picture);
                        canvas.Restore();
                    }
                using var picture = recorder.EndRecording();
                image = SKImage.FromPicture(picture, new(width, height)) ?? throw new MapGpuResourceException("Could not allocate stable sprite page");
            }
            entries.Add(item.Id, new(image, source));
            stableNodes.Add(item.Id, stableRecency.AddLast(item.Id));
            Bytes += bytes;
            Admissions++;
        }
        foreach (var item in pending.Values) { item.Picture.Dispose(); item.Pixels?.Dispose(); }
        pending.Clear();
    }

    private void ClearStablePages()
    {
        foreach (var id in stableRecency) entries[id].Page.Dispose();
        stableRecency.Clear();
        stableNodes.Clear();
    }
}
