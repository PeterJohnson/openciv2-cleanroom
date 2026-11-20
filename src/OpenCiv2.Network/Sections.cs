using OpenCiv2.Base;
using OpenCiv2.Core;
using OpenCiv2.IO;

namespace OpenCiv2.Network;

public readonly record struct SectionDescriptor(
    int Size = 0,
    int PaddedSize = 0,
    int Id = 0,
    uint Checksum = 0,
    int MirrorOffset = 0);
public sealed record ImageBlock(SectionDescriptor Descriptor, OwnedList<byte> Data);
public sealed class StateSection
{
    public SectionDescriptor Descriptor { get; set; }

    /// <summary>Materialized wire bytes, including established padding through PaddedSize.</summary>
    public byte[] Backing { get; set; } = [];
}
public sealed class StateSections
{
    private readonly StateSection[] sections = Enumerable.Range(0, Sections.Count).Select(_ => new StateSection())
        .ToArray();
    public StateSection this[int index] => sections[index];
}
public enum ImageApplyStop { Complete, Truncated, InvalidSize, InvalidSection, DestinationExtent }
public readonly record struct ImageApplyResult(
    ImageApplyStop Stop = ImageApplyStop.Complete,
    int Applied = 0,
    int Consumed = 0);
public sealed class StateMirror
{
    public byte[] Bytes { get; set; } = [];
    public int Scan { get; set; }
    public int ScanEnd { get; set; }
    public int Section { get; set; }
    public int SectionOffset { get; set; }
}
public enum ImageKind { Full, Delta, Join, MapHeader }

/// <summary>N02 sections 5–6 and Appendix C. Explicit materialized wire sections, never CLR object bytes.
/// World publication, event-pool locking and compression are caller operations.</summary>
public static class Sections
{
    public const int Count = 24, PrefixSize = 20;

    public static uint Checksum(ReadOnlySpan<byte> backing, int size)
    {
        if (size <= 0)
        {
            return 0;
        }

        var reader = new ByteReader(backing);
        uint total = 0;
        if (size % 4 == 0)
        {
            for (int i = 0; i < size / 4; ++i)
            {
                total = unchecked(total + reader.U32());
            }
        }
        else if (size % 2 == 0)
        {
            for (int i = 0; i < size / 4; ++i)
            {
                total = unchecked(total + reader.U16());
            }
        }
        else
        {
            for (int i = 0; i < size; ++i)
            {
                total = unchecked(total + reader.U8());
            }
        }

        return total;
    }

    public static byte[] EncodeDescriptor(SectionDescriptor descriptor)
    {
        var writer = new ByteWriter();
        writer.I32(descriptor.Size);
        writer.I32(descriptor.PaddedSize);
        writer.I32(descriptor.Id);
        writer.U32(descriptor.Checksum);
        writer.I32(descriptor.MirrorOffset);
        return writer.ToArray();
    }

    public static SectionDescriptor DecodeDescriptor(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != PrefixSize)
        {
            throw new DecodeException(Math.Min(bytes.Length, PrefixSize), "Incorrect section prefix extent");
        }

        var reader = new ByteReader(bytes);
        return new(reader.I32(), reader.I32(), reader.I32(), reader.U32(), reader.I32());
    }

    public static byte[] EncodeRawImage(IEnumerable<ImageBlock> blocks)
    {
        var writer = new ByteWriter();
        foreach (var block in blocks)
        {
            if (block.Descriptor.Size < 0 || block.Data.Count != block.Descriptor.Size || block.Descriptor.Id < 0
                || block.Descriptor.Id >= Count)
            {
                throw new ArgumentException("Image block outside admitted ID/size domain");
            }

            writer.Append(EncodeDescriptor(block.Descriptor));
            writer.Append(block.Data.ToArray());
        }
        return writer.ToArray();
    }

    public static OwnedList<ImageBlock> DecodeRawImage(ReadOnlySpan<byte> bytes)
    {
        var reader = new ByteReader(bytes);
        var blocks = new List<ImageBlock>();
        while (!reader.Remaining.IsEmpty)
        {
            var offset = reader.Position;
            var descriptor = DecodeDescriptor(reader.Take(PrefixSize));
            if (descriptor.Size < 0 || descriptor.Id < 0 || descriptor.Id >= Count)
            {
                throw new DecodeException(offset, "Image block outside admitted ID/size domain");
            }

            blocks.Add(new(descriptor, new(reader.Take(descriptor.Size).ToArray())));
        }
        return new(blocks);
    }

    /// <summary>Sequential commits keep earlier blocks on rejection. Ancillary prefix fields are ignored;
    /// each live checksum is recomputed from the incoming payload. Input must be disjoint from sections.</summary>
    public static ImageApplyResult ApplyRawImage(StateSections sections, ReadOnlySpan<byte> bytes)
    {
        int applied = 0, consumed = 0;
        while (consumed < bytes.Length)
        {
            if (bytes.Length - consumed < PrefixSize)
            {
                return new(ImageApplyStop.Truncated, applied, consumed);
            }

            var descriptor = DecodeDescriptor(bytes.Slice(consumed, PrefixSize));
            consumed += PrefixSize;
            if (descriptor.Size < 0)
            {
                return new(ImageApplyStop.InvalidSize, applied, consumed);
            }

            if (descriptor.Id < 0 || descriptor.Id >= Count)
            {
                return new(ImageApplyStop.InvalidSection, applied, consumed);
            }

            if (descriptor.Size > bytes.Length - consumed)
            {
                return new(ImageApplyStop.Truncated, applied, consumed);
            }

            var section = sections[descriptor.Id];
            if (descriptor.Size > section.Backing.Length)
            {
                return new(ImageApplyStop.DestinationExtent, applied, consumed);
            }

            var incoming = bytes.Slice(consumed, descriptor.Size);
            incoming.CopyTo(section.Backing);
            section.Descriptor = section.Descriptor with { Checksum = Checksum(incoming, descriptor.Size) };
            consumed += descriptor.Size;
            ++applied;
        }
        return new(ImageApplyStop.Complete, applied, consumed);
    }

    public static OwnedList<int> ClassicSizes(Geometry geometry)
    {
        var coarse = geometry.CoarseWidth * geometry.CoarseHeight;
        var area = geometry.Area;
        return new(
            [4, 0x14c, 0x790, 0x2ca0, 0x400, 0x10000, 0x5800, 0x3f0, 0x4b0, 100, 0x154, 0x494, 16, coarse, coarse,
                area, area, area, area, area, area, area, area * 6, 50000]);
    }

    public static void Register(StateSections sections)
    {
        for (int i = 0; i < Count; ++i)
        {
            var section = sections[i];
            var d = section.Descriptor;
            if (d.Size < 0 || d.Size > int.MaxValue - 3)
            {
                throw new ArgumentException("Section size outside nonwrapping backing domain");
            }

            section.Descriptor = d with { Id = i, PaddedSize = (d.Size + 3) & ~3, Checksum = 0 };
            Admit(section);
        }
        // All descriptor registrations precede mirrored offset assignment. Event offset is retained.
        int offset = 0;
        for (int i = 0; i < Count - 1; ++i)
        {
            var d = sections[i].Descriptor;
            sections[i].Descriptor = d with { MirrorOffset = offset };
            if (d.PaddedSize > int.MaxValue - offset)
            {
                throw new ArgumentException("Mirror extent exceeds nonwrapping backing domain");
            }

            offset += d.PaddedSize;
        }
    }

    public static void AllocateMirror(StateMirror mirror, StateSections sections, bool host)
    {
        mirror.Bytes = [];
        int size = 0;
        for (int i = 0; i < Count - 1; ++i)
        {
            Admit(sections[i]);
            var padded = sections[i].Descriptor.PaddedSize;
            if (padded > int.MaxValue - size)
            {
                throw new ArgumentException("Mirror size exceeds managed backing domain");
            }

            size += padded;
        }
        mirror.Bytes = new byte[size];
        mirror.ScanEnd = host ? size : sections[15].Descriptor.MirrorOffset;
        if (mirror.ScanEnd < 0 || mirror.ScanEnd > size)
        {
            throw new ArgumentException("Mirror scan end outside admitted backing");
        }

        CopyToMirror(mirror, sections);
        mirror.Scan = mirror.Section = mirror.SectionOffset = 0;
    }

    public static void InvalidateMirror(StateMirror mirror, StateSections sections)
    {
        CopyToMirror(mirror, sections);
        for (int i = 0; i < mirror.Bytes.Length; ++i)
        {
            mirror.Bytes[i] = unchecked((byte)~mirror.Bytes[i]);
        }

        mirror.Scan = mirror.Section = mirror.SectionOffset = 0;
    }

    public static byte[] BuildRawImage(StateSections sections, ImageKind kind, int unitScan, int cityScan)
    {
        int[] selected = kind switch
        {
            ImageKind.Join => [1, 7, 3, 2, 9, 11, 23],
            ImageKind.MapHeader => [1, 12],
            ImageKind.Full or ImageKind.Delta => Enumerable.Range(0, Count).ToArray(),
            _ => throw new ArgumentException("Unknown image kind", nameof(kind)),
        };
        var writer = new ByteWriter();
        foreach (var index in selected)
        {
            var section = sections[index];
            Admit(section);
            var prefix = section.Descriptor;
            var oldChecksum = kind is ImageKind.Full or ImageKind.Delta;
            if (oldChecksum && index is 5 or 6)
            {
                var scan = index == 5 ? unitScan : cityScan;
                var extent = index == 5 ? 32 : 88;
                if (scan < 0 || scan > prefix.Size / extent)
                {
                    throw new ArgumentException("Image scan bound exceeds registered section");
                }

                prefix = prefix with { Size = scan * extent };
            }
            var checksum = Checksum(section.Backing, prefix.Size);
            if (kind == ImageKind.Delta && (prefix.Size == 0 || checksum == section.Descriptor.Checksum))
            {
                continue;
            }

            section.Descriptor = section.Descriptor with { Checksum = checksum };
            if (!oldChecksum)
            {
                prefix = prefix with { Checksum = checksum };
            }

            writer.Append(EncodeDescriptor(prefix));
            writer.Append(section.Backing.AsSpan(0, prefix.Size));
        }
        return writer.ToArray();
    }

    private static void Admit(StateSection section)
    {
        var d = section.Descriptor;
        if (d.Size < 0 || d.PaddedSize < d.Size || d.PaddedSize % 4 != 0 || d.PaddedSize > section.Backing.Length)
        {
            throw new ArgumentException("State section lacks declared data/padding backing");
        }
    }

    private static void CopyToMirror(StateMirror mirror, StateSections sections)
    {
        int position = 0;
        for (int i = 0; i < Count - 1; ++i)
        {
            var section = sections[i];
            Admit(section);
            var padded = section.Descriptor.PaddedSize;
            if (padded > mirror.Bytes.Length - position)
            {
                throw new ArgumentException("Section does not fit mirror backing");
            }

            section.Backing.AsSpan(0, section.Descriptor.Size).CopyTo(mirror.Bytes.AsSpan(position));
            position += padded;
        }
    }
}
