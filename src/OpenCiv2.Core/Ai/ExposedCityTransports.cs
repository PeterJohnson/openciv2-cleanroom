using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A05 §10 exposed-city transport retasking. Capacity, not domain/role, admits a unit.
/// Uses full damage-adjusted allowance and a strict distance comparison, preserving task/spent state.</summary>
public sealed class ExposedCityTransports(GameState world, RuleTables rules, StackOperations stacks)
{
    public void Retask(CitySlot city)
    {
        MapKnowledge.RequireLocal(world);
        var destination = world.Entities.Cities.At(city).Position;
        int cityOwner = unchecked((sbyte)world.Entities.Cities.At(city).Owner.Value);
        for (int slot = 0; slot < world.Entities.Units.End; ++slot)
        {
            ref var unit = ref world.Entities.Units.Storage[slot];
            if (!unit.Token.Occupied)
            { continue; }
            int owner = unchecked((sbyte)unit.Owner.Value);
            if (owner == 0 || world.Globals.HumanPlayers.Contains(owner) || owner == cityOwner
                || world.Civilizations[owner].Treaties[cityOwner].Peace
                || unchecked((sbyte)rules.Units[unit.Type.Value].Capacity) == 0
                || stacks.Count(slot) <= 1 || !world.Map.TileAt(unit.Position).Ocean)
            { continue; }
            if (world.Map.Geometry.WeightedDistance(destination, unit.Position, world.Globals.Rules.FlatWorld)
                < UnitMovement.Allowance(world, rules, unit))
            { unit.Orders = UnitOrder.GoTo; unit.Destination = destination; }
        }
    }
}
