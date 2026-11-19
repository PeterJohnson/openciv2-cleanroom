using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace OpenCiv2.Formats.Graphics;

/// <summary>Owned straight-alpha RGBA rows. Copying preserves the decoder's ownership.</summary>
public sealed class RgbaImage
{
    private readonly byte[] pixels;
    internal RgbaImage(int width, int height, byte[] pixels) => (Width, Height, this.pixels) = (width, height, pixels);
    public int Width { get; }
    public int Height { get; }
    public ReadOnlySpan<byte> Pixels => pixels.AsSpan(0, Width * Height * 4);
    public void CopyPixels(Span<byte> destination) => Pixels.CopyTo(destination);
}

/// <summary>P03 slice decoder for the pinned atlas's non-interlaced RGBA8 PNG profile.
/// Uses .NET's zlib stream and owns all framing, CRC, filter and pixel validation.</summary>
public static class AtlasPng
{
    public const int MaxPixels = 5_000_000;
    private const int MaxEncodedBytes = 32 * 1024 * 1024;
    private const int CrcTableWidth = 256;
    private static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];
    private static ReadOnlySpan<byte> Rgba8 => [8, 6, 0, 0, 0];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static (int Width, int Height) Dimensions(ReadOnlySpan<byte> data)
    {
        if (data.Length < 33 || data.Length > MaxEncodedBytes || !data[..8].SequenceEqual(Signature) ||
            BinaryPrimitives.ReadUInt32BigEndian(data[8..]) != 13 || !data[12..16].SequenceEqual("IHDR"u8))
            throw new InvalidDataException("Invalid PNG header");
        uint width = BinaryPrimitives.ReadUInt32BigEndian(data[16..]);
        uint height = BinaryPrimitives.ReadUInt32BigEndian(data[20..]);
        if (width == 0 || height == 0 || (ulong)width * height > MaxPixels)
            throw new InvalidDataException("PNG dimensions exceed the atlas pixel budget");
        if (!data[24..29].SequenceEqual(Rgba8))
            throw new InvalidDataException("Atlas PNG must be non-interlaced RGBA8");
        return ((int)width, (int)height);
    }

    public static RgbaImage Decode(ReadOnlySpan<byte> data)
    {
        var (width, height) = Dimensions(data);
        using var compressed = new MemoryStream();
        bool haveData = false, closedData = false, ended = false, palette = false;
        int offset = 8;
        while (offset <= data.Length - 12)
        {
            uint length = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
            if (length > data.Length - offset - 12) throw new InvalidDataException("Truncated PNG chunk");
            int size = (int)length;
            var chunk = data.Slice(offset + 4, size + 4);
            var type = chunk[..4];
            if (!ValidChunkName(type) || Crc(chunk) != BinaryPrimitives.ReadUInt32BigEndian(data[(offset + size + 8)..]))
                throw new InvalidDataException("Invalid PNG chunk name or CRC");
            if (type.SequenceEqual("IDAT"u8))
            {
                if (closedData) throw new InvalidDataException("PNG IDAT chunks must be consecutive");
                haveData = true;
                compressed.Write(chunk[4..]);
            }
            else
            {
                if (haveData) closedData = true;
                if (type.SequenceEqual("IEND"u8))
                {
                    ended = size == 0 && offset + 12 == data.Length;
                    break;
                }
                if (type.SequenceEqual("PLTE"u8))
                {
                    if (palette || haveData || size == 0 || size > 768 || size % 3 != 0)
                        throw new InvalidDataException("Invalid PNG palette");
                    palette = true;
                }
                else if (((type[0] & 32) == 0 && offset != 8) || type.SequenceEqual("tRNS"u8))
                    throw new InvalidDataException("Unsupported PNG chunk for RGBA8 atlas");
            }
            offset += size + 12;
        }

        if (!ended || compressed.Length < 6) throw new InvalidDataException("PNG has no complete image");
        compressed.Position = 0;
        using var inflater = new ZLibStream(compressed, CompressionMode.Decompress);
        int rowBytes = width * 4;
        int filteredRowBytes = rowBytes + 1;
        byte[] pixels = GC.AllocateUninitializedArray<byte>(filteredRowBytes * height);
        try
        {
            inflater.ReadExactly(pixels);
            uint a = 1, b = 0;
            AddAdler(ref a, ref b, pixels);
            if (inflater.ReadByte() != -1 || BinaryPrimitives.ReadUInt32BigEndian(compressed.GetBuffer().AsSpan((int)compressed.Length - 4)) != (b << 16 | a))
                throw new InvalidDataException("PNG zlib stream does not match its declared image");

            // Reconstruct each row in place, then slide it past the filter bytes.
            // Completed rows form the contiguous RGBA image at the front of the buffer.
            for (int y = 0; y < height; y++)
            {
                int sourceOffset = y * filteredRowBytes;
                byte filter = pixels[sourceOffset];
                if (filter > 4) throw new InvalidDataException("Invalid PNG row filter");
                var row = pixels.AsSpan(sourceOffset + 1, rowBytes);
                var above = y == 0 ? ReadOnlySpan<byte>.Empty : pixels.AsSpan((y - 1) * rowBytes, rowBytes);
                Unfilter(row, above, filter);
                row.CopyTo(pixels.AsSpan(y * rowBytes, rowBytes));
            }
        }
        catch (EndOfStreamException e) { throw new InvalidDataException("Truncated PNG image data", e); }
        return new(width, height, pixels);
    }

    private static void AddAdler(ref uint a, ref uint b, ReadOnlySpan<byte> filtered)
    {
        // Reduce in bounded blocks. Even an all-255 block keeps both sums
        // inside uint after the previous block's modulo reduction.
        const int blockSize = 4096;
        while (!filtered.IsEmpty)
        {
            int count = Math.Min(filtered.Length, blockSize);
            int x = 0;
            if (Ssse3.IsSupported && count >= 16)
                x = AddAdlerSsse3(ref a, ref b, filtered[..count]);
            else if (Vector128.IsHardwareAccelerated)
            {
                // In sixteen cumulative updates the first byte contributes
                // sixteen times, the second fifteen, and the last once.
                var early = Vector128.Create((ushort)16, 15, 14, 13, 12, 11, 10, 9);
                var late = Vector128.Create((ushort)8, 7, 6, 5, 4, 3, 2, 1);
                for (; x <= count - Vector128<byte>.Count; x += Vector128<byte>.Count)
                {
                    var (low, high) = Vector128.Widen(Vector128.Create<byte>(filtered[x..]));
                    b += 16 * a + (uint)(Vector128.Dot(low, early) + Vector128.Dot(high, late));
                    a += Vector128.Sum(low + high);
                }
            }
            foreach (byte value in filtered[x..count])
            {
                a += value;
                b += a;
            }
            a %= 65521;
            b %= 65521;
            filtered = filtered[count..];
        }
    }

    private static int AddAdlerSsse3(ref uint a, ref uint b, ReadOnlySpan<byte> filtered)
    {
        var weights = Vector128.Create((sbyte)16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1);
        var ones = Vector128.Create((short)1);
        var byteSums = Vector128<uint>.Zero;
        var precedingSums = Vector128<uint>.Zero;
        var weightedSums = Vector128<uint>.Zero;
        int end = filtered.Length & ~15;
        for (int x = 0; x < end; x += 16)
        {
            var bytes = Vector128.Create<byte>(filtered[x..]);
            precedingSums += byteSums;
            byteSums += Sse2.SumAbsoluteDifferences(bytes, Vector128<byte>.Zero).AsUInt32();
            var weightedPairs = Ssse3.MultiplyAddAdjacent(bytes, weights);
            weightedSums += Sse2.MultiplyAddAdjacent(weightedPairs, ones).AsUInt32();
        }
        // Each completed vector adds its byte sum to all sixteen later updates.
        b += (uint)end * a + 16 * Vector128.Sum(precedingSums) + Vector128.Sum(weightedSums);
        a += Vector128.Sum(byteSums);
        return end;
    }

    private static void Unfilter(Span<byte> row, ReadOnlySpan<byte> above, int filter)
    {
        const int pixel = 4;
        switch (filter)
        {
            case 1:
            case 4 when above.IsEmpty:
                UnfilterSub(row);
                break;
            case 2 when !above.IsEmpty:
                UnfilterUp(row, above);
                break;
            case 3 when above.IsEmpty:
                for (int x = pixel; x < row.Length; x++) row[x] = unchecked((byte)(row[x] + row[x - pixel] / 2));
                break;
            case 3:
                for (int x = 0; x < pixel; x++) row[x] = unchecked((byte)(row[x] + above[x] / 2));
                for (int x = pixel; x < row.Length; x++) row[x] = unchecked((byte)(row[x] + (row[x - pixel] + above[x]) / 2));
                break;
            case 4 when Vector128.IsHardwareAccelerated:
                UnfilterPaeth(row, above);
                break;
            case 4:
                for (int x = 0; x < pixel; x++) row[x] = unchecked((byte)(row[x] + above[x]));
                for (int x = pixel; x < row.Length; x++) row[x] = unchecked((byte)(row[x] + Paeth(row[x - pixel], above[x], above[x - pixel])));
                break;
        }
    }

    private static void UnfilterSub(Span<byte> row)
    {
        const int pixel = 4;
        int x = 0;
        if (Vector128.IsHardwareAccelerated && row.Length >= Vector128<byte>.Count)
        {
            // Accumulate each RGBA channel across the four pixels in a vector.
            // Shuffle fills indices outside the source with zero. The last
            // reconstructed pixel becomes the carry into the next vector.
            var onePixel = Vector128.Create((byte)255, 255, 255, 255, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
            var twoPixels = Vector128.Create((byte)255, 255, 255, 255, 255, 255, 255, 255, 0, 1, 2, 3, 4, 5, 6, 7);
            var carry = Vector128<byte>.Zero;
            for (; x <= row.Length - Vector128<byte>.Count; x += Vector128<byte>.Count)
            {
                var values = Vector128.Create<byte>(row[x..]);
                values += Sse2.IsSupported ? Sse2.ShiftLeftLogical128BitLane(values, 4) : Vector128.Shuffle(values, onePixel);
                values += Sse2.IsSupported ? Sse2.ShiftLeftLogical128BitLane(values, 8) : Vector128.Shuffle(values, twoPixels);
                values += carry;
                values.CopyTo(row[x..]);
                carry = Vector128.Create(values.AsUInt32().GetElement(3)).AsByte();
            }
        }
        for (x = Math.Max(x, pixel); x < row.Length; x++)
            row[x] = unchecked((byte)(row[x] + row[x - pixel]));
    }

    private static void UnfilterUp(Span<byte> row, ReadOnlySpan<byte> above)
    {
        int x = 0;
        if (Vector128.IsHardwareAccelerated)
            for (; x <= row.Length - Vector128<byte>.Count; x += Vector128<byte>.Count)
                (Vector128.Create<byte>(row[x..]) + Vector128.Create(above[x..])).CopyTo(row[x..]);
        for (; x < row.Length; x++) row[x] = unchecked((byte)(row[x] + above[x]));
    }

    private static void UnfilterPaeth(Span<byte> row, ReadOnlySpan<byte> above)
    {
        var left = Vector128<short>.Zero;
        var corner = Vector128<short>.Zero;
        for (int x = 0; x < row.Length; x += 4)
        {
            // The four channels are independent within each pixel. Distances
            // use signed lanes; adding the residual wraps as an RGBA byte.
            var up = Vector128.WidenLower(ReadPixel(above[x..])).AsInt16();
            var leftDelta = left - corner;
            var upDelta = up - corner;
            var toLeft = Vector128.Abs(upDelta);
            var toUp = Vector128.Abs(leftDelta);
            var toCorner = Vector128.Abs(leftDelta + upDelta);
            var prediction = Vector128.ConditionalSelect(Vector128.LessThanOrEqual(toUp, toCorner), up, corner);
            prediction = Vector128.ConditionalSelect(Vector128.LessThanOrEqual(toLeft, Vector128.Min(toUp, toCorner)), left, prediction);
            var decoded = ReadPixel(row[x..]) + Vector128.Narrow(prediction.AsUInt16(), Vector128<ushort>.Zero);
            BinaryPrimitives.WriteUInt32LittleEndian(row[x..], decoded.AsUInt32().ToScalar());
            left = Vector128.WidenLower(decoded).AsInt16();
            corner = up;
        }

        static Vector128<byte> ReadPixel(ReadOnlySpan<byte> bytes) =>
            Vector128.CreateScalar(BinaryPrimitives.ReadUInt32LittleEndian(bytes)).AsByte();
    }

    private static int Paeth(int left, int up, int corner)
    {
        int estimate = left + up - corner;
        int a = Math.Abs(estimate - left), b = Math.Abs(estimate - up), c = Math.Abs(estimate - corner);
        return a <= b && a <= c ? left : b <= c ? up : corner;
    }

    private static bool ValidChunkName(ReadOnlySpan<byte> name)
    {
        foreach (byte c in name)
            if (c is not (>= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z')) return false;
        return (name[2] & 32) == 0;
    }

    private static uint Crc(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        while (bytes.Length >= 4)
        {
            uint block = crc ^ BinaryPrimitives.ReadUInt32LittleEndian(bytes);
            crc = CrcTable[3 * CrcTableWidth + (block & 255)] ^ CrcTable[2 * CrcTableWidth + ((block >> 8) & 255)] ^
                CrcTable[CrcTableWidth + ((block >> 16) & 255)] ^ CrcTable[block >> 24];
            bytes = bytes[4..];
        }
        foreach (byte value in bytes) crc = (crc >> 8) ^ CrcTable[(crc ^ value) & 255];
        return ~crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[4 * CrcTableWidth];
        for (uint i = 0; i < CrcTableWidth; i++)
        {
            uint value = i;
            for (int bit = 0; bit < 8; bit++) value = (value >> 1) ^ ((value & 1) != 0 ? 0xedb88320U : 0);
            table[i] = value;
        }
        // Each successive table advances the contribution by one zero byte.
        // Together four lookups process a little-endian word at a time.
        for (int i = CrcTableWidth; i < table.Length; i++)
        {
            uint previous = table[i - CrcTableWidth];
            table[i] = (previous >> 8) ^ table[previous & 255];
        }
        return table;
    }
}
