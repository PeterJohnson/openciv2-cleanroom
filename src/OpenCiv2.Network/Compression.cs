using System.Buffers.Binary;

namespace OpenCiv2.Network;

public enum RunCode { Byte, Word }
public enum CodecStop { Complete, NoGain, OutputCapacity, TruncatedInput, RejectedToken, BackingLimit }
public readonly record struct CodecResult(CodecStop Stop = CodecStop.Complete, int Written = 0)
{
    public bool Complete => Stop == CodecStop.Complete;
}

/// <summary>N02 section 7 bounded codecs. Logical input only; word runs exclude unsafe token 32767.
/// Owned image transforms replace input only on success. Scratch codecs require disjoint spans.</summary>
public static class Compression
{
    public const int DefaultLimit = 8 * 1024 * 1024;

    public static CodecResult EncodeRuns(
        RunCode code,
        ReadOnlySpan<byte> input,
        Span<byte> output,
        ref int outputLength)
    {
        if (code != RunCode.Byte && code != RunCode.Word)
        {
            throw new ArgumentException("Unknown run-length code width", nameof(code));
        }

        if (input.Overlaps(output))
        {
            throw new ArgumentException("Run encoder requires disjoint input and output");
        }

        var encoder = new Encoder(code, input, output);
        var result = encoder.Run();
        if (result.Complete)
        {
            outputLength = result.Written;
        }

        return result;
    }

    private ref struct Encoder(RunCode code, ReadOnlySpan<byte> input, Span<byte> output)
    {
        private readonly bool word = code == RunCode.Word;
        private readonly ReadOnlySpan<byte> input = input;
        private readonly Span<byte> output = output;
        private int written;
        private CodecStop stop;

        private bool HasRoom(int count)
        {
            if (count > output.Length - written)
            {
                stop = CodecStop.OutputCapacity;
                return false;
            }
            if (written + count >= input.Length)
            {
                stop = CodecStop.NoGain;
                return false;
            }
            return true;
        }

        private bool EmitByte(byte value)
        {
            if (written == output.Length)
            {
                stop = CodecStop.OutputCapacity;
                return false;
            }
            output[written++] = value;
            if (written >= input.Length)
            {
                stop = CodecStop.NoGain;
                return false;
            }
            return true;
        }

        private bool EmitToken(int count, bool literal, int begin)
        {
            var header = count + (literal ? (word ? 32768 : 128) : 0);
            if (word)
            {
                if (!HasRoom(2))
                {
                    return false;
                }

                BinaryPrimitives.WriteUInt16LittleEndian(output[written..], (ushort)header);
                written += 2;
                var body = literal ? count : 1;
                if (!HasRoom(body))
                {
                    return false;
                }

                if (literal)
                {
                    input.Slice(begin, count).CopyTo(output[written..]);
                }
                else
                {
                    output[written] = input[begin];
                }

                written += body;
                return true;
            }
            if (!EmitByte((byte)header))
            {
                return false;
            }

            if (!literal)
            {
                return EmitByte(input[begin]);
            }

            for (var i = 0; i < count; ++i)
            {
                if (!EmitByte(input[begin + i]))
                {
                    return false;
                }
            }

            return true;
        }

        public CodecResult Run()
        {
            var threshold = word ? 3 : 2;
            var maximum = word ? 32767 : 127;
            int begin = 0, scan = 1;
            while (begin < input.Length)
            {
                while (scan < input.Length && input[scan] == input[begin])
                {
                    ++scan;
                }

                if (scan - begin > threshold)
                {
                    while (begin < scan)
                    {
                        var count = Math.Min(word ? maximum - 1 : maximum, scan - begin);
                        if (!EmitToken(count, false, begin))
                        {
                            return new(stop, written);
                        }

                        begin += count;
                    }
                }
                while (scan < input.Length)
                {
                    if (scan + 1 == input.Length)
                    {
                        ++scan;
                        break;
                    }
                    if (input[scan + 1] == input[scan])
                    {
                        break;
                    }

                    ++scan;
                }
                while (begin < scan)
                {
                    var count = Math.Min(maximum, scan - begin);
                    if (!EmitToken(count, true, begin))
                    {
                        return new(stop, written);
                    }

                    begin += count;
                }
            }
            return new(CodecStop.Complete, written);
        }
    }

    public static CodecResult MeasureWordRuns(ReadOnlySpan<byte> input, ref uint decodedSize)
    {
        int position = 0;
        ulong size = 0;
        while (position < input.Length)
        {
            if (input.Length - position < 2)
            {
                return new(CodecStop.TruncatedInput);
            }

            var code = BinaryPrimitives.ReadUInt16LittleEndian(input[position..]);
            if (code == 32767)
            {
                return new(CodecStop.RejectedToken);
            }

            position += 2;
            var count = code & 32767;
            var payload = code < 32768 ? 1 : count;
            if (payload > input.Length - position)
            {
                return new(CodecStop.TruncatedInput);
            }

            position += payload;
            size += (uint)count;
            if (size > uint.MaxValue)
            {
                return new(CodecStop.BackingLimit);
            }
        }
        decodedSize = (uint)size;
        return new();
    }

    public static CodecResult DecodeWordRuns(ReadOnlySpan<byte> input, Span<byte> output, ref int outputLength)
    {
        if (input.Overlaps(output))
        {
            throw new ArgumentException("Run decoder requires disjoint input and output");
        }

        uint measured = 0;
        var result = MeasureWordRuns(input, ref measured);
        if (!result.Complete)
        {
            return result;
        }

        if (measured > output.Length)
        {
            return new(CodecStop.OutputCapacity);
        }

        int position = 0, written = 0;
        while (position < input.Length)
        {
            var code = BinaryPrimitives.ReadUInt16LittleEndian(input[position..]);
            position += 2;
            var count = code & 32767;
            if (code >= 32768)
            {
                input.Slice(position, count).CopyTo(output[written..]);
                position += count;
            }
            else
            {
                output.Slice(written, count).Fill(input[position]);
                ++position;
            }
            written += count;
        }
        outputLength = written;
        return new(CodecStop.Complete, written);
    }

    public static CodecResult CompressByteImage(ref byte[] image) => Compress(RunCode.Byte, ref image);

    public static CodecResult CompressFullImage(ref byte[] image) => Compress(RunCode.Word, ref image);

    private static CodecResult Compress(RunCode code, ref byte[] image)
    {
        var encoded = new byte[image.Length];
        int length = 0;
        var result = EncodeRuns(code, image, encoded, ref length);
        if (!result.Complete)
        {
            return result.Stop == CodecStop.OutputCapacity ? result with { Stop = CodecStop.NoGain } : result;
        }

        image = encoded[..length];
        return result;
    }

    public static CodecResult ExpandByteImage(ref byte[] image, int limit = DefaultLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        int position = 0, size = 0;
        // A final unmatched byte is ignored. A zero run still consumes its value.
        while (image.Length - position > 1)
        {
            var code = image[position++];
            var count = code & 127;
            var payload = code < 128 ? 1 : count;
            if (payload > image.Length - position)
            {
                return new(CodecStop.TruncatedInput);
            }

            if (count > limit - size)
            {
                return new(CodecStop.BackingLimit);
            }

            size += count;
            position += payload;
        }
        var decoded = new byte[size];
        position = 0;
        int written = 0;
        while (image.Length - position > 1)
        {
            var code = image[position++];
            var count = code & 127;
            if (code >= 128)
            {
                image.AsSpan(position, count).CopyTo(decoded.AsSpan(written));
                position += count;
            }
            else
            {
                decoded.AsSpan(written, count).Fill(image[position]);
                ++position;
            }
            written += count;
        }
        image = decoded;
        return new(CodecStop.Complete, size);
    }

    public static CodecResult ExpandSharedImage(
        ref byte[] image,
        uint fullReceiptCount,
        bool compressed,
        int limit = DefaultLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        if (fullReceiptCount == 0)
        {
            return compressed ? ExpandByteImage(ref image, limit) : new(CodecStop.Complete, image.Length);
        }

        uint measured = 0;
        var result = MeasureWordRuns(image, ref measured);
        if (!result.Complete)
        {
            return result;
        }

        if (measured > limit)
        {
            return new(CodecStop.BackingLimit);
        }

        var decoded = new byte[(int)measured];
        int length = 0;
        result = DecodeWordRuns(image, decoded, ref length);
        if (result.Complete)
        {
            image = decoded;
        }

        return result;
    }
}
