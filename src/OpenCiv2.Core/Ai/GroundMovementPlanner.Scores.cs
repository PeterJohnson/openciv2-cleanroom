using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public sealed partial class GroundMovementPlanner
{
    private bool BaseScore(UnitTacticsContext c, Candidate q, int direction, string call)
    {
        unchecked
        {
            if (c.SettlerType)
            {
                if (c.Terrain == Terrain.Ocean && Adjacent(q.Point, c.Owner, true))
                { return false; }
                q.Score = Roll("ground-base", call, direction, 4);
                return true;
            }
            if (Unit(c.Unit).Visibility == default && c.Terrain != Terrain.Ocean)
            {
                q.Score = Roll("ground-base", call, direction, 2);
                q.Score -= c.Role == (int)RuleRole.Attack ? 2 * (sbyte)rules.Terrain[(int)q.Terrain].MovementCost : (sbyte)rules.Terrain[(int)q.Terrain].DefenseFactor;
                return true;
            }
            q.Score = Roll("ground-base", call, direction, 4);
            if (q.Unit is not { } unit || q.Owner != c.Owner)
            {
                if (c.Role < 6)
                { q.Score += 4 * (sbyte)rules.Terrain[(int)q.Terrain].DefenseFactor; }
                else
                {
                    q.Score += 6 - ((sbyte)rules.Terrain[(int)q.Terrain].MovementCost - 1) * rules.Cosmic.RoadMovementMultiplier;
                    if (c.NearestDistance < 4 && c.HomeCity != c.NearestCity)
                    {
                        var nearest = c.NearestCity ?? throw new InvalidOperationException("A04 nonmilitary score requires nearest city");
                        int distance = world.Map.Geometry.StepDistance(q.Point, world.Entities.Cities.At(nearest).Position, world.Globals.Rules.FlatWorld);
                        int cost = (sbyte)rules.Terrain[(int)q.Terrain].MovementCost;
                        if (distance == 1)
                        { q.Score += cost <= rules.Cosmic.RoadMovementMultiplier ? 8 : -4; }
                        else if (distance == 2 && cost <= rules.Cosmic.RoadMovementMultiplier && world.Map.TileAt(q.Point).Features.HasFlag(MapFeatures.Road))
                        { q.Score += 4; }
                    }
                }
                return true;
            }
            var features = world.Map.TileAt(q.Point).Features;
            if (features.HasFlag(MapFeatures.Unit) && (features & (MapFeatures.CitySite | MapFeatures.Fortification)) == 0 && Adjacent(q.Point, c.Owner))
            {
                switch (c.Role)
                {
                    case (int)RuleRole.Attack:
                        int attack = CountRole(unit, RuleRole.Attack);
                        q.Score += attack < 1 && CountRole(unit, RuleRole.Defend) != 0 ? 8 : -8 * attack;
                        break;
                    case (int)RuleRole.Defend:
                        int defend = CountRole(unit, RuleRole.Defend);
                        q.Score += defend < 1 ? 8 : -4 * defend;
                        break;
                    case (int)RuleRole.NavalSuperiority:
                        if (CountRole(unit, RuleRole.SeaTransport) != 0)
                        { q.Score += 8; }
                        q.Score -= 4 * CountRole(unit, RuleRole.NavalSuperiority);
                        break;
                    case (int)RuleRole.SeaTransport:
                        if (CountRole(unit, RuleRole.NavalSuperiority) != 0)
                        { q.Score += 8; }
                        q.Score -= 8 * CountRole(unit, RuleRole.SeaTransport);
                        break;
                    case (int)RuleRole.Settle:
                    case AiGoalRoles.FoundCity:
                        q.Score -= 4;
                        break;
                    case (int)RuleRole.Diplomacy:
                        if (CountRole(unit, RuleRole.Attack) + CountRole(unit, RuleRole.Defend) != 0)
                        { q.Score += 8; }
                        break;
                }
            }
            else
            {
                switch (c.Role)
                {
                    case (int)RuleRole.Attack:
                        int cost = stacks.ProductionCost(unit.Value, rules), count = stacks.Count(unit.Value);
                        q.Score += 4 * cost / (count + 1);
                        break;
                    case (int)RuleRole.Defend:
                        int attack = stacks.Attack(unit.Value, rules), production = stacks.ProductionCost(unit.Value, rules);
                        q.Score += 2 * attack / (production + 1);
                        break;
                    case (int)RuleRole.Settle:
                        int defense = (sbyte)rules.Terrain[(int)q.Terrain].DefenseFactor;
                        q.Score += 2 * (defense + stacks.ProductionCost(unit.Value, rules));
                        break;
                    case (int)RuleRole.Diplomacy:
                        if (CountRole(unit, RuleRole.Attack) + CountRole(unit, RuleRole.Defend) != 0)
                        { q.Score += 8; }
                        break;
                }
            }
            return true;
        }
    }
    private bool Territory(UnitTacticsContext c, Candidate q)
    {
        unchecked
        {
            bool withdrew = false;
            if (q.Unit is null && !q.Ocean && (sbyte)Rule(c.Unit).Role < 5)
            {
                if (c.NearestDistance < 4)
                {
                    var nearest = c.NearestCity ?? throw new InvalidOperationException("A04 territory score requires nearest city");
                    int other = world.Entities.Cities.At(nearest).Owner.Value;
                    var treaty = world.Civilizations[c.Owner].Treaties[other];
                    if (other != c.Owner && (treaty.Ceasefire || treaty.Peace) && !treaty.Alliance
                        && (treaty.Peace || DiplomaticAttitudes.Get(world, c.Owner, other) < 50))
                    {
                        int distance = world.Map.Geometry.WeightedDistance(world.Entities.Cities.At(nearest).Position, q.Point, world.Globals.Rules.FlatWorld);
                        if (distance > c.NearestDistance && c.NearestDistance < 3)
                        { q.Score += 5; withdrew = true; }
                        if (distance < c.NearestDistance)
                        { return false; }
                        if (distance == c.NearestDistance)
                        { q.Score -= 5; }
                    }
                }
                int claimant = world.Map.TileAt(q.Point).Claimant;
                if (claimant > 0 && claimant != c.Owner)
                {
                    var treaty = world.Civilizations[c.Owner].Treaties[claimant];
                    if ((treaty.Ceasefire || treaty.Peace) && !treaty.Alliance)
                    {
                        int Age() => world.Civilizations[claimant].Reputation - world.Civilizations[c.Owner].ReputationCredit[c.Owner];
                        if (treaty.Peace && Age() < 7 && !withdrew)
                        { return false; }
                        if (treaty.Peace && Age() < 7 || DiplomaticAttitudes.Get(world, c.Owner, claimant) < 50)
                        { q.Score -= 5; }
                        if ((world.Map.TileAt(q.Point).Features & (MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Road)) != 0)
                        { q.Score -= 3; }
                    }
                }
            }
            bool penalty = q.Owner switch
            {
                null => (world.Civilizations[c.Owner].SubversionCount & 15) >= 8,
                >= 0 and < 8 => world.Civilizations[c.Owner].Treaties[q.Owner.Value].Alliance,
                8 => (world.Civilizations[c.Owner].Attitude[0] & 15) >= 8,
                _ => throw new InvalidOperationException("A04 occupant owner has no supported treaty meaning")
            };
            if (penalty)
            { q.Score -= 6; }
            if (q.Unit is null)
            {
                if (world.Map.CityOwner(q.Point) is not null && q.Owner != c.Owner)
                {
                    int other = q.Owner ?? throw new InvalidOperationException("A04 empty city has no occupant owner");
                    var treaty = world.Civilizations[c.Owner].Treaties[other];
                    if ((treaty.Ceasefire || treaty.Peace || treaty.Alliance)
                        && (world.Civilizations[other].Reputation < 7 || !treaty.Vendetta || treaty.Alliance))
                    { return false; }
                    q.Score = 999;
                    c.HasOrders = c.Hold = false;
                }
                if (world.Map.Hut(q.Point))
                { q.Score += 20; }
            }
            return true;
        }
    }
}
