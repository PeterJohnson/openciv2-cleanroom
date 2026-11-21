using System.Buffers.Binary;
using OpenCiv2.Base;
using OpenCiv2.Formats;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // P10 original-file input contract and Microsoft RIFF/PCM format definitions.
    private static void WavePcmSamples()
    {
        foreach (int bits in new[] { 8, 16, 24, 32 })
        {
            byte[] values = bits switch
            {
                8 => [0, 128, 255],
                16 => [0, 128, 0, 0, 255, 127],
                24 => [0, 0, 128, 0, 0, 0, 255, 255, 127],
                _ => [0, 0, 0, 128, 0, 0, 0, 0, 255, 255, 255, 127]
            };
            byte[] wave = MakeWave(bits, 1, values);
            var audio = WaveAudio.Decode(wave);
            float maximum = (float)(1 - 1 / Math.Pow(2, bits - 1));
            Check(audio.SampleRate == 22050 && audio.Channels == 1 && audio.Frames == 3, "WAV metadata");
            Check(audio.Samples.SequenceEqual(new float[] { -1, 0, maximum }), "signed PCM normalization");
            Array.Fill(wave, (byte)0);
            Check(audio.Samples[0] == -1, "decoded samples owned");
            Check(BinaryPrimitives.ReadSingleLittleEndian(audio.EncodeFloat32()) == -1, "portable PCM transport");
        }
        var stereo = WaveAudio.Decode(MakeWave(16, 2, [0, 64, 0, 192, 0, 32, 0, 224]));
        Check(stereo.Frames == 2 && stereo.Samples.SequenceEqual(new float[] { .5f, -.5f, .25f, -.25f }), "channel ordering");
        float[] source = [.125f, -.25f]; var owned = new PcmAudio(8000, 1, source); source[0] = 0;
        Check(owned.Samples[0] == .125f, "device input ownership");
        Throws<ArgumentException>(() => new PcmAudio(8000, 1, [float.NaN]));
        Throws<ArgumentException>(() => new PcmAudio(8000, 2, [0]));
        Throws<ArgumentException>(() => new PcmAudio(7999, 1, [0]));
    }
    private static void WaveMalformedInputs()
    {
        byte[] valid = MakeWave(8, 1, [0, 128, 255]);
        for (int length = 0; length < valid.Length; length++)
        {
            byte[] shortened = valid[..length];
            Throws<InvalidDataException>(() => WaveAudio.Decode(shortened));
        }
        foreach (int offset in new[] { 0, 4, 8, 16, 24, 36, 40, 42, 44, 48, 52, 54 })
        {
            var bad = valid.ToArray(); bad[offset] ^= 255;
            Throws<InvalidDataException>(() => WaveAudio.Decode(bad));
        }
        Throws<InvalidDataException>(() => WaveAudio.Decode(MakeWave(16, 2, [0, 0, 0])));
        Throws<InvalidDataException>(() => WaveAudio.Decode(MakeWave(8, 1, [])));
        var duplicate = valid.Concat(valid.AsSpan(36, 24).ToArray()).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(duplicate.AsSpan(4), duplicate.Length - 8);
        Throws<InvalidDataException>(() => WaveAudio.Decode(duplicate));
    }
    private static byte[] MakeWave(int bits, int channels, byte[] data)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(0); writer.Write("WAVE"u8);
        writer.Write("JUNK"u8); writer.Write(3); writer.Write(new byte[] { 1, 2, 3, 0 });
        writer.Write("data"u8); writer.Write(data.Length); writer.Write(data); if ((data.Length & 1) != 0) writer.Write((byte)0);
        writer.Write("fmt "u8); writer.Write(16); writer.Write((ushort)1); writer.Write((ushort)channels);
        writer.Write(22050); writer.Write(22050 * channels * bits / 8); writer.Write((ushort)(channels * bits / 8)); writer.Write((ushort)bits);
        var bytes = stream.ToArray(); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), bytes.Length - 8); return bytes;
    }
}
