using System.Security.Cryptography;
using System.Text.Json;

namespace OpenCiv2.Formats.Graphics;

/// <summary>P03/F03. The caller provides read-once streams from a selected content root.
/// Release identity is compiled from the reviewed inventories, independently of the input manifest.</summary>
public static class PinnedGraphics
{
    private const string AtlasPath = "data/atlas-hd.pack";

    private sealed record FilePin(int Size, string Sha256);

    public static AssetAtlas LoadAtlas(Func<string, Stream> open)
    {
        using var inventory = Inventory("assets");
        var release = inventory.RootElement.GetProperty("runtimeRelease");
        var files = release.GetProperty("files").EnumerateObject()
            .ToDictionary(
                f => f.Name,
                f => Pin(f.Value),
                StringComparer.Ordinal);
        using var manifest = open("package.json");
        byte[] metadata = new byte[16385];
        int length = manifest.ReadAtLeast(metadata, metadata.Length, false);
        if (length == metadata.Length)
        {
            throw new InvalidDataException("Asset manifest is too large");
        }

        try
        {
            using var parsed = JsonDocument.Parse(metadata.AsMemory(0, length));
            var root = parsed.RootElement;
            RequireProperties(root, ["schema", "kind", "version", "files"]);
            int schema = root.GetProperty("schema").GetInt32();
            string? kind = root.GetProperty("kind").GetString(), version = root.GetProperty("version").GetString();
            string expectedVersion = release.GetProperty("version").GetString()!;
            if (schema != 1 || kind != "openciv2-assets" || version != expectedVersion)
            {
                throw new InvalidDataException(
                    $"Asset package identity does not match the pinned release: expected schema 1, kind 'openciv2-assets', version '{expectedVersion}'; " +
                    $"found schema {schema}, kind '{kind}', version '{version}'. " +
                    $"Download {release.GetProperty("url").GetString()}, verify its SHA-256 against dependencies.json, and select the extracted package root.");
            }

            var fileMap = root.GetProperty("files");
            RequireProperties(fileMap, files.Keys);
            foreach (var (path, pin) in files)
            {
                var row = fileMap.GetProperty(path);
                RequireProperties(row, ["size", "sha256"]);
                if (Pin(row) != pin)
                {
                    throw new InvalidDataException($"Asset manifest does not match the release pin for '{path}'");
                }
            }
        }
        catch (Exception e) when (e is JsonException or FormatException or InvalidOperationException)
        {
            throw new InvalidDataException("Invalid asset package manifest", e);
        }

        byte[]? atlas = null;
        foreach (var (path, pin) in files)
        {
            using var source = open(path);
            if (path == AtlasPath)
            {
                atlas = ReadExact(source, pin.Size, AssetAtlas.MaxBytes, path);
                CheckHash(SHA256.HashData(atlas), pin, path);
            }
            else
            {
                VerifyStream(source, pin, path);
            }
        }
        return AssetAtlas.Parse(atlas!);
    }

    public static PinnedFonts LoadFonts(Func<string, Stream> open)
    {
        using var inventory = Inventory("fonts");
        var root = inventory.RootElement;
        var license = root.GetProperty("license");
        using (var source = open(license.GetProperty("file").GetString()!))
        {
            VerifyStream(source, Pin(license), "DejaVu LICENSE");
        }

        var fonts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var face in root.GetProperty("faces").EnumerateArray())
        {
            var path = face.GetProperty("file").GetString()!;
            var pin = Pin(face);
            using var source = open(path);
            var bytes = ReadExact(source, pin.Size, 1024 * 1024, path);
            CheckHash(SHA256.HashData(bytes), pin, path);
            fonts.Add(face.GetProperty("id").GetString()!, bytes);
        }
        return new(fonts);
    }

    internal static byte[] ReadExact(Stream stream, int size, int limit, string path)
    {
        if (size <= 0 || size > limit || stream.CanSeek && stream.Length - stream.Position != size)
        {
            throw new InvalidDataException($"Invalid size for '{path}'");
        }

        byte[] bytes = GC.AllocateUninitializedArray<byte>(size);
        try
        {
            stream.ReadExactly(bytes);
        }
        catch (EndOfStreamException e)
        {
            throw new InvalidDataException($"Truncated '{path}'", e);
        }
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException($"Unexpected trailing bytes in '{path}'");
        }

        return bytes;
    }

    private static void VerifyStream(Stream stream, FilePin pin, string path)
    {
        if (stream.CanSeek && stream.Length - stream.Position != pin.Size)
        {
            throw new InvalidDataException($"Invalid size for '{path}'");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[65536];
        int remaining = pin.Size;
        while (remaining != 0)
        {
            int read = stream.Read(buffer, 0, Math.Min(buffer.Length, remaining));
            if (read == 0)
            {
                throw new InvalidDataException($"Truncated '{path}'");
            }

            hash.AppendData(buffer, 0, read);
            remaining -= read;
        }
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException($"Unexpected trailing bytes in '{path}'");
        }

        CheckHash(hash.GetHashAndReset(), pin, path);
    }

    private static FilePin Pin(JsonElement item) =>
        new(item.GetProperty("size").GetInt32(), item.GetProperty("sha256").GetString()!);

    private static void CheckHash(ReadOnlySpan<byte> hash, FilePin pin, string path)
    {
        if (!Convert.ToHexStringLower(hash).Equals(pin.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"SHA-256 mismatch for '{path}'");
        }
    }

    private static JsonDocument Inventory(string name)
    {
        using var stream = typeof(PinnedGraphics).Assembly.GetManifestResourceStream($"OpenCiv2.Graphics.{name}.json")!;
        return JsonDocument.Parse(stream);
    }

    private static void RequireProperties(JsonElement node, IEnumerable<string> names)
    {
        var remaining = names.ToHashSet(StringComparer.Ordinal);
        foreach (var property in node.EnumerateObject())
        {
            if (!remaining.Remove(property.Name))
            {
                throw new InvalidDataException($"Unexpected or duplicate asset field '{property.Name}'");
            }
        }

        if (remaining.Count != 0)
        {
            throw new InvalidDataException("Incomplete asset manifest");
        }
    }
}

/// <summary>Validated encoded faces, independent of runtime glyph and drawing resources.</summary>
public sealed class PinnedFonts
{
    private readonly Dictionary<string, byte[]> faces;

    internal PinnedFonts(Dictionary<string, byte[]> faces)
    {
        this.faces = faces;
        Faces = Array.AsReadOnly(faces.Keys.ToArray());
    }

    public IReadOnlyList<string> Faces { get; }

    public Stream OpenFace(string id) => new MemoryStream(faces[id], writable: false);
}
