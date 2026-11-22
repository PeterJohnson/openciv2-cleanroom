using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.City;

/// <summary>C07 directional route primitives. Delivery owns payment and reciprocal insertion.</summary>
public static class TradeRoutes
{
    private static int Partner(CitySlot? slot) => slot is { } value ? unchecked((short)value.Value) : -1;

    public static void Remove(GameState world, CitySlot slot, int index)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        while (index < unchecked((sbyte)city.RouteCount) - 1)
        {
            city.RoutePartners[index] = city.RoutePartners[index + 1];
            city.RouteGoods[index] = city.RouteGoods[index + 1];
            ++index;
        }
        city.RouteCount = unchecked((byte)(city.RouteCount - 1));
        city.Flags.TradeDirty = true;
    }

    public static void Write(GameState world, CitySlot slot, int index, int destination, int cargo)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        short partner = unchecked((short)destination);
        city.RoutePartners[index] = partner == -1 ? null : new(unchecked((ushort)partner));
        city.RouteGoods[index] = unchecked((sbyte)Arithmetic.ClampAB(cargo, -1, 15));
        city.Flags.TradeDirty = true;
    }

    public static void Add(GameState world, RuleTables rules, CitySlot slot, int destination, int cargo)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        for (int i = 0; i < unchecked((sbyte)city.RouteCount); ++i)
        {
            if (Partner(city.RoutePartners[i]) == destination && city.RouteGoods[i] == cargo)
            {
                return;
            }
        }

        int selected;
        if (unchecked((sbyte)city.RouteCount) < CityLimits.TradeRoutes)
        {
            selected = unchecked((sbyte)city.RouteCount);
            city.RouteCount = unchecked((byte)(city.RouteCount + 1));
        }
        else
        {
            int minimum = 9999;
            selected = -1;
            for (int i = 0; i < unchecked((sbyte)city.RouteCount); ++i)
            {
                if (city.RouteGoods[i] < 0 && cargo < 0)
                {
                    return;
                }
                int partner = Partner(city.RoutePartners[i]);
                ref var other = ref world.Entities.Cities.Storage[partner];
                int value = other.BaseTrade;
                int connection = RoadConnections.Query(world, rules, unchecked((sbyte)city.Owner.Value), city.Position, other.Position);
                if (BuildingQueries.HasBuilding(world, slot, BuildingIds.Airport)
                    && BuildingQueries.HasBuilding(world, new((ushort)partner), BuildingIds.Airport) && connection < 2)
                {
                    connection = 1;
                }
                if (connection != 0)
                {
                    value = unchecked(value + ((connection * value) >> 1));
                }
                if (unchecked((sbyte)city.Owner.Value) == unchecked((sbyte)other.Owner.Value))
                {
                    value >>= 1;
                }
                if (value < minimum)
                {
                    minimum = value;
                    selected = i;
                }
            }
            if (minimum > world.Entities.Cities.Storage[destination].BaseTrade || selected < 0)
            {
                // X01/C07 repair: no invented replacement index when no route beat the sentinel.
                return;
            }
        }
        Write(world, slot, selected, destination, cargo);
        Commodities.Refresh(world, rules, slot);
        // Preserve the full destination argument's bounds before converting it to a logical slot.
        _ = world.Entities.Cities.Storage[destination];
        Commodities.Refresh(world, rules, new((ushort)destination));
    }
}
