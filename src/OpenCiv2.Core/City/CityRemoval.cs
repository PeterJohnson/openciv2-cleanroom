using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>C01 local city deletion. Presentation owns closing/refreshing views; callers own civilization elimination.</summary>
public sealed class CityRemoval(GameState world, RuleTables rules, StackOperations stacks,
    UnitLoss loss, Action<CityTarget> removed)
{
    public void Delete(int slot)
    {
        foreach (var check in DeleteSteps(slot))
        { loss.CompleteCheck(check); }
    }

    internal IEnumerable<CivilizationCheck> DeleteSteps(int slot)
    {
        if (slot < 0 || !world.Entities.Cities.Storage[slot].Token.Occupied)
        {
            yield break;
        }
        MapKnowledge.RequireLocal(world);
        world.CityDeletionActive = true;
        ref var city = ref world.Entities.Cities.Storage[slot];
        CityTarget target = new(new((ushort)slot), city.Token);
        var position = city.Position;
        int owner = unchecked((sbyte)city.Owner.Value);
        world.Civilizations[owner].CityCount = unchecked((short)(world.Civilizations[owner].CityCount - 1));
        foreach (var check in SupportedUnitSteps(slot))
        { yield return check; }
        world.Entities.Cities.Storage[slot].Token = default;
        if (slot == world.Entities.Cities.End - 1)
        {
            world.Entities.Cities.SetEnd(world.Entities.Cities.End - 1);
            world.Globals.CityEnd = unchecked((short)world.Entities.Cities.End);
        }
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var other = ref world.Entities.Cities.Storage[i];
            if (!other.Token.Occupied)
            {
                continue;
            }
            for (int route = unchecked((sbyte)other.RouteCount) - 1; route >= 0; --route)
            {
                if ((other.RoutePartners[route]?.Value ?? -1) == slot)
                {
                    TradeRoutes.Remove(world, new((ushort)i), route);
                }
            }
        }
        for (int i = 0; i < RuleLimits.Wonders; ++i)
        {
            if (world.Globals.WonderCity[i] == slot)
            {
                world.Globals.WonderCity[i] = BuildingQueries.Destroyed;
            }
        }
        CivilizationSet nearbyOwners = default;
        world.Map.TileAt(position).Features &= ~MapFeatures.CitySite;
        for (int i = 0; i < MapNeighborhoods.Radius.Length; ++i)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, position, MapNeighborhoods.Radius[i], world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(point) || world.Map.TileAt(point).Ocean)
            {
                continue;
            }
            ref var tile = ref world.Map.TileAt(point);
            tile.Fertility = (byte)(8 + tile.Fertility % 8);
            if (i <= 20)
            {
                if (tile.Claimant == owner)
                {
                    tile.Claimant = 0;
                }
                if (world.Map.CityOrUnitOwner(point) is > 0 and < EntityLimits.Civilizations and var other && other != owner)
                {
                    nearbyOwners = nearbyOwners.With(other);
                }
            }
        }
        if (nearbyOwners.Count != 0)
        {
            for (int current = stacks.FirstAt(position); current >= 0; current = stacks.Next(current))
            {
                ref var unit = ref world.Entities.Units.Storage[current];
                unit.Visibility = unit.Visibility.Union(nearbyOwners);
            }
        }
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            if (world.Entities.Cities.Storage[i].Token.Occupied)
            {
                CityArea.Refresh(world, new((ushort)i));
            }
        }
        removed(target);
        world.CityDeletionActive = false;
    }

    /// <summary>C01 deletion scans all supported units; C09 capture passes its retained old owner.</summary>
    internal IEnumerable<CivilizationCheck> SupportedUnitSteps(int slot, int? oldOwner = null)
    {
        MapKnowledge.RequireLocal(world);
        for (int i = world.Entities.Units.End - 1; i >= 0; --i)
        {
            ref var unit = ref world.Entities.Units.Storage[i];
            if (!unit.Token.Occupied || oldOwner is { } owner && unchecked((sbyte)unit.Owner.Value) != owner
                || (unit.Home?.Value ?? 255) != slot)
            { continue; }
            if (unit.Type.Value == UnitTypeIds.Partisans)
            { unit.Home = null; continue; }
            if (!world.Globals.HumanPlayers.Contains(unchecked((sbyte)unit.Owner.Value) & 31)
                && CityQueries.At(world, unit.Position) is { } other && other.Value != slot)
            {
                if (rules.Units[unit.Type.Value].Role == RuleRole.Defend
                    && stacks.CountRole(i, (int)RuleRole.Defend, rules) == 1)
                {
                    byte home = unchecked((byte)other.Value);
                    unit.Home = home == 255 ? null : new(home);
                    continue;
                }
                world.Entities.Cities.At(other).Flags.DefenderLost = true;
            }
            foreach (var check in loss.DestroyAndRedrawSteps(i, true))
            { yield return check; }
            i = world.Entities.Units.End;
        }
    }
}
