using System.Buffers.Binary;

namespace OpenCiv2.Formats.Graphics;

/// <summary>P03 §§5, 8, 9. The independently decoded, bounded indexed map sheet.
/// Keep palette indices until transparency checks and registration markers have been resolved.</summary>
internal sealed class IndexedMapSheet(byte[] indices, uint[] palette, byte? transparent)
{
    private const int Width = 640, Height = 480;
    private const int Pixels = Width * Height;
    public const int MaxBytes = 4 * 1024 * 1024;

    public byte Index(int x, int y) => indices[y * Width + x];

    public uint Rgb(int x, int y) => palette[Index(x, y)];

    private static byte SheetColor(byte index) => unchecked((byte)(index + 10));

    public ArtPoint Anchor(int x, int y, int width, int height, byte marker, ArtPoint adjustment)
    {
        int column = 0, row = 0;
        for (int i = 0; i < width; i++)
        {
            if (SheetColor(Index(x + i, y - 1)) == marker)
            {
                column = i + adjustment.X;
            }
        }

        for (int i = 0; i < height; i++)
        {
            if (SheetColor(Index(x - 1, y + i)) == marker)
            {
                row = i + adjustment.Y;
            }
        }

        return new(column, row);
    }

    public RgbaImage Crop(int x, int y, int width, int height, uint? primary = null, uint? secondary = null)
    {
        if (x < 0 || y < 0 || width <= 0 || height <= 0 || width > Width - x || height > Height - y)
        {
            throw new InvalidDataException("Unbacked map sheet rectangle");
        }

        byte[] pixels = new byte[width * height * 4];
        int offset = 0;
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                WritePixel(pixels.AsSpan(offset, 4), Index(x + column, y + row), primary, secondary);
                offset += 4;
            }
        }
        return new(width, height, pixels);
    }

    /// <summary>P03 TERRAIN1 supplies a shared 64×32 stencil and a separate ocean blend source.</summary>
    public RgbaImage BlendQuadrant(int sourceX, int sourceY, ArtPoint quadrant)
    {
        const int width = 32, height = 16;
        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int offsetX = x + quadrant.X, offsetY = y + quadrant.Y;
                byte mask = Index(1 + offsetX, 447 + offsetY);
                byte index = SheetColor(mask) == 10 ? Index(sourceX + offsetX, sourceY + offsetY) : mask;
                WritePixel(pixels.AsSpan((y * width + x) * 4, 4), index, null, null);
            }
        }

        return new(width, height, pixels);
    }

    private void WritePixel(Span<byte> rgba, byte index, uint? primary, uint? secondary)
    {
        byte color = SheetColor(index);
        uint rgb = color switch
        {
            6 when primary is uint tint => tint,
            5 when secondary is uint tint => tint,
            _ => palette[index]
        };
        rgba[0] = (byte)(rgb >> 16 & 255);
        rgba[1] = (byte)(rgb >> 8 & 255);
        rgba[2] = (byte)(rgb & 255);
        rgba[3] = color is 7 or 9 || index == transparent ? (byte)0 : (byte)255;
    }

    public static IndexedMapSheet Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length > MaxBytes)
        {
            throw Bad("sheet byte budget");
        }

        if (data.StartsWith("BM"u8))
        {
            return Bmp(data);
        }

        if (data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8))
        {
            return Gif(data);
        }

        throw Bad("magic");
    }

    private static IndexedMapSheet Bmp(ReadOnlySpan<byte> data)
    {
        if (data.Length < 1078 || I32(data, 14) != 40 || I32(data, 18) != Width ||
            (I32(data, 22) != Height && I32(data, 22) != -Height) || U16(data, 26) != 1 || U16(data, 28) != 8)
        {
            throw Bad("expected a 640×480 indexed BMP with its 256-entry palette");
        }

        int offset = I32(data, 10);
        if (offset < 1078 || offset > data.Length)
        {
            throw Bad("BMP pixel offset");
        }

        var colors = new uint[256];
        for (int i = 0; i < colors.Length; i++)
        {
            int p = 54 + 4 * i;
            colors[i] = (uint)(data[p + 2] << 16 | data[p + 1] << 8 | data[p]);
        }
        bool topDown = I32(data, 22) < 0;
        var indices = new byte[Pixels];
        int compression = I32(data, 30);
        if (compression == 0)
        {
            if (data.Length - offset < Pixels)
            {
                throw Bad("truncated BMP scanlines");
            }

            for (int y = 0; y < Height; y++)
            {
                data.Slice(offset + (topDown ? y : Height - y - 1) * Width, Width)
                    .CopyTo(
                        indices.AsSpan(y * Width, Width));
            }
        }
        else if (compression == 1 && !topDown)
        {
            BmpRuns(data[offset..], indices);
        }
        else
        {
            throw Bad("unsupported BMP compression/orientation");
        }

        return new(indices, colors, null);
    }

    // Safe RLE8 cursor: explicit x/y, reset x at EOL, admit a delta only within the sheet.
    private static void BmpRuns(ReadOnlySpan<byte> data, Span<byte> output)
    {
        var r = new Input(data);
        int x = 0, y = Height - 1;
        while (!r.Empty)
        {
            int count = r.Byte();
            byte value = r.Byte();
            if (count != 0)
            {
                if (y < 0 || count > Width - x)
                {
                    throw Bad("BMP run outside the row");
                }

                output.Slice(y * Width + x, count).Fill(value);
                x += count;
            }
            else if (value == 0)
            {
                if (y < 0)
                {
                    throw Bad("BMP row overflow");
                }

                x = 0;
                y--;
            }
            else if (value == 1)
            {
                return;
            }
            else if (value == 2)
            {
                x += r.Byte();
                y -= r.Byte();
                if (x >= Width || y < 0)
                {
                    throw Bad("BMP delta outside the sheet");
                }
            }
            else
            {
                if (y < 0 || value > Width - x)
                {
                    throw Bad("BMP literal outside the row");
                }

                r.Read(value).CopyTo(output.Slice(y * Width + x, value));
                if ((value & 1) != 0)
                {
                    r.Byte();
                }

                x += value;
            }
        }
        throw Bad("missing BMP end marker");
    }

    private static IndexedMapSheet Gif(ReadOnlySpan<byte> data)
    {
        var r = new Input(data);
        r.Read(6);
        if (r.Word() != Width || r.Word() != Height)
        {
            throw Bad("GIF sheet dimensions");
        }

        byte flags = r.Byte();
        r.Read(2);
        if ((flags & 128) == 0)
        {
            throw Bad("missing GIF global palette");
        }

        uint[] colors = Colors(ref r, 2 << (flags & 7));
        byte? transparency = null;
        byte tag = r.Byte();
        while (tag == 0x21)
        {
            byte kind = r.Byte();
            if (kind == 0xf9)
            {
                if (r.Byte() != 4)
                {
                    throw Bad("GIF graphics-control block");
                }

                byte control = r.Byte();
                r.Read(2);
                byte key = r.Byte();
                if ((control & 1) != 0)
                {
                    transparency = key;
                }

                if (r.Byte() != 0)
                {
                    throw Bad("GIF graphics-control terminator");
                }
            }
            else
            {
                int length;
                while ((length = r.Byte()) != 0)
                {
                    r.Read(length);
                }
            }
            tag = r.Byte();
        }
        if (tag != 0x2c || r.Word() != 0 || r.Word() != 0 || r.Word() != Width || r.Word() != Height)
        {
            throw Bad("expected one full GIF map sheet");
        }

        byte descriptor = r.Byte();
        if ((descriptor & 128) != 0)
        {
            colors = Colors(ref r, 2 << (descriptor & 7));
        }

        int minimum = r.Byte();
        if (minimum is < 2 or > 8)
        {
            throw Bad("GIF LZW code size");
        }

        using var dataBlocks = new MemoryStream();
        int block;
        while ((block = r.Byte()) != 0)
        {
            dataBlocks.Write(r.Read(block));
        }

        if (r.Byte() != 0x3b || !r.Empty)
        {
            throw Bad("trailing or multi-frame GIF data");
        }

        byte[] indices = Lzw(dataBlocks.GetBuffer().AsSpan(0, (int)dataBlocks.Length), minimum, colors.Length);
        if ((descriptor & 64) != 0)
        {
            var ordered = new byte[Pixels];
            int source = 0;
            foreach (var (first, step) in new[] { (0, 8), (4, 8), (2, 4), (1, 2) })
            {
                for (int y = first; y < Height; y += step, source++)
                {
                    indices.AsSpan(source * Width, Width).CopyTo(ordered.AsSpan(y * Width, Width));
                }
            }

            indices = ordered;
        }
        return new(indices, colors, transparency);
    }

    private static byte[] Lzw(ReadOnlySpan<byte> bytes, int minimum, int palette)
    {
        Span<ushort> prefixes = stackalloc ushort[4096];
        Span<byte> suffixes = stackalloc byte[4096], symbols = stackalloc byte[4096];
        var pixels = new byte[Pixels];
        int clear = 1 << minimum, stop = clear + 1, next = stop + 1, bits = minimum + 1;
        int bit = 0, written = 0, previous = -1;
        while (bit <= bytes.Length * 8 - bits)
        {
            int code = 0;
            for (int i = 0; i < bits; i++, bit++)
            {
                code |= ((bytes[bit / 8] >> (bit & 7)) & 1) << i;
            }

            if (code == clear)
            {
                bits = minimum + 1;
                next = stop + 1;
                previous = -1;
                continue;
            }
            if (code == stop)
            {
                if (written != Pixels)
                {
                    throw Bad("wrong GIF pixel count");
                }

                return pixels;
            }

            bool repeatFirst = code == next && previous >= 0;
            if (code >= next && !repeatFirst)
            {
                throw Bad("unbacked GIF dictionary code");
            }

            int at = repeatFirst ? previous : code, count = 0;
            while (at >= clear)
            {
                if (at <= stop || at >= next || count >= symbols.Length - 1)
                {
                    throw Bad("invalid GIF dictionary prefix");
                }

                symbols[count++] = suffixes[at];
                at = prefixes[at];
            }
            if (at >= palette)
            {
                throw Bad("GIF pixel outside its palette");
            }

            if (count + 1 + (repeatFirst ? 1 : 0) > Pixels - written)
            {
                throw Bad("GIF pixel overflow");
            }

            pixels[written++] = (byte)at;
            while (count > 0)
            {
                pixels[written++] = symbols[--count];
            }

            if (repeatFirst)
            {
                pixels[written++] = (byte)at;
            }

            if (previous >= 0 && next < 4096)
            {
                prefixes[next] = (ushort)previous;
                suffixes[next++] = (byte)at;
                if (next == 1 << bits && bits < 12)
                {
                    bits++;
                }
            }
            previous = code;
        }
        throw Bad("GIF LZW stream ended before its stop code");
    }

    private static uint[] Colors(ref Input r, int count)
    {
        var colors = new uint[count];
        for (int i = 0; i < count; i++)
        {
            colors[i] = (uint)(r.Byte() << 16 | r.Byte() << 8 | r.Byte());
        }

        return colors;
    }

    private static int I32(ReadOnlySpan<byte> bytes, int index) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes[index..]);

    private static ushort U16(ReadOnlySpan<byte> bytes, int index) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes[index..]);

    private static InvalidDataException Bad(string issue) => new($"Invalid scenario map sheet: {issue}");

    private ref struct Input(ReadOnlySpan<byte> bytes)
    {
        private ReadOnlySpan<byte> remaining = bytes;
        public bool Empty => remaining.IsEmpty;

        public ReadOnlySpan<byte> Read(int size)
        {
            if (remaining.Length < size)
            {
                throw Bad("truncated indexed image");
            }

            var value = remaining[..size];
            remaining = remaining[size..];
            return value;
        }

        public byte Byte() => Read(1)[0];

        public ushort Word() => BinaryPrimitives.ReadUInt16LittleEndian(Read(2));
    }
}
