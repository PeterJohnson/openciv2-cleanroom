using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A03 §3 city terrain jobs, using retained C02 suggestions, actual A01 insertion
/// and U03's uniform road-planning path. F04 owns calculation and job resets.</summary>
public sealed class CityTerrainGoals(GameState world, RuleTables rules)
{
    private CityCalculationWorkspace Work => world.Calculation;
    private bool Knows(int owner, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance);

    public void Publish(CitySlot slot)
    {
        MapKnowledge.RequireLocal(world);
        int owner = world.Entities.Cities.At(slot).Owner.Value;
        var goals = new SharedGoals(world, rules, new MovementGoalQueries(world, rules));
        bool polluted = false;
        for (int i = 0; i < 20; ++i)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, world.Entities.Cities.At(slot).Position,
                MapNeighborhoods.Radius[i], world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(point) || !world.Map.TileAt(point).Features.HasFlag(MapFeatures.Pollution))
            {
                continue;
            }
            goals.AddCurrent(owner, point, 21, 6);
            world.Entities.Cities.At(slot).Flags.TerrainWorkGoal = true;
            polluted = true;
        }
        if (polluted)
        {
            return;
        }

        if (Knows(owner, AdvanceIds.Railroad) && (!world.Globals.HumanPlayers.Contains(owner) || !Work.JobFound))
        {
            if (Work.JobPriority < 4)
            {
                Work.JobPriority = 3;
            }
            FindRoad(slot);
            if (!Work.JobFound)
            {
                return;
            }
        }
        Work.JobPriority = unchecked(Work.JobPriority + 2);
        if (!Work.JobFound)
        {
            Work.JobPriority = 2;
            FindRoad(slot);
        }
        if (!Work.JobFound)
        {
            world.Entities.Cities.At(slot).Flags.TerrainWorkGoal = false;
            return;
        }
        if (Work.JobPriority > 2)
        {
            if (world.Entities.Cities.At(slot).Size > 4)
            {
                Work.JobPriority = unchecked(Work.JobPriority + 1);
            }
            if (BuildingQueries.HasBuilding(world, slot, BuildingIds.Palace))
            {
                Work.JobPriority = unchecked(Work.JobPriority + 1);
            }
        }
        if (world.Entities.Cities.At(slot).Size < 4)
        {
            Work.JobPriority = unchecked(Work.JobPriority - 1);
        }
        goals.AddCurrent(owner, Work.JobPosition, 21, Work.JobPriority);
        world.Entities.Cities.At(slot).Flags.TerrainWorkGoal = true;
    }

    /// <summary>Returns whether this call found a road job. A miss preserves any earlier job;
    /// path selectors/cache and the first rejected candidate's effects are retained.</summary>
    public bool FindRoad(CitySlot slot)
    {
        MapKnowledge.RequireLocal(world);
        var region = world.Map.LandRegion(world.Entities.Cities.At(slot).Position);
        int owner = world.Entities.Cities.At(slot).Owner.Value;
        if (region is null)
        {
            throw new InvalidOperationException("A03 road planning has no regional city-count backing for an ocean city");
        }
        if (world.Civilizations[owner].ContinentCities[region.Value] <= 1)
        {
            return false;
        }
        for (int pass = 0; pass < 2; ++pass)
        {
            for (int i = 0; i < world.Entities.Cities.End; ++i)
            {
                ref var city = ref world.Entities.Cities.Storage[i];
                if (!city.Token.Occupied || i == slot.Value)
                {
                    continue;
                }
                if (city.Owner.Value != owner)
                {
                    if (pass == 0)
                    {
                        continue;
                    }
                    var relation = world.Civilizations[owner].Treaties[city.Owner.Value];
                    if (!relation.Peace && !relation.Alliance)
                    {
                        continue;
                    }
                }
                var target = city.Position;
                var current = world.Entities.Cities.At(slot).Position;
                if (world.Map.LandRegion(target) != region
                    || world.Map.Geometry.StepDistance(current, target, world.Globals.Rules.FlatWorld) >= 23)
                {
                    continue;
                }
                var paths = world.Paths;
                paths.UniformCost = true;
                paths.Owner = -1;
                paths.SelectedUnitType = UnitTypeIds.Warriors;
                paths.Goal = target;
                int steps = 0;
                bool found = false;
                while (true)
                {
                    int direction = GenerationConnectivity.UniformDirection(world.Map, world.Globals.Rules.FlatWorld,
                        rules, paths, current, 99);
                    if (direction is < 0 or >= 8)
                    {
                        break;
                    }
                    current = MapNeighborhoods.Offset(world.Map.Geometry, current,
                        MapNeighborhoods.Directions[direction], world.Globals.Rules.FlatWorld);
                    if (current == target)
                    {
                        break;
                    }
                    if (world.Map.CityOwner(current) is null)
                    {
                        var tile = world.Map.TileAt(current);
                        found = !tile.Features.HasFlag(MapFeatures.Road)
                            ? !tile.River || Knows(owner, AdvanceIds.BridgeBuilding)
                            : !tile.Features.HasFlag(MapFeatures.Railroad) && Knows(owner, AdvanceIds.Railroad);
                    }
                    if (++steps > 50 || found)
                    {
                        break;
                    }
                }
                paths.UniformCost = false;
                if (found && (world.Map.CityOrUnitOwner(current) is not { } occupant || occupant == owner)
                    && (TerritoryQueries.PeaceOwner(world, current, owner) is not { } claimant || claimant == owner))
                {
                    Work.JobFound = true;
                    Work.JobPosition = current;
                    return true;
                }
            }
        }
        return false;
    }
}
