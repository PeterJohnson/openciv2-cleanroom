using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class NavalGoalFixture
    {
        public RevelationFixture Data { get; } = new();
        public NavalGoalFixture(params int[] types)
        {
            Data.World.Map.Allocate(new(80, 48));
            foreach (ref var tile in Data.World.Map.Tiles) { tile.Terrain = Terrain.Ocean; }
            Data.World.Globals.HumanPlayers = CivilizationSet.Only(2); Data.World.Multiplayer.LocalHuman = 2;
            Data.Rules.Units[1] = Data.Rules.Units[1] with { Role = RuleRole.NavalSuperiority, Capacity = 4 };
            Data.Rules.Units[5] = Data.Rules.Units[0] with { Role = RuleRole.Settle };
            for (int i = types.Length - 1; i >= 0; --i) Data.AddUnit(i, 1, types[i], CargoPoint);
        }
        public void Land(MapPoint p, byte region, short fertile = 24)
        {
            Data.World.Map.TileAt(p).Terrain = Terrain.Grassland; Data.World.Map.TileAt(p).Region = region;
            Data.World.Map.Regions[region] = new(80, fertile, 0, default(RegionSet).With(0));
        }
        public void City(int slot, int owner, MapPoint p, byte region, byte policy = 0, short trade = 0)
        {
            Data.AddCity(slot, owner, p); Land(p, region);
            Data.World.Civilizations[1].ContinentPolicy[region] = policy;
            Data.World.Entities.Cities.At(new((ushort)slot)).BaseTrade = trade;
        }
        public NavalTransportContext State(bool scan = true)
        {
            var c = TacticEntry(Data, EntryRandom()).EnterOrdinary(new(0), "naval-entry").Context!;
            return new(c, scan ? ScanNavalCargo(Data, c) : default);
        }
        public NavalGoals Goals(GameplayRandom? random = null) => new(Data.World, Data.Rules, Data.Stacks, new(Data.World), random ?? GoalRandom());
    }
    private static GameplayRandom GoalRandom(params Choice[] choices) => new(new ControlledChoices(new(choices)), "naval-goals");
    private static Choice NavalGoalChoice(string site, int hi, uint code, int attempt = 0) =>
        Choice.Create(new("A05." + site, "goals", attempt), new SamplingLaw.SecondaryRange(0, hi), ChoiceCode.Secondary(code));
    private static Choice[] NavalRayChoices(int direction) => Enumerable.Range(2, 22)
        .Select(k => NavalGoalChoice("explore-ray", 7, ((uint)direction << 29) + 0x10000000, k)).ToArray();
    private static NavalSeaTactics SeaTactics(RevelationFixture f, GameplayRandom random)
    {
        var goals = new SharedGoals(f.World, f.Rules, new MovementGoalQueries(f.World, f.Rules));
        var ports = new NavalPorts(f.World, f.Rules, f.Stacks, goals, random);
        var unload = new NavalUnload(f.World, f.Rules, f.Stacks, goals, new(f.World));
        return new(f.World, new(f.World, f.Rules, f.Stacks), new(f.World, f.Rules, f.Stacks, ports, unload),
            new(f.World, f.Rules, f.Stacks, new(f.World), random), goals);
    }

    private static void NavalGoalsReinforcement()
    {
        var tie = new NavalGoalFixture(1, 0); tie.City(0, 1, new(20, 10), 1, 1); tie.City(1, 1, new(18, 10), 2, 1);
        tie.City(2, 2, new(12, 10), 3); tie.Data.World.Civilizations[1].Treaties[2].War = true;
        tie.Data.Unit(0).Counter = 7; var c = tie.State(); c.CivilizationCursor = 6; tie.Goals().War(c);
        Equal(new MapPoint(20, 10), tie.Data.Unit(0).Destination); Equal((byte)0x41, tie.Data.Unit(0).AiTask);
        Equal((byte)7, tie.Data.Unit(0).Counter); Equal(6, c.CivilizationCursor); Equal((byte)0, tie.Data.Unit(0).MovesUsed);
        foreach (bool priority in new[] { false, true })
        {
            var f = new NavalGoalFixture(1, 0); f.City(0, 1, new(20, 10), 1, 1); f.City(1, 1, new(18, 10), 2, 1);
            f.Data.AddUnit(2, 1, 0, new(20, 10)); f.Data.AddUnit(3, 1, 0, new(18, 10));
            if (priority) f.Data.World.Civilizations[1].Goals[0] = new(new(20, 10), (sbyte)RuleRole.Defend, 3);
            f.Goals().War(f.State()); Equal(priority ? new MapPoint(20, 10) : new(18, 10), f.Data.Unit(0).Destination);
        }
        foreach (int x in new[] { 72, 74 })
        {
            var f = new NavalGoalFixture(1, 0); f.City(0, 1, new(x, 10), 1, 1); f.Goals().War(f.State());
            Equal(x == 72 ? UnitOrder.GoTo : UnitOrder.None, f.Data.Unit(0).Orders); // Empty-city scores 23 and 24, with strict admission.
        }
        var home = new NavalGoalFixture(1, 0); home.City(0, 1, CargoPoint, 1); home.City(1, 1, new(20, 10), 2, 1);
        home.Goals().War(home.State()); Equal(new MapPoint(20, 10), home.Data.Unit(0).Destination);
    }

    private static void NavalGoalsEmptyCityDirectedAllies()
    {
        foreach (int variant in Enumerable.Range(0, 3))
        {
            var f = new NavalGoalFixture(1, 0); f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Capacity = 0 };
            f.City(0, 2, new(20, 10), 1); f.City(1, 3, new(variant == 2 ? 16 : 22, 10), 2);
            f.City(2, 1, new(24, 10), 3, 1); f.Data.World.Entities.Cities.At(new(0)).Flags.CoastalFortressUsed = true;
            f.Data.World.Globals.HumanPlayers = CivilizationSet.Only(4).With(5);
            f.Data.World.Civilizations[1].Treaties[2].War = f.Data.World.Civilizations[1].Treaties[3].War = true;
            f.Data.World.Civilizations[4].Treaties[2].War = f.Data.World.Civilizations[5].Treaties[2].War = true;
            if (variant == 1) f.Data.World.Civilizations[1].Treaties[4].Alliance = true;
            else f.Data.World.Civilizations[4].Treaties[1].Alliance = true;
            f.Data.World.Civilizations[5].Treaties[1].Alliance = variant == 2;
            var c = f.State(); f.Goals().War(c);
            Equal((byte)0x49, f.Data.Unit(0).AiTask); Equal((byte)(variant == 0 ? 0 : 1), f.Data.Unit(0).Counter);
            Equal(variant == 0 ? new MapPoint(20, 10) : new(variant == 2 ? 16 : 22, 10), f.Data.Unit(0).Destination);
            Equal(1, c.CivilizationCursor); // The final owned-city rejection still publishes B.
        }
    }

    private static void NavalGoalsGeneralStrideAndModifiers()
    {
        var f = new NavalGoalFixture(1); f.City(0, 2, new(20, 10), 1); f.City(1, 7, new(12, 10), 4);
        f.City(8, 3, new(28, 10), 2); f.City(16, 1, new(40, 10), 3);
        f.Data.World.Globals.HumanPlayers = CivilizationSet.Only(5).With(6);
        foreach (int ally in new[] { 4, 5, 6 }) f.Data.World.Civilizations[1].Treaties[ally].Alliance = true;
        f.Data.World.Civilizations[5].Treaties[2].War = true; f.Data.World.Civilizations[6].Treaties[3].War = true;
        f.Data.World.Civilizations[1].Treaties[3].Aggressor = f.Data.World.Civilizations[1].Treaties[3].War = true;
        f.Data.World.Entities.Cities.At(new(8)).Flags.CoastalFortressUsed = true;
        var c = f.State(); f.Goals().War(c);
        Equal(new MapPoint(28, 10), f.Data.Unit(0).Destination); Equal((byte)0x34, f.Data.Unit(0).AiTask);
        Equal((byte)0, f.Data.Unit(0).Counter); Equal(1, c.CivilizationCursor);
    }

    private static void NavalGoalsGeneralAdmission()
    {
        foreach (int variant in Enumerable.Range(0, 5))
        {
            var f = new NavalGoalFixture(1); f.City(0, 2, new(20, 10), 1);
            f.Data.World.Civilizations[1].Treaties[2].Peace = true;
            f.Data.World.Civilizations[1].Treaties[2].HostileIntent = variant != 0;
            f.Data.World.Civilizations[1].Treaties[2].Alliance = variant == 2;
            if (variant == 3) f.Data.World.Globals.CivByRank[7] = 2;
            if (variant == 4) { f.Data.World.Globals.CivByRank[7] = 1; f.Data.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); }
            var c = f.State(); f.Goals().War(c);
            Equal(variant is 1 or 4 ? UnitOrder.GoTo : UnitOrder.None, f.Data.Unit(0).Orders); Equal(2, c.CivilizationCursor);
        }
    }

    private static NavalGoalFixture NavalTradeFixture(bool settlers = false)
    {
        var f = settlers ? new NavalGoalFixture(1, 5, 3, 3) : new NavalGoalFixture(1, 3, 3);
        f.City(0, 1, new(10, 30), 1); f.City(1, 2, new(20, 10), 2, trade: 10); f.City(2, 3, new(30, 10), 3, trade: 10);
        f.Data.Unit(settlers ? 2 : 1).Home = new(0); // A later Trade member with absent home must not erase this one.
        return f;
    }
    private static void NavalGoalsTradeHomeAndMemoryCursor()
    {
        foreach (int variant in Enumerable.Range(0, 4))
        {
            var f = NavalTradeFixture(true); f.Data.World.Globals.PowerRank[2] = 7;
            f.Data.World.Entities.Cities.Storage[1].RememberedSize[3] = (byte)(variant == 0 ? 4 : 5);
            f.Data.World.Entities.Cities.Storage[1].RememberedSize[4] = (byte)(variant == 2 ? 128 : 0);
            if (variant == 3)
            { f.Data.World.Entities.Cities.Storage[1].Token = default; f.City(3, 2, new(20, 10), 2, trade: 10); }
            var c = f.State(); var random = GoalRandom(); f.Goals(random).Choose(c, "goals"); random.RequireChoicesConsumed();
            Equal(variant is 1 or 3 ? new MapPoint(30, 10) : new(20, 10), f.Data.Unit(0).Destination);
            Equal((byte)0x54, f.Data.Unit(0).AiTask); Equal(8, c.CivilizationCursor);
        }
    }

    private static void NavalGoalsTradeDirectedCursor()
    {
        foreach (bool retainedLarge in new[] { false, true })
        {
            var f = NavalTradeFixture(); f.Data.Unit(0).Counter = 2;
            f.Data.World.Globals.PowerRank[2] = 7; f.Data.World.Globals.CivByRank[7] = 2;
            f.Data.World.Civilizations[2].CityCount = (short)(retainedLarge ? 0 : 99);
            f.Data.World.Civilizations[3].CityCount = (short)(retainedLarge ? 5 : 0);
            var c = f.State(); var random = GoalRandom(); f.Goals(random).Choose(c, "goals"); random.RequireChoicesConsumed();
            Equal(retainedLarge ? new MapPoint(30, 10) : new(20, 10), f.Data.Unit(0).Destination);
            Equal(3, c.CivilizationCursor); Equal((byte)2, f.Data.Unit(0).Counter);
        }
    }

    private static void NavalGoalsTradeArithmeticAndOwnership()
    {
        foreach (int variant in Enumerable.Range(0, 4))
        {
            var f = NavalTradeFixture(); f.Data.World.Entities.Cities.At(new(2)).BaseTrade = 17;
            f.Data.World.Civilizations[1].Treaties[2].War = variant is 1 or 3;
            f.Data.World.Civilizations[1].Treaties[2].Alliance = variant is 2 or 3;
            f.Goals().Trade(f.State()); Equal(variant is 1 or 3 ? new MapPoint(30, 10) : new(20, 10), f.Data.Unit(0).Destination);
        }
        var owned = NavalTradeFixture(); owned.City(3, 1, new(30, 12), 4, trade: 30);
        owned.Goals().Trade(owned.State()); Equal(new MapPoint(20, 10), owned.Data.Unit(0).Destination);
        var signed = NavalTradeFixture(); signed.Data.World.Entities.Cities.At(new(1)).BaseTrade = -10;
        signed.Data.World.Entities.Cities.At(new(2)).BaseTrade = -10;
        signed.Goals().Trade(signed.State()); Equal(new MapPoint(10, 30), signed.Data.Unit(0).Destination);
    }

    private static void NavalGoalsTradeMissingCursorPrefix()
    {
        foreach (int? cursor in new int?[] { null, -1, 9 })
        {
            var f = NavalTradeFixture(); f.Data.World.Globals.PowerRank[2] = 7;
            var c = f.State(); c.CivilizationCursor = cursor;
            Throws<InvalidOperationException>(() => f.Goals().Trade(c));
            Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); Equal(new MapPoint(10, 30), f.Data.Unit(0).Destination);
            Equal((byte)0x54, f.Data.Unit(0).AiTask); Equal(cursor, c.CivilizationCursor);
        }
    }

    private static void NavalGoalsOtherOwnedPorts()
    {
        var f = new NavalGoalFixture(1, 0); f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Capacity = 0 };
        f.City(0, 1, new(20, 10), 1); f.City(1, 1, new(26, 10), 2, 1);
        f.City(2, 1, new(30, 10), 3, 32); f.City(3, 1, new(22, 10), 4, 4); f.City(4, 2, new(24, 10), 5);
        var c = f.State(); var random = GoalRandom(); f.Goals(random).Other(c, "goals"); random.RequireChoicesConsumed();
        Equal(new MapPoint(30, 10), f.Data.Unit(0).Destination); Equal((byte)0x35, f.Data.Unit(0).AiTask); Equal(2, c.CivilizationCursor);
        var home = new NavalGoalFixture(1, 0); home.City(0, 1, CargoPoint, 1); home.City(1, 1, new(24, 10), 2);
        home.Goals().Other(home.State(), "goals"); Equal(new MapPoint(24, 10), home.Data.Unit(0).Destination);
    }

    private static void NavalGoalsExploreDrawsAndReuse()
    {
        foreach (bool reuse in new[] { false, true })
        {
            var f = new NavalGoalFixture(1); f.Land(new(41, 25), 3, 17);
            if (reuse) { f.Data.Unit(0).AiTask = AiTaskIds.ExploreRegion; f.Data.Unit(0).Destination = new(41, 25); }
            var random = reuse ? GoalRandom() : GoalRandom(NavalGoalChoice("explore-y", 47, 0x88000000), NavalGoalChoice("explore-x", 79, 0x82000000));
            f.Goals(random).Other(f.State(), "goals"); random.RequireChoicesConsumed();
            Equal(new MapPoint(42, 24), f.Data.Unit(0).Destination); Equal((byte)0x56, f.Data.Unit(0).AiTask);
            Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal(CargoPoint, f.Data.Unit(0).Position);
        }
    }

    private static void NavalGoalsExploreBoundariesAndRays()
    {
        foreach (int variant in Enumerable.Range(0, 5))
        {
            var f = new NavalGoalFixture(1); byte region = (byte)(variant == 1 ? 0 : variant == 2 ? 63 : 3);
            f.Land(new(41, 25), region, (short)(variant == 0 ? 16 : 24));
            if (variant == 3) f.Data.World.Civilizations[1].ContinentCities[region] = 1;
            if (variant == 4) f.Data.World.Map.Regions[region] = f.Data.World.Map.Regions[region] with { NeighboringWater = default };
            f.Data.Unit(0).AiTask = AiTaskIds.ExploreRegion; f.Data.Unit(0).Destination = new(41, 25);
            var random = GoalRandom(NavalRayChoices(7)); f.Goals(random).Other(f.State(), "goals"); random.RequireChoicesConsumed();
            Equal(UnitOrder.None, f.Data.Unit(0).Orders);
        }
        var rays = new NavalGoalFixture(1); rays.Data.Rules.Units[1] = rays.Data.Rules.Units[1] with { Capacity = 0 };
        rays.Land(new(14, 10), 1); rays.Land(new(16, 10), 2);
        var all = GoalRandom(NavalRayChoices(1)); rays.Goals(all).Other(rays.State(), "goals"); all.RequireChoicesConsumed();
        Equal(new MapPoint(17, 9), rays.Data.Unit(0).Destination); Equal((byte)0x36, rays.Data.Unit(0).AiTask);
    }

    private static void NavalSeaPrefixDispatch()
    {
        var land = new NavalGoalFixture(1, 0); land.Data.Rules.Units[1] = land.Data.Rules.Units[1] with { Domain = RuleDomain.Land };
        land.Data.Unit(0).MovesUsed = 6; land.Data.Unit(1).Orders = UnitOrder.Sleeping;
        var lc = land.State(false); Equal(true, lc.Tactics.NoMoves);
        Equal(NavalRoute.Continue, SeaTactics(land.Data, GoalRandom()).Choose(lc, 0, "goals").Route);
        Equal(UnitOrder.Sleeping, land.Data.Unit(1).Orders);
        var stopped = new NavalGoalFixture(1, 0); stopped.Data.Unit(0).MovesUsed = 6; stopped.Data.Unit(0).Orders = UnitOrder.GoTo;
        stopped.Data.Unit(1).Orders = UnitOrder.Sleeping;
        var standing = stopped.Data.World.Civilizations[1].StandingGoals;
        standing[0] = new(CargoPoint, 2, 5); standing[1] = new(CargoPoint, 18, 4); standing[2] = new(new(12, 10), 2, 3);
        stopped.Data.World.Civilizations[1].StandingGoals = standing;
        var sc = stopped.State(false); Equal(NavalRoute.ReturnOne, SeaTactics(stopped.Data, GoalRandom()).Choose(sc, 0, "goals").Route);
        Equal(UnitOrder.None, stopped.Data.Unit(1).Orders); Equal(UnitOrder.GoTo, stopped.Data.Unit(0).Orders);
        Equal((sbyte)-1, stopped.Data.World.Civilizations[1].StandingGoals[0].Role); Equal((sbyte)-1, stopped.Data.World.Civilizations[1].StandingGoals[1].Role);
        Equal((sbyte)2, stopped.Data.World.Civilizations[1].StandingGoals[2].Role);
        foreach (bool transport in new[] { false, true })
        {
            var f = new NavalGoalFixture(1, 0); f.Data.AddUnit(2, 2, 1, new(12, 10)); f.City(0, 1, new(20, 10), 1, 1);
            if (transport) f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Role = RuleRole.SeaTransport };
            var c = f.State(false); Equal(true, c.Tactics.EnemyZone);
            Equal(NavalRoute.Continue, SeaTactics(f.Data, GoalRandom()).Choose(c, 0, "goals").Route);
            Equal(transport ? UnitOrder.GoTo : UnitOrder.None, f.Data.Unit(0).Orders);
        }
        var unloading = UnloadFixture(1, 0, 7); UnloadLand(unloading, new(11, 9)); unloading.Unit(0).MovesUsed = 6;
        unloading.Unit(2).Orders = UnitOrder.Sleeping;
        var entry = TacticEntry(unloading, EntryRandom()).EnterOrdinary(new(0), "sea");
        var uc = new NavalTransportContext(entry.Context!);
        Equal(NavalRoute.Finish, SeaTactics(unloading, GoalRandom()).Choose(uc, entry.Recovery!.Value.DamageClass, "goals").Route);
        Equal(UnitOrder.None, unloading.Unit(2).Orders); Equal((byte)0x55, unloading.Unit(0).AiTask);
    }

    private static void NavalSeaPrefixActualMovement()
    {
        var f = new OrderFixture(); var d = f.Data;
        d.World.Globals.HumanPlayers = CivilizationSet.Only(2); d.World.Multiplayer.LocalHuman = 2;
        d.Unit(0).Type = new(1); d.Unit(0).Orders = UnitOrder.None;
        d.Rules.Units[1] = d.Rules.Units[1] with { Role = RuleRole.NavalSuperiority, Capacity = 4 };
        d.World.Map.TileAt(new(18, 12)).Terrain = Terrain.Ocean;
        d.AddUnit(d.World.Entities.Units.End, 1, 0, new(18, 12));
        d.AddCity(d.World.Entities.Cities.End, 1, new(20, 12));
        d.World.Map.TileAt(new(20, 12)).Region = 1; d.World.Civilizations[1].ContinentPolicy[1] = 1;
        var entry = TacticEntry(d, EntryRandom()).EnterOrdinary(new(0), "sea");
        var state = new NavalTransportContext(entry.Context!); var random = GoalRandom();
        Equal(NavalRoute.Continue, SeaTactics(d, random).Choose(state, entry.Recovery!.Value.DamageClass, "goals").Route);
        Equal(new MapPoint(20, 12), d.Unit(0).Destination); Equal((byte)0x41, d.Unit(0).AiTask);
        using var move = f.Begin(); f.Complete(move); random.RequireChoicesConsumed();
        Equal(1, move.MovementResult); Equal(new MapPoint(20, 12), d.Unit(0).Position);
        Equal(true, d.Unit(0).Flags.GoalReached); Equal(UnitOrder.None, d.Unit(0).Orders); Equal((byte)6, d.Unit(0).MovesUsed);
    }
}
