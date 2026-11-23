using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public sealed partial class SpecialUnitDestinations
{
    private SpecialUnitChoice? ChooseDiplomat(UnitTacticsContext c)
    {
        if (HasGoto(c) && CityQueries.At(world, Actor(c).Destination) is { } target)
        {
            int owner = world.Entities.Cities.At(target).Owner.Value;
            var relation = Civ(c).Treaties[owner];
            if (owner != c.Owner && !relation.Ceasefire && !relation.Peace && !relation.Alliance)
            { c.HasOrders = true; return new(SpecialUnitRoute.PlanMove); }
        }
        if (world.Map.TileAt(c.Position).Ocean)
        { return new(SpecialUnitRoute.PlanMove); }
        bool raceBehind = SpaceshipQueries.RaceBehind(world, rules, c.Owner);
        int best = -999;
        CitySlot? destination = null;
        for (int slot = 0; slot < world.Entities.Cities.End; ++slot)
        {
            ref var city = ref world.Entities.Cities.Storage[slot];
            if (!city.Token.Occupied)
            { continue; }
            int region = world.Map.TileAt(city.Position).Region;
            if (!world.Map.TileAt(c.Position).Ocean)
            { if (region != c.Region) { continue; } }
            else
            {
                bool connected = false;
                for (int direction = 0; direction < 8; ++direction)
                {
                    var p = Neighbor(c.Position, direction);
                    if (world.Map.Geometry.InBounds(p) && !world.Map.TileAt(p).Ocean && world.Map.TileAt(p).Region == region)
                    { connected = true; break; }
                }
                if (!connected)
                { continue; }
            }
            int other = city.Owner.Value;
            int distance = Distance(c.Position, city.Position);
            if ((Actor(c).Home?.Value ?? 255) % 3 != slot % 3
                && distance > 3 + (world.Map.TileAt(c.Position).Features.HasFlag(MapFeatures.Road) ? 1 : 0))
            { distance += 8; }
            int score;
            if (other == c.Owner)
            { score = 99 - distance; }
            else
            {
                score = 100;
                var treaty = Civ(c).Treaties[other];
                ref var foreign = ref world.Civilizations[other];
                if (treaty.Ceasefire || treaty.Peace || treaty.Alliance)
                {
                    if (!world.Globals.HumanPlayers.Contains(other) || city.Flags.TechnologyStolen)
                    { continue; }
                    if (!SpaceshipQueries.RaceBehind(world, rules, c.Owner))
                    {
                        if (world.Globals.PowerRank[other] < world.Globals.PowerRank[c.Owner]
                            || Actor(c).Type.Value != UnitTypeIds.Spy && foreign.NumAdvances - Civ(c).NumAdvances < 10
                                && (Civ(c).Government > Government.Fundamentalism || world.Globals.PowerRank[other] < 7
                                    || treaty.Alliance && foreign.NumAdvances < Civ(c).NumAdvances)
                            || !treaty.Alliance && foreign.Government > Government.Fundamentalism
                                && Civ(c).ContinentMilitary[c.Region] < foreign.ContinentAttack[c.Region] && Civ(c).ContinentCities[c.Region] != 0)
                        { continue; }
                    }
                    score /= 2;
                }
                score += treaty.War ? 100 : DiplomaticAttitudes.Get(world, c.Owner, other);
                score /= distance + 1;
                if (city.Flags.TechnologyStolen)
                { score /= 2; }
                if (Civ(c).NumAdvances + 6 < foreign.NumAdvances)
                { score /= 2; }
                if (raceBehind)
                { score = 1; }
                score += 100;
            }
            if (score > best)
            { best = score; destination = new((ushort)slot); }
        }
        if (destination is null)
        { return HasGoto(c) ? new(SpecialUnitRoute.Finish) : null; }
        c.HasOrders = true;
        ref var selected = ref world.Entities.Cities.At(destination.Value);
        if (!HasGoto(c) || selected.Owner.Value != c.Owner)
        { UnitOrders.SetGoto(ref Actor(c), AiTaskIds.DiplomatTarget, selected.Position); }
        return new(SpecialUnitRoute.PlanMove);
    }
}
