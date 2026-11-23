using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public sealed partial class GroundMovementPlanner
{
    private IEnumerable<UnitMissionDecision?> Foreign(Operation operation, UnitTacticsContext c, Candidate q, Scores scores, int k, string call)
    {
        q.Attack = true;
        var target = q.Unit!.Value;
        int other = q.Owner ?? throw new InvalidOperationException("A04 foreign stack has no occupant owner");
        if (c.SettlerType || Rule(c.Unit).Role == RuleRole.Trade)
        { yield break; }
        if (c.Domain == RuleDomain.Sea && Rule(c.Unit).Capacity != 0)
        {
            if (!q.Ocean && unchecked((sbyte)Rule(c.Unit).Attack) < 3)
            { yield break; }
            if (stacks.Count(c.Unit.Value) > 1 && (unchecked((sbyte)Rule(c.Unit).Attack) <= unchecked((sbyte)Rule(target).Defense)
                || unchecked((sbyte)Rule(target).Attack) <= unchecked((sbyte)Rule(c.Unit).Defense)))
            { yield break; }
        }
        if (world.Map.TileAt(c.Position).Ocean && c.Domain == RuleDomain.Land && !Rule(c.Unit).Abilities.Amphibious)
        { yield break; }
        var treaty = world.Civilizations[c.Owner].Treaties[other];
        bool attack = false;
        if (!treaty.Ceasefire && !treaty.Peace)
        {
            if (!world.Globals.HumanPlayers.Contains(other))
            { attack = true; }
            else if (c.Role != (int)RuleRole.Diplomacy)
            {
                int reputation = world.Civilizations[other].Reputation;
                int attitude = DiplomaticAttitudes.Get(world, c.Owner, other);
                attack = 5 * reputation + attitude + Roll("ground-attack-attitude", call, k, 19) - 10 > 50;
            }
        }
        if (attack)
        {
            target = strength.BestDefender(target, c.Unit) ?? throw new InvalidOperationException("A04 foreign stack has no combat defender");
            q.Unit = target;
            if (Rule(target).Domain == RuleDomain.Air && Rule(target).Range != 0 && !Rule(c.Unit).Abilities.AttackAir
                && world.Map.CityOwner(q.Point) is null && world.Map.AirbaseOwner(q.Point) is null
                && stacks.CountAircraftCarriers(target.Value, rules) == 0)
            { yield break; }
            if (Rule(target).Role == RuleRole.Trade && c.HomeCity == c.NearestCity)
            {
                if (!(world.Civilizations[c.Owner].Government < world.Civilizations[other].Government || world.Civilizations[c.Owner].Treaties[other].Vendetta))
                { yield break; }
                int range = (int)world.Civilizations[other].Government - (int)world.Civilizations[c.Owner].Government;
                if (Roll("ground-trade-government", call, k, range) == 0)
                { yield break; }
            }
            int value = AttackValue(c, q, target, k);
            if (value >= (other == 0 ? 12 : 6))
            { q.Score = unchecked(q.Score + 4 * value); q.Continue = true; yield break; }
            q.Score = -999;
            if (c.Policy == (int)RuleRole.Attack && c.Role == (int)RuleRole.Attack && c.Domain == RuleDomain.Land
                && (Unit(target).Flags.Veteran || Unit(target).Orders == UnitOrder.Fortified)
                && (unchecked((sbyte)rules.Terrain[(int)q.Terrain].DefenseFactor) > 3 || Fortress(q.Point))
                && stacks.Count(target.Value) < 2 && world.Map.CityOwner(q.Point) is null
                && Roll("ground-fallback-bribe", call, k, 3) == 0)
            {
                foreach (var pending in Bribe(operation, target, c.Owner, call, k))
                { yield return pending; }
            }
            yield break;
        }
        if ((treaty.Ceasefire || treaty.Peace) && Rule(target).Role == RuleRole.Diplomacy
            && stacks.Count(target.Value) == 1 && Rule(c.Unit).Attack != 0)
        {
            var city = CityQueries.Nearest(world, q.Point).Slot ?? throw new InvalidOperationException("A04 peaceful diplomat requires nearest city backing");
            if (world.Entities.Cities.At(city).Owner.Value == c.Owner)
            {
                if (scores.Best < 60)
                { scores.Best = scores.Unscaled = 60; scores.Direction = k; scores.Attack = true; scores.AttackClass = 2; }
                yield break;
            }
        }
        if (!(c.Role == (int)RuleRole.Diplomacy && (c.Policy == (int)RuleRole.Attack || c.Policy == (int)RuleRole.Settle || !treaty.Ceasefire && !treaty.Peace)
            || c.Policy == (int)RuleRole.Attack && c.Role == (int)RuleRole.Attack))
        { yield break; }
        if (!((c.Role == (int)RuleRole.Diplomacy || c.HomeCity == c.NearestCity) && world.Globals.Difficulty > Difficulty.Prince))
        {
            if (Unit(target).Orders != UnitOrder.Fortified)
            { yield break; }
            bool hostile = false;
            for (int human = 1; human < EntityLimits.Civilizations; ++human)
            { if (world.Globals.HumanPlayers.Contains(human)) { hostile |= DiplomaticAttitudes.Hostile(world, other, human); } }
            if (!hostile)
            { yield break; }
        }
        if (c.Domain != RuleDomain.Land)
        { yield break; }
        if (!world.Globals.HumanPlayers.Contains(other))
        {
            bool peaceful = false;
            for (int human = 1; human < EntityLimits.Civilizations; ++human)
            { if (world.Globals.HumanPlayers.Contains(human)) { peaceful |= !DiplomaticAttitudes.Hostile(world, other, human); } }
            if (peaceful)
            { yield break; }
        }
        for (int human = 1; human < EntityLimits.Civilizations; ++human)
        { if (world.Globals.HumanPlayers.Contains(human) && world.Civilizations[human].ContinentCities[c.Region] != 0) { yield break; } }
        if (stacks.Count(target.Value) >= 2 || world.Map.CityOwner(q.Point) is not null)
        { yield break; }
        if (Roll("ground-opportunistic-bribe", call, k, 7) == 0
            || c.NearestCity is { } nearest && world.Globals.HumanPlayers.Contains(world.Entities.Cities.At(nearest).Owner.Value))
        { foreach (var pending in Bribe(operation, target, c.Owner, call, k)) { yield return pending; } }
    }
    private IEnumerable<UnitMissionDecision?> Bribe(Operation operation, UnitSlot target, int owner, string call, int direction)
    {
        using var child = espionage.Bribe(target.Value, owner, call + "/bribe/" + direction);
        while (child.Result is null)
        {
            var pending = child.Resume(operation.TakeReply());
            if (child.Result is null)
            { yield return pending; }
        }
    }
    private int AttackValue(UnitTacticsContext c, Candidate q, UnitSlot target, int direction)
    {
        unchecked
        {
            int value = combat.Preview(c.Unit, direction);
            int cost = stacks.ProductionCost(target.Value, rules), actorCost = (sbyte)Rule(c.Unit).CostRows;
            value = (cost + 1) * value / actorCost;
            if (world.Map.CityOwner(q.Point) is not null)
            { value *= 3; }
            if (c.Role == (int)RuleRole.NavalSuperiority && Rule(target).Role == RuleRole.SeaTransport)
            { value *= 2; }
            if (!c.Guard)
            {
                if (c.Role == (int)RuleRole.Attack)
                {
                    if (c.Policy == (int)RuleRole.Attack)
                    { value *= 2; }
                    int attack = (sbyte)Rule(c.Unit).Attack;
                    if (stacks.Attack(c.Unit.Value, rules) > 2 * attack)
                    { value *= 2; q.AttackClass = 1; }
                    if (CountRole(target, RuleRole.Attack) != 0)
                    { value *= 2; q.AttackClass = 1; }
                    if (SharedGoals.PriorityAt(world.Civilizations[c.Owner], c.Position, (sbyte)RuleRole.Attack) != 0)
                    { value *= 2; }
                }
                if (world.Globals.Rules.NormalCombat)
                {
                    int maximum = UnitMovement.MaximumHitPoints(rules, Unit(target));
                    if (UnitMovement.RemainingHitPoints(world, rules, ref Unit(target)) <= maximum / 3)
                    { value *= 2; q.AttackClass = 1; }
                }
                if (Rule(target).Abilities.Submarine)
                { value *= 3; }
            }
            else
            {
                value /= 2;
                if ((sbyte)Rule(target).Attack <= (sbyte)Rule(c.Unit).Defense)
                { value = 0; }
            }
            if (c.Type == UnitTypeIds.Partisans && (sbyte)Rule(target).Role > 4)
            { q.AttackClass = 2; }
            var role = Rule(target).Role;
            if (q.Owner == 0 && role == RuleRole.Trade)
            { if (!world.Globals.HumanPlayers.Contains(0)) { value /= 2; } }
            else if (q.Owner == 0 && (sbyte)role > 4)
            { value += 12; }
            else if (role == RuleRole.Diplomacy)
            { value *= 2; }
            return value;
        }
    }
}
