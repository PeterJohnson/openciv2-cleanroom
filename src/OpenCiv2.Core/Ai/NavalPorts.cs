using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public enum NavalPortResult { Continue, Hold }

/// <summary>A05 §4 port selection and recruitment using real city connectivity, stack queries,
/// regional quotas and current goals. A Hold result is completed by A04's shared direction path.</summary>
public sealed class NavalPorts(GameState world, RuleTables rules, StackOperations stacks, SharedGoals goals, GameplayRandom random)
{
    public NavalPortResult Select(NavalTransportContext state, string call)
    {
        ArgumentException.ThrowIfNullOrEmpty(call);
        MapKnowledge.RequireLocal(world);
        var c = state.Tactics;
        ref Unit Actor() => ref world.Entities.Units.At(c.Unit);
        ref Civilization Civ() => ref world.Civilizations[c.Owner];
        int Hold() => unchecked((sbyte)rules.Units[Actor().Type.Value].Capacity);
        CitySlot Home() => c.HomeCity ?? throw new InvalidOperationException("A05 port recruitment requires the captured home city");
        int Policy() => state.SelectedPortPolicy ?? throw new InvalidOperationException("A05 port priority requires a selected-port policy");
        int Priority() => Policy() == 4 ? 4 : 2;
        bool Needed() => state.CargoNeeded ?? throw new InvalidOperationException("A05 recruitment requires retained cargo demand");
        int CountSettlers(MapPoint p)
        {
            int first = stacks.FirstAt(p);
            return first < 0 ? 0 : stacks.CountRole(first, (int)RuleRole.Settle, rules);
        }
        void AtDestination(int role, int priority) => goals.AddCurrent(c.Owner, Actor().Destination, role, priority);
        void AtHome(int role, int priority) => goals.AddCurrent(c.Owner, world.Entities.Cities.At(Home()).Position, role, priority);

        int minimum = c.NearestDistance == 0 || state.OnlySettlersWithoutGoto ? Math.Min(Hold(), 3) : 1;
        if (state.Released >= minimum && !state.Room)
        { return NavalPortResult.Continue; }
        state.ExpandNeighbors = state.Room;
        int best = 999;
        bool tradeOnly = false;
        for (int slot = 0; slot < world.Entities.Cities.End; ++slot)
        {
            var id = new CitySlot((ushort)slot);
            ref var city = ref world.Entities.Cities.Storage[slot];
            if (!city.Token.Occupied || city.Owner.Value != c.Owner || !city.Flags.Coastal)
            { continue; }
            int counter = unchecked((sbyte)Actor().Counter);
            if (city.Flags.FerryPort && !(counter < 0 && counter == -1 - slot % 64))
            { continue; }
            int region = world.Map.TileAt(city.Position).Region;
            if (!(c.HomeDistance == 0 ? CityQueries.ShareOcean(world, Home(), id) : CityQueries.TouchesBody(world, id, c.Region)))
            { continue; }
            state.CargoNeeded = false;
            if (Civ().ContinentMilitary[region] < AssessmentQueries.DefenseQuota(world, c.Owner, region)
                && Civ().ContinentPolicy[region] != 4)
            {
                if (Civ().ContinentTradeUnits[region] == 0)
                { continue; }
                state.CargoNeeded = true;
            }
            int score = world.Map.Geometry.WeightedDistance(c.Position, city.Position, world.Globals.Rules.FlatWorld);
            int first = stacks.FirstAt(city.Position);
            if (first >= 0 && stacks.CountRole(first, (int)RuleRole.SeaTransport, rules) > (score == 0 ? 1 : 0))
            { score += 16; }
            if (Civ().ContinentPolicy[region] == 4 || Civ().ContinentFlags[region] == default)
            { score -= Civ().ContinentAttack[region] >> 2; }
            else
            { score += 16; }
            if (score >= best)
            { continue; }
            best = score;
            state.SelectedPort = id;
            tradeOnly = Needed();
            state.SelectedPortPolicy = Civ().ContinentPolicy[region];
            UnitOrders.SetGoto(ref Actor(), AiTaskIds.RecruitPort, city.Position);
        }
        var settlers = default(UnitRoleSet).With((int)RuleRole.Settle);
        if (c.NearestDistance == 0 && Actor().Counter % 4 != ((world.Globals.Turn - 1) & 3)
            && (!state.Roles.Contains((int)RuleRole.Settle) || state.Roles == settlers || state.Released < Hold()))
        { best = 1000; }

        // At best >= 12 both possible priorities take the fallback; no policy read is needed yet.
        if (best < 12 && best < 3 * Priority())
        {
            var selected = state.SelectedPort ?? throw new InvalidOperationException("A05 attractive score requires a selected port");
            Actor().Counter = unchecked((byte)(-1 - selected.Value % 64));
            world.Entities.Cities.At(selected).Flags.FerryPort = true;
            world.Entities.Cities.At(selected).Flags.FreshPort = true;
            if (!tradeOnly)
            {
                if (CountSettlers(Actor().Destination) == 0 && Policy() != 5)
                { AtDestination((int)RuleRole.Settle, Priority()); AtDestination(AiGoalRoles.FoundCity, Priority() - 1); }
                if (Policy() != (int)RuleRole.Defend)
                { AtDestination((int)RuleRole.Defend, Priority()); }
                if (Policy() != (int)RuleRole.Attack)
                { AtDestination((int)RuleRole.Attack, Priority()); }
                if (Policy() is 4 or 5)
                { AtDestination((int)RuleRole.Diplomacy, Priority()); }
            }
            AtDestination((int)RuleRole.Trade, 2);
            return NavalPortResult.Continue;
        }

        if (c.HomeDistance != 0 || stacks.CountRole(c.Unit.Value, (int)RuleRole.SeaTransport, rules) >= 2
            || best != 1000 && random.SecondaryRange(new ChoiceKey("A05.port-fallback", call), 0, 3) == 0
            || Civ().ContinentFlags[c.Region].StrongerEnemy)
        { return NavalPortResult.Continue; }
        int military = Civ().ContinentMilitary[c.Region];
        int quota = AssessmentQueries.DefenseQuota(world, c.Owner, c.Region);
        state.WorkingCity = Home();
        if (military >= quota || c.Policy == 4)
        { state.CargoNeeded = true; }
        if (Civ().ContinentTradeUnits[c.Region] != 0)
        { tradeOnly = true; }
        if (!tradeOnly && !Needed())
        { return NavalPortResult.Continue; }
        world.Entities.Cities.At(Home()).Flags.FerryPort = true;
        world.Entities.Cities.At(Home()).Flags.FreshPort = true;
        if (Needed())
        {
            c.NearestDistance = CountSettlers(world.Entities.Cities.At(Home()).Position);
            if (c.NearestDistance == 0 && c.Policy != 5)
            { AtHome((int)RuleRole.Settle, Priority()); AtHome(AiGoalRoles.FoundCity, Priority() - 1); }
            if (c.Policy != (int)RuleRole.Defend)
            { AtHome((int)RuleRole.Defend, Priority()); }
            if (c.Policy != (int)RuleRole.Attack)
            { AtHome((int)RuleRole.Attack, Priority()); }
            if (c.Policy is 4 or 5)
            { AtDestination((int)RuleRole.Diplomacy, Priority()); }
        }
        AtHome((int)RuleRole.Trade, 2);
        return NavalPortResult.Hold;
    }
}
