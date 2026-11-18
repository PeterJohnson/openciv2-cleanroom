using OpenCiv2.Network;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void CompressionExactTokens()
    {
        byte[] raw = [.. Enumerable.Repeat((byte)7, 100), 1, 2, 3];
        foreach (var code in new[] { RunCode.Byte, RunCode.Word })
        {
            var scratch = new byte[raw.Length]; int length = 999;
            Equal(true, Compression.EncodeRuns(code, raw, scratch, ref length).Complete);
            byte[] expected = code == RunCode.Byte ? [100, 7, 131, 1, 2, 3] : [100, 0, 7, 3, 128, 1, 2, 3];
            BytesEqual(expected, scratch[..length]);
            Equal(true, Compression.ExpandSharedImage(ref expected, code == RunCode.Word ? 1U : 0U, true).Complete); BytesEqual(raw, expected);
        }
        var output = new byte[10]; int size = 999;
        Equal((int)CodecStop.NoGain, (int)Compression.EncodeRuns(RunCode.Byte, [8, 8], output, ref size).Stop); Equal(999, size);
        Equal(true, Compression.EncodeRuns(RunCode.Byte, [8, 8, 8], output, ref size).Complete); Equal(2, size); BytesEqual([3, 8], output[..size]);
        size = 999; Equal((int)CodecStop.NoGain, (int)Compression.EncodeRuns(RunCode.Word, [8, 8, 8], output, ref size).Stop); Equal(999, size);
        Equal(true, Compression.EncodeRuns(RunCode.Word, [8, 8, 8, 8], output, ref size).Complete); Equal(3, size); BytesEqual([4, 0, 8], output[..size]);
    }
    private static void CompressionScratchPrefixes()
    {
        foreach (var code in new[] { RunCode.Byte, RunCode.Word })
        {
            byte[] raw = [.. Enumerable.Repeat((byte)3, 50), 7, 8, 9];
            byte[] expected = code == RunCode.Byte ? [50, 3, 131, 7, 8, 9] : [50, 0, 3, 3, 128, 7, 8, 9];
            for (int capacity = 0; capacity <= expected.Length; ++capacity)
            {
                var backing = Enumerable.Repeat((byte)0xcc, expected.Length + 5).ToArray(); int size = 999;
                var result = Compression.EncodeRuns(code, raw, backing.AsSpan(0, capacity), ref size);
                if (capacity == expected.Length) { Equal(true, result.Complete); Equal(expected.Length, size); }
                else { Equal((int)CodecStop.OutputCapacity, (int)result.Stop); Equal(999, size); }
                BytesEqual(expected[..result.Written], backing[..result.Written]); Equal(true, backing.Skip(result.Written).All(b => b == 0xcc));
            }
            var scratch = Enumerable.Repeat((byte)0xcc, 5).ToArray(); int length = 91;
            var noGain = Compression.EncodeRuns(code, [1, 2, 3], scratch, ref length);
            Equal((int)CodecStop.NoGain, (int)noGain.Stop); Equal(91, length);
            BytesEqual(code == RunCode.Byte ? [131, 1, 2] : [3, 128], scratch[..noGain.Written]);
            Equal(true, Compression.EncodeRuns(code, [], [], ref length).Complete); Equal(0, length);
        }
        int count = 4; byte[] shared = [1, 1, 1, 1];
        Throws<ArgumentException>(() => Compression.EncodeRuns((RunCode)2, [], [], ref count));
        Throws<ArgumentException>(() => Compression.EncodeRuns(RunCode.Byte, shared, shared, ref count));
        Throws<ArgumentException>(() => Compression.DecodeWordRuns(shared, shared, ref count)); Equal(4, count);
    }
    private static void CompressionSafeWordRuns()
    {
        foreach (int count in new[] { 32766, 32767, 32768, 65532, 65535 })
        {
            var image = Enumerable.Repeat((byte)0x42, count).ToArray(); Equal(true, Compression.CompressFullImage(ref image).Complete);
            int remaining = count;
            for (int position = 0; position < image.Length; position += 3)
            {
                var expected = Math.Min(32766, remaining); Equal(expected, image[position] | image[position + 1] << 8); Equal((byte)0x42, image[position + 2]); remaining -= expected;
            }
            Equal(0, remaining); Equal(true, Compression.ExpandSharedImage(ref image, 1, true).Complete); Equal(count, image.Length); Equal(true, image.All(b => b == 0x42));
        }
    }
    private static void CompressionBoundariesAndTails()
    {
        foreach (int count in new[] { 0, 1, 2, 3, 4, 126, 127, 128, 129, 32766, 32767, 32768 })
        {
            byte[] raw = [.. Enumerable.Repeat((byte)255, 500), .. Enumerable.Range(0, count).Select(i => unchecked((byte)i))];
            foreach (bool full in new[] { false, true })
            {
                var image = raw.ToArray(); Equal(true, (full ? Compression.CompressFullImage(ref image) : Compression.CompressByteImage(ref image)).Complete);
                Equal(true, Compression.ExpandSharedImage(ref image, full ? 1U : 0U, true).Complete); BytesEqual(raw, image);
            }
        }
        for (int tail = 0; tail < 256; ++tail)
        {
            byte[] raw = [.. Enumerable.Repeat((byte)7, 100), 1, 2, (byte)tail];
            foreach (bool full in new[] { false, true })
            {
                var image = raw.ToArray(); Equal(true, (full ? Compression.CompressFullImage(ref image) : Compression.CompressByteImage(ref image)).Complete);
                Equal(true, Compression.ExpandSharedImage(ref image, full ? 1U : 0U, true).Complete); BytesEqual(raw, image);
            }
        }
    }
    private static void CompressionImageFailureRetention()
    {
        foreach (bool full in new[] { false, true })
        {
            byte[] original = [1, 2, 3, 4]; var image = original;
            Equal((int)CodecStop.NoGain, (int)(full ? Compression.CompressFullImage(ref image) : Compression.CompressByteImage(ref image)).Stop);
            Equal(true, ReferenceEquals(image, original)); BytesEqual([1, 2, 3, 4], image);
        }
        foreach (byte[] original in new byte[][] { [1], [3, 0], [2, 128, 7], [255, 127, 7] })
        {
            var image = original; Equal(false, Compression.ExpandSharedImage(ref image, 1, true).Complete); Equal(true, ReferenceEquals(original, image));
        }
        byte[] raw = [3, 0, 7]; var limited = raw; Equal((int)CodecStop.BackingLimit, (int)Compression.ExpandSharedImage(ref limited, 1, true, 2).Stop); Equal(true, ReferenceEquals(raw, limited));
        raw = [127, 7]; limited = raw; Equal((int)CodecStop.BackingLimit, (int)Compression.ExpandByteImage(ref limited, 126).Stop); Equal(true, ReferenceEquals(raw, limited));
        raw = [2, 7, 131, 8]; limited = raw; Equal((int)CodecStop.TruncatedInput, (int)Compression.ExpandByteImage(ref limited).Stop); Equal(true, ReferenceEquals(raw, limited));
        Throws<ArgumentOutOfRangeException>(() => Compression.ExpandByteImage(ref limited, -1)); Equal(true, ReferenceEquals(raw, limited));
        raw = Enumerable.Repeat((byte)9, 200).ToArray(); var compressed = raw; Equal(true, Compression.CompressByteImage(ref compressed).Complete);
        Equal(false, ReferenceEquals(raw, compressed)); Equal(200, raw.Length); Equal(true, raw.All(b => b == 9));
    }
    private static void CompressionWordValidation()
    {
        uint measured = 777; int length = 999;
        foreach (var (input, stop) in new (byte[], CodecStop)[] { ([1], CodecStop.TruncatedInput), ([3, 0], CodecStop.TruncatedInput), ([3, 0, 65, 2, 128], CodecStop.TruncatedInput), ([255, 127, 7], CodecStop.RejectedToken) })
        {
            var output = Enumerable.Repeat((byte)0xa5, 10).ToArray();
            Equal((int)stop, (int)Compression.MeasureWordRuns(input, ref measured).Stop); Equal(777U, measured);
            Equal((int)stop, (int)Compression.DecodeWordRuns(input, output, ref length).Stop); Equal(999, length); Equal(true, output.All(b => b == 0xa5));
        }
        byte[] tokens = [3, 0, 65, 2, 128, 88, 89, 0, 0, 66, 0, 128];
        Equal(true, Compression.MeasureWordRuns(tokens, ref measured).Complete); Equal(5U, measured);
        byte[] small = [9, 9, 9, 9]; Equal((int)CodecStop.OutputCapacity, (int)Compression.DecodeWordRuns(tokens, small, ref length).Stop); Equal(999, length); BytesEqual([9, 9, 9, 9], small);
        var exact = new byte[5]; Equal(true, Compression.DecodeWordRuns(tokens, exact, ref length).Complete); Equal(5, length); BytesEqual([65, 65, 65, 88, 89], exact);
        measured = 777; var overflow = new byte[131081 * 3]; for (int i = 0; i < overflow.Length; i += 3) { overflow[i] = 254; overflow[i + 1] = 127; overflow[i + 2] = 65; }
        Equal((int)CodecStop.BackingLimit, (int)Compression.MeasureWordRuns(overflow, ref measured).Stop); Equal(777U, measured);
        var bytes = overflow; Equal((int)CodecStop.BackingLimit, (int)Compression.ExpandSharedImage(ref bytes, 1, true, int.MaxValue).Stop); Equal(true, ReferenceEquals(bytes, overflow));
        Equal(true, Compression.MeasureWordRuns([], ref measured).Complete); Equal(0U, measured);
    }
    private static void CompressionByteZeroAndTail()
    {
        byte[] image = [0, 99, 129, 88, 0xaa]; Equal(true, Compression.ExpandByteImage(ref image).Complete); BytesEqual([88], image);
        foreach (var source in new byte[][] { [], [0xcc], [0, 8], [128, 0xcc], [128, 128, 128] })
        { var bytes = source; Equal(true, Compression.ExpandByteImage(ref bytes, 0).Complete); Equal(0, bytes.Length); }
        image = [3, 7]; Equal(true, Compression.ExpandByteImage(ref image, 3).Complete); BytesEqual([7, 7, 7], image);
        image = [129, 8]; Equal(true, Compression.ExpandByteImage(ref image, 1).Complete); BytesEqual([8], image);
    }
    private static void CompressionReceiptSelection()
    {
        foreach (bool compressed in new[] { false, true }) foreach (uint receipt in new[] { 1U, uint.MaxValue })
        {
            byte[] image = [4, 0, 65]; Equal(true, Compression.ExpandSharedImage(ref image, receipt, compressed).Complete); BytesEqual([65, 65, 65, 65], image);
        }
        byte[] raw = [4, 0, 65]; var bytes = raw;
        Equal(true, Compression.ExpandSharedImage(ref bytes, 0, false, 0).Complete); Equal(true, ReferenceEquals(raw, bytes));
        Equal(true, Compression.ExpandSharedImage(ref bytes, 0, true).Complete); BytesEqual([0, 0, 0, 0], bytes);
        foreach (bool full in new[] { false, true })
        {
            byte[] empty = []; Equal(true, (full ? Compression.CompressFullImage(ref empty) : Compression.CompressByteImage(ref empty)).Complete); Equal(0, empty.Length);
            Equal(true, Compression.ExpandSharedImage(ref empty, full ? 1U : 0U, true).Complete); Equal(0, empty.Length);
        }
    }
}
