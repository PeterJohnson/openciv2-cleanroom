using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Formats;

public static partial class StateConversion
{
    // Explicit mappings keep the implementation's component set independent of classic bit positions.
    private static readonly (byte Bit, MapFeatures Feature)[] TileFeatureBits =
    [
        (0x01, MapFeatures.Unit), (0x02, MapFeatures.CitySite),
        (0x04, MapFeatures.Irrigation), (0x08, MapFeatures.Mine),
        (0x10, MapFeatures.Road), (0x20, MapFeatures.Railroad),
        (0x40, MapFeatures.Fortification), (0x80, MapFeatures.Pollution)
    ];

    public static MapFeatures DecodeMapFeatures(byte bits)
    {
        var features = MapFeatures.None;
        foreach (var (bit, feature) in TileFeatureBits)
        {
            if ((bits & bit) != 0)
            {
                features |= feature;
            }
        }
        return features;
    }

    public static byte EncodeMapFeatures(MapFeatures features)
    {
        byte bits = 0;
        var remaining = features;
        foreach (var (bit, feature) in TileFeatureBits)
        {
            if (features.HasFlag(feature))
            {
                bits |= bit;
            }
            remaining &= ~feature;
        }
        if (remaining != MapFeatures.None)
        {
            throw new ArgumentException("Unrecognized tile feature", nameof(features));
        }
        return bits;
    }

    public static CivilizationSet DecodeCivilizations(byte bits)
    {
        var members = new CivilizationSet();
        for (int i = 0; i < EntityLimits.Civilizations; ++i)
        {
            members = members.With(i, (bits & (1 << i)) != 0);
        }
        return members;
    }

    public static byte EncodeCivilizations(CivilizationSet members)
    {
        byte bits = 0;
        for (int i = 0; i < EntityLimits.Civilizations; ++i)
        {
            if (members.Contains(i))
            {
                bits |= (byte)(1 << i);
            }
        }
        return bits;
    }

    /// <summary>W01 §§5–6: opaque fields never become live gameplay state.</summary>
    public static Tile ToLive(StoredTile value) => new()
    {
        Terrain = value.Terrain,
        River = (value.TerrainFlags & 0x80) != 0,
        ResourceSuppressed = (value.TerrainFlags & 0x40) != 0,
        Features = DecodeMapFeatures(value.Features),
        Claimant = value.Claimant,
        Region = value.Region,
        RevealedTo = DecodeCivilizations(value.Revelation),
        Fertility = value.Fertility,
        Owner = value.Owner
    };

    public static StoredTile ToStored(Tile value) => new()
    {
        Terrain = value.Terrain,
        TerrainFlags = (byte)((value.River ? 0x80 : 0) | (value.ResourceSuppressed ? 0x40 : 0)),
        Features = EncodeMapFeatures(value.Features),
        Claimant = value.Claimant,
        Region = value.Region,
        Revelation = EncodeCivilizations(value.RevealedTo),
        Fertility = value.Fertility,
        Owner = value.Owner
    };

    public static StoredTile PreserveOpaque(StoredTile output, StoredTile imported) => output with
    {
        TerrainFlags = (byte)((output.TerrainFlags & 0xc0) | (imported.TerrainFlags & 0x30)),
        RadiusOther = imported.RadiusOther
    };
}
