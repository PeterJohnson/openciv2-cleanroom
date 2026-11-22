using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>C01 authoritative local city creation with actual C06, C07, C08, W03 and U01 children.
/// Founding policy, worker assignment and city-output calculation belong to callers.</summary>
public sealed class CityCreation(
    GameState world, ProcessContext process, NameCatalog names, UnitCreation units, Action tooManyCities)
{
    public CitySlot? Create(CivilizationSlot owner, MapPoint position)
    {
        MapKnowledge.RequireLocal(world);
        var pool = world.Entities.Cities;
        int index = 0;
        while (index < pool.End && pool.Storage[index].Token.Occupied)
        {
            ++index;
        }

        if (index == pool.End)
        {
            if (pool.End >= EntityLimits.CreatableCities)
            {
                if (world.Globals.HumanPlayers.Contains(owner.Value))
                {
                    tooManyCities();
                }

                return null;
            }
            pool.SetEnd(pool.End + 1);
        }
        world.Globals.CityEnd = (short)pool.End;
        var slot = new CitySlot((ushort)index);
        ref var civ = ref world.Civilizations[owner.Value];
        civ.CityCount = unchecked((short)(civ.CityCount + 1));
        civ.LastCityTurn = world.Globals.Turn;
        ref var tile = ref world.Map.TileAt(position);
        tile.Features = (tile.Features | MapFeatures.CitySite) & ~MapFeatures.Fortification;
        ref var city = ref pool.At(slot);
        city.Position = position;
        city.Owner = owner;
        city.Token = process.Tokens.AssignCity();
        city.Founder = owner;
        city.TurnsCaptured = 0;
        city.Flags = new() { TradeDirty = true };
        city.Size = 1;
        city.KnownTo = default;
        city.RememberedSize = default;
        if (world.Globals.WonderCity[WonderIds.Apollo] >= 0)
        {
            for (int c = 0; c < EntityLimits.Civilizations; ++c)
            {
                city.KnownTo = city.KnownTo.With(c);
                city.RememberedSize[c] = 1;
                MapKnowledge.Explore(world, c, position);
            }
            for (int c = 1; c < EntityLimits.Civilizations; ++c)
            {
                MapKnowledge.Remember(world, c, position);
            }
        }
        city.Specialists = default;
        city.Production = UnitTypeIds.Settlers;
        int best = 0;
        var rules = process.Rules.Definitions;
        for (int type = 2; type < RuleLimits.UnitTypes; ++type)
        {
            var rule = rules.Units[type];
            if (rule.Role != RuleRole.Defend || !UnitAvailability.CanBuild(world, rules, owner.Value, slot, new((byte)type)))
            {
                continue;
            }

            int score = unchecked((sbyte)rule.Defense) * 8 / unchecked((sbyte)rule.CostRows);
            if (rule.Abilities.AntiHorseDefense)
            {
                ++score;
            }

            if (score >= best)
            {
                best = score;
                city.Production = (sbyte)type;
            }
        }
        civ.UnitsInProduction[city.Production] = unchecked((byte)(civ.UnitsInProduction[city.Production] + 1));
        city.FoodBox = city.ShieldBox = city.BaseTrade = 0;
        city.Name = string.Empty;
        CityNames.DefaultName(ref city, world.Civilizations, process.Rules, names);
        city.WorkedTiles = default;
        city.SpecialistCount = 0;
        city.Improvements = default;
        if (civ.CityCount == 1)
        {
            BuildingQueries.SetBuilding(world, slot, BuildingIds.Palace, true);
            civ.CapitalX = unchecked((short)position.X);
            if (!world.Globals.HumanPlayers.Contains(owner.Value))
            {
                FirstAiCity(slot, owner, position);
            }
        }
        city.RouteCount = 0;
        city.RoutePartners[0] = new(0);
        // C01 Appendix A's partial clear: positive residual references retain their upper byte.
        // All negative encodings remain semantic absence; F06 owns their imported opaque encoding.
        if (city.RoutePartners[1] is { } retained)
        {
            short partner = unchecked((short)(retained.Value & 0xff00));
            city.RoutePartners[1] = partner < 0 ? null : new((ushort)partner);
        }
        city.RouteGoods = default;
        for (int i = 0; i <= 8; ++i)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, position,
                i == 8 ? new(0, 0) : MapNeighborhoods.Directions[i], world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(point))
            {
                continue;
            }

            ref var neighbor = ref world.Map.TileAt(point);
            if (neighbor.Ocean)
            {
                if (world.Map.Regions[neighbor.Region].Water > 1 && (neighbor.Region < 63 || city.Flags.Coastal))
                {
                    city.Flags.OceanAccess = true;
                }
                city.Flags.Coastal = true;
            }
            if (neighbor.River || neighbor.Terrain == Terrain.Mountains)
            {
                city.Flags.HydroPossible = true;
            }
        }
        CityArea.Refresh(world, slot);
        Commodities.Refresh(world, rules, slot);
        city.Science = city.Tax = city.Trade = 0;
        city.TotalFood = city.TotalShields = city.Happy = city.Unhappy = 0;
        return slot;
    }

    private void FirstAiCity(CitySlot slot, CivilizationSlot owner, MapPoint position)
    {
        for (int i = 0; i < 20; ++i)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, position, MapNeighborhoods.City[i], world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(point))
            {
                continue;
            }

            MapKnowledge.Explore(world, owner.Value, point);
            ref var tile = ref world.Map.TileAt(point);
            if (world.Globals.Turn >= 40 && i < 8 && !tile.RevealedTo.Intersects(world.Globals.HumanPlayers)
                && process.Rules.Definitions.Terrain[(int)tile.Terrain].Work?.Irrigation.Result == TerrainResult.ImproveInPlace)
            {
                tile.Features |= MapFeatures.Irrigation;
            }
        }
        ref var city = ref world.Entities.Cities.At(slot);
        if (world.Globals.Turn > 40 && !world.Map.TileAt(position).RevealedTo.Intersects(world.Globals.HumanPlayers))
        {
            city.Size = (sbyte)Math.Clamp((world.Globals.Turn - 20) / 20, 2, 10);
            BuildingQueries.SetBuilding(world, slot, BuildingIds.Temple, true);
            BuildingQueries.SetBuilding(world, slot, BuildingIds.Marketplace, true);
            BuildingQueries.SetBuilding(world, slot, BuildingIds.Library, true);
        }
        if (world.Globals.Turn > 40 && city.Production != UnitTypeIds.Settlers)
        {
            units.Create(new((byte)city.Production), owner, position);
        }
    }
}
