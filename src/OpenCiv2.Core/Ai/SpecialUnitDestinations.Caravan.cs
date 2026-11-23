using OpenCiv2.Contracts;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public sealed partial class SpecialUnitDestinations
{
    private SpecialUnitChoice? ChooseCaravan(UnitTacticsContext c)
    {
        if (HasGoto(c))
        { return new(SpecialUnitRoute.Finish); }
        if (world.Map.TileAt(c.Position).Ocean)
        {
            int landingBest = 0, selected = -1;
            for (int direction = 0; direction < 8; ++direction)
            {
                var p = Neighbor(c.Position, direction);
                if (!world.Map.Geometry.InBounds(p) || world.Map.TileAt(p).Ocean
                    || Civ(c).ContinentFlags[world.Map.TileAt(p).Region] == default)
                { continue; }
                int score = 0;
                for (int other = 1; other <= 7; ++other)
                {
                    int size = Civ(c).ContinentCitySize[c.Region];
                    score += other == c.Owner ? (size + 1) >> 1 : 2 * size;
                }
                if (score > landingBest)
                { landingBest = score; selected = direction; }
            }
            if (selected >= 0)
            { Actor(c).AiTask = AiTaskIds.TradeLanding; return new(SpecialUnitRoute.Direction, selected); }
        }
        CitySlot? home = Actor(c).Home ?? c.HomeCity;
        if (home is null)
        { return null; }
        int best = 0;
        CitySlot? destination = null;
        for (int slot = 0; slot < world.Entities.Cities.End; ++slot)
        {
            ref var city = ref world.Entities.Cities.Storage[slot];
            if (!city.Token.Occupied || world.Map.TileAt(city.Position).Region != c.Region)
            { continue; }
            ref var source = ref world.Entities.Cities.Storage[home.Value.Value];
            bool newRoute = true;
            for (int route = 0; route < unchecked((sbyte)source.RouteCount); ++route)
            {
                if (route >= 3)
                { throw new InvalidOperationException("A07 caravan route read exceeds the supported three partners"); }
                if (source.RoutePartners[route] == new CitySlot((ushort)slot))
                { newRoute = false; }
            }
            int score = unchecked((source.BaseTrade + city.BaseTrade) * Distance(city.Position, source.Position)) / 24;
            if (city.Owner.Value == c.Owner)
            { score /= 2; }
            else
            { score = unchecked((200 - Math.Clamp(DiplomaticAttitudes.Get(world, c.Owner, city.Owner.Value), 0, 100)) * score) / 100; }
            if (!newRoute)
            { score /= 2; }
            score = unchecked((5 - unchecked((sbyte)city.RouteCount)) * score) / 5;
            if (c.NearestCity is { } nearest && c.HomeCity != c.NearestCity && c.Region == c.NearestRegion
                && city.Owner != world.Entities.Cities.At(nearest).Owner)
            {
                score /= 3;
                if (world.Globals.HumanPlayers.Contains(world.Entities.Cities.At(nearest).Owner.Value))
                { score /= 2; }
            }
            if (score > best)
            { best = score; destination = new((ushort)slot); }
        }
        if (destination is { } target)
        { UnitOrders.SetGoto(ref Actor(c), AiTaskIds.CityTarget, world.Entities.Cities.At(target).Position); return new(SpecialUnitRoute.Finish); }
        if (Actor(c).Home is { } actualHome && world.Map.TileAt(world.Entities.Cities.Storage[actualHome.Value].Position).Region == c.Region)
        { UnitOrders.SetGoto(ref Actor(c), AiTaskIds.CityTarget, world.Entities.Cities.Storage[actualHome.Value].Position); return new(SpecialUnitRoute.Finish); }
        return new(SpecialUnitRoute.Continue);
    }
}
