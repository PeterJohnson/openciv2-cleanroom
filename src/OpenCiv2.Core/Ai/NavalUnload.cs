using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public enum NavalRoute { Continue, Direction, Finish, ReturnOne }
public readonly record struct NavalChoice(NavalRoute Route, int Direction = 8);

/// <summary>A05 §5 adjacent land and unloading choices, with actual landing selection,
/// current goals, movement exhaustion and role wakeup. Does not relocate passengers.</summary>
public sealed class NavalUnload(GameState world, RuleTables rules, StackOperations stacks, SharedGoals goals, NavalTargets targets)
{
    public NavalChoice Choose(NavalTransportContext state, int damageClass)
    {
        MapKnowledge.RequireLocal(world);
        var c = state.Tactics;
        ref Unit Actor() => ref world.Entities.Units.At(c.Unit);
        ref Civilization Civ() => ref world.Civilizations[c.Owner];
        bool Has(RuleRole role) => state.Roles.Contains((int)role);
        int Left() => UnitMovement.Left(world, rules, Actor());
        int Distance(MapPoint a, MapPoint b) => world.Map.Geometry.WeightedDistance(a, b, world.Globals.Rules.FlatWorld);
        bool onlyTrade = state.Roles == default(UnitRoleSet).With((int)RuleRole.Trade);
        for (int d = 0; d < 8; ++d)
        {
            var p = MapNeighborhoods.Offset(world.Map.Geometry, c.Position, MapNeighborhoods.Directions[d], world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(p) || world.Map.TileAt(p).Ocean)
            { continue; }
            int? owner = world.Map.UnitOwner(p);
            state.CivilizationCursor = owner ?? -1;
            if (owner is not null && owner != c.Owner)
            { continue; }
            if (owner == c.Owner && stacks.Count(stacks.FirstAt(p)) > 1)
            { continue; }
            bool peaceNear = c.NearestCity is { } nearby && world.Entities.Cities.At(nearby).Owner.Value != c.Owner
                && Civ().Treaties[world.Entities.Cities.At(nearby).Owner.Value].Peace && c.NearestDistance < 4;
            int region = world.Map.TileAt(p).Region;
            state.WorkingCity = c.NearestCity;
            state.CargoNeeded = false;
            bool tradeDrop = false, exploreOne = false;
            if (!Has(RuleRole.Trade) && peaceNear)
            { continue; }
            if (Has(RuleRole.Trade) && region == c.NearestRegion)
            {
                CitySlot? home = null;
                for (int current = stacks.First(c.Unit.Value); current >= 0; current = stacks.Next(current))
                {
                    ref var member = ref world.Entities.Units.Storage[current];
                    if (rules.Units[member.Type.Value].Role == RuleRole.Trade)
                    { home = member.Home; }
                }
                home ??= CityQueries.Nearest(world, p, c.Owner).Slot ?? new CitySlot(0);
                var nearest = c.NearestCity ?? throw new InvalidOperationException("A05 trade landing requires the captured nearest city");
                ref var origin = ref world.Entities.Cities.Storage[home.Value.Value];
                ref var city = ref world.Entities.Cities.At(nearest);
                tradeDrop = Actor().AiTask != AiTaskIds.RecruitPort;
                if (world.Map.TileAt(origin.Position).Region == c.NearestRegion && Distance(city.Position, origin.Position) < 16)
                { tradeDrop = false; }
                if (city.Owner.Value == c.Owner && (city.Size < 8 || Distance(city.Position, origin.Position) < 16))
                { tradeDrop = false; }
                for (int route = 0; route < unchecked((sbyte)origin.RouteCount); ++route)
                { if (origin.RoutePartners[route] == nearest) { tradeDrop = false; } }
            }
            bool need = Civ().ContinentMilitary[region] < AssessmentQueries.DefenseQuota(world, c.Owner, region)
                && Civ().ContinentPolicy[region] != 4;
            if (Civ().ContinentCities[region] == 0 && Civ().ContinentMilitary[region] < 8)
            { need = true; }
            int policy = Civ().ContinentPolicy[region];
            if (state.Roles.Contains(policy) && default(UnitRoleSet).With(0).With(1).Contains(policy))
            { need = true; }
            if (Has(RuleRole.Attack) && c.NearestCity is { } enemy && c.HomeCity != c.NearestCity
                && c.HomeDistance > 10 && c.NearestDistance < 4 && region == c.NearestRegion
                && world.Globals.HumanPlayers.Contains(world.Entities.Cities.At(enemy).Owner.Value)
                && Civ().Treaties[world.Entities.Cities.At(enemy).Owner.Value].War)
            { need = true; }
            if (Actor().Orders == UnitOrder.GoTo && world.Map.TileAt(Actor().Destination).Region == region
                && Actor().AiTask != AiTaskIds.RecruitPort)
            { need = true; state.GotoLandingRegion = true; }
            if (!Has(RuleRole.Settle) && Civ().ContinentFlags[region] == default)
            {
                if (unchecked((short)Civ().ContinentMilitary[region]) == 0 && world.Map.Regions[region].Land > 49)
                { Civ().ContinentFlags[region].TransportRequested = true; exploreOne = true; }
                else
                { need = false; }
            }
            if (state.Room && (Civ().ContinentFlags[region].CitiesWithoutPeace || Civ().ContinentFlags[region].MilitaryWithoutPeace))
            { need = false; }
            if (damageClass > 1)
            { need = true; }
            if (!state.Room && world.Map.CityOwner(p) is not null && owner != c.Owner && Hostile(c.Owner, owner))
            { need = true; }
            if (onlyTrade)
            { need = false; }
            state.CargoNeeded = need;
            if (onlyTrade ? !tradeDrop : !need)
            {
                bool matched = state.GotoLandingRegion ?? throw new InvalidOperationException("A05 landing counter requires retained Goto-region state");
                if (matched)
                { Actor().Counter = (byte)(world.Globals.Turn & 7); }
            }
            bool inlandLatitude = p.Y > 1 && p.Y < world.Map.Geometry.Height - 2;
            if (need && world.Map.Regions[region].Fertile > 4 && inlandLatitude)
            {
                if (Left() >= rules.Cosmic.RoadMovementMultiplier && (c.NearestDistance != 0 || c.Policy != (int)RuleRole.Defend))
                {
                    if (targets.BestLandingDirection(c.Unit) is { } direction && direction != 8)
                    { Actor().AiTask = AiTaskIds.RepositionLanding; return new(NavalRoute.Direction, direction); }
                }
                if (Civ().ContinentPolicy[region] == (int)RuleRole.Attack)
                { goals.AddCurrent(c.Owner, c.Position, (int)RuleRole.NavalSuperiority, 5); }
                Actor().AiTask = AiTaskIds.UnloadCargo;
                UnitMovement.Exhaust(world, rules, ref Actor());
                Actor().Orders = UnitOrder.None;
                if (!tradeDrop)
                { state.Roles = state.Roles.Without((int)RuleRole.Trade); }
                UnitOrders.WakeByRole(world, rules, stacks, c.Unit.Value, state.Roles, exploreOne);
                return new(NavalRoute.Finish);
            }
            if (tradeDrop && inlandLatitude)
            {
                if (Civ().ContinentPolicy[region] == (int)RuleRole.Attack)
                { goals.AddCurrent(c.Owner, c.Position, (int)RuleRole.NavalSuperiority, 5); }
                Actor().AiTask = AiTaskIds.UnloadTrade;
                UnitMovement.Exhaust(world, rules, ref Actor());
                state.Roles = default(UnitRoleSet).With((int)RuleRole.Trade);
                if (!peaceNear)
                { state.Roles = state.Roles.With((int)RuleRole.Defend); }
                UnitOrders.WakeByRole(world, rules, stacks, c.Unit.Value, state.Roles, exploreOne);
                Actor().Orders = UnitOrder.None;
                return new(NavalRoute.Finish);
            }
            if ((world.Globals.Turn & 3) == 0)
            { Actor().Orders = UnitOrder.None; }
        }
        if (Left() <= rules.Cosmic.RoadMovementMultiplier && Actor().Orders == UnitOrder.GoTo)
        {
            int x = unchecked(Actor().Destination.X + c.Position.X) >> 1;
            int y = unchecked(Actor().Destination.Y + c.Position.Y) >> 1;
            x -= x & 1;
            x += y & 1;
            goals.AddCurrent(c.Owner, new(x, y), (int)RuleRole.NavalSuperiority, 3);
        }
        return new(NavalRoute.Continue);
    }

    private bool Hostile(int owner, int? other)
    {
        if (other is { } civilization)
        { return DiplomaticAttitudes.Hostile(world, owner, civilization); }
        int bits = unchecked((ushort)world.Civilizations[owner].SubversionCount);
        if ((bits & 0x2000) != 0)
        { return true; }
        if ((bits & 8) != 0)
        { return false; }
        if ((bits & 1) == 0 || (bits & 4) != 0)
        { return false; }
        var relation = world.Civilizations[owner].Treaties[7];
        return ((relation.RetainedHighFlags & 0xfe) | (relation.SpyChat ? 1 : 0)) > 49;
    }
}
