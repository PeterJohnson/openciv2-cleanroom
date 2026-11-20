using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.IO;

namespace OpenCiv2.Formats;

public static partial class ClassicRecords
{
    private static void RequireSize(ReadOnlySpan<byte> bytes, int size)
    {
        if (bytes.Length != size)
        {
            throw new DecodeException(Math.Min(size, bytes.Length), "Incorrect fixed-record length");
        }
    }

    internal static void RequireCount<T>(IReadOnlyList<T> values, int count)
    {
        if (values.Count != count)
        {
            throw new ArgumentException("Incorrect fixed-record array length");
        }
    }

    private static CitySlot? ReadHome(ref ByteReader r)
    {
        byte home = r.U8();
        return home == 255 ? null : new CitySlot(home);
    }

    private static void WriteHome(ByteWriter w, CitySlot? home)
    {
        if (home is { Value: >= 255 })
        {
            throw new ArgumentException("Unit home city outside classic encoding");
        }

        w.U8(home is { } slot ? (byte)slot.Value : (byte)255);
    }

    public static Tile DecodeTile(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 6);
        return new Tile
        {
            Terrain = (Terrain)(bytes[0] & 15),
            TerrainFlags = (byte)(bytes[0] & 0xf0),
            Features = bytes[1],
            Claimant = (byte)(bytes[2] >> 5),
            RadiusOther = (byte)(bytes[2] & 31),
            Region = bytes[3],
            Revelation = bytes[4],
            Fertility = (byte)(bytes[5] & 15),
            Owner = (bytes[5] >> 4) == 15 ? null : (byte)(bytes[5] >> 4)
        };
    }

    public static byte[] Encode(Tile v)
    {
        if ((byte)v.Terrain > 15 || (v.TerrainFlags & 15) != 0 || v.Claimant > 7 ||
            v.RadiusOther > 31 || v.Fertility > 15 || v.Owner is > 14)
        {
            throw new ArgumentException("Tile field outside classic encoding");
        }

        return [(byte)((byte)v.Terrain | v.TerrainFlags), v.Features, (byte)((v.Claimant << 5) | v.RadiusOther),
            v.Region, v.Revelation, (byte)(((v.Owner ?? 15) << 4) | v.Fertility)];
    }

    public static RegionSummary DecodeRegion(ReadOnlySpan<byte> bytes)
    {
        RequireSize(bytes, 16);
        var r = new ByteReader(bytes);
        short land = r.I16(), fertile = r.I16(), water = r.I16();
        byte first = r.U8();
        ulong neighbors = r.U64();
        return new(land, fertile, water, neighbors, new([first, r.U8()]));
    }

    public static byte[] Encode(RegionSummary v)
    {
        RequireCount(v.Retained, 2);
        var w = new ByteWriter();
        w.I16(v.Land);
        w.I16(v.Fertile);
        w.I16(v.Water);
        w.U8(v.Retained[0]);
        w.U64(v.NeighboringWater);
        w.U8(v.Retained[1]);
        return w.ToArray();
    }
}
