using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public sealed partial class NavalGoals
{
    public void Trade(NavalTransportContext state)
    {
        MapKnowledge.RequireLocal(world);
        var c = state.Tactics;
        if (HasGoto(c) || !state.Roles.Contains((int)RuleRole.Trade) || !HasHold(c))
        { return; }
        int best = -999;
        var originRegion = world.Map.LandRegion(c.Position);
        CitySlot? home = null;
        for (int current = stacks.First(c.Unit.Value); current >= 0; current = stacks.Next(current))
        {
            ref var member = ref world.Entities.Units.Storage[current];
            if (rules.Units[member.Type.Value].Role == RuleRole.Trade && member.Home is { } memberHome)
            { home = memberHome; }
        }
        home ??= CityQueries.Nearest(world, c.Position, c.Owner).Slot ?? new CitySlot(0);
        for (int slot = 0; slot < world.Entities.Cities.End; ++slot)
        {
            ref var city = ref world.Entities.Cities.Storage[slot];
            if (!city.Token.Occupied)
            { continue; }
            int region = world.Map.TileAt(city.Position).Region;
            if (region == originRegion)
            { continue; }
            bool owned = city.Owner.Value == c.Owner;
            if (!(owned ? CityConnected(c, new((ushort)slot)) : LandConnected(c, region)))
            { continue; }
            int score = Distance(world.Entities.Cities.Storage[home.Value.Value].Position, city.Position);
            if (owned)
            { score /= 2; }
            if (Civ(c).Treaties[city.Owner.Value].War)
            { score -= score / 3; }
            if (Civ(c).Treaties[city.Owner.Value].Alliance)
            { score += score / 3; }
            if (world.Globals.HumanPlayers.Contains(city.Owner.Value) && world.Globals.PowerRank[city.Owner.Value] == 7
                && CursorCityCount(state) > 4)
            { score /= 2; }
            score = unchecked(score * city.BaseTrade) / ((Distance(c.Position, city.Position) >> 3) + 1);
            if (score <= best)
            { continue; }
            best = score;
            UnitOrders.SetGoto(ref Actor(c), AiTaskIds.OverseasTrade, city.Position);
        }
    }

    private int CursorCityCount(NavalTransportContext state)
    {
        int cursor = state.CivilizationCursor ?? throw new InvalidOperationException("A05 trade discount requires the retained civilization cursor");
        if (cursor is >= 0 and <= 7)
        { return world.Civilizations[cursor].CityCount; }
        if (cursor != 8)
        { throw new InvalidOperationException("A05 trade discount has no city-count backing for this cursor"); }
        ref var remembered = ref world.Entities.Cities.Storage[1].RememberedSize;
        return unchecked((short)(remembered[3] + 256 * remembered[4]));
    }
}
