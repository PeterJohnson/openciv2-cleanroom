using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A06 §§2–6 air recovery, expended attacks, superiority and city raids.
/// Produces A04 routes and orders; U04/U02 execute ordinary travel and attacks.</summary>
public sealed class AirTactics(GameState world, RuleTables rules, StackOperations stacks, UnitRecovery recovery)
{
    public SpecialUnitChoice Choose(UnitTacticsContext context)
    {
        MapKnowledge.RequireLocal(world);
        return new Choice(world, rules, stacks, recovery, context).Choose();
    }

    private sealed class Choice(GameState world, RuleTables rules, StackOperations stacks, UnitRecovery recovery, UnitTacticsContext c)
    {
        private readonly int owner = unchecked((sbyte)world.Entities.Units.At(c.Unit).Owner.Value);
        private ref Unit Actor => ref world.Entities.Units.At(c.Unit);
        private UnitRule Rule => rules.Units[Actor.Type.Value];
        private UnitRule Cached => rules.Units[c.Type];
        private int MaxMoves() => UnitMovement.Allowance(world, rules, Actor);
        private int MovesLeft() => UnitMovement.Left(world, rules, Actor);
        private int Road => rules.Cosmic.RoadMovementMultiplier;
        private bool Goto => Actor.Orders == UnitOrder.GoTo;
        private int Distance(MapPoint a, MapPoint b) => world.Map.Geometry.StepDistance(a, b, world.Globals.Rules.FlatWorld);
        private MapPoint Neighbor(MapPoint p, int direction) => MapNeighborhoods.Offset(world.Map.Geometry, p,
            MapNeighborhoods.Directions[direction], world.Globals.Rules.FlatWorld);
        private bool InBounds(MapPoint p) => world.Map.Geometry.InBounds(p);
        private bool Fortress(MapPoint p) => (world.Map.TileAt(p).Features & (MapFeatures.CitySite | MapFeatures.Fortification)) == MapFeatures.Fortification;
        private SpecialUnitChoice SetGoto(byte task, MapPoint p)
        { UnitOrders.SetGoto(ref Actor, task, p); return new(SpecialUnitRoute.Finish); }
        private SpecialUnitChoice GoHome()
        { recovery.GoHome(c.Unit); return new(SpecialUnitRoute.Finish); }

        public SpecialUnitChoice Choose()
        {
            int remaining;
            if (Rule.Range == 0)
            { remaining = unchecked(MaxMoves() * 2); }
            else
            {
                int range = unchecked((sbyte)Rule.Range), counter = unchecked((sbyte)Actor.Counter);
                int allowance = MaxMoves(), left = MovesLeft();
                remaining = unchecked((range - (counter + 1)) * allowance + left);
            }
            int reach = remaining / Road;
            int home = NearestOwnedDistance(c.Position);
            if (Rule.Range == 1 && !Goto)
            {
                int allowance = MaxMoves(), left = MovesLeft();
                if (left <= (allowance >> 1) && reach < home + 1 && home <= reach)
                { Actor.AiTask = AiTaskIds.AirReturn; return GoHome(); }
            }
            else if (Rule.Range != 0 && Rule.Range != 1
                && unchecked((sbyte)Rule.Range) / 2 <= unchecked((sbyte)Actor.Counter) && home <= reach)
            { Actor.AiTask = AiTaskIds.AirReturn; return GoHome(); }

            if (Rule.Abilities.DestroyedAfterAttack && unchecked((sbyte)Cached.Attack) < 99)
            { return ExpendedAttack(); }
            if (c.Role == (int)RuleRole.AirSuperiority)
            {
                var (target, distance) = SuperiorityTarget();
                if (target is { } selected)
                { return StrikeOrBase(selected, distance, home, reach); }
            }
            if (c.HomeDistance != 0)
            { return GoHome(); }
            return CityRaid() ?? new(SpecialUnitRoute.Continue);
        }

        // Query scratch is local: only its resulting distance is consumed by this chooser.
        private int NearestOwnedDistance(MapPoint p)
        {
            int best = 9999;
            for (int slot = 0; slot < world.Entities.Cities.End; ++slot)
            {
                ref var city = ref world.Entities.Cities.Storage[slot];
                if (city.Token.Occupied && city.Owner.Value == owner)
                {
                    int d = Distance(p, city.Position);
                    if (d < best)
                    {
                        best = d;
                    }
                }
            }
            return best;
        }

        private SpecialUnitChoice ExpendedAttack()
        {
            int best = unchecked((sbyte)Rule.CostRows * 4);
            UnitSlot? target = null;
            for (int slot = 0; slot < world.Entities.Units.End; ++slot)
            {
                ref var unit = ref world.Entities.Units.Storage[slot];
                if (!unit.Token.Occupied || unit.Owner.Value == owner || !world.Civilizations[owner].Treaties[unit.Owner.Value].War
                    || rules.Units[unit.Type.Value].Domain == RuleDomain.Air || !InBounds(unit.Position))
                { continue; }
                int allowance = MaxMoves(), road = Road, distance = Distance(c.Position, unit.Position);
                if (distance > allowance / road)
                { continue; }
                int score = unchecked((sbyte)rules.Units[unit.Type.Value].CostRows);
                bool special = Fortress(unit.Position) || (world.Map.AirbaseOwner(unit.Position) ?? -1) != 0;
                if (!special && world.Map.CityOwner(unit.Position) is null)
                { score = stacks.ProductionCost(slot, rules); }
                else if (!special)
                {
                    if (stacks.Count(slot) == 1)
                    {
                        score = unchecked(score * 2);
                    }

                    for (int d = 0; d < 8; ++d)
                    {
                        var p = Neighbor(unit.Position, d);
                        if (InBounds(p) && world.Map.UnitOwner(p) == owner)
                        { score = unchecked(score * 2); break; }
                    }
                }
                if (world.Map.TileAt(unit.Position).Ocean)
                {
                    score = unchecked(score * 2);
                }

                if (Fortress(unit.Position))
                {
                    score = unchecked(score * 2);
                }

                score = unchecked(score + (sbyte)rules.Units[unit.Type.Value].Capacity);
                if (score >= best)
                { best = score; target = new((ushort)slot); }
            }
            if (target is { } selected)
            {
                var targetPosition = world.Entities.Units.At(selected).Position;
                for (int d = 0; d < 8; ++d)
                {
                    var p = Neighbor(targetPosition, d);
                    if (!InBounds(p) || world.Map.CityOrUnitOwner(p) is not null)
                    { continue; }
                    Relocate(p);
                    c.Position = p;
                    return new(SpecialUnitRoute.Direction, d ^ 4);
                }
            }
            int count = stacks.CountExpendedAttackers(c.Unit.Value, rules);
            if (count > 1)
            {
                for (int slot = 0; slot < world.Entities.Cities.End; ++slot)
                {
                    ref var city = ref world.Entities.Cities.Storage[slot];
                    if (!city.Token.Occupied || city.Owner.Value != owner
                        || stacks.CountExpendedAttackers(stacks.FirstAt(city.Position), rules) >= count)
                    { continue; }
                    int distance = Distance(c.Position, city.Position), allowance = MaxMoves();
                    if (distance <= allowance / Road)
                    { Relocate(city.Position); c.Position = city.Position; return new(SpecialUnitRoute.Direction); }
                }
            }
            return new(SpecialUnitRoute.Direction);
        }

        private void Relocate(MapPoint p)
        { stacks.Relocate(c.Unit.Value, p, true); Actor.Visibility = default; }

        private (UnitSlot? Target, int Distance) SuperiorityTarget()
        {
            UnitSlot? target = null;
            int best = 9999, selectedDistance = 9999;
            for (int slot = 0; slot < world.Entities.Units.End; ++slot)
            {
                ref var unit = ref world.Entities.Units.Storage[slot];
                if (!unit.Token.Occupied || unit.Owner.Value == owner)
                { continue; }
                var treaty = world.Civilizations[owner].Treaties[unit.Owner.Value];
                if (treaty.Ceasefire || treaty.Peace || !(unit.Visibility.Contains(owner) || unit.Owner.Value == unchecked((byte)owner)))
                { continue; }
                var rule = rules.Units[unit.Type.Value];
                if (rule.Domain != RuleDomain.Air)
                {
                    if (rule.Capacity == 0 ? stacks.Count(slot) > 1 : stacks.CountShips(slot, rules) > 1)
                    { continue; }
                    int attack = unchecked((Actor.Flags.Veteran ? 3 : 2) * (sbyte)Cached.Attack * (sbyte)Cached.HitPoints * (sbyte)Cached.Firepower);
                    int defense = unchecked((sbyte)rule.HitPoints * (sbyte)rule.Defense * (sbyte)rule.Firepower * (unit.Flags.Veteran ? 3 : 2));
                    if (rule.Capacity != 0 && stacks.Count(slot) == 1)
                    {
                        defense = unchecked(defense * 2);
                    }

                    int comparison = unchecked(((defense * (sbyte)rules.Terrain[(int)world.Map.TileAt(unit.Position).Terrain].DefenseFactor) >> 1) * 3);
                    if (comparison > unchecked(attack * 2))
                    { continue; }
                }
                int distance = Distance(c.Position, unit.Position), adjusted = unchecked(2 * distance);
                if (world.Globals.HumanPlayers.Contains(unit.Owner.Value))
                {
                    adjusted >>= 1;
                }

                if (rules.Units[unit.Type.Value].Range != 0)
                {
                    --adjusted;
                }

                if (stacks.Count(slot) == 1)
                {
                    --adjusted;
                }

                if (adjusted <= best)
                { target = new((ushort)slot); best = adjusted; selectedDistance = distance; }
            }
            return (target, selectedDistance);
        }

        private SpecialUnitChoice StrikeOrBase(UnitSlot target, int distance, int home, int reach)
        {
            MapPoint TargetPosition() => world.Entities.Units.At(target).Position;
            if (distance <= reach || reach < home)
            {
                if (home <= reach - distance && distance == 1 || reach < home)
                { return SetGoto(AiTaskIds.Strike, TargetPosition()); }
                int bestReturn = 9999;
                for (int d = 0; d < 8; ++d)
                {
                    var p = Neighbor(TargetPosition(), d);
                    if (!InBounds(p) || world.Map.CityOwner(p) is not null)
                    { continue; }
                    int? occupant = world.Map.UnitOwner(p);
                    if (occupant is not null && occupant != owner || Distance(c.Position, p) > distance - 1)
                    { continue; }
                    int back = NearestOwnedDistance(p);
                    if (back == 9999)
                    {
                        back = 0;
                    }

                    bestReturn = Math.Min(bestReturn, back);
                }
                if (distance + bestReturn <= reach)
                { return SetGoto(AiTaskIds.Strike, TargetPosition()); }
            }
            var objective = TargetPosition();
            for (int slot = 0; slot < world.Entities.Cities.End; ++slot)
            {
                ref var city = ref world.Entities.Cities.Storage[slot];
                if (!city.Token.Occupied || !world.Globals.HumanPlayers.Contains(city.Owner.Value) || !city.Flags.Airlifted)
                { continue; }
                int d = Distance(objective, city.Position);
                var treaty = world.Civilizations[owner].Treaties[city.Owner.Value];
                if (!treaty.Vendetta)
                {
                    if (!treaty.War)
                    {
                        d = unchecked(d * 2);
                    }

                    if (treaty.Peace)
                    {
                        continue;
                    }
                }
                if (d < distance)
                { distance = d; objective = city.Position; }
            }
            int bestBase = 999;
            CitySlot? selected = null;
            for (int slot = 0; slot < world.Entities.Cities.End; ++slot)
            {
                ref var city = ref world.Entities.Cities.Storage[slot];
                if (!city.Token.Occupied || city.Owner.Value != owner || Distance(c.Position, city.Position) > reach)
                { continue; }
                int d = Distance(objective, city.Position);
                if (d < bestBase)
                { bestBase = d; selected = new((ushort)slot); }
            }
            if (selected is { } homeCity)
            { return SetGoto(AiTaskIds.StageAirStrike, world.Entities.Cities.At(homeCity).Position); }
            if (c.HomeDistance != 0)
            { return GoHome(); }
            Actor.AiTask = AiTaskIds.AirHold;
            return new(SpecialUnitRoute.Direction);
        }

        private SpecialUnitChoice? CityRaid()
        {
            if (c.Role != (int)RuleRole.Attack || Rule.Range == 0 || unchecked((sbyte)Cached.Attack) >= 99)
            { return null; }
            if (Goto)
            {
                if (Actor.Counter != 0 || !c.EnemyZone)
                {
                    return null;
                }

                c.HasOrders = true;
                return new(SpecialUnitRoute.PlanMove);
            }
            if (world.Globals.Status.Scenario && world.Scenario.Flags.HistoricalAi && world.Globals.Turn == 1
                && world.Scenario.ObjectiveCiv == owner && world.Map.UnitOwner(new(45, 53)) is not null)
            { SetGoto(AiTaskIds.CityRaid, new(45, 53)); return RaidReturn(); }
            int best = -9999;
            CitySlot? selected = null;
            for (int slot = 0; slot < world.Entities.Cities.End; ++slot)
            {
                ref var city = ref world.Entities.Cities.Storage[slot];
                if (!city.Token.Occupied || city.Owner.Value == owner)
                { continue; }
                var treaty = world.Civilizations[owner].Treaties[city.Owner.Value];
                if (treaty.Ceasefire || treaty.Peace)
                { continue; }
                int distance = Distance(c.Position, city.Position), left = MovesLeft();
                if (distance > left / Road)
                { continue; }
                int first = stacks.FirstAt(city.Position);
                if (first < 0)
                { continue; }
                int size = unchecked((sbyte)city.Size), count = stacks.Count(first);
                int score = unchecked(size * 10 - count * 25);
                if (world.Map.TileAt(city.Position).Region == c.Region)
                {
                    score += 40;
                }

                for (int d = 0; d < 8; ++d)
                {
                    var p = Neighbor(city.Position, d);
                    if (InBounds(p) && world.Map.UnitOwner(p) == owner)
                    {
                        score += 60;
                    }
                }
                if (score >= best)
                { best = score; selected = new((ushort)slot); }
            }
            if (selected is not { } target)
            {
                return null;
            }

            SetGoto(AiTaskIds.CityRaid, world.Entities.Cities.At(target).Position);
            return RaidReturn();
        }

        private SpecialUnitChoice RaidReturn()
        {
            if (c.EnemyZone)
            {
                c.HasOrders = true;
            }

            return new(c.HasOrders ? SpecialUnitRoute.PlanMove : SpecialUnitRoute.Finish);
        }
    }
}
