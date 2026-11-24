using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.City;

public sealed record CommodityCity(CityTarget City, string Name, string? Adjective, int Size, bool Demand);

/// <summary>C07 exact signed membership and observer-specific demand/supply lists. No refresh or mutation.</summary>
public static class CommodityReport
{
    public static OwnedList<CommodityCity> Cities(GameState world, int viewer, int commodity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(viewer);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(viewer, EntityLimits.Civilizations);
        List<CommodityCity> rows = [];
        foreach (bool demand in new[] { true, false })
        {
            for (int i = 0; i < world.Entities.Cities.End; ++i)
            {
                var slot = new CitySlot((ushort)i);
                ref var city = ref world.Entities.Cities.At(slot);
                if (!city.Token.Occupied)
                {
                    continue;
                }

                bool owned = city.Owner.Value == viewer;
                bool palace = BuildingQueries.HasBuilding(world, slot, BuildingIds.Palace);
                bool reveal = world.Globals.RevealMap != 0;
                if (!owned && !(demand ? city.RememberedSize[viewer] != 0 || palace || reveal : reveal))
                {
                    continue;
                }

                bool included = false;
                for (int j = 0; j < CityLimits.TradeRoutes; ++j)
                {
                    if ((demand ? city.Demands[j] : city.Supplies[j]) == commodity)
                    { included = true; break; }
                }
                if (!included)
                {
                    continue;
                }

                int size = !owned && !reveal && !palace && city.RememberedSize[viewer] != 0
                    ? unchecked((sbyte)city.RememberedSize[viewer]) : city.Size;
                rows.Add(new(new(slot, city.Token), city.Name, owned ? null : world.Names[city.Owner.Value].Adjective, size, demand));
            }
        }
        return new(rows);
    }
}
