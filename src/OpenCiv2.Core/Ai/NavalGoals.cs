using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A05 §§6–8 ordered naval war, trade and exploration destinations.
/// Publishes actual Goto orders; their execution remains U04/U03/U02.</summary>
public sealed partial class NavalGoals(GameState world, RuleTables rules, StackOperations stacks, NavalTargets targets, GameplayRandom random)
{
    private ref Unit Actor(UnitTacticsContext c) => ref world.Entities.Units.At(c.Unit);
    private ref Civilization Civ(UnitTacticsContext c) => ref world.Civilizations[c.Owner];
    private bool HasGoto(UnitTacticsContext c) => Actor(c).Orders == UnitOrder.GoTo;
    private bool HasHold(UnitTacticsContext c) => rules.Units[Actor(c).Type.Value].Capacity != 0;
    private CitySlot Home(UnitTacticsContext c) => c.HomeCity ?? throw new InvalidOperationException("A05 connectivity requires the captured home city");
    private int Distance(MapPoint a, MapPoint b) => world.Map.Geometry.WeightedDistance(a, b, world.Globals.Rules.FlatWorld);
    private bool CityConnected(UnitTacticsContext c, CitySlot city) => c.HomeDistance == 0
        ? CityQueries.ShareOcean(world, Home(c), city) : CityQueries.TouchesBody(world, city, c.Region);
    private bool LandConnected(UnitTacticsContext c, int region) => c.HomeDistance == 0
        ? AssessmentQueries.CityTouchesRegion(world, Home(c), region) : world.Map.Regions[region].NeighboringWater.Contains(c.Region);

    public void Choose(NavalTransportContext state, string call)
    {
        ArgumentException.ThrowIfNullOrEmpty(call);
        War(state);
        Trade(state);
        Other(state, call);
    }

    public void War(NavalTransportContext state)
    {
        MapKnowledge.RequireLocal(world);
        var c = state.Tactics;
        bool military = state.Roles.Intersects(default(UnitRoleSet).With((int)RuleRole.Attack).With((int)RuleRole.Defend));
        if (!HasGoto(c) && military && HasHold(c))
        {
            int best = 24;
            for (int slot = 0; slot < world.Entities.Cities.End; ++slot)
            {
                ref var city = ref world.Entities.Cities.Storage[slot];
                var id = new CitySlot((ushort)slot);
                if (!city.Token.Occupied || city.Owner.Value != c.Owner
                    || Civ(c).ContinentPolicy[world.Map.TileAt(city.Position).Region] != (int)RuleRole.Defend || !CityConnected(c, id))
                { continue; }
                int score = Distance(c.Position, city.Position);
                if (world.Map.UnitOwner(city.Position) is null)
                { score /= 2; }
                else if (SharedGoals.PriorityAt(Civ(c), city.Position, (sbyte)RuleRole.Defend) != 0)
                { score -= score / 3; }
                if (score < best)
                { best = score; UnitOrders.SetGoto(ref Actor(c), AiTaskIds.ReinforcePort, city.Position); }
            }
        }
        if (!HasGoto(c) && military)
        {
            int best = 999;
            for (int slot = 0; slot < world.Entities.Cities.End; ++slot)
            {
                ref var city = ref world.Entities.Cities.Storage[slot];
                if (!city.Token.Occupied)
                { continue; }
                int other = city.Owner.Value;
                state.CivilizationCursor = other;
                if (other == c.Owner || !Civ(c).Treaties[other].War || world.Map.UnitOwner(city.Position) is not null)
                { continue; }
                _ = world.Map.TileAt(city.Position).Region;
                if (!CityConnected(c, new((ushort)slot)))
                { continue; }
                int score = Distance(c.Position, city.Position);
                for (int human = 1; human <= 7; ++human)
                {
                    if (world.Globals.HumanPlayers.Contains(human) && world.Civilizations[human].Treaties[c.Owner].Alliance
                        && world.Civilizations[human].Treaties[other].War)
                    { score /= 2; break; }
                }
                if (city.Flags.CoastalFortressUsed)
                { score *= 2; }
                if (score >= best)
                { continue; }
                best = score;
                Actor(c).Counter = (byte)(slot % 8);
                UnitOrders.SetGoto(ref Actor(c), AiTaskIds.RaidEmptyCity, city.Position);
            }
        }
        if (HasGoto(c))
        { return; }
        int? ally = null;
        for (int other = 0; other <= 7; ++other)
        {
            if (Civ(c).Treaties[other].Alliance && (world.Globals.HumanPlayers.Contains(other) || ally is null))
            { ally = other; }
        }
        state.CivilizationCursor = 8;
        if (!state.Roles.Contains((int)RuleRole.Attack) && state.Roles.Contains((int)RuleRole.Settle))
        { return; }
        int attackBest = 999;
        var originRegion = world.Map.LandRegion(c.Position);
        for (int slot = Actor(c).Counter % 8; slot < world.Entities.Cities.End; slot += 8)
        {
            ref var city = ref world.Entities.Cities.Storage[slot];
            if (!city.Token.Occupied)
            { continue; }
            int other = city.Owner.Value;
            state.CivilizationCursor = other;
            var treaty = Civ(c).Treaties[other];
            if (other == c.Owner || treaty.Alliance || treaty.Peace && !treaty.HostileIntent)
            { continue; }
            int strongest = world.Globals.CivByRank[7];
            if (world.Globals.HumanPlayers.Contains(strongest) && strongest != c.Owner)
            { continue; }
            int region = world.Map.TileAt(city.Position).Region;
            if (region == originRegion || !LandConnected(c, region))
            { continue; }
            int score = Distance(c.Position, city.Position);
            if (ally is > 0 && world.Civilizations[ally.Value].Treaties[other].War)
            { score /= 2; }
            if (Civ(c).Treaties[other].Aggressor)
            { score /= 2; }
            if (Civ(c).Treaties[other].War && world.Map.UnitOwner(city.Position) is null)
            { score /= 2; }
            if (city.Flags.CoastalFortressUsed)
            { score *= 2; }
            if (score >= attackBest)
            { continue; }
            attackBest = score;
            UnitOrders.SetGoto(ref Actor(c), AiTaskIds.OverseasAttack, city.Position);
        }
    }
}
