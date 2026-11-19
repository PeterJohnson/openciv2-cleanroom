using System.Buffers.Binary;
using System.IO.Compression;
using OpenCiv2.Formats.Graphics;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // P03 / C2ATLAS1: a tiny owned archive with a public art binding and deduplicated PNG/proxy data.
    private static void AtlasAdmission()
    {
        byte[] png = TinyPng([0, 10, 20, 30, 128]);
        const int payload = 780 + 2 * 180;
        byte[] pack = new byte[payload + png.Length + 1];
        "C2ATLAS1"u8.CopyTo(pack);
        BinaryPrimitives.WriteUInt32LittleEndian(pack.AsSpan(8), 2);
        for (int i = 0; i < 2; i++)
        {
            var entry = pack.AsSpan(780 + 180 * i, 180);
            entry[0] = (byte)('a' + i);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[160..], 1);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[164..], 1);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[168..], payload);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[172..], (uint)png.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[176..], (uint)(payload + png.Length));
        }
        png.CopyTo(pack, payload);
        pack[^1] = 82;
        AssetAtlas Read(byte[] source) => AssetAtlas.Read(new MemoryStream(source), source.Length);
        var atlas = Read(pack);
        Equal(2, atlas.Sprites.Count);
        Equal(new AtlasSprite("a", 1, 1, 1, 1), atlas.Sprite("a"));
        using var proxy = atlas.OpenProxy("b")!;
        Equal(82, proxy.ReadByte());
        Equal(-1, proxy.ReadByte());
        using var encoded = atlas.OpenPng("a");
        Equal(false, encoded.CanWrite);
        Equal((long)png.Length, encoded.Length);
        var bytes = new byte[4];
        atlas.Decode("a").CopyPixels(bytes);
        Equal("0A141E80", Convert.ToHexString(bytes));
        foreach (var (index, value) in new (int, byte)[]
        {
            (0, 0), (8, 3), (780, 0xff), (781, 0x80), (783, 0xff), (960, (byte)'a'),
            (780 + 160, 0), (780 + 168, 0), (780 + 172 + 3, 0xff), (780 + 176 + 3, 0xff), (payload, 0)
        })
        {
            var bad = pack.ToArray();
            bad[index] = value;
            Throws<InvalidDataException>(() => Read(bad));
        }
        var overlap = pack.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(overlap.AsSpan(780 + 176), payload + 1);
        Throws<InvalidDataException>(() => Read(overlap));
        Throws<InvalidDataException>(() => Read(pack[..^1]));
        Throws<InvalidDataException>(() => atlas.OpenPng("missing"));
    }

    private static void PngFiltersAndFailures()
    {
        byte[] png = TinyPng([
            0, 10, 20, 30, 0, 1, 2, 3, 5,
            1, 10, 20, 30, 0, 247, 238, 229, 5,
            2, 0, 0, 0, 0, 0, 0, 0, 0,
            3, 5, 10, 15, 0, 252, 247, 243, 3,
            4, 0, 0, 0, 0, 0, 0, 0, 0], 2, 5);
        var image = AtlasPng.Decode(png);
        var result = new byte[40];
        image.CopyPixels(result);
        Equal(string.Concat(Enumerable.Repeat("0A141E0001020305", 5)), Convert.ToHexString(result));
        Equal("FFAA5500090A0B0C", Convert.ToHexString(AtlasPng.Decode(TinyPng([2, 255, 170, 85, 0, 9, 10, 11, 12], 2)).Pixels));
        Equal("0A141E000A141E00", Convert.ToHexString(AtlasPng.Decode(TinyPng([3, 10, 20, 30, 0, 5, 10, 15, 0], 2)).Pixels));
        Equal("0A141E0001020305", Convert.ToHexString(AtlasPng.Decode(TinyPng([4, 10, 20, 30, 0, 247, 238, 229, 5], 2)).Pixels));
        // The four Paeth channels choose corner, up on a tie, left on a tie, and up.
        var paeth = TinyPng([0, 30, 30, 30, 30, 10, 70, 10, 10, 4, 20, 236, 40, 0, 40, 231, 230, 253], 2, 2);
        Equal("1E1E1E1E0A460A0A320A461E462D2C07", Convert.ToHexString(AtlasPng.Decode(paeth).Pixels));
        byte[] wide = new byte[8193];
        Array.Fill(wide, byte.MaxValue, 1, wide.Length - 1);
        Equal(true, AtlasPng.Decode(TinyPng(wide, 2048)).Pixels.SequenceEqual(wide.AsSpan(1)));
        const int narrowRows = 1031;
        byte[] narrow = new byte[narrowRows * 5], expected = new byte[narrowRows * 4];
        narrow[4] = 255;
        for (int y = 0; y < narrowRows; y++)
        {
            narrow[y * 5] = 2;
            narrow[y * 5 + 1] = 1;
            expected[y * 4] = unchecked((byte)(y + 1));
            expected[y * 4 + 3] = 255;
        }
        var narrowImage = AtlasPng.Decode(TinyPng(narrow, height: narrowRows));
        Equal(true, narrowImage.Pixels.SequenceEqual(expected));
        byte[] oversizedCopy = new byte[expected.Length + 1];
        oversizedCopy[^1] = 91;
        narrowImage.CopyPixels(oversizedCopy);
        Equal(true, oversizedCopy.AsSpan(0, expected.Length).SequenceEqual(expected));
        Equal((byte)91, oversizedCopy[^1]);
        byte[] badChunkCrc = png.ToArray();
        badChunkCrc[^13] ^= 1; // Final IDAT CRC, immediately before the IEND chunk.
        foreach (var malformed in new[]
        {
            png[..^1], png.Concat(new byte[1]).ToArray(), TinyPng([5, 0, 0, 0, 0]), TinyPng([0, 1]),
            TinyPng([0, 1, 2, 3, 4, 5]), TinyPng([0, 1, 2, 3, 4], width: 5_000_001),
            badChunkCrc, TinyPng([0, 1, 2, 3, 4], corruptZlibTrailer: true)
        }) Throws<InvalidDataException>(() => AtlasPng.Decode(malformed));
        png[29] ^= 1;
        Throws<InvalidDataException>(() => AtlasPng.Decode(png));
    }

    private static void PinnedPackageAdmission()
    {
        foreach (string invalid in new[]
        {
            "[]", "{}", """{"schema":1,"schema":1,"kind":"openciv2-assets","version":"2026.11.07.1","files":{}}""",
            """{"schema":1,"kind":"openciv2-assets","version":"2026.11.07.1","files":{"../LICENSE":{}}}""",
            """{"schema":1,"kind":"openciv2-assets","version":"2026.11.07.1","files":{"data\\atlas-hd.pack":{}}}""",
            """{"schema":1,"kind":"openciv2-assets","version":"untrusted","files":{}}""", new string(' ', 16385)
        })
        {
            int opens = 0;
            Throws<InvalidDataException>(() => PinnedGraphics.LoadAtlas(path =>
            {
                Equal("package.json", path);
                opens++;
                return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalid));
            }));
            Equal(1, opens);
        }
    }

    private static byte[] TinyPng(byte[] scanlines, int width = 1, int height = 1, bool corruptZlibTrailer = false)
    {
        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 6;
        Chunk("IHDR"u8, header);
        using var compressed = new MemoryStream();
        using (var stream = new ZLibStream(compressed, CompressionMode.Compress, leaveOpen: true))
            stream.Write(scanlines);
        var payload = compressed.ToArray();
        if (corruptZlibTrailer) payload[^1] ^= 1;
        Chunk("IDAT"u8, payload.AsSpan(0, 4));
        Chunk("IDAT"u8, payload.AsSpan(4));
        Chunk("IEND"u8, []);
        return png.ToArray();

        void Chunk(ReadOnlySpan<byte> name, ReadOnlySpan<byte> data)
        {
            Span<byte> field = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(field, data.Length);
            png.Write(field);
            png.Write(name);
            png.Write(data);
            uint crc = uint.MaxValue;
            foreach (byte value in name.ToArray().Concat(data.ToArray()))
            {
                crc ^= value;
                for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0U : 0xedb88320U);
            }
            BinaryPrimitives.WriteUInt32BigEndian(field, ~crc);
            png.Write(field);
        }
    }
}
