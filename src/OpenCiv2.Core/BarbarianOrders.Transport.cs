using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed partial class BarbarianOrders
{
    private IEnumerable<BarbarianOrderDecision?> Transport(Operation op, Context c, string call)
    {
        int count = stacks.Count(c.Unit.Value), leaders = stacks.CountRole(c.Unit.Value, (int)RuleRole.Diplomacy, rules);
        if (count - leaders < 2)
        {
            foreach (var pending in Delete(op, c, call + "/thin-transport"))
            { yield return pending; }
            op.Result = 1;
            yield break;
        }
        Actor(c).Counter = unchecked((byte)(Actor(c).Counter + 1));
        if (unchecked((sbyte)Actor(c).Counter) > 30)
        {
            foreach (var pending in Delete(op, c, call + "/old-transport"))
            { yield return pending; }
            op.Result = 1;
            yield break;
        }
        for (int d = 0; d < 8; ++d)
        {
            var point = Offset(c.Origin, d);
            if (!world.Map.Geometry.InBounds(point))
            { continue; }
            if (c.City is { } city && c.Near < 9 && !world.Map.TileAt(point).Ocean
                && world.Map.CityOwner(point) is null && world.Map.UnitOwner(point) is null
                && point.Y > 1 && point.Y < world.Map.Geometry.Height - 2
                && world.Map.TileAt(point).Region == world.Map.TileAt(world.Entities.Cities.At(city).Position).Region)
            {
                if (stacks.Count(c.Unit.Value) < 3 && stacks.CountRole(c.Unit.Value, (int)RuleRole.Attack, rules) == 0
                    && stacks.CountRole(c.Unit.Value, (int)RuleRole.Defend, rules) == 0 && c.Near < 3)
                {
                    foreach (var pending in Delete(op, c, call + "/empty-landing"))
                    { yield return pending; }
                }
                else
                { AnnounceLanding(op, c, point); }
                op.Result = Finish(c);
                yield break;
            }
            if (world.Map.TileAt(point).Ocean && world.Map.UnitOwner(point) is > 0)
            { op.Result = Direction(c, d); yield break; }
        }
        if (Actor(c).Orders == UnitOrder.GoTo)
        { op.Result = Finish(c); yield break; }
        int best = 0;
        CitySlot? selected = null;
        int? body = world.Map.TileAt(c.Origin).Ocean ? world.Map.TileAt(c.Origin).Region : null;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            var city = new CitySlot((ushort)i);
            if (!world.Entities.Cities.At(city).Token.Occupied || world.Entities.Cities.At(city).Owner.Value == 0
                || body is { } water && !world.Map.Regions[world.Map.TileAt(world.Entities.Cities.At(city).Position).Region].NeighboringWater.Contains(water))
            { continue; }
            int share = CityCaptureQueries.TreasuryShare(world, city);
            int distance = world.Map.Geometry.WeightedDistance(c.Origin, world.Entities.Cities.At(city).Position, world.Globals.Rules.FlatWorld);
            int score = unchecked(share + 50) / unchecked(distance + 1);
            if (score > best)
            { best = score; selected = city; }
        }
        if (selected is not { } target)
        {
            foreach (var pending in Delete(op, c, call + "/no-target"))
            { yield return pending; }
            op.Result = 1;
            yield break;
        }
        new NavalTargets(world).NearShoreGoto(c.Unit, AiTaskIds.RaidTransport, world.Entities.Cities.At(target).Position);
        op.Result = Finish(c);
    }
    private void AnnounceLanding(Operation op, Context c, MapPoint point)
    {
        for (int v = 1; v < EntityLimits.Civilizations; ++v)
        {
            if (!world.Globals.CivsAlive.Contains(v) || !world.Globals.HumanPlayers.Contains(v)
                || !world.Map.TileAt(point).RevealedTo.Contains(v)
                || c.City is not { } city || world.Entities.Cities.At(city).Owner.Value != v
                || world.Multiplayer.Saved.AutoplayActive)
            { continue; }
            Nearest(c, point);
            var nearest = c.City ?? throw new InvalidOperationException("Barbarian landing has no nearest city");
            string name = world.Entities.Cities.At(nearest).Name;
            if (v == world.Multiplayer.LocalHuman)
            {
                if (world.Globals.Multiplayer == GameMode.SinglePlayer)
                {
                    stacks.MarkSeen(c.Unit.Value, v);
                    op.Present(() => effects.EnsureVisible(point, c.Owner));
                    op.Present(() => effects.Redraw(point));
                }
                var notice = new BarbarianLandingNotice(v, new(c.Unit, Actor(c).Token), name, point);
                op.Present(() => effects.Landing(notice));
            }
        }
        Actor(c).Visibility = Actor(c).Visibility.Union(world.Map.TileAt(point).RevealedTo);
        Actor(c).AiTask = AiTaskIds.UnloadRaid;
        UnitOrders.WakeByRole(world, rules, stacks, c.Unit.Value, new(ushort.MaxValue), false);
        UnitMovement.Exhaust(world, rules, ref Actor(c));
    }
}
