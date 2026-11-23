using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public sealed partial class NavalGoals
{
    public void Other(NavalTransportContext state, string call)
    {
        ArgumentException.ThrowIfNullOrEmpty(call);
        MapKnowledge.RequireLocal(world);
        var c = state.Tactics;
        if (!HasGoto(c))
        {
            int best = 999;
            var originRegion = world.Map.LandRegion(c.Position);
            for (int slot = 0; slot < world.Entities.Cities.End; ++slot)
            {
                ref var city = ref world.Entities.Cities.Storage[slot];
                if (!city.Token.Occupied)
                { continue; }
                state.CivilizationCursor = city.Owner.Value;
                if (city.Owner.Value != c.Owner)
                { continue; }
                int distance = Distance(c.Position, city.Position);
                int region = world.Map.TileAt(city.Position).Region;
                if (distance <= 7 || region == originRegion || Civ(c).ContinentPolicy[region] == 4
                    || !CityConnected(c, new((ushort)slot)))
                { continue; }
                if (!state.Roles.Contains(Civ(c).ContinentPolicy[region]))
                { distance *= 2; }
                if (distance >= best)
                { continue; }
                best = distance;
                UnitOrders.SetGoto(ref Actor(c), AiTaskIds.AnotherLandmassPort, city.Position);
            }
        }
        if (!HasGoto(c) && HasHold(c))
        {
            var target = Actor(c).Destination;
            if (Actor(c).AiTask != AiTaskIds.ExploreRegion)
            {
                int y = random.SecondaryRange(new ChoiceKey("A05.explore-y", call), 0, world.Map.Geometry.Height - 1);
                int x = random.SecondaryRange(new ChoiceKey("A05.explore-x", call), 0, world.Map.Geometry.Width - 1);
                x -= x & 1;
                x += y & 1;
                target = new(x, y);
            }
            if (world.Map.LandRegion(target) is { } region && region is > 0 and < 63
                && world.Map.Regions[region].Fertile > 16 && region != world.Map.LandRegion(c.Position)
                && Civ(c).ContinentCities[region] == 0 && LandConnected(c, region))
            { targets.NearShoreGoto(c.Unit, AiTaskIds.ExploreRegion, target); }
        }
        if (HasGoto(c))
        { return; }
        for (int radius = 2; radius <= 23; ++radius)
        {
            int direction = random.SecondaryRange(new ChoiceKey("A05.explore-ray", call, Attempt: radius), 0, 7);
            var delta = MapNeighborhoods.Directions[direction];
            var p = new MapPoint(world.Map.Geometry.WrapX(unchecked(c.Position.X + radius * delta.X), world.Globals.Rules.FlatWorld),
                unchecked(c.Position.Y + radius * delta.Y));
            if (!world.Map.Geometry.InBounds(p) || p.Y <= 1 || p.Y > world.Map.Geometry.Height - 3 || world.Map.TileAt(p).Ocean)
            { continue; }
            int region = world.Map.TileAt(p).Region;
            if (Civ(c).ContinentCities[region] == 0 && LandConnected(c, region))
            { targets.NearShoreGoto(c.Unit, AiTaskIds.ExploreNearby, p); }
        }
    }
}
