using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U04 return-to-recovery search. Installs Goto or exhausts at the selected
/// destination; does not change home affiliation, AI task or execute movement.</summary>
public sealed class UnitRecovery(GameState world, RuleTables rules)
{
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private UnitRule Rule(UnitSlot slot) => rules.Units[Unit(slot).Type.Value];
    public void GoHome(UnitSlot slot)
    {
        MapKnowledge.RequireLocal(world);
        int type = Unit(slot).Type.Value, owner = Unit(slot).Owner.Value;
        var origin = Unit(slot).Position;
        int? region = Rule(slot).Domain == RuleDomain.Land ? world.Map.TileAt(origin).Region : null;
        CitySlot? city = null;
        int cityDistance = 9999, carrierDistance = 999;
        MapPoint carrier = origin;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var candidate = ref world.Entities.Cities.Storage[i];
            if (!candidate.Token.Occupied || candidate.Owner.Value != owner
                || region is { } raw && world.Map.TileAt(candidate.Position).Region != raw)
            { continue; }
            int distance = world.Map.Geometry.StepDistance(origin, candidate.Position, world.Globals.Rules.FlatWorld);
            if (distance < cityDistance)
            { cityDistance = distance; city = new((ushort)i); }
        }
        if (Rule(slot).Domain == RuleDomain.Air)
        {
            for (int i = 0; i < world.Entities.Units.End; ++i)
            {
                ref var candidate = ref world.Entities.Units.Storage[i];
                if (!candidate.Token.Occupied || candidate.Owner.Value != owner)
                { continue; }
                var abilities = rules.Units[candidate.Type.Value].Abilities;
                if (!abilities.CarryAircraft && !(abilities.Submarine && rules.Units[type].Abilities.DestroyedAfterAttack))
                { continue; }
                int distance = world.Map.Geometry.StepDistance(origin, candidate.Position, world.Globals.Rules.FlatWorld);
                if (distance < carrierDistance)
                { carrierDistance = distance; carrier = candidate.Position; }
            }
            if (world.Map.AirbaseOwner(origin) is not null)
            {
                if (!world.Globals.HumanPlayers.Contains(owner))
                { UnitMovement.Exhaust(world, rules, ref Unit(slot)); }
                return;
            }
        }
        var destination = origin;
        int best = 999;
        if (cityDistance < best)
        { best = cityDistance; destination = world.Entities.Cities.At(city!.Value).Position; }
        if (carrierDistance < best)
        { best = carrierDistance; destination = carrier; }
        if (best >= 999)
        { return; }
        if (world.Globals.HumanPlayers.Contains(owner) && Rule(slot).Range != 0)
        {
            int range = unchecked((sbyte)Rule(slot).Range), counter = unchecked((sbyte)Unit(slot).Counter);
            int allowance = UnitMovement.Allowance(world, rules, Unit(slot));
            int remaining = UnitMovement.Left(world, rules, Unit(slot));
            int road = rules.Cosmic.RoadMovementMultiplier;
            int reach = unchecked((range - (counter + 1)) * allowance + remaining) / road;
            if (reach < world.Map.Geometry.StepDistance(origin, destination, world.Globals.Rules.FlatWorld))
            { return; }
        }
        if (destination == origin)
        { UnitMovement.Exhaust(world, rules, ref Unit(slot)); }
        else
        {
            Unit(slot).Orders = UnitOrder.GoTo;
            Unit(slot).Destination = new(unchecked((short)destination.X), unchecked((short)destination.Y));
        }
    }
}
