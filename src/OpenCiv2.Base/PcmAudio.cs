using System.Buffers.Binary;

namespace OpenCiv2.Base;

/// <summary>Owned, interleaved linear samples. No device, decoder or gameplay state.</summary>
public sealed class PcmAudio
{
    public const int MaximumSamples = 16 * 1024 * 1024;
    private readonly float[] samples;
    public int SampleRate { get; }
    public int Channels { get; }
    public ReadOnlySpan<float> Samples => samples;
    public int Frames => samples.Length / Channels;
    public double DurationSeconds => (double)Frames / SampleRate;

    public PcmAudio(int sampleRate, int channels, ReadOnlySpan<float> values)
    {
        if (sampleRate is < 8000 or > 192000 || channels is < 1 or > 2 || values.Length == 0 ||
            values.Length > MaximumSamples || values.Length % channels != 0 || values.Length / channels > sampleRate * 600)
        {
            throw new ArgumentException("Audio must be mono/stereo, 8–192 kHz and at most ten minutes/16M samples.");
        }

        foreach (float value in values)
        {
            if (!float.IsFinite(value) || value < -1 || value > 1)
            {
                throw new ArgumentException("Invalid linear sample.");
            }
        }

        SampleRate = sampleRate;
        Channels = channels;
        samples = values.ToArray();
    }

    public byte[] EncodeFloat32()
    {
        var bytes = new byte[samples.Length * 4];
        for (int i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4), samples[i]);
        }

        return bytes;
    }
}
