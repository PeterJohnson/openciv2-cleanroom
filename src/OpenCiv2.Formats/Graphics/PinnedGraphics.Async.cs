using System.Security.Cryptography;

namespace OpenCiv2.Formats.Graphics;

public static partial class PinnedGraphics
{
    /// <summary>P03 pinned content admission for asynchronous sources. Reads the atlas once
    /// into its owned buffer and hashes other release files without retaining their payloads.</summary>
    public static async Task<AssetAtlas> LoadAtlasAsync(Func<string, CancellationToken, Task<Stream>> open,
        CancellationToken cancellationToken = default)
    {
        using var inventory = Inventory("assets");
        var release = inventory.RootElement.GetProperty("runtimeRelease");
        var files = release.GetProperty("files").EnumerateObject()
            .ToDictionary(f => f.Name, f => Pin(f.Value), StringComparer.Ordinal);
        await using (var manifest = await open("package.json", cancellationToken))
        {
            byte[] bytes = new byte[16385];
            int count = await manifest.ReadAtLeastAsync(bytes, bytes.Length, false, cancellationToken);
            using var buffered = new MemoryStream(bytes, 0, count, writable: false);
            ValidateManifest(buffered, release, files);
        }

        byte[]? atlas = null;
        foreach (string path in inventory.RootElement.GetProperty("selectedRuntimeFiles").EnumerateArray().Select(value => value.GetString()!))
        {
            var pin = files[path];
            await using var source = await open(path, cancellationToken);
            if (path == AtlasPath)
            {
                atlas = await ReadPinnedAsync(source, pin, AssetAtlas.MaxBytes, path, cancellationToken);
            }
            else
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[65536];
                int remaining = pin.Size;
                while (remaining > 0)
                {
                    int count = await source.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken);
                    if (count == 0)
                    {
                        throw new InvalidDataException($"Truncated '{path}'");
                    }

                    hash.AppendData(buffer, 0, count);
                    remaining -= count;
                }
                if (await source.ReadAsync(buffer.AsMemory(0, 1), cancellationToken) != 0)
                {
                    throw new InvalidDataException($"Unexpected trailing bytes in '{path}'");
                }

                CheckHash(hash.GetHashAndReset(), pin, path);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return AssetAtlas.Parse(atlas!);
    }

    public static async Task<PinnedFonts> LoadFontsAsync(Func<string, CancellationToken, Task<Stream>> open,
        CancellationToken cancellationToken = default)
    {
        using var inventory = Inventory("fonts");
        var root = inventory.RootElement;
        var license = root.GetProperty("license");
        await using (var source = await open(license.GetProperty("file").GetString()!, cancellationToken))
        {
            await ReadPinnedAsync(source, Pin(license), 65536, "DejaVu LICENSE", cancellationToken);
        }

        var fonts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var face in root.GetProperty("faces").EnumerateArray())
        {
            string path = face.GetProperty("file").GetString()!;
            await using var source = await open(path, cancellationToken);
            fonts.Add(face.GetProperty("id").GetString()!,
                await ReadPinnedAsync(source, Pin(face), 1024 * 1024, path, cancellationToken));
        }
        return new(fonts);
    }

    private static async Task<byte[]> ReadPinnedAsync(Stream source, FilePin pin, int limit, string path,
        CancellationToken cancellationToken)
    {
        if (pin.Size <= 0 || pin.Size > limit)
        {
            throw new InvalidDataException($"Invalid size for '{path}'");
        }

        byte[] bytes = GC.AllocateUninitializedArray<byte>(pin.Size);
        try
        {
            await source.ReadExactlyAsync(bytes, cancellationToken);
        }
        catch (EndOfStreamException e)
        {
            throw new InvalidDataException($"Truncated '{path}'", e);
        }
        if (await source.ReadAsync(new byte[1], cancellationToken) != 0)
        {
            throw new InvalidDataException($"Unexpected trailing bytes in '{path}'");
        }

        CheckHash(SHA256.HashData(bytes), pin, path);
        return bytes;
    }
}
