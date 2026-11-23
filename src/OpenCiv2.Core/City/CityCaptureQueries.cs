using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.City;

/// <summary>C09 shared treasury and central-city queries, also consumed by W06 and V01.</summary>
public static class CityCaptureQueries
{
    public static int TreasuryShare(GameState world, CitySlot slot)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        int owner = unchecked((sbyte)city.Owner.Value);
        int threshold = 32000 / city.Size;
        ref var civ = ref world.Civilizations[owner];
        int share = civ.Gold < threshold
            ? unchecked(city.Size * civ.Gold) / (civ.TotalCitySize + 1)
            : unchecked(civ.Gold / (civ.TotalCitySize + 1) * city.Size);
        return share < 0 ? 32000 : share;
    }

    public static CitySlot? CentralCity(GameState world, int owner)
    {
        CitySlot? best = null;
        int bestTotal = 32767;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var city = ref world.Entities.Cities.Storage[i];
            if (!city.Token.Occupied || unchecked((sbyte)city.Owner.Value) != owner)
            {
                continue;
            }

            int total = 0;
            for (int j = 0; j < world.Entities.Cities.End; ++j)
            {
                ref var other = ref world.Entities.Cities.Storage[j];
                if (other.Token.Occupied && unchecked((sbyte)other.Owner.Value) == owner)
                {
                    total = unchecked(total + world.Map.Geometry.WeightedDistance(city.Position, other.Position, world.Globals.Rules.FlatWorld));
                }
            }
            if (total < bestTotal)
            { bestTotal = total; best = new((ushort)i); }
        }
        return best;
    }
}
