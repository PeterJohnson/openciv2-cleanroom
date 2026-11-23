using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed partial class BarbarianOrders
{
    private int Roll(string site, string call, int direction, int maximum) =>
        random.SecondaryRange(new ChoiceKey("W06.order-" + site, call, direction), 0, maximum);
    private IEnumerable<BarbarianOrderDecision?> Neighbors(Operation op, Context c, string call)
    {
        int best = -999, selected = 8;
        for (int d = 0; d < 8; ++d)
        {
            var point = Offset(c.Origin, d);
            if (!world.Map.Geometry.InBounds(point) || (Rule(c).Domain == RuleDomain.Sea) != world.Map.TileAt(point).Ocean)
            { continue; }
            int? occupant = world.Map.UnitOwner(point);
            int first = stacks.FirstAt(point);
            int? firstOwner = first < 0 ? null : world.Entities.Units.Storage[first].Owner.Value;
            int score = 0;
            if (Role(c) < 5)
            {
                if (world.Map.CityOwner(point) is { } other && other != c.Owner)
                {
                    if (world.Map.TileAt(c.Origin).Ocean)
                    { continue; }
                    if (world.Map.UnitOwner(point) is null)
                    {
                        using var child = ransom.Begin(c.Unit, point, c.City, call + "/ransom-" + d);
                        while (child.Result is null)
                        {
                            var pending = child.Resume(op.TakeReply());
                            if (pending is not null)
                            { yield return new BarbarianOrderDecision.Ransom(pending); }
                            else if (child.Result is null)
                            { yield return null; }
                        }
                        op.Report(child.PresentationFailures);
                        c.City = child.City;
                        if (child.Result == BarbarianRansomOutcome.Paid)
                        { op.Result = 1; yield break; }
                        if (child.Result == BarbarianRansomOutcome.Finish)
                        { op.Result = Finish(c); yield break; }
                    }
                    if (c.Type != UnitTypeIds.Partisans || world.Map.UnitOwner(point) is null)
                    { op.Result = Direction(c, d); yield break; }
                    if (((world.Globals.Turn + c.Unit.Value) & 7) == 0)
                    { continue; }
                    if (stacks.Count(c.Unit.Value) < 2)
                    { Actor(c).Orders = UnitOrder.Fortifying; }
                    if (best < 99)
                    { best = 99; selected = 8; }
                }
                if (occupant != c.Owner)
                { score = 4; }
                if (world.Map.TileAt(point).Features.HasFlag(MapFeatures.Fortification))
                { score += 8; }
                else if ((world.Map.TileAt(point).Features & (MapFeatures.Irrigation | MapFeatures.Mine)) != 0)
                { score += 6; }
                score += Roll("military-neighbor", call, d, 5);
                if (first >= 0)
                {
                    if (firstOwner == c.Owner)
                    {
                        if (!world.Map.TileAt(c.Origin).Ocean)
                        { continue; }
                        score -= 20;
                    }
                    else
                    {
                        if (world.Map.TileAt(c.Origin).Ocean)
                        { continue; }
                        int role = unchecked((sbyte)rules.Units[world.Entities.Units.Storage[first].Type.Value].Role);
                        if (c.Type == UnitTypeIds.Partisans)
                        {
                            if (role < 5)
                            { continue; }
                            score += 99;
                        }
                        else if (role != (int)RuleRole.Diplomacy)
                        { score += 99; }
                    }
                }
                if (c.Zone && Role(c) == (int)RuleRole.Attack && first < 0
                    && SpatialControl.InEnemyZone(world, point, c.Owner) is not null
                    && !rules.Units[Actor(c).Type.Value].Abilities.IgnoreZonesOfControl)
                { continue; }
                if (unchecked((sbyte)Actor(c).LastDirection) == d)
                { score += 6; }
                var delta = MapNeighborhoods.Directions[d];
                if (c.PursuitX != -3 && c.PursuitX == Math.Sign(delta.X))
                { score += 2; }
                if (c.PursuitY != -3 && c.PursuitY == Math.Sign(delta.Y))
                { score += 2; }
            }
            else
            {
                score = first < 0 ? 0 : firstOwner == c.Owner ? 99 : -99;
                c.Near = CityQueries.Nearest(world, point).Distance;
                int movement = unchecked((sbyte)rules.Terrain[(int)world.Map.TileAt(point).Terrain].MovementCost);
                score = unchecked(score + movement + Roll("leader-neighbor", call, d, 3) + 4 * c.Near);
            }
            if (score >= best)
            { best = score; selected = d; }
        }
        if (world.Map.CityOwner(c.Origin) is not null && stacks.Count(c.Unit.Value) <= 1 && best < 99)
        { op.Result = Direction(c, 8); yield break; }
        if (best < 99 && UnitMovement.Left(world, rules, Actor(c)) <= rules.Cosmic.RoadMovementMultiplier
            && c.City is { } city && world.Globals.HumanPlayers.Contains(world.Entities.Cities.At(city).Owner.Value))
        {
            var features = world.Map.TileAt(c.Origin).Features;
            if ((features & (MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Fortification)) != 0
                || c.Type == UnitTypeIds.Partisans && features.HasFlag(MapFeatures.Road))
            {
                using var child = pillage.Begin(c.Unit);
                while (!child.Completed)
                {
                    if (child.Resume(op.TakeReply()) is { } pending)
                    { yield return new BarbarianOrderDecision.Pillage(pending); }
                }
                op.Report(child.PresentationFailures);
                UnitMovement.Exhaust(world, rules, ref Actor(c));
                op.Result = Finish(c);
                yield break;
            }
        }
        op.Result = Direction(c, selected);
    }
}
