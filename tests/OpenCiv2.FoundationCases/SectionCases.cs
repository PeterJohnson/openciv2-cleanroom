using System.Buffers.Binary;
using OpenCiv2.Base;
using OpenCiv2.Core;
using OpenCiv2.IO;
using OpenCiv2.Network;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // Bounded materialized wire fixture. Session producers must supply real classic section encodings.
    private static StateSections SyntheticSections()
    {
        var sections = new StateSections();
        for (int i = 0; i < Sections.Count; ++i)
        {
            var size = i == 5 ? 96 : i == 6 ? 176 : 3;
            sections[i].Descriptor = new(size, MirrorOffset: -71);
            sections[i].Backing = Enumerable.Repeat((byte)(i + 1), (size + 3) & ~3).ToArray();
        }
        Sections.Register(sections);
        return sections;
    }
    private static void SectionChecksums()
    {
        byte[] bytes = [1, 2, 3, 4, 5, 6];
        Equal(513U, Sections.Checksum(bytes, 6)); Equal(0U, Sections.Checksum([], 2)); Equal(0U, Sections.Checksum([], -4)); Equal(0U, Sections.Checksum([], int.MinValue)); Equal(0U, Sections.Checksum([], 0));
        Equal(15U, Sections.Checksum(bytes, 5)); Equal(0x04030201U, Sections.Checksum(bytes, 4)); Equal(1U, Sections.Checksum([255, 255, 255, 255, 2, 0, 0, 0], 8));
        Throws<DecodeException>(() => Sections.Checksum([1], 6));
        Equal(513U, Sections.Checksum([1, 2], 6)); Equal(1540U, Sections.Checksum([1, 2, 3, 4], 10));
        var collision = bytes.ToArray(); collision[5] = 99; Equal(Sections.Checksum(bytes, 6), Sections.Checksum(collision, 6));
    }
    private static void SectionDescriptorAndImageBytes()
    {
        byte[] expected = [3, 0, 0, 0, 255, 255, 255, 255, 2, 0, 0, 0, 0x78, 0x56, 0x34, 0x12, 254, 255, 255, 255, 1, 2, 3];
        var descriptor = new SectionDescriptor(3, -1, 2, 0x12345678, -2);
        BytesEqual(expected[..20], Sections.EncodeDescriptor(descriptor)); Equal(descriptor, Sections.DecodeDescriptor(expected.AsSpan(0, 20)));
        var blocks = Sections.DecodeRawImage(expected); Equal(1, blocks.Count); Equal(descriptor, blocks[0].Descriptor); Equal(new OwnedList<byte>([1, 2, 3]), blocks[0].Data);
        BytesEqual(expected, Sections.EncodeRawImage(blocks)); expected[20] = 9; Equal((byte)1, blocks[0].Data[0]);
        var multiple = new ImageBlock[] { blocks[0], new(new(0, -9, 23, uint.MaxValue, int.MinValue), new([])), blocks[0] };
        Equal(new OwnedList<ImageBlock>(multiple), Sections.DecodeRawImage(Sections.EncodeRawImage(multiple))); Equal(0, Sections.DecodeRawImage([]).Count);
        for (int n = 0; n < 23; ++n)
        {
            var cut = expected[..n]; if (n != 0) Throws<DecodeException>(() => Sections.DecodeRawImage(cut));
            if (n != 20) Throws<DecodeException>(() => Sections.DecodeDescriptor(cut));
        }
        foreach (var bad in new[] { descriptor with { Size = -1 }, descriptor with { Id = -1 }, descriptor with { Id = 24 }, descriptor with { Size = 4 } })
        {
            var image = Sections.EncodeDescriptor(bad).Concat(new byte[] { 1, 2, 3 }).ToArray(); Throws<DecodeException>(() => Sections.DecodeRawImage(image));
            Throws<ArgumentException>(() => Sections.EncodeRawImage([new(bad, new([1, 2, 3]))]));
        }
    }
    private static void SectionRegistrationAndSizes()
    {
        var sections = SyntheticSections(); int offset = 0;
        for (int i = 0; i < Sections.Count; ++i)
        {
            var d = sections[i].Descriptor; Equal(i, d.Id); Equal(0U, d.Checksum); Equal((d.Size + 3) & ~3, d.PaddedSize);
            Equal(i == 23 ? -71 : offset, d.MirrorOffset); offset += d.PaddedSize;
        }
        Equal(new OwnedList<int>([4, 332, 1936, 11424, 1024, 65536, 22528, 1008, 1200, 100, 340, 1172, 16, 260, 260, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 12000, 50000]), Sections.ClassicSizes(new(80, 50)));
        var sizes = Sections.ClassicSizes(new(2, 1)); Equal(1, sizes[13]); Equal(1, sizes[15]); Equal(6, sizes[22]);
        sizes = Sections.ClassicSizes(new(2, 32767)); Equal(32767, sizes[15]); Equal(196602, sizes[22]);
    }
    private static void SectionRegistrationFailurePrefixes()
    {
        foreach (int badSize in new[] { -1, int.MaxValue, int.MaxValue - 2 })
        {
            var sections = SyntheticSections();
            for (int i = 0; i < Sections.Count; ++i) sections[i].Descriptor = sections[i].Descriptor with { Id = 99, Checksum = 99, MirrorOffset = -7 };
            sections[3].Descriptor = sections[3].Descriptor with { Size = badSize };
            Throws<ArgumentException>(() => Sections.Register(sections)); Equal(2, sections[2].Descriptor.Id); Equal(0U, sections[2].Descriptor.Checksum);
            Equal(99, sections[3].Descriptor.Id); Equal(99U, sections[3].Descriptor.Checksum); Equal(-7, sections[0].Descriptor.MirrorOffset);
        }
        var missing = SyntheticSections(); missing[2].Backing = [1]; missing[2].Descriptor = missing[2].Descriptor with { Id = 99, Checksum = 99 };
        missing[0].Descriptor = missing[0].Descriptor with { MirrorOffset = -9 };
        Throws<ArgumentException>(() => Sections.Register(missing)); Equal(2, missing[2].Descriptor.Id); Equal(0U, missing[2].Descriptor.Checksum); Equal(-9, missing[0].Descriptor.MirrorOffset);
    }
    private static void SectionMirrorSemantics()
    {
        var sections = SyntheticSections(); var host = new StateMirror(); var client = new StateMirror();
        Sections.AllocateMirror(host, sections, true); Sections.AllocateMirror(client, sections, false); BytesEqual(host.Bytes, client.Bytes);
        Equal(host.Bytes.Length, host.ScanEnd); Equal(sections[15].Descriptor.MirrorOffset, client.ScanEnd); Equal(true, client.ScanEnd < client.Bytes.Length);
        Equal((byte)0, host.Bytes[3]); Equal((byte)1, sections[0].Backing[3]);
        sections[0].Backing[0] = 0x20; host.Scan = host.Section = host.SectionOffset = 99; Sections.InvalidateMirror(host, sections);
        Equal((byte)0xdf, host.Bytes[0]); Equal((byte)255, host.Bytes[3]); Equal(0, host.Scan); Equal(0, host.Section); Equal(0, host.SectionOffset);
        Sections.InvalidateMirror(host, sections); Equal((byte)0xdf, host.Bytes[0]); Equal((byte)0, host.Bytes[3]);
        sections[0].Descriptor = sections[0].Descriptor with { MirrorOffset = 9999 };
        Sections.AllocateMirror(host, sections, true); Equal((byte)0x20, host.Bytes[0]); // Copy uses cumulative padded extents, not descriptor offsets.
    }
    private static void SectionMirrorFailurePrefixes()
    {
        var sections = SyntheticSections(); var mirror = new StateMirror { Bytes = [1, 2, 3], Scan = 9, ScanEnd = 7, Section = 4, SectionOffset = 2 };
        sections[0].Descriptor = sections[0].Descriptor with { PaddedSize = 2 };
        Throws<ArgumentException>(() => Sections.AllocateMirror(mirror, sections, true)); Equal(0, mirror.Bytes.Length); Equal(9, mirror.Scan); Equal(7, mirror.ScanEnd);
        sections = SyntheticSections(); sections[15].Descriptor = sections[15].Descriptor with { MirrorOffset = -1 };
        Throws<ArgumentException>(() => Sections.AllocateMirror(mirror, sections, false)); Equal(true, mirror.Bytes.Length > 0); Equal(true, mirror.Bytes.All(b => b == 0)); Equal(-1, mirror.ScanEnd); Equal(9, mirror.Scan);
        sections = SyntheticSections(); mirror.Bytes = new byte[7]; mirror.Scan = 19;
        Throws<ArgumentException>(() => Sections.InvalidateMirror(mirror, sections)); BytesEqual([1, 1, 1, 0, 0, 0, 0], mirror.Bytes); Equal(19, mirror.Scan);
    }
    private static void SectionImageChecksumOrdering()
    {
        var sections = SyntheticSections(); for (int i = 0; i < Sections.Count; ++i) sections[i].Descriptor = sections[i].Descriptor with { Checksum = 0x12345678 };
        var full = Sections.DecodeRawImage(Sections.BuildRawImage(sections, ImageKind.Full, 2, 1)); Equal(24, full.Count);
        for (int i = 0; i < full.Count; ++i)
        { Equal(i, full[i].Descriptor.Id); Equal(0x12345678U, full[i].Descriptor.Checksum); Equal(Sections.Checksum(full[i].Data.ToArray(), full[i].Descriptor.Size), sections[i].Descriptor.Checksum); }
        Equal(64, full[5].Data.Count); Equal(96, full[5].Descriptor.PaddedSize); Equal(88, full[6].Data.Count); Equal(176, sections[6].Descriptor.Size);
        Equal(0, Sections.BuildRawImage(sections, ImageKind.Delta, 2, 1).Length);
        var old = sections[1].Descriptor.Checksum; sections[1].Backing[0] = 99;
        var delta = Sections.DecodeRawImage(Sections.BuildRawImage(sections, ImageKind.Delta, 2, 1)); Equal(1, delta.Count); Equal(1, delta[0].Descriptor.Id); Equal(old, delta[0].Descriptor.Checksum);
        var join = Sections.DecodeRawImage(Sections.BuildRawImage(sections, ImageKind.Join, -1, -1)); Equal(new OwnedList<int>([1, 7, 3, 2, 9, 11, 23]), new(join.Select(b => b.Descriptor.Id)));
        foreach (var block in join) Equal(Sections.Checksum(block.Data.ToArray(), block.Descriptor.Size), block.Descriptor.Checksum);
        var map = Sections.DecodeRawImage(Sections.BuildRawImage(sections, ImageKind.MapHeader, 999, 999)); Equal(new OwnedList<int>([1, 12]), new(map.Select(b => b.Descriptor.Id)));
        sections[5].Descriptor = sections[5].Descriptor with { Checksum = 7 }; delta = Sections.DecodeRawImage(Sections.BuildRawImage(sections, ImageKind.Delta, 0, 1));
        Equal(7U, sections[5].Descriptor.Checksum); Equal(false, delta.Any(b => b.Descriptor.Id == 5));
        sections[1].Backing[0]--; sections[1].Backing[1]++; Equal(0, Sections.BuildRawImage(sections, ImageKind.Delta, 0, 1).Length); // Matching checksum is the complete selection test.
    }
    private static void SectionImageProductionFailures()
    {
        foreach (var kind in new[] { ImageKind.Full, ImageKind.Delta }) foreach (int scan in new[] { -1, 4 })
        {
            var sections = SyntheticSections();
            Throws<ArgumentException>(() => Sections.BuildRawImage(sections, kind, scan, 1));
            Equal(3U, sections[0].Descriptor.Checksum); Equal(15U, sections[4].Descriptor.Checksum); Equal(0U, sections[5].Descriptor.Checksum);
        }
        var invalid = SyntheticSections(); invalid[7].Backing = [];
        Throws<ArgumentException>(() => Sections.BuildRawImage(invalid, ImageKind.Join, 0, 0)); Equal(6U, invalid[1].Descriptor.Checksum); Equal(0U, invalid[7].Descriptor.Checksum); Equal(0U, invalid[3].Descriptor.Checksum);
        Throws<ArgumentException>(() => Sections.BuildRawImage(invalid, (ImageKind)99, 0, 0));
        var full = SyntheticSections(); Throws<ArgumentException>(() => Sections.BuildRawImage(full, ImageKind.Full, 1, 3)); Equal(0U, full[6].Descriptor.Checksum); Equal(true, full[5].Descriptor.Checksum != 0);
    }
    private static void SectionImageSequentialApplication()
    {
        var blocks = new ImageBlock[] { new(new(64, -1, 2, 0xdeadbeef, -77), new(Enumerable.Repeat((byte)65, 64))), new(new(32, 999, 3, 0x12345678, -88), new(Enumerable.Repeat((byte)66, 32))) };
        var raw = Sections.EncodeRawImage(blocks);
        foreach (bool full in new[] { false, true })
        {
            var image = raw.ToArray(); Equal(true, (full ? Compression.CompressFullImage(ref image) : Compression.CompressByteImage(ref image)).Complete); Equal(true, image.Length < raw.Length);
            Equal(true, Compression.ExpandSharedImage(ref image, full ? 1U : 0U, true).Complete); BytesEqual(raw, image);
            var sections = new StateSections(); sections[2].Descriptor = new(64, 64, 2, 7); sections[2].Backing = new byte[64]; sections[3].Descriptor = new(32, 32, 3, 8, 64); sections[3].Backing = new byte[32];
            Equal(new ImageApplyResult(ImageApplyStop.Complete, 2, raw.Length), Sections.ApplyRawImage(sections, image));
            BytesEqual(blocks[0].Data.ToArray(), sections[2].Backing); BytesEqual(blocks[1].Data.ToArray(), sections[3].Backing);
            Equal(new SectionDescriptor(64, 64, 2, 0x14141410), sections[2].Descriptor); Equal(new SectionDescriptor(32, 32, 3, 0x12121210, 64), sections[3].Descriptor);
        }
        foreach (var reason in new[] { ImageApplyStop.Truncated, ImageApplyStop.InvalidSection, ImageApplyStop.InvalidSize, ImageApplyStop.DestinationExtent })
        {
            var sections = new StateSections(); sections[2].Descriptor = new(64, 64, 2, 7); sections[2].Backing = new byte[64]; sections[3].Descriptor = new(32, 32, 3, 8, 64); sections[3].Backing = new byte[32]; var bad = raw.ToArray();
            if (reason == ImageApplyStop.Truncated) bad = bad[..^1];
            if (reason == ImageApplyStop.InvalidSection) bad[84 + 8] = 24;
            if (reason == ImageApplyStop.InvalidSize) bad[84 + 3] = 128;
            if (reason == ImageApplyStop.DestinationExtent) sections[3].Backing = new byte[31];
            Equal(new ImageApplyResult(reason, 1, 104), Sections.ApplyRawImage(sections, bad)); Equal(0x14141410U, sections[2].Descriptor.Checksum); Equal(8U, sections[3].Descriptor.Checksum); Equal(true, sections[3].Backing.All(b => b == 0));
        }
    }
    private static void SectionImageApplicationExtents()
    {
        var raw = Sections.EncodeRawImage([new(new(3, -1, 2, 999, -1), new([1, 2, 3])), new(new(1, -1, 2, 999, -1), new([9]))]);
        for (int size = 0; size <= raw.Length; ++size)
        {
            var sections = new StateSections(); sections[2].Backing = new byte[3]; sections[2].Descriptor = new(1, 0, 99, 44, -9);
            var result = Sections.ApplyRawImage(sections, raw.AsSpan(0, size));
            var applied = size < 23 ? 0 : size < 44 ? 1 : 2;
            Equal(applied, result.Applied); Equal(size == 0 || size == 23 || size == 44 ? (int)ImageApplyStop.Complete : (int)ImageApplyStop.Truncated, (int)result.Stop);
            Equal(size < 20 ? 0 : size < 23 ? 20 : size < 43 ? 23 : size < 44 ? 43 : 44, result.Consumed);
            Equal(new SectionDescriptor(1, 0, 99, applied == 0 ? 44U : applied == 1 ? 6U : 9U, -9), sections[2].Descriptor);
            if (applied == 2) BytesEqual([9, 2, 3], sections[2].Backing);
        }
        var empty = new StateSections(); empty[23].Descriptor = new(0, 0, 23, 99);
        Equal(new ImageApplyResult(ImageApplyStop.Complete, 1, 20), Sections.ApplyRawImage(empty, Sections.EncodeDescriptor(new(0, -1, 23, 99, -2)))); Equal(0U, empty[23].Descriptor.Checksum);
    }
}
