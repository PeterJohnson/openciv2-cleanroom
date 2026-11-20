using System.Security.Cryptography;
using System.Text.Json;
using OpenCiv2.Formats.Graphics;

/// <summary>P03 browser adapter. Same-origin HTTP supplies streams to the same pinned
/// readers as desktop; selected map sheets retain F03 precedence and case-independent lookup.</summary>
internal static class BrowserContent
{
    internal sealed record Loaded(AssetAtlas Atlas, PinnedFonts Fonts, MapArtSelection? Art);

    internal static async Task<Loaded> LoadAsync(HttpClient client, string? theme, CancellationToken cancellationToken = default)
    {
        if (theme is not null and not ("native" or "classic"))
        {
            throw new ArgumentException("Unknown map art selection");
        }

        var atlas = await PinnedGraphics.LoadAtlasAsync(
            (path, token) => client.GetStreamAsync("content/assets/" + path, token), cancellationToken);
        var fonts = await PinnedGraphics.LoadFontsAsync(
            (path, token) => client.GetStreamAsync("content/fonts/" + path, token), cancellationToken);
        MapArtSelection? art = null;
        if (theme is not null)
        {
            var files = await LoadFilesAsync(client, "content/art/index.json", "content/art/", cancellationToken);
            Stream? Open(string directory, string name)
            {
                var key = directory + "/" + name;
                return files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes, writable: false) : null;
            }
            art = MapArtSelection.Load(path => Open("native", path) ?? throw new FileNotFoundException(path),
                theme == "classic" ? leaf => Open("scenario", leaf) : null,
                theme == "classic" ? leaf => Open("installation", leaf) : null);
        }
        return new(atlas, fonts, art);
    }

    internal static async Task<Dictionary<string, byte[]>> LoadFilesAsync(HttpClient client, string index, string prefix,
        CancellationToken cancellationToken = default)
    {
        await using var source = await client.GetStreamAsync(index, cancellationToken);
        byte[] metadata = new byte[1024 * 1024 + 1];
        int count = await source.ReadAtLeastAsync(metadata, metadata.Length, false, cancellationToken);
        if (count == metadata.Length)
        {
            throw new InvalidDataException("Content index is too large");
        }

        using var document = JsonDocument.Parse(metadata.AsMemory(0, count));
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        int total = 0;
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            string path = entry.GetProperty("path").GetString()!;
            if (string.IsNullOrEmpty(path) || path.Contains('\\') || path.Contains(':') || path.Contains('%') ||
                path.Contains('?') || path.Contains('#') || path.Split('/').Any(part => part is "" or "." or ".."))
            {
                throw new InvalidDataException("Invalid content path");
            }

            int size = entry.GetProperty("size").GetInt32();
            if (size < 0 || size > 64 * 1024 * 1024 || (total += size) > 128 * 1024 * 1024 || files.ContainsKey(path))
            {
                throw new InvalidDataException("Duplicate or oversized content entry");
            }

            await using var input = await client.GetStreamAsync(prefix + path, cancellationToken);
            byte[] bytes = new byte[size];
            await input.ReadExactlyAsync(bytes, cancellationToken);
            if (await input.ReadAsync(new byte[1], cancellationToken) != 0 ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != entry.GetProperty("sha256").GetString())
            {
                throw new InvalidDataException($"Content identity mismatch: {path}");
            }

            files.Add(path, bytes);
        }
        return files;
    }
}
