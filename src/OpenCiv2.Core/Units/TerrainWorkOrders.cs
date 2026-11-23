using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public enum TerrainWorkProblem
{
    OnlySettlers, RadioRequired, ConstructionRequired, CannotImprove, AlreadyAirbase,
    AlreadyFortress, BridgeRequired, AlreadyRailroad, RailroadRequired, AlreadyMining,
    NoWater, AlreadyFarmland, RefrigerationRequired, EngineersRequired, NoPollution
}

public interface ITerrainWorkEffects
{
    void Notice(UnitSlot unit, UnitOrder job, TerrainWorkProblem problem);
    void Redraw(MapPoint position, int radius);
    void RedrawStatus();
    void UnitChanged(UnitSlot unit);
    /// <summary>The open, unsuspended, undeferred city selection; null otherwise.
    /// W04 checks distance and invokes the actual C03 calculation before CityChanged.</summary>
    CitySlot? CityToRefresh { get; }
    void CityChanged(CitySlot city);
}

/// <summary>W04 player work commands and queued execution, with actual U01/U02/W01/W03/C03 children.
/// P05/U04 callers own command authority/selectability; direct work has no player eligibility gates.</summary>
public sealed class TerrainWorkOrders(GameState world, RuleState installed, StackOperations stacks, ITerrainWorkEffects effects)
{
    private RuleTables Rules => installed.Definitions;
    private bool Knows(int owner, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance);
    private Tile Tile(MapPoint point) => world.Map.TileAt(point);
    private static bool Fortress(Tile tile) => tile.Features.HasFlag(MapFeatures.Fortification) && !tile.Features.HasFlag(MapFeatures.CitySite);
    private static bool Farmland(Tile tile) => tile.Features.HasFlag(MapFeatures.Irrigation | MapFeatures.Mine);
    private static bool Mine(Tile tile) => tile.Features.HasFlag(MapFeatures.Mine) && !tile.Features.HasFlag(MapFeatures.Irrigation);
    public static bool IsWork(UnitOrder order) => order is >= UnitOrder.Fortress and <= UnitOrder.Airbase;
    private BaseTerrainWork Work(int terrain) => Rules.Terrain[terrain].Work ?? throw new InvalidOperationException("Terrain work definitions are required");

    /// <summary>Pure command availability for a player projection; execution repeats these live checks.</summary>
    public static TerrainWorkProblem? Check(GameState world, RuleTables rules, UnitSlot slot, UnitOrder job)
    {
        ref var unit = ref world.Entities.Units.At(slot);
        var point = unit.Position;
        int owner = unit.Owner.Value, terrain = (int)world.Map.TileAt(point).Terrain;
        return Check(world, rules, slot, job, point, owner, terrain);
    }

    private static TerrainWorkProblem? Check(GameState world, RuleTables rules, UnitSlot slot, UnitOrder job, MapPoint point, int owner, int terrain)
    {
        Tile Tile(MapPoint p) => world.Map.TileAt(p);
        BaseTerrainWork Work(int t) => rules.Terrain[t].Work ?? throw new InvalidOperationException("Terrain work definitions are required");
        bool Knows(int c, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, c, advance);
        if (!IsWork(job))
        {
            throw new ArgumentOutOfRangeException(nameof(job));
        }

        if (rules.Units[world.Entities.Units.At(slot).Type.Value].Role != RuleRole.Settle)
        {
            return TerrainWorkProblem.OnlySettlers;
        }

        switch (job)
        {
            case UnitOrder.Airbase:
                if (!Knows(owner, AdvanceIds.Radio))
                {
                    return TerrainWorkProblem.RadioRequired;
                }

                if (Tile(point).City || Fortress(Tile(point)) || Tile(point).Ocean)
                {
                    return TerrainWorkProblem.CannotImprove;
                }

                if (world.Map.AirbaseOwner(point) is not null)
                {
                    return TerrainWorkProblem.AlreadyAirbase;
                }

                break;
            case UnitOrder.Fortress:
                if (!Knows(owner, AdvanceIds.Construction))
                {
                    return TerrainWorkProblem.ConstructionRequired;
                }

                if (Tile(point).City || Tile(point).Ocean || Fortress(Tile(point)))
                {
                    return TerrainWorkProblem.CannotImprove;
                }

                if (Fortress(Tile(point)))
                {
                    return TerrainWorkProblem.AlreadyFortress;
                }

                break;
            case UnitOrder.Road:
                if (Tile(point).City || Tile(point).Ocean)
                {
                    return TerrainWorkProblem.CannotImprove;
                }

                if (Tile(point).River && !Knows(owner, AdvanceIds.BridgeBuilding))
                {
                    return TerrainWorkProblem.BridgeRequired;
                }

                if (Tile(point).Features.HasFlag(MapFeatures.Railroad))
                {
                    return TerrainWorkProblem.AlreadyRailroad;
                }

                if (Tile(point).Features.HasFlag(MapFeatures.Road) && !Knows(owner, AdvanceIds.Railroad))
                {
                    return TerrainWorkProblem.RailroadRequired;
                }

                break;
            case UnitOrder.Irrigate:
            case UnitOrder.Mine:
                var outcome = job == UnitOrder.Irrigate ? Work(terrain).Irrigation.Result : Work(terrain).Mining.Result;
                if ((int)outcome >= 0)
                {
                    break;
                }

                if (outcome == TerrainResult.NoResult || Tile(point).City)
                {
                    return TerrainWorkProblem.CannotImprove;
                }

                if (job == UnitOrder.Mine)
                {
                    if (Mine(Tile(point)))
                    {
                        return TerrainWorkProblem.AlreadyMining;
                    }
                }
                else
                {
                    if (!TerrainSources.Irrigation(world, point))
                    {
                        return TerrainWorkProblem.NoWater;
                    }

                    if (Farmland(Tile(point)))
                    {
                        return TerrainWorkProblem.AlreadyFarmland;
                    }

                    if (Tile(point).Features.HasFlag(MapFeatures.Irrigation) && !Knows(owner, AdvanceIds.Refrigeration))
                    {
                        return TerrainWorkProblem.RefrigerationRequired;
                    }
                }
                break;
            case UnitOrder.Transform:
                if (Tile(point).Ocean)
                {
                    return TerrainWorkProblem.CannotImprove;
                }

                if (world.Entities.Units.At(slot).Type.Value != UnitTypeIds.Engineers)
                {
                    return TerrainWorkProblem.EngineersRequired;
                }

                break;
            case UnitOrder.Clean:
                if (!Tile(point).Features.HasFlag(MapFeatures.Pollution))
                {
                    return TerrainWorkProblem.NoPollution;
                }

                break;
        }
        return null;
    }

    public bool Request(UnitSlot slot, UnitOrder job)
    {
        MapKnowledge.RequireLocal(world);
        ref var unit = ref world.Entities.Units.At(slot);
        var point = unit.Position;
        int owner = unit.Owner.Value, terrain = (int)Tile(point).Terrain;
        if (Check(world, Rules, slot, job, point, owner, terrain) is { } problem)
        {
            effects.Notice(slot, job, problem);
            return false;
        }
        effects.Redraw(point, 0);
        Execute(slot, job);
        return true;
    }

    public bool Execute(UnitSlot slot, UnitOrder job)
    {
        MapKnowledge.RequireLocal(world);
        if (!IsWork(job))
        {
            throw new ArgumentOutOfRangeException(nameof(job));
        }

        ref var actor = ref world.Entities.Units.At(slot);
        int owner = actor.Owner.Value;
        var point = actor.Position;
        int terrain = (int)Tile(point).Terrain;
        var oldOrder = actor.Orders;
        actor.Orders = job;
        int MovementCost() => unchecked((sbyte)Rules.Terrain[terrain].MovementCost);
        int required = job switch
        {
            UnitOrder.Fortress => MovementCost() / 2 + 3,
            UnitOrder.Road => MovementCost() * (Tile(point).Features.HasFlag(MapFeatures.Road) ? 4 : 2) + (Tile(point).River ? 2 : 0),
            UnitOrder.Irrigate => unchecked((sbyte)Work(terrain).Irrigation.WorkTurns),
            UnitOrder.Mine => unchecked((sbyte)Work(terrain).Mining.WorkTurns),
            UnitOrder.Transform => MovementCost() * Rules.Cosmic.TransformationWork,
            _ => 4
        };
        UnitMovement.Exhaust(world, Rules, ref actor);
        for (int current = stacks.First(slot.Value); current >= 0; current = stacks.Next(current))
        {
            ref var other = ref world.Entities.Units.Storage[current];
            if (current == slot.Value || Rules.Units[other.Type.Value].Role != RuleRole.Settle || other.Orders != job)
            {
                continue;
            }

            actor.Counter = unchecked((byte)(actor.Counter + other.Counter));
            other.Counter = 0;
            break;
        }
        actor.Counter = unchecked((byte)(actor.Counter + 1));
        if (actor.Type.Value == UnitTypeIds.Engineers)
        {
            actor.Counter = unchecked((byte)(actor.Counter + 1));
        }

        if (unchecked((sbyte)actor.Counter) < required)
        {
            effects.Redraw(point, 0);
            if (actor.Orders != oldOrder)
            {
                effects.UnitChanged(slot);
            }

            return false;
        }
        for (int current = stacks.First(slot.Value); current >= 0; current = stacks.Next(current))
        {
            ref var other = ref world.Entities.Units.Storage[current];
            if (Rules.Units[other.Type.Value].Role == RuleRole.Settle && other.Orders == job)
            { other.Counter = 0; other.Orders = UnitOrder.None; }
        }
        Complete(job, point, terrain, owner);
        MapKnowledge.Remember(world, actor.Owner.Value, point);
        effects.Redraw(point, 1);
        if (actor.Owner.Value == world.Multiplayer.LocalHuman)
        {
            effects.RedrawStatus();
        }

        if (actor.Orders != oldOrder)
        {
            effects.UnitChanged(slot);
        }

        if (effects.CityToRefresh is { } city && world.Map.Geometry.WeightedDistance(world.Entities.Cities.At(city).Position, point, world.Globals.Rules.FlatWorld) < 3)
        {
            new CityEconomy(world, installed, stacks).Calculate(city);
            effects.CityChanged(city);
        }
        return true;
    }

    private void Features(MapPoint point, MapFeatures features, bool set = true) => world.Map.Mutate(new TileChange.Features(point, features, set), true);
    private void Complete(UnitOrder job, MapPoint point, int terrain, int owner)
    {
        switch (job)
        {
            case UnitOrder.Fortress:
                if (world.Map.CityOwner(point) is null)
                { Features(point, MapFeatures.Fortification); Features(point, MapFeatures.CitySite, false); }
                break;
            case UnitOrder.Airbase:
                if (world.Map.CityOwner(point) is null)
                {
                    Features(point, MapFeatures.Fortification | MapFeatures.CitySite);
                }

                break;
            case UnitOrder.Road:
                if (!Tile(point).Features.HasFlag(MapFeatures.Road) && world.Map.CityOwner(point) is null)
                {
                    Features(point, MapFeatures.Road);
                }
                else if (Knows(owner, AdvanceIds.Railroad))
                {
                    Features(point, MapFeatures.Road | MapFeatures.Railroad);
                }

                break;
            case UnitOrder.Irrigate:
            case UnitOrder.Mine:
                var outcome = job == UnitOrder.Irrigate ? Work(terrain).Irrigation.Result : Work(terrain).Mining.Result;
                if ((int)outcome >= 0)
                {
                    SetTerrain(point, outcome);
                    Cleanup(point, (int)(job == UnitOrder.Irrigate ? Work(terrain).Irrigation.Result : Work(terrain).Mining.Result));
                }
                else if (job == UnitOrder.Mine)
                { Features(point, MapFeatures.Irrigation, false); Features(point, MapFeatures.Mine); }
                else if (!Tile(point).Features.HasFlag(MapFeatures.Irrigation))
                { Features(point, MapFeatures.Mine, false); Features(point, MapFeatures.Irrigation); }
                else if (Knows(owner, AdvanceIds.Refrigeration))
                {
                    Features(point, MapFeatures.Irrigation | MapFeatures.Mine);
                }

                break;
            case UnitOrder.Transform:
                SetTerrain(point, Work(terrain).Transformation);
                Cleanup(point, (int)Work(terrain).Transformation);
                break;
            case UnitOrder.Clean:
                if (Tile(point).Features.HasFlag(MapFeatures.Pollution))
                {
                    world.Globals.PollutedTiles = unchecked((short)(world.Globals.PollutedTiles - 1));
                }

                Features(point, MapFeatures.Pollution, false);
                break;
        }
    }

    // W01's numeric terrain-result contract also admits custom operands that set
    // preserved terrain flags; retain those effects before an unsupported rule read.
    private void SetTerrain(MapPoint point, TerrainResult result)
    {
        byte value = unchecked((byte)result);
        world.Map.Mutate(new TileChange.Terrain(point, (Terrain)(value % 16),
            AddRiver: value >= 128, SuppressResource: value % 128 >= 64), true);
    }

    private void Cleanup(MapPoint point, int destination)
    {
        if (Work(destination).Irrigation.Result != TerrainResult.ImproveInPlace && Tile(point).Features.HasFlag(MapFeatures.Irrigation))
        {
            Features(point, MapFeatures.Irrigation | MapFeatures.Mine, false);
        }

        if (Work(destination).Mining.Result != TerrainResult.ImproveInPlace && Mine(Tile(point)))
        {
            Features(point, MapFeatures.Mine, false);
        }
    }
}
