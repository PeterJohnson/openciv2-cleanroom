using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using static OpenCiv2.Base.Arithmetic;

namespace OpenCiv2.Core.City;

/// <summary>C07 commodity survey, scores, ranking, exceptional entries and route/cargo suppression.
/// The supported repair starts the road accumulator at zero on every refresh.</summary>
public static class Commodities
{
    public static bool Refresh(GameState world, RuleTables rules, CitySlot slot)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        if (!city.Flags.TradeDirty && world.Globals.RevealMap == 0 && (world.Globals.Turn + slot.Value) % 16 != 0)
        {
            return false;
        }
        city.Flags.TradeDirty = false;
        int advances = world.Civilizations[city.Owner.Value].NumAdvances;
        var scores = Score(world, rules, slot);
        Rank(ref city, scores.Supply, scores.Demand);
        Exceptional(world, slot, advances);
        Suppress(world, rules, slot);
        return true;
    }

    internal sealed record Scores(int[] Supply, int[] Demand);

    /// <summary>Raw scores before competition/ranking. Internal for specification-derived arithmetic cases.</summary>
    internal static Scores Score(GameState world, RuleTables rules, CitySlot slot)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        var map = world.Map;
        var center = city.Position;
        int owner = city.Owner.Value, region = map.TileAt(center).Region;
        int p = (city.Size + 2) / 5, a = world.Civilizations[owner].NumAdvances;
        int dx = center.X - (map.Geometry.Width >> 1), dy = center.Y - (map.Geometry.Height >> 1);
        int east = Math.Max(dx, 0), south = Math.Max(dy, 0), x = Math.Abs(dx), y = Math.Abs(dy);
        var terrain = new int[11];
        int river = 0, road = 0;
        foreach (var offset in MapNeighborhoods.City)
        {
            var point = MapNeighborhoods.Offset(map.Geometry, center, offset, world.Globals.Rules.FlatWorld);
            if (!map.Geometry.InBounds(point))
            {
                continue;
            }
            ref var tile = ref map.TileAt(point);
            ++terrain[(int)tile.Terrain];
            if (map.SpecialResource(point) != 0)
            {
                terrain[(int)tile.Terrain] += 3;
            }
            if (tile.River)
            {
                ++river;
            }

            if (tile.Features.HasFlag(MapFeatures.Road))
            {
                ++road;
            }
        }
        terrain[(int)Terrain.Tundra] += terrain[(int)Terrain.Glacier];
        terrain[(int)Terrain.Glacier] = 0;
        int T(Terrain type) => terrain[(int)type];
        bool Knows(int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance);
        bool Has(int building) => BuildingQueries.HasBuilding(world, slot, building);
        int Tribe() => world.Civilizations[owner].Tribe;
        int Size() => world.Entities.Cities.At(slot).Size;
        int RegionSize() => map.Regions[region].Land;
        int tundra = T(Terrain.Tundra), desert = T(Terrain.Desert), plains = T(Terrain.Plains);
        int grass = T(Terrain.Grassland), forest = T(Terrain.Forest), hills = T(Terrain.Hills);
        int mountains = T(Terrain.Mountains), swamp = T(Terrain.Swamp), jungle = T(Terrain.Jungle), ocean = T(Terrain.Ocean);
        var supply = new int[16];
        var demand = new int[16];
        // All scores retain F01 signed32 operations, including negative custom-state intermediates.
        unchecked
        {
            int v = 3 * jungle + 6 * tundra + 4 * forest + 3 * river;
            if (a < 16)
            {
                v *= 2;
            }

            if (a < 24)
            {
                v *= 2;
            }

            if (a < 49)
            {
                if (Size() < 3)
                {
                    v *= 2;
                }
            }
            else
            {
                v /= 2;
            }

            if (Size() > 7)
            {
                v /= 2;
            }

            supply[(int)Commodity.Hides] = v;

            supply[(int)Commodity.Wool] = (river / 2 + 2 * hills + grass) * (tundra + (map.Geometry.Height / 3 < y ? 3 : 2));
            v = 8 * ocean - y;
            if (Size() > 9)
            {
                v >>= 1;
            }

            if (a > 32)
            {
                v >>= 1;
            }

            supply[(int)Commodity.Beads] = v;

            v = (3 * plains + desert - river) * ClampAB(a / 10, 1, 2);
            if (Knows(AdvanceIds.Industrialization))
            {
                v = (3 * v) >> 1;
            }

            if (a < 8)
            {
                v >>= 1;
            }

            if (a < 16)
            {
                v >>= 1;
            }

            supply[(int)Commodity.Cloth] = v;

            v = 3 * ocean + 4 * desert + 2 * swamp - a / 6;
            if (!Knows(AdvanceIds.Pottery))
            {
                v /= 3;
            }

            if (Has(BuildingIds.Aqueduct))
            {
                v += v >> 1;
            }

            if ((region & 1) != 0 && region < 6)
            {
                v += v >> 1;
            }

            supply[(int)Commodity.Salt] = v;

            v = (jungle + plains + forest + swamp + 1) * hills * 5;
            v = ShiftSigned(v, Math.Min(p / 2, 2) - 1);
            if ((region & 1) != 0 && region > 1)
            {
                v += v >> 1;
            }

            if (a < 20)
            {
                v >>= 1;
            }

            supply[(int)Commodity.Coal] = v;

            v = 5 * mountains + 5 * hills;
            if (region != 0 && (region & 1) == 0)
            {
                v *= 2;
            }

            supply[(int)Commodity.Copper] = v;
            supply[(int)Commodity.Dye] = (5 * grass - plains + river) * (region != 0 && region % 4 == 0 ? 4 : 2);

            int b = ClampAB(5 * river - grass, 0, 4 * plains);
            int c = ClampAB(4 * plains, 0, b);
            v = c + ((map.Geometry.Width >> 1) - x) / 2 + b;
            if (south != 0)
            {
                v >>= 1;
            }

            v = ShiftSigned(v, ClampAB(p - 1, -2, 1));
            if ((region - 2) % 4 == 0)
            {
                v += v >> 1;
            }

            if (Tribe() == 9)
            {
                v *= 2;
            }

            if (Size() > 10)
            {
                v >>= 1;
            }

            supply[(int)Commodity.Wine] = v;

            v = (2 * forest + jungle + 1) * (hills + 1);
            if (v != 0)
            {
                v += 2 * east;
                if (region != 0 && region % 5 == 0)
                {
                    v *= 2;
                }
            }
            if (Tribe() == 11)
            {
                v *= 2;
            }

            supply[(int)Commodity.Silk] = v;
            v = 8 * mountains;
            if (v != 0)
            {
                v += hills + x;
                if (!Knows(AdvanceIds.IronWorking))
                {
                    v >>= 1;
                }

                if (region > 8)
                {
                    v += v >> 1;
                }

                if (Size() < 5)
                {
                    v >>= 1;
                }
            }
            supply[(int)Commodity.Silver] = v;
            if (Tribe() == 10)
            {
                supply[(int)Commodity.Silk] *= 2;
            }

            v = (3 * jungle + 2 * swamp + 2 * desert) * ((ocean + river) / 2);
            if (v != 0)
            {
                if (y < 10)
                {
                    v *= 2;
                }

                v -= y;
                if (RegionSize() < 26)
                {
                    v += v >> 1;
                }

                if (RegionSize() > 300)
                {
                    v >>= 1;
                }

                if (region == 1)
                {
                    v >>= 1;
                }
            }
            supply[(int)Commodity.Spice] = v;
            v = (mountains + 1) * (swamp + 1) * (desert + 1) + plains;
            if (v != 0)
            {
                v = ClampAB(p, 1, 4) * v / 2;
                if (region == 7)
                {
                    v += v >> 1;
                }
            }
            supply[(int)Commodity.Gems] = v;
            v = (mountains + hills / 2 + 1) * (river + 2);
            if (mountains > 2)
            {
                v *= 2;
            }

            if (Size() > 4)
            {
                v *= 2;
            }

            if (Size() > 9)
            {
                v *= 2;
            }

            supply[(int)Commodity.Gold] = v;

            v = 8 * tundra + 10 * desert + 6 * swamp + 12 * T(Terrain.Glacier);
            if (world.Globals.AdvanceKnownBy[AdvanceIds.Combustion].Count == 0)
            {
                v >>= 3;
            }

            if (v == 0)
            {
                v = -1;
            }
            else
            {
                if (region == 17)
                {
                    v *= 3;
                }
                else if (region > 1 && (region - 1) % 8 == 0)
                {
                    v += v >> 1;
                }

                v *= ClampAB(p / 2 - 2, 1, 2);
            }
            supply[(int)Commodity.Oil] = v;
            v = (tundra + desert + 1) * (hills + river + 1) * (mountains + 1);
            if (!Knows(AdvanceIds.NuclearFission))
            {
                v = 0;
            }

            if (v == 0)
            {
                v = -1;
            }
            else
            {
                if (region != 0 && region % 10 == 0)
                {
                    v += v >> 1;
                }

                v = v * Math.Min(p, 6) / 6;
            }
            supply[(int)Commodity.Uranium] = v;

            v = 5 * (tundra + T(Terrain.Glacier)) + 3 * y / 2 + 2 * mountains + forest;
            demand[(int)Commodity.Cloth] = 4 * hills + 4 * forest + ((a / 10) * v) / 8;
            if (Size() < 3)
            {
                v *= 2;
            }

            if (Knows(AdvanceIds.Industrialization))
            {
                v /= 3;
            }

            if (Knows(AdvanceIds.MassProduction))
            {
                v = 1;
            }

            if (a < 10)
            {
                v *= 2;
            }

            if (a < 20)
            {
                v *= 2;
            }

            if (a > 47)
            {
                v /= 2;
            }

            demand[(int)Commodity.Hides] = v;
            v = 2 * Math.Abs((map.Geometry.Height >> 2) - y) + 2 * plains + forest;
            if (Knows(AdvanceIds.Industrialization))
            {
                v *= 2;
            }

            demand[(int)Commodity.Wool] = v;
            v = y + ((21 - ocean) * 3) / 2;
            if (Size() < 4)
            {
                v += v >> 1;
            }

            if (Size() > 11)
            {
                v /= 2;
            }

            if (a > 47)
            {
                v /= 2;
            }

            demand[(int)Commodity.Beads] = v;

            v = 0;
            int remaining = Size(), weight = 8;
            while (weight != 0 && remaining > 0)
            {
                int chunk = ClampAB(remaining, 0, 5);
                v += chunk * weight;
                weight /= 2;
                remaining -= chunk;
            }
            demand[(int)Commodity.Salt] = v - a / 2;

            v = (y + 10) * p + a;
            if (Size() < 5)
            {
                v = 0;
            }

            if (Size() < 8)
            {
                v >>= 1;
            }

            if (Knows(AdvanceIds.Industrialization))
            {
                v *= 2;
            }

            if (Knows(AdvanceIds.Electricity))
            {
                v *= 2;
            }

            if (Has(BuildingIds.PowerPlant))
            {
                v *= 2;
            }

            if (Has(BuildingIds.HydroPlant) || Has(BuildingIds.NuclearPlant) || Has(BuildingIds.SolarPlant))
            {
                v >>= 3;
            }

            demand[(int)Commodity.Coal] = v;
            v = (river + road + 1) * p;
            if (v <= supply[(int)Commodity.Copper])
            {
                v >>= 1;
            }

            if (Has(BuildingIds.Marketplace))
            {
                v += v >> 1;
            }

            if (Has(BuildingIds.Bank))
            {
                v += v >> 1;
            }

            if (Knows(AdvanceIds.Electricity))
            {
                v += v >> 1;
            }

            if (Knows(AdvanceIds.Computers))
            {
                v /= 4;
            }

            if (Size() < 5)
            {
                v >>= 1;
            }

            demand[(int)Commodity.Copper] = v;
            v = supply[(int)Commodity.Cloth] + road;
            if (Knows(AdvanceIds.Chemistry))
            {
                v /= 2;
            }

            if (Knows(AdvanceIds.MassProduction))
            {
                v /= 2;
            }

            demand[(int)Commodity.Dye] = v;
            demand[(int)Commodity.Wine] = 4 * p + 4 + Math.Abs(city.Position.X - city.Position.Y);
            v = 2 * ((map.Geometry.Height >> 1) - y) - x + (map.Geometry.Width >> 1)
                + 4 * jungle + plains / 2 + 2 * swamp + 4 * desert;
            v = ShiftSigned(v, ClampAB(p - 1, -1, 1));
            if (region == 1 && slot.Value % 4 >= 2)
            {
                v += v >> 1;
            }

            if (Size() < 7)
            {
                v >>= 1;
            }

            demand[(int)Commodity.Silk] = v;

            v = demand[(int)Commodity.Silver] = 8 * Size();
            Commodity metal = ((city.Position.Y + city.Position.X) % 3) switch
            {
                1 => Commodity.Gems,
                2 => Commodity.Gold,
                _ => Commodity.Silver
            };
            if (Has(BuildingIds.Cathedral) || BuildingQueries.HasWonder(world, rules, owner, WonderIds.Michelangelo))
            {
                v = (3 * v) >> 1;
            }

            if (Has(BuildingIds.Bank))
            {
                v = (3 * v) >> 1;
            }

            if (Knows(AdvanceIds.Economics))
            {
                v >>= 1;
            }

            if (Knows(AdvanceIds.Computers))
            {
                v >>= 1;
            }

            if (Tribe() == 17)
            {
                v *= 2;
            }

            demand[(int)metal] = v;

            v = RegionSize() / 10 - Math.Max(a - 12, 0);
            if (Size() < 4)
            {
                v /= 2;
            }

            if (RegionSize() > 400)
            {
                if (Size() > 7)
                {
                    v *= 2;
                }

                if ((-city.Position.X - city.Position.Y) % 4 == 0)
                {
                    v = 0;
                }
            }
            if (Knows(AdvanceIds.Refrigeration))
            {
                v >>= 1;
            }

            demand[(int)Commodity.Spice] = v;
            v = -1;
            if (Knows(AdvanceIds.Industrialization))
            {
                v = (a / 3) * (p + 2);
                if (v <= supply[(int)Commodity.Oil])
                {
                    v >>= 1;
                }

                if (!Has(BuildingIds.Factory))
                {
                    if (Size() < 5)
                    {
                        v >>= 1;
                    }

                    if (Size() < 10)
                    {
                        v >>= 1;
                    }

                    if (Size() < 20)
                    {
                        v >>= 1;
                    }
                }
                else
                {
                    v = (3 * v) / 2;
                }

                if (Knows(AdvanceIds.Automobile))
                {
                    v *= 3;
                }

                if (Has(BuildingIds.Superhighways))
                {
                    v *= 2;
                }

                if (Has(BuildingIds.MassTransit))
                {
                    v >>= 1;
                }

                if (Has(BuildingIds.RecyclingCenter))
                {
                    v >>= 1;
                }
            }
            demand[(int)Commodity.Oil] = v;
            v = -1;
            if (Knows(AdvanceIds.NuclearFission))
            {
                v = ShiftSigned(a * a, ClampAB(p - 3, -3, 0));
                if (Has(BuildingIds.NuclearPlant) || Has(BuildingIds.SdiDefense))
                {
                    v *= 2;
                }
            }
            demand[(int)Commodity.Uranium] = v;
        }
        return new(supply, demand);
    }

    internal static void Rank(ref State.City city, int[] supply, int[] demand)
    {
        for (int g = 0; g < 16; ++g)
        {
            if (supply[g] < demand[g] && supply[g] > 0)
            {
                supply[g] = 0;
            }
            else if (supply[g] >= demand[g] && demand[g] > 0)
            {
                demand[g] = 0;
            }
        }
        Publish(ref city.Supplies, supply);
        Publish(ref city.Demands, demand);
    }

    private static void Publish(ref TradeRouteArray<sbyte> output, int[] scores)
    {
        var ranked = Enumerable.Range(0, 16).OrderByDescending(g => scores[g]).ThenByDescending(g => g).ToArray();
        for (int i = 0; i < CityLimits.TradeRoutes; ++i)
        {
            if (scores[ranked[i]] < 0)
            {
                throw new InvalidOperationException("C07 ranking has fewer than three nonnegative commodity scores");
            }
            output[i] = (sbyte)ranked[i];
        }
    }

    internal static void Exceptional(GameState world, CitySlot slot, int advances)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        int x = city.Position.X, y = city.Position.Y, owner = city.Owner.Value;
        bool Knows(int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance);
        int demand = advances < 32 ? (5 * y + 3 * x) % 14 : (5 * y + 3 * x) % 9 + 5;
        int supply = advances < 32 ? (7 * y + 13 * x) % 14 : (7 * y + 13 * x) % 9 + 5;
        if (supply == demand)
        {
            supply = -2;
        }

        bool advancedDemand = false, advancedSupply = false;
        for (int i = 0; i < CityLimits.TradeRoutes; ++i)
        {
            advancedDemand |= city.Demands[i] > 13;
            advancedSupply |= city.Supplies[i] > 13;
            if (city.Demands[i] == demand || city.Supplies[i] == demand)
            {
                demand = -2;
            }

            if (city.Demands[i] == supply || city.Supplies[i] == supply)
            {
                supply = -2;
            }
        }
        if (demand < 0 && !advancedDemand)
        {
            if (Knows(AdvanceIds.Industrialization) && (demand & 1) != 0)
            {
                demand = (int)Commodity.Oil;
            }
            else if (Knows(AdvanceIds.NuclearFission) && (demand & 1) == 0)
            {
                demand = (int)Commodity.Uranium;
            }
        }
        if (demand >= 0)
        {
            city.Demands[1] = unchecked((sbyte)demand);
        }

        if (supply < 0 && !advancedSupply)
        {
            supply = 14 + (supply & 1);
            if (supply == (int)Commodity.Oil && !Knows(AdvanceIds.Industrialization))
            {
                supply = -1;
            }

            if (supply == (int)Commodity.Uranium && !Knows(AdvanceIds.NuclearFission))
            {
                supply = -1;
            }
        }
        if (supply >= 0)
        {
            city.Supplies[1] = unchecked((sbyte)supply);
        }
    }

    internal static void Suppress(GameState world, RuleTables rules, CitySlot slot)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        for (int i = 0; i < unchecked((sbyte)city.RouteCount); ++i)
        {
            int goods = city.RouteGoods[i];
            if (goods < 0)
            {
                continue;
            }

            Negate(ref city.Supplies, goods);
            if (city.RoutePartners[i] is not { } partner)
            {
                throw new InvalidOperationException("C07 nonfood route requires a backed partner");
            }
            ref var other = ref world.Entities.Cities.At(partner);
            int reciprocal = -1;
            for (int j = 0; j < unchecked((sbyte)other.RouteCount); ++j)
            {
                if (other.RoutePartners[j] == slot)
                {
                    reciprocal = other.RouteGoods[j];
                }
            }
            if (reciprocal >= 0)
            {
                Negate(ref city.Demands, reciprocal);
            }
        }
        foreach (ref readonly var unit in world.Entities.Units.Scanned)
        {
            if (unit.Token.Occupied && rules.Units[unit.Type.Value].Role == RuleRole.Trade
                && unchecked((byte)(unit.Home?.Value ?? 255)) == slot.Value && unchecked((sbyte)unit.Counter) >= 0)
            {
                Negate(ref city.Supplies, unit.Counter);
            }
        }
    }

    private static void Negate(ref TradeRouteArray<sbyte> entries, int goods)
    {
        for (int i = 0; i < CityLimits.TradeRoutes; ++i)
        {
            if (entries[i] == goods)
            {
                entries[i] = unchecked((sbyte)-entries[i]);
            }
        }
    }
}
