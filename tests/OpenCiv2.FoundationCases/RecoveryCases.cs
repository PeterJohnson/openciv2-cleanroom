using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class RecoveryFixture
    {
        public RevelationFixture Data { get; } = new();
        public UnitRecovery Home { get; }
        public GroundRecovery Recovery { get; }
        public RecoveryFixture(int type = 0)
        {
            Data.AddUnit(0, 1, type, new(10, 10));
            for (int i = 0; i < 3; ++i) Data.Rules.Units[i] = Data.Rules.Units[i] with { HitPoints = 12 };
            Data.Rules.Terrain[(int)Terrain.Grassland] = new() { MovementCost = 1, DefenseFactor = 2 };
            Data.Rules.Terrain[(int)Terrain.Ocean] = new() { MovementCost = 1, DefenseFactor = 2 };
            Data.Unit(0).AiTask = 19; Data.Unit(0).Destination = new(8, 8);
            Home = new(Data.World, Data.Rules); Recovery = new(Data.World, Data.Rules, Data.Stacks, Home);
        }
        public UnitTacticsContext Context() => new(Data.World, Data.Rules, new(0));
        public void Ai() => Data.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        public GroundRecoveryResult Evaluate() => Recovery.Evaluate(Context());
    }

    private static void RecoveryHomeCitiesAndRegions()
    {
        var f = new RecoveryFixture(); f.Data.AddCity(0, 1, new(14, 10)); f.Data.AddCity(1, 1, new(12, 12));
        Equal(new CitySlot(1), CityQueries.Nearest(f.Data.World, new(10, 10), 1).Slot);
        f.Home.GoHome(new(0)); Equal(new MapPoint(14, 10), f.Data.Unit(0).Destination);
        Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); Equal((byte)19, f.Data.Unit(0).AiTask); Equal(null, f.Data.Unit(0).Home);
        f.Data.World.Map.TileAt(new(10, 10)).Terrain = Terrain.Ocean;
        f.Data.World.Map.TileAt(new(10, 10)).Region = 7;
        f.Data.Unit(0).Orders = UnitOrder.None; f.Home.GoHome(new(0)); Equal(UnitOrder.None, f.Data.Unit(0).Orders);
        f.Data.World.Map.TileAt(new(12, 12)).Region = 7;
        f.Home.GoHome(new(0)); Equal(new MapPoint(12, 12), f.Data.Unit(0).Destination); // Raw origin region still gates a Land actor on Ocean.
        var seam = new RecoveryFixture(); seam.Data.World.Globals.Rules.FlatWorld = false;
        seam.Data.Stacks.Relocate(0, new(0, 10), false); seam.Data.AddCity(0, 1, new(38, 10)); seam.Data.AddCity(1, 1, new(4, 10));
        seam.Home.GoHome(new(0)); Equal(new MapPoint(38, 10), seam.Data.Unit(0).Destination);
    }

    private static void RecoveryHomeCarriersAndAirbases()
    {
        foreach (bool tied in new[] { false, true })
        {
            var f = new RecoveryFixture(2); f.Data.AddCity(0, 1, new(14, 10));
            f.Data.Rules.Units[3] = f.Data.Rules.Units[0] with { Capacity = 0, Abilities = new() { CarryAircraft = true } };
            f.Data.AddUnit(1, 1, 3, tied ? new(12, 12) : new(12, 10));
            f.Data.Unit(1).MovesUsed = 255; f.Data.Unit(1).Flags.Waiting = true;
            f.Home.GoHome(new(0)); Equal(tied ? new MapPoint(14, 10) : new MapPoint(12, 10), f.Data.Unit(0).Destination);
        }
        foreach (bool missile in new[] { false, true })
        {
            var f = new RecoveryFixture(2);
            f.Data.Rules.Units[2] = f.Data.Rules.Units[2] with { Abilities = new() { DestroyedAfterAttack = missile } };
            f.Data.Rules.Units[3] = f.Data.Rules.Units[1] with { Abilities = new() { Submarine = true } };
            f.Data.AddUnit(1, 1, 3, new(12, 10)); f.Home.GoHome(new(0));
            Equal(missile ? UnitOrder.GoTo : UnitOrder.None, f.Data.Unit(0).Orders);
        }
        foreach (bool human in new[] { false, true })
        {
            var f = new RecoveryFixture(2); if (!human) f.Ai();
            f.Data.World.Map.TileAt(new(10, 10)).Features |= MapFeatures.CitySite | MapFeatures.Fortification;
            f.Data.World.Map.TileAt(new(10, 10)).Owner = 2; f.Data.Unit(0).MovesUsed = 1;
            f.Home.GoHome(new(0)); Equal((byte)(human ? 1 : 6), f.Data.Unit(0).MovesUsed);
            Equal(UnitOrder.None, f.Data.Unit(0).Orders); Equal(new MapPoint(8, 8), f.Data.Unit(0).Destination);
        }
    }

    private static void RecoveryHomeFuelAndArrival()
    {
        foreach (var (range, counter, allowed) in new[] { (1, 0, false), (2, 0, true), (1, 255, true), (255, 0, false) })
        {
            var f = new RecoveryFixture(); f.Data.AddCity(0, 1, new(14, 10));
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Range = (byte)range };
            f.Data.Unit(0).Counter = (byte)counter; f.Data.Unit(0).MovesUsed = 3;
            f.Home.GoHome(new(0)); Equal(allowed ? UnitOrder.GoTo : UnitOrder.None, f.Data.Unit(0).Orders);
            Equal((byte)3, f.Data.Unit(0).MovesUsed); // Range gate also applies to customized Land rules.
        }
        var arrived = new RecoveryFixture(); arrived.Data.AddCity(0, 1, new(10, 10)); arrived.Home.GoHome(new(0));
        Equal((byte)6, arrived.Data.Unit(0).MovesUsed); Equal(new MapPoint(8, 8), arrived.Data.Unit(0).Destination);
        var failed = new RecoveryFixture(); failed.Data.AddCity(0, 1, new(14, 10));
        failed.Data.Rules.Units[0] = failed.Data.Rules.Units[0] with { Range = 1 };
        failed.Data.Rules.Cosmic = failed.Data.Rules.Cosmic with { RoadMovementMultiplier = 0 };
        Throws<DivideByZeroException>(() => failed.Home.GoHome(new(0)));
        Equal(UnitOrder.None, failed.Data.Unit(0).Orders); Equal((byte)0, failed.Data.Unit(0).MovesUsed);
    }

    private static void RecoveryContextAndDamageClasses()
    {
        foreach (bool human in new[] { false, true })
        {
            var f = new RecoveryFixture(); if (!human) f.Ai();
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Settle }; f.Data.Unit(0).Flags.Colonist = true;
            var context = f.Context(); Equal(human ? 21 : 5, context.Role); Equal(human, f.Data.Unit(0).Flags.Colonist);
            Equal(9999, context.HomeDistance); Equal(-1, context.HomeRegion);
        }
        foreach (var (damage, hp, level) in new[] { (0, 12, 0), (3, 12, 1), (4, 12, 2), (7, 12, 3), (0, 128, 3) })
        {
            var f = new RecoveryFixture(); f.Ai(); f.Data.Unit(0).Damage = (byte)damage; f.Data.Unit(0).MovesUsed = 1;
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { HitPoints = (byte)hp };
            f.Data.Unit(0).Flags.Retreating = true;
            var result = f.Evaluate(); Equal(level, result.DamageClass); Equal(GroundRecoveryRoute.ScenarioGuard, result.Route);
            Equal(false, f.Data.Unit(0).Flags.Retreating); Equal((byte)(level == 3 ? 68 : 19), f.Data.Unit(0).AiTask);
        }
        var cargo = new RecoveryFixture(); cargo.Ai(); cargo.Data.Rules.Units[0] = cargo.Data.Rules.Units[0] with { Capacity = 1 };
        cargo.Data.Unit(0).Damage = 7; cargo.Data.Unit(0).MovesUsed = 6; cargo.Data.Unit(0).Flags.Retreating = true;
        var captured = cargo.Context(); Equal(GroundRecoveryRoute.TransportWork, cargo.Recovery.Evaluate(captured).Route);
        Equal(true, captured.NoMoves); Equal(true, cargo.Data.Unit(0).Flags.Retreating); Equal((byte)19, cargo.Data.Unit(0).AiTask);
    }

    private static void RecoveryCityAndPeaceGates()
    {
        foreach (bool atCity in new[] { false, true })
        {
            var f = new RecoveryFixture(); f.Ai(); f.Data.Unit(0).Damage = 4;
            f.Data.AddCity(0, 1, atCity ? new(10, 10) : new(12, 10)); var context = f.Context();
            Equal(atCity ? GroundRecoveryRoute.CityGuard : GroundRecoveryRoute.Finish, f.Recovery.Evaluate(context).Route);
            Equal(atCity, context.Hold); Equal(atCity, context.Guard);
            if (!atCity) { Equal(new MapPoint(12, 10), f.Data.Unit(0).Destination); Equal((byte)100, f.Data.Unit(0).AiTask); }
        }
        foreach (int variant in new[] { 0, 1, 2 })
        {
            var f = new RecoveryFixture(); f.Ai(); f.Data.Unit(0).Damage = 3; f.Data.World.Civilizations[1].ContinentPolicy[0] = 1;
            f.Data.World.Map.TileAt(new(10, 10)).Claimant = 2; f.Data.World.Civilizations[1].Treaties[2].Peace = true;
            f.Data.World.Civilizations[2].Reputation = (byte)(variant == 1 ? 6 : 5);
            f.Data.World.Civilizations[1].Treaties[2].Alliance = variant == 2;
            var result = f.Evaluate(); Equal(variant == 0, result.PeacefulTerritory);
            Equal(variant == 0 ? GroundRecoveryRoute.ScenarioGuard : GroundRecoveryRoute.Hold, result.Route);
        }
        var forced = new RecoveryFixture(); forced.Ai(); forced.Data.Unit(0).Damage = 4; forced.Data.Unit(0).Flags.Retreating = true;
        forced.Data.AddCity(0, 2, new(12, 10)); forced.Data.AddCity(1, 1, new(30, 10)); var attack = forced.Context(); attack.ForceAttack = true;
        var resultForced = forced.Recovery.Evaluate(attack); Equal(0, resultForced.DamageClass); Equal(GroundRecoveryRoute.ScenarioGuard, resultForced.Route);
        Equal(false, forced.Data.Unit(0).Flags.Retreating);
    }

    private static void RecoveryAirAndSeaRest()
    {
        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            var f = new RecoveryFixture(2); f.Ai(); f.Data.Unit(0).Damage = (byte)(variant == 3 ? 1 : 4);
            if (variant == 1) f.Data.Unit(0).Visibility = CivilizationSet.Only(3);
            if (variant == 2) f.Data.Rules.Units[2] = f.Data.Rules.Units[2] with { Range = 1 };
            if (variant == 3) f.Data.Unit(0).Flags.Retreating = true;
            var result = f.Evaluate(); Equal(variant is 0 or 3 ? GroundRecoveryRoute.Hold : GroundRecoveryRoute.ScenarioGuard, result.Route);
            Equal(variant is 0 or 3, f.Data.Unit(0).Flags.Retreating);
        }
        foreach (bool seenByHuman in new[] { false, true })
        {
            var f = new RecoveryFixture(1); f.Ai(); f.Data.Unit(0).Damage = 3;
            f.Data.Unit(0).Visibility = CivilizationSet.Only(seenByHuman ? 2 : 3);
            f.Data.World.Map.TileAt(new(10, 10)).Terrain = Terrain.Ocean;
            Equal(seenByHuman ? GroundRecoveryRoute.ScenarioGuard : GroundRecoveryRoute.Hold, f.Evaluate().Route);
        }
    }

    private static void RecoverySevereRoutes()
    {
        foreach (bool distant in new[] { false, true })
        {
            var f = new RecoveryFixture(); f.Ai(); f.Data.Unit(0).Damage = 7; f.Data.Unit(0).MovesUsed = 1;
            f.Data.Unit(0).Flags.Garrison = true; f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Movement = 12 };
            f.Data.AddUnit(1, 2, 0, new(12, 10)); f.Data.AddCity(0, 1, distant ? new(24, 10) : new(14, 10));
            Equal(distant ? GroundRecoveryRoute.Hold : GroundRecoveryRoute.Finish, f.Evaluate().Route);
            Equal((byte)68, f.Data.Unit(0).AiTask); Equal(distant ? UnitOrder.None : UnitOrder.GoTo, f.Data.Unit(0).Orders);
        }
        var transport = new RecoveryFixture(1); transport.Ai(); transport.Data.Rules.Units[1] = transport.Data.Rules.Units[1] with { Role = RuleRole.SeaTransport };
        transport.Data.Unit(0).Damage = 7; transport.Data.Unit(0).MovesUsed = 1; transport.Data.AddUnit(1, 1, 0, new(10, 10));
        transport.Data.AddCity(0, 1, new(14, 10)); Equal(GroundRecoveryRoute.ScenarioGuard, transport.Evaluate().Route);
        Equal(UnitOrder.None, transport.Data.Unit(0).Orders); Equal((byte)68, transport.Data.Unit(0).AiTask);
    }

    private static void RecoveryActualOrderMovement()
    {
        var f = new OrderFixture(); var d = f.Data; var w = d.World;
        w.Globals.HumanPlayers = CivilizationSet.Only(2); w.Multiplayer.LocalHuman = 2;
        d.Unit(0).Type = new(1); d.Unit(0).Damage = 4; d.Unit(0).Orders = UnitOrder.None;
        d.Unit(0).Visibility = CivilizationSet.Only(2);
        d.Rules.Units[1] = d.Rules.Units[1] with { Role = RuleRole.NavalSuperiority };
        d.Rules.Terrain[(int)Terrain.Ocean] = d.Rules.Terrain[(int)Terrain.Ocean] with { MovementCost = 1 };
        w.Map.TileAt(new(18, 12)).Terrain = w.Map.TileAt(new(20, 12)).Terrain = Terrain.Ocean;
        d.AddCity(w.Entities.Cities.End, 1, new(22, 12));
        var context = new UnitTacticsContext(w, d.Rules, new(0));
        var recovery = new GroundRecovery(w, d.Rules, d.Stacks, new(w, d.Rules));
        Equal(GroundRecoveryRoute.PlanMove, recovery.Evaluate(context).Route); Equal(true, context.HasOrders);
        Equal(UnitOrder.GoTo, d.Unit(0).Orders); Equal(new MapPoint(22, 12), d.Unit(0).Destination);
        using var op = f.Begin(); f.Complete(op); Equal(1, op.Result); Equal(1, op.MovementResult);
        Equal(new MapPoint(20, 12), d.Unit(0).Position); Equal((byte)3, d.Unit(0).MovesUsed);
        Equal(UnitOrder.GoTo, d.Unit(0).Orders); Equal(false, d.Unit(0).Flags.GoalReached);
    }
}
