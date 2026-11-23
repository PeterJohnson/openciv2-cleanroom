using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public enum PartisanTargetRoute { Continue, PlanMove, Direction }
public readonly record struct PartisanTargetChoice(PartisanTargetRoute Route, int Direction = 8);

/// <summary>A04 §5 pillage admission and partisan targets. Actual W04 children retain any
/// attack warning; every completed child routes the parent to common Finish.</summary>
public sealed class GroundPartisans(GameState world, RuleTables rules, Pillage pillage)
{
    public Pillage.Operation? BeginPillage(UnitTacticsContext c)
    {
        MapKnowledge.RequireLocal(world);
        c.CitySelector = c.NearestCity;
        ref var unit = ref world.Entities.Units.At(c.Unit);
        int moves = UnitMovement.Allowance(world, rules, unit);
        if (moves > rules.Cosmic.RoadMovementMultiplier || c.NearestCity is not { } city)
        { return null; }
        int other = world.Entities.Cities.At(city).Owner.Value;
        if (!world.Globals.HumanPlayers.Contains(other) || !world.Civilizations[c.Owner].Treaties[other].War
            || rules.Units[unit.Type.Value].Domain != RuleDomain.Land || rules.Units[unit.Type.Value].Attack == 0)
        { return null; }
        const MapFeatures work = MapFeatures.Fortification | MapFeatures.Mine | MapFeatures.Irrigation;
        var features = world.Map.TileAt(c.Position).Features;
        bool selected = (features & work) != 0 && c.NearestDistance <= 3
            || (features & (work | MapFeatures.Road)) != 0
            && (world.Civilizations[c.Owner].ContinentCities[c.Region] == 0 && c.NearestDistance < 8 || c.Type == UnitTypeIds.Partisans);
        return selected && !c.ForceAttack ? pillage.Begin(c.Unit) : null;
    }

    public PartisanTargetChoice Targets(UnitTacticsContext c)
    {
        MapKnowledge.RequireLocal(world);
        if (c.Type == UnitTypeIds.Partisans)
        {
            for (int i = 0; i < world.Entities.Units.End; ++i)
            {
                ref var target = ref world.Entities.Units.Storage[i];
                if (!target.Token.Occupied || unchecked((sbyte)rules.Units[target.Type.Value].Role) <= 4
                    || target.Owner.Value == c.Owner)
                { continue; }
                var relation = world.Civilizations[c.Owner].Treaties[target.Owner.Value];
                if (relation.Ceasefire || relation.Peace
                    || world.Map.Geometry.StepDistance(c.Position, target.Position, world.Globals.Rules.FlatWorld) >= 4
                    || !target.Visibility.Contains(c.Owner) && target.Owner.Value != c.Owner
                    || world.Map.TileAt(target.Position).Region != c.Region)
                { continue; }
                UnitOrders.SetGoto(ref world.Entities.Units.At(c.Unit), AiTaskIds.HuntNonmilitary, target.Position);
                c.HasOrders = true;
                break;
            }
        }
        if (c.HasOrders)
        { return new(PartisanTargetRoute.PlanMove); }
        if (c.Type != UnitTypeIds.Partisans || c.CitySelector is not { } city)
        { return new(PartisanTargetRoute.Continue); }
        ref var destination = ref world.Entities.Cities.At(city);
        int other = destination.Owner.Value;
        var treaty = world.Civilizations[c.Owner].Treaties[other];
        if (!world.Globals.HumanPlayers.Contains(other) || treaty.Ceasefire || treaty.Peace)
        { return new(PartisanTargetRoute.Continue); }
        int distance = world.Map.Geometry.WeightedDistance(c.Position, destination.Position, world.Globals.Rules.FlatWorld);
        int? direction = null;
        for (int i = 0; i < MapLimits.Directions; ++i)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, c.Position, MapNeighborhoods.Directions[i], world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(point) || world.Map.UnitOwner(point) is not null)
            { continue; }
            if (world.Map.CityOwner(point) is not null)
            { direction = null; break; }
            if (world.Map.Geometry.WeightedDistance(point, destination.Position, world.Globals.Rules.FlatWorld) <= distance
                && (world.Map.TileAt(point).Features & (MapFeatures.Road | MapFeatures.Mine | MapFeatures.Irrigation | MapFeatures.Fortification)) != 0)
            { direction = i; }
        }
        if (direction is not { } selected)
        { return new(PartisanTargetRoute.Continue); }
        world.Entities.Units.At(c.Unit).AiTask = AiTaskIds.PartisanPillage;
        return new(PartisanTargetRoute.Direction, selected);
    }
}
