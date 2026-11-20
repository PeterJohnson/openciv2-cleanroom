using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
namespace OpenCiv2.Formats;

public sealed record StoredRegionSummary(short Land, short Fertile, short Water, ulong NeighboringWater, OwnedList<byte> Retained);
public static partial class StateConversion
{
    public static RegionSet DecodeRegions(ulong bits)
    {
        var result = new RegionSet();
        for (int i = 0; i < MapLimits.Regions; ++i)
        {
            result = result.With(i, (bits & (1UL << i)) != 0);
        }

        return result;
    }
    public static ulong EncodeRegions(RegionSet value)
    {
        ulong bits = 0;
        for (int i = 0; i < MapLimits.Regions; ++i)
        {
            if (value.Contains(i))
            {
                bits |= 1UL << i;
            }
        }

        return bits;
    }
    public static RegionSummary ToLive(StoredRegionSummary value) => new(value.Land, value.Fertile, value.Water, DecodeRegions(value.NeighboringWater));
    public static StoredRegionSummary ToStored(RegionSummary value) => new(value.Land, value.Fertile, value.Water, EncodeRegions(value.NeighboringWater), new([0, 0]));
}
