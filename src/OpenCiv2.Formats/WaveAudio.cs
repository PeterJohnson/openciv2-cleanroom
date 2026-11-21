using System.Buffers.Binary;
using OpenCiv2.Base;

namespace OpenCiv2.Formats;

/// <summary>P10 authored WAV profile: RIFF PCM 8/16/24/32-bit mono/stereo.
/// Microsoft RIFF/WAVEFORMATEX defines the container; no original-game decoder is used.</summary>
public static class WaveAudio
{
    public static PcmAudio Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 || bytes.Length > 32 * 1024 * 1024 || !bytes[..4].SequenceEqual("RIFF"u8) ||
            !bytes.Slice(8, 4).SequenceEqual("WAVE"u8) || BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]) != bytes.Length - 8)
        {
            throw new InvalidDataException("Invalid or oversized RIFF/WAVE file.");
        }

        ReadOnlySpan<byte> format = default, data = default;
        bool foundFormat = false, foundData = false;
        for (int offset = 12; offset < bytes.Length;)
        {
            if (bytes.Length - offset < 8)
            {
                throw new InvalidDataException("Truncated WAV chunk.");
            }

            var header = bytes.Slice(offset, 8);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
            long next = (long)offset + 8 + size + (size & 1);
            if (next > bytes.Length)
            {
                throw new InvalidDataException("Truncated WAV payload or padding.");
            }

            var content = bytes.Slice(offset + 8, (int)size);
            if (header[..4].SequenceEqual("fmt "u8))
            {
                if (foundFormat)
                {
                    throw new InvalidDataException("Duplicate WAV format.");
                }

                foundFormat = true;
                format = content;
            }
            if (header[..4].SequenceEqual("data"u8))
            {
                if (foundData)
                {
                    throw new InvalidDataException("Duplicate WAV data.");
                }

                foundData = true;
                data = content;
            }
            offset = (int)next;
        }
        if (!foundFormat || !foundData || format.Length < 16 || BinaryPrimitives.ReadUInt16LittleEndian(format) != 1)
        {
            throw new InvalidDataException("Only integer PCM WAV is supported.");
        }

        int channels = BinaryPrimitives.ReadUInt16LittleEndian(format[2..]);
        uint rate = BinaryPrimitives.ReadUInt32LittleEndian(format[4..]);
        int bits = BinaryPrimitives.ReadUInt16LittleEndian(format[14..]);
        int stride = channels * (bits / 8);
        if (channels is < 1 or > 2 || rate is < 8000 or > 192000 || bits is not (8 or 16 or 24 or 32) ||
            BinaryPrimitives.ReadUInt16LittleEndian(format[12..]) != stride ||
            BinaryPrimitives.ReadUInt32LittleEndian(format[8..]) != rate * stride || data.Length == 0 || data.Length % stride != 0 ||
            data.Length / stride > rate * 600 || data.Length / (bits / 8) > PcmAudio.MaximumSamples)
        {
            throw new InvalidDataException("Unsupported or inconsistent PCM WAV format/length.");
        }

        if (format.Length != 16 && (format.Length < 18 || BinaryPrimitives.ReadUInt16LittleEndian(format[16..]) != format.Length - 18))
        {
            throw new InvalidDataException("Invalid WAV format extension.");
        }

        var values = new float[data.Length / (bits / 8)];
        for (int i = 0, offset = 0; i < values.Length; i++, offset += bits / 8)
        {
            values[i] = bits switch
            {
                8 => (data[offset] - 128) / 128f,
                16 => BinaryPrimitives.ReadInt16LittleEndian(data[offset..]) / 32768f,
                24 => ((data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16) << 8 >> 8) / 8388608f,
                _ => (float)(BinaryPrimitives.ReadInt32LittleEndian(data[offset..]) / 2147483648d)
            };
        }

        return new((int)rate, channels, values);
    }
}
