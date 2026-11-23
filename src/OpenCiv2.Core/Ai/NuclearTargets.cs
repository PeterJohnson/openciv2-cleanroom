using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A06 §7 nuclear city selection, actual staging relocation and diplomatic consequences.
/// Returns D0–D7 or Hold (8); A04 must reload position before applying that direction.</summary>
public sealed class NuclearTargets(GameState world, RuleTables rules, StackOperations stacks)
{
    public int Choose(UnitSlot unit)
    {
        MapKnowledge.RequireLocal(world);
        int owner = unchecked((sbyte)world.Entities.Units.At(unit).Owner.Value);
        ref var civ = ref world.Civilizations[owner];
        int best = -1;
        CitySlot? target = null;
        for (int slot = 0; slot < world.Entities.Cities.End; ++slot)
        {
            ref var city = ref world.Entities.Cities.Storage[slot];
            var id = new CitySlot((ushort)slot);
            if (!city.Token.Occupied || city.Owner.Value == owner || BuildingQueries.HasBuilding(world, id, BuildingIds.SdiDefense)
                || unchecked((sbyte)city.Size) <= 4)
            { continue; }
            int other = city.Owner.Value;
            int ownStrength = civ.MilitaryStrength, theirStrength = world.Civilizations[other].MilitaryStrength;
            if (!(2 * theirStrength > 3 * ownStrength || civ.Treaties[other].Vendetta
                || world.Civilizations[other].UnitsActive[UnitTypeIds.NuclearMissile] == 0 && 2 * theirStrength > ownStrength)
                || !civ.Treaties[other].NuclearThreat || civ.Treaties[other].Peace)
            { continue; }
            int score = 0;
            for (int direction = 0; direction <= 8; ++direction)
            {
                var p = Neighbor(city.Position, direction);
                if (!world.Map.Geometry.InBounds(p))
                { continue; }
                int occupant = world.Map.UnitOwner(p) ?? -1;
                if (occupant <= 0)
                { continue; }
                if (occupant == owner)
                { score -= 2; }
                else if (civ.Treaties[occupant].NuclearThreat && !civ.Treaties[occupant].Peace)
                {
                    for (int current = stacks.FirstAt(p); current >= 0; current = stacks.Next(current))
                    { ++score; }
                }
                else
                { score -= 99; }
            }
            score += unchecked((sbyte)city.Size) >> 1;
            if (score <= best)
            { continue; }
            bool reachable = false;
            for (int home = 0; home < world.Entities.Cities.End; ++home)
            {
                ref var origin = ref world.Entities.Cities.Storage[home];
                if (!origin.Token.Occupied || origin.Owner.Value != owner)
                { continue; }
                int distance = world.Map.Geometry.StepDistance(origin.Position, city.Position, world.Globals.Rules.FlatWorld);
                int allowance = UnitMovement.Allowance(world, rules, world.Entities.Units.At(unit));
                if (distance <= allowance / rules.Cosmic.RoadMovementMultiplier)
                { reachable = true; break; }
            }
            if (reachable)
            { best = score; target = id; }
        }
        int shortage = Math.Max(5 - civ.CityCount, 0);
        if (best < 10 - shortage || civ.UnitsActive[world.Entities.Units.At(unit).Type.Value] <= 1)
        { return 8; }
        var selected = target ?? throw new InvalidOperationException("A06 nuclear staging requires a selected city");
        for (int direction = 0; direction < 8; ++direction)
        {
            var p = Neighbor(world.Entities.Cities.At(selected).Position, direction);
            if (!world.Map.Geometry.InBounds(p) || world.Map.CityOwner(p) is not null || world.Map.UnitOwner(p) is not null)
            { continue; }
            stacks.Relocate(unit.Value, p, true);
            world.Entities.Units.At(unit).Visibility = default;
            int Victim() => world.Entities.Cities.At(selected).Owner.Value;
            if (world.Globals.HumanPlayers.Contains(Victim()))
            {
                if (!civ.Treaties[Victim()].Nuked)
                {
                    civ.Treaties[Victim()].Nuked = true;
                    civ.Treaties[Victim()].Vendetta = false;
                    DiplomaticRelations.Set(world, owner, Victim(), new() { Incident = true });
                }
                else
                { world.Civilizations[Victim()].LastContactTurn[owner] = unchecked((short)(world.Civilizations[Victim()].LastContactTurn[owner] - 2)); }
            }
            return direction ^ 4;
        }
        return 8;
    }

    private MapPoint Neighbor(MapPoint target, int direction) => direction == 8
        ? new(world.Map.Geometry.WrapX(target.X, world.Globals.Rules.FlatWorld), target.Y)
        : MapNeighborhoods.Offset(world.Map.Geometry, target, MapNeighborhoods.Directions[direction], world.Globals.Rules.FlatWorld);
}
