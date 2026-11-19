using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Text;

namespace OpenCiv2.Formats.Graphics;

public sealed record AtlasSprite(string Key, int LogicalWidth, int LogicalHeight, int PixelWidth, int PixelHeight);

/// <summary>P03 / asset-format.md C2ATLAS1. Owns the encoded package bytes and lends no mutable storage.</summary>
public sealed class AssetAtlas
{
    internal const int MaxBytes = 256 * 1024 * 1024;
    private const int HeaderSize = 780, RecordSize = 180, KeySize = 160;
    private readonly byte[] bytes;
    private readonly Dictionary<string, Entry> entries;
    private sealed record Entry(AtlasSprite Sprite, int Offset, int Length, int Proxy);

    private AssetAtlas(byte[] bytes, Dictionary<string, Entry> entries)
    {
        this.bytes = bytes;
        this.entries = entries;
        Sprites = Array.AsReadOnly(entries.Values.Select(e => e.Sprite).ToArray());
    }

    public ReadOnlyCollection<AtlasSprite> Sprites { get; }
    public AtlasSprite Sprite(string key) => Find(key).Sprite;
    public (byte Red, byte Green, byte Blue) Palette(byte index)
    {
        int offset = 12 + index * 3;
        return (bytes[offset], bytes[offset + 1], bytes[offset + 2]);
    }

    public Stream OpenPng(string key)
    {
        var entry = Find(key);
        return new MemoryStream(bytes, entry.Offset, entry.Length, writable: false, publiclyVisible: false);
    }

    public Stream? OpenProxy(string key)
    {
        var entry = Find(key);
        return entry.Proxy == 0 ? null : new MemoryStream(bytes, entry.Proxy,
            entry.Sprite.LogicalWidth * entry.Sprite.LogicalHeight, writable: false, publiclyVisible: false);
    }

    public RgbaImage Decode(string key)
    {
        var entry = Find(key);
        return AtlasPng.Decode(bytes.AsSpan(entry.Offset, entry.Length));
    }

    private Entry Find(string key) => entries.TryGetValue(key, out var entry) ? entry :
        throw new InvalidDataException($"Missing atlas binding '{key}'");

    public static AssetAtlas Read(Stream source, int size) => Parse(PinnedGraphics.ReadExact(source, size, MaxBytes, "atlas"));

    internal static AssetAtlas Parse(byte[] bytes)
    {
        if (bytes.Length < HeaderSize || !bytes.AsSpan(0, 8).SequenceEqual("C2ATLAS1"u8))
            throw new InvalidDataException("Invalid C2ATLAS1 header");

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8));
        if (count == 0 || count > 100_000 || count > (bytes.Length - HeaderSize) / RecordSize)
            throw new InvalidDataException("Invalid C2ATLAS1 entry count");

        int payload = HeaderSize + (int)count * RecordSize;
        var entries = new Dictionary<string, Entry>((int)count, StringComparer.Ordinal);
        var ranges = new HashSet<(int Offset, int Length, bool Proxy)>();
        var utf8 = new UTF8Encoding(false, true);
        string? previous = null;
        for (int i = 0; i < count; i++)
        {
            var row = bytes.AsSpan(HeaderSize + i * RecordSize, RecordSize);
            int end = row[..KeySize].IndexOf((byte)0);
            if (end <= 0 || row[end..KeySize].ContainsAnyExcept((byte)0))
                throw new InvalidDataException($"Invalid atlas binding at entry {i}");
            string key;
            try { key = utf8.GetString(row[..end]); }
            catch (DecoderFallbackException e) { throw new InvalidDataException($"Invalid binding UTF-8 at entry {i}", e); }
            if (previous is not null && StringComparer.Ordinal.Compare(previous, key) >= 0)
                throw new InvalidDataException($"Atlas bindings must be sorted and unique at '{key}'");
            previous = key;
            uint width = BinaryPrimitives.ReadUInt32LittleEndian(row[160..]);
            uint height = BinaryPrimitives.ReadUInt32LittleEndian(row[164..]);
            uint png = BinaryPrimitives.ReadUInt32LittleEndian(row[168..]);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(row[172..]);
            uint proxy = BinaryPrimitives.ReadUInt32LittleEndian(row[176..]);
            if (width == 0 || height == 0 || (ulong)width * height > AtlasPng.MaxPixels ||
                png < payload || length < 33 || (ulong)png + length > (uint)bytes.Length ||
                (proxy != 0 && (proxy < payload || (ulong)proxy + (ulong)width * height > (uint)bytes.Length)))
                throw new InvalidDataException($"Invalid atlas dimensions or payload range for '{key}'");

            var (x, y) = AtlasPng.Dimensions(bytes.AsSpan((int)png, (int)length));
            entries.Add(key, new(new(key, (int)width, (int)height, x, y), (int)png, (int)length, (int)proxy));
            ranges.Add(((int)png, (int)length, false));
            if (proxy != 0) ranges.Add(((int)proxy, (int)(width * height), true));
        }
        int endOfPrevious = payload;
        foreach (var range in ranges.OrderBy(r => r.Offset))
        {
            if (range.Offset < endOfPrevious) throw new InvalidDataException("Atlas payloads overlap without sharing the same blob");
            endOfPrevious = range.Offset + range.Length;
        }
        return new(bytes, entries);
    }
}
