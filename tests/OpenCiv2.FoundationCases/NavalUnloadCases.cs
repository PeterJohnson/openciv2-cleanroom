using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static RevelationFixture UnloadFixture(params int[] types)
    {
        var f = CargoFixture(types);
        foreach (ref var tile in f.World.Map.Tiles) { tile.Terrain = Terrain.Ocean; }
        f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Multiplayer.LocalHuman = 2;
        f.Rules.Units[1] = f.Rules.Units[1] with { Role = RuleRole.SeaTransport, Capacity = 4 };
        f.Rules.Units[5] = f.Rules.Units[0] with { Role = RuleRole.Settle };
        f.Rules.Units[7] = f.Rules.Units[1] with { Role = RuleRole.Attack };
        f.Rules.Units[8] = f.Rules.Units[1] with { Role = RuleRole.Trade };
        f.Rules.Units[9] = f.Rules.Units[1] with { Role = RuleRole.Defend };
        return f;
    }
    private static void UnloadLand(RevelationFixture f, MapPoint p, byte region = 1, short fertile = 8)
    {
        f.World.Map.TileAt(p).Terrain = Terrain.Grassland; f.World.Map.TileAt(p).Region = region;
        f.World.Map.Regions[region] = new(64, fertile, 0, default);
    }
    private static NavalTransportContext UnloadState(RevelationFixture f)
    {
        var entry = TacticEntry(f, EntryRandom()).EnterOrdinary(new(0), "unload-entry");
        return new(entry.Context!, ScanNavalCargo(f, entry.Context!));
    }
    private static NavalUnload Unloader(RevelationFixture f) => new(f.World, f.Rules, f.Stacks,
        new(f.World, f.Rules, new MovementGoalQueries(f.World, f.Rules)), new(f.World));
    private static NavalChoice TransportChoice(RevelationFixture f, NavalTransportContext c, int damage = 0)
    {
        var random = PortRandom();
        var ports = new NavalPorts(f.World, f.Rules, f.Stacks, new(f.World, f.Rules, new MovementGoalQueries(f.World, f.Rules)), random);
        var result = new NavalTransport(f.World, f.Rules, f.Stacks, ports, Unloader(f)).Choose(c, damage, "ports");
        random.RequireChoicesConsumed(); return result;
    }

    private static void NavalUnloadNoMovesExplorationAndFormats()
    {
        var f = UnloadFixture(1, 0, 7, 7); UnloadLand(f, new(11, 9));
        f.AddCity(0, 1, CargoPoint); f.World.Map.TileAt(CargoPoint).Terrain = Terrain.Grassland;
        f.World.Entities.Cities.At(new(0)).Flags.Coastal = true;
        f.Unit(0).MovesUsed = 6; f.Unit(2).Orders = f.Unit(3).Orders = UnitOrder.Sleeping;
        var c = UnloadState(f); Equal(true, c.Tactics.NoMoves); Equal(null, c.GotoLandingRegion);
        Equal(new NavalChoice(NavalRoute.Finish), TransportChoice(f, c));
        Equal(null, c.SelectedPort); Equal(false, f.World.Entities.Cities.At(new(0)).Flags.FerryPort);
        Equal((byte)0x55, f.Unit(0).AiTask); Equal(UnitOrder.None, f.Unit(0).Orders); Equal((byte)6, f.Unit(0).MovesUsed);
        Equal(UnitOrder.None, f.Unit(2).Orders); Equal(UnitOrder.Sleeping, f.Unit(3).Orders);
        Equal(true, f.World.Civilizations[1].ContinentFlags[1].TransportRequested);
        Equal(false, f.World.Civilizations[1].ContinentFlags[1].HasSettlementRestrictions);
        var stored = StateConversion.ToStored(f.World.Civilizations[1]); Equal((byte)0x80, stored.ContinentFlags[1]);
        var restored = StateConversion.ToLive(stored); Equal(true, restored.ContinentFlags[1].TransportRequested);
        restored.ContinentFlags[1].TransportRequested = false;
        Equal((byte)0, StateConversion.PreserveOpaque(StateConversion.ToStored(restored), stored).ContinentFlags[1]);
        var imported = new StoredCivilization { Treaties = new(Enumerable.Range(0, 8).Select(i => i == 7 ? 0xa1008000u : 0u)) };
        var changed = StateConversion.ToLive(imported); changed.Treaties[7].RetainedHighFlags = 0x32; changed.Treaties[7].SpyChat = false;
        Equal(0x32008000u, StateConversion.PreserveOpaque(StateConversion.ToStored(changed), imported).Treaties[7]);
        Equal(0, TacticOrders(f).Finish(new(0), c.Tactics.Domain));
        for (int i = 0; i < 4; ++i) Equal(CargoPoint, f.Unit(i).Position);
    }

    private static void NavalUnloadRepositionDirection()
    {
        var f = UnloadFixture(1, 5); UnloadLand(f, new(11, 9)); UnloadLand(f, new(13, 9));
        var c = UnloadState(f); var result = TransportChoice(f, c);
        Equal(new NavalChoice(NavalRoute.Direction, 1), result); Equal(true, c.Room);
        Equal((byte)0x62, f.Unit(0).AiTask); Equal((byte)0, f.Unit(0).MovesUsed); Equal(CargoPoint, f.Unit(0).Position);
        Equal(0, TacticOrders(f).Direction(c.Tactics, result.Direction));
        Equal(UnitOrder.AiGoTo, f.Unit(0).Orders); Equal(new MapPoint(12, 10), f.Unit(0).Destination);
        Equal((byte)0, f.Unit(0).MovesUsed);
    }

    private static void NavalUnloadTradeWakeAndPeace()
    {
        foreach (bool peace in new[] { false, true })
        {
            var f = UnloadFixture(1, 3, 8, 9);
            f.AddCity(0, 1, new(38, 10)); UnloadLand(f, new(38, 10), 2);
            f.AddCity(1, 2, new(11, 9)); UnloadLand(f, new(11, 9), 1, 0);
            f.World.Civilizations[1].Treaties[2].Peace = peace;
            f.World.Civilizations[1].ContinentFlags[1].MilitaryWithPeace = true;
            f.Unit(0).MovesUsed = 6; f.Unit(1).Home = f.Unit(2).Home = new(0);
            for (int i = 1; i < 4; ++i) f.Unit(i).Orders = UnitOrder.Sleeping;
            var c = UnloadState(f); Equal(new NavalChoice(NavalRoute.Finish), TransportChoice(f, c));
            Equal((byte)0x75, f.Unit(0).AiTask); Equal(UnitOrder.None, f.Unit(2).Orders);
            Equal(peace ? UnitOrder.Sleeping : UnitOrder.None, f.Unit(3).Orders);
            Equal(!peace, c.Roles.Contains((int)RuleRole.Defend)); Equal(false, c.CargoNeeded);
            Equal(null, c.GotoLandingRegion); Equal(UnitOrder.None, f.Unit(0).Orders);
            for (int i = 0; i < 4; ++i) Equal(CargoPoint, f.Unit(i).Position);
        }
    }

    private static void NavalUnloadLastHomeAndExistingRoutes()
    {
        foreach (int variant in Enumerable.Range(0, 4))
        {
            var f = UnloadFixture(1, 3, 3);
            f.AddCity(0, 1, new(38, 10)); UnloadLand(f, new(38, 10), 2);
            f.AddCity(1, 2, new(11, 9)); UnloadLand(f, new(11, 9));
            f.AddCity(2, 1, new(20, 10)); UnloadLand(f, new(20, 10));
            f.World.Civilizations[1].ContinentFlags[1].MilitaryWithPeace = true;
            f.World.Globals.Turn = 5; f.Unit(0).Counter = 9; f.Unit(0).MovesUsed = 6;
            f.Unit(0).Orders = UnitOrder.GoTo; f.Unit(0).Destination = new(11, 9);
            f.Unit(1).Home = new(0); f.Unit(2).Home = variant == 0 ? null : new CitySlot(0);
            f.World.Entities.Cities.At(new(0)).RouteCount = (byte)(variant == 3 ? 255 : variant == 2 ? 1 : 0);
            f.World.Entities.Cities.At(new(0)).RoutePartners[0] = new(1);
            var c = UnloadState(f); var result = TransportChoice(f, c);
            bool deliver = variant is 1 or 3;
            Equal(deliver ? NavalRoute.Finish : NavalRoute.Continue, result.Route);
            Equal((byte)(deliver ? 9 : 5), f.Unit(0).Counter); Equal(true, c.GotoLandingRegion);
            Equal(deliver ? UnitOrder.None : UnitOrder.GoTo, f.Unit(0).Orders);
        }
    }

    private static void NavalUnloadAbsentOwnerRelations()
    {
        foreach (int variant in Enumerable.Range(0, 6))
        {
            var f = UnloadFixture(1, 5, 7); f.Rules.Units[1] = f.Rules.Units[1] with { Capacity = 1 };
            f.Rules.Units[7] = f.Rules.Units[7] with { Role = RuleRole.Settle };
            UnloadLand(f, new(11, 9), 1, 0); f.AddCity(0, 2, new(12, 10)); UnloadLand(f, new(12, 10), 2);
            foreach (int region in new[] { 1, 2 })
            { f.World.Civilizations[1].ContinentCities[region] = 1; f.World.Civilizations[1].ContinentMilitary[region] = 8; f.World.Civilizations[1].ContinentPolicy[region] = 4; }
            f.Unit(0).MovesUsed = 6; f.Unit(0).Orders = UnitOrder.GoTo; f.Unit(0).Destination = new(11, 9); f.Unit(0).Counter = 9;
            f.Unit(2).Orders = UnitOrder.Sleeping; f.World.Globals.Turn = 1;
            f.World.Civilizations[1].SubversionCount = (short)(variant switch { 0 => 8192, 1 => 8200, 4 => 9, 5 => 5, _ => 1 });
            uint high = variant == 3 ? 49u : 50u;
            f.World.Civilizations[1].Treaties[7] = StateConversion.DecodeRelationship(high << 24);
            var c = UnloadState(f); var result = TransportChoice(f, c); bool hostile = variant < 3;
            Equal(hostile ? NavalRoute.Finish : NavalRoute.Continue, result.Route);
            Equal(hostile ? UnitOrder.None : UnitOrder.Sleeping, f.Unit(2).Orders); Equal((byte)(hostile ? 9 : 1), f.Unit(0).Counter);
            Equal(-1, c.CivilizationCursor); Equal(hostile, c.CargoNeeded); Equal(true, c.GotoLandingRegion);
            var stored = StateConversion.ToStored(f.World.Civilizations[1]); Equal(high, stored.Treaties[7] >> 24);
            Equal((byte)(high & 0xfe), StateConversion.ToLive(stored).Treaties[7].RetainedHighFlags);
        }
    }

    private static void NavalUnloadRoomThreatAndDamage()
    {
        foreach (bool damaged in new[] { false, true })
        {
            var f = UnloadFixture(1, 5, 7); f.Rules.Units[7] = f.Rules.Units[7] with { Role = RuleRole.Settle };
            UnloadLand(f, new(11, 9)); f.World.Civilizations[1].ContinentFlags[1].MilitaryWithoutPeace = true;
            f.World.Globals.Turn = 4; f.Unit(0).Counter = 9; f.Unit(0).MovesUsed = 6; f.Unit(0).Damage = (byte)(damaged ? 4 : 0);
            f.Unit(0).Orders = UnitOrder.GoTo; f.Unit(0).Destination = new(11, 9); f.Unit(2).Orders = UnitOrder.Sleeping;
            var entry = TacticEntry(f, EntryRandom()).EnterOrdinary(new(0), "damage");
            var c = new NavalTransportContext(entry.Context!, ScanNavalCargo(f, entry.Context!));
            Equal(damaged ? NavalRoute.Finish : NavalRoute.Continue, TransportChoice(f, c, entry.Recovery!.Value.DamageClass).Route);
            Equal(true, c.Room); Equal(true, c.GotoLandingRegion); Equal(damaged, c.CargoNeeded);
            Equal((byte)(damaged ? 9 : 4), f.Unit(0).Counter); Equal(UnitOrder.None, f.Unit(0).Orders);
            Equal(damaged ? UnitOrder.None : UnitOrder.Sleeping, f.Unit(2).Orders);
        }
    }

    private static void NavalTransportReorderAndRetry()
    {
        var home = UnloadFixture(1, 7, 5, 5, 5);
        home.Rules.Units[1] = home.Rules.Units[1] with { Capacity = 3 };
        home.Rules.Units[7] = home.Rules.Units[7] with { Role = RuleRole.Settle };
        home.AddCity(0, 1, CargoPoint); UnloadLand(home, CargoPoint);
        home.Unit(0).Counter = 255; home.Unit(1).Orders = UnitOrder.Sleeping; home.World.Globals.Turn = 10;
        var atHome = UnloadState(home); Equal(NavalRoute.Continue, TransportChoice(home, atHome).Route);
        Equal("1,0,2,3,4", Chain(home, 0)); Equal((byte)2, home.Unit(0).Counter);
        Equal(UnitOrder.Sleeping, home.Unit(1).Orders); Equal((byte)0, home.Unit(0).MovesUsed);
        var waiting = UnloadFixture(1, 5, 5);
        waiting.AddCity(0, 1, CargoPoint); UnloadLand(waiting, CargoPoint);
        waiting.World.Civilizations[1].ContinentPolicy[1] = 1;
        waiting.AddCity(1, 1, new(12, 10)); UnloadLand(waiting, new(12, 10), 2);
        waiting.World.Entities.Cities.At(new(1)).Flags.Coastal = true;
        waiting.World.Civilizations[1].ContinentPolicy[2] = 4; waiting.World.Globals.Turn = 2;
        var waitState = UnloadState(waiting);
        Equal(new NavalChoice(NavalRoute.Direction, 8), TransportChoice(waiting, waitState));
        Equal("0,1,2", Chain(waiting, 0)); Equal(new MapPoint(12, 10), waiting.Unit(0).Destination);
        foreach (int variant in Enumerable.Range(0, 3))
        {
            bool unload = variant != 0;
            var f = UnloadFixture(1, 0); f.AddCity(0, 2, new(20, 10)); UnloadLand(f, new(20, 10), 2);
            f.World.Entities.Cities.At(new(0)).Flags.Coastal = true; UnloadLand(f, new(11, 9));
            f.Unit(0).Orders = UnitOrder.GoTo; f.Unit(0).Destination = new(20, 10); f.Unit(0).AiTask = AiTaskIds.ReinforcePort;
            f.World.Globals.Turn = (short)(variant == 2 ? 16 : 1); var c = UnloadState(f); c.Tactics.RetryTransport = variant == 1;
            Equal(unload ? NavalRoute.Finish : NavalRoute.Continue, TransportChoice(f, c).Route);
            Equal(variant == 2, c.ExpandNeighbors); Equal((byte)(unload ? 6 : 0), f.Unit(0).MovesUsed);
            Equal(unload ? true : null, c.CargoNeeded);
        }
    }

    private static void NavalUnloadMidpointAndMissingState()
    {
        var f = UnloadFixture(1); f.World.Globals.Rules.FlatWorld = false;
        f.Stacks.Relocate(0, new(38, 10), false); f.Unit(0).MovesUsed = 3;
        f.Unit(0).Orders = UnitOrder.GoTo; f.Unit(0).Destination = new(0, 12);
        var c = UnloadState(f); Equal(NavalRoute.Continue, Unloader(f).Choose(c, 0).Route);
        Equal(3, SharedGoals.PriorityAt(f.World.Civilizations[1], new(19, 11), (sbyte)RuleRole.NavalSuperiority));
        Equal(0, SharedGoals.PriorityAt(f.World.Civilizations[1], new(39, 11), (sbyte)RuleRole.NavalSuperiority));
        var missing = UnloadFixture(1, 0, 7); UnloadLand(missing, new(11, 9));
        missing.World.Civilizations[1].ContinentCities[1] = 1; missing.World.Civilizations[1].ContinentPolicy[1] = 4;
        missing.Unit(0).MovesUsed = 6; missing.Unit(2).Orders = UnitOrder.Sleeping;
        var unresolved = UnloadState(missing);
        Throws<InvalidOperationException>(() => TransportChoice(missing, unresolved));
        Equal(true, missing.World.Civilizations[1].ContinentFlags[1].TransportRequested);
        Equal(null, unresolved.GotoLandingRegion); Equal(false, unresolved.CargoNeeded);
        Equal(UnitOrder.Sleeping, missing.Unit(2).Orders);
    }
}
