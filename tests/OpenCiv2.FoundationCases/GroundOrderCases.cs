using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static GroundOrders TacticOrders(RevelationFixture data) => new(data.World, data.Rules, data.Stacks,
        new(data.World, data.Rules, new MovementGoalQueries(data.World, data.Rules)));

    private static void GroundRetainedGoto()
    {
        foreach (var role in new[] { RuleRole.Attack, RuleRole.NavalSuperiority, RuleRole.Settle })
        {
            var f = new RecoveryFixture(); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = role };
            f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(16, 10); f.Data.AddUnit(1, 2, 0, new(12, 10));
            var context = f.Context(); var route = TacticOrders(f.Data).RetainedGoto(context);
            Equal(role == RuleRole.Settle ? GroundOrderRoute.Finish : GroundOrderRoute.Continue, route);
            Equal(role == RuleRole.Attack, context.HasOrders); Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders);
        }
        foreach (bool city in new[] { false, true })
        {
            var f = new RecoveryFixture(); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Trade };
            f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(12, 10);
            if (city) f.Data.AddCity(0, 2, new(12, 10)); else f.Data.AddUnit(1, 2, 0, new(12, 10));
            f.Data.World.Civilizations[1].Treaties[2].Peace = true; f.Data.World.Civilizations[1].Treaties[2].War = true;
            Equal(city ? GroundOrderRoute.Finish : GroundOrderRoute.Continue, TacticOrders(f.Data).RetainedGoto(f.Context()));
            Equal(city ? UnitOrder.GoTo : UnitOrder.None, f.Data.Unit(0).Orders);
        }
        var ferry = new RecoveryFixture(1); ferry.Data.Rules.Units[1] = ferry.Data.Rules.Units[1] with { Role = RuleRole.SeaTransport };
        ferry.Data.AddUnit(1, 1, 0, new(10, 10)); ferry.Data.AddCity(0, 2, new(12, 10));
        ferry.Data.Unit(0).Orders = UnitOrder.GoTo; ferry.Data.Unit(0).Destination = new(12, 10);
        var retained = ferry.Context(); var orders = TacticOrders(ferry.Data);
        Equal(GroundOrderRoute.RetryTransport, orders.RetainedGoto(retained)); Equal(true, retained.RetryTransport);
        Equal(UnitOrder.None, ferry.Data.Unit(0).Orders);
        ferry.Data.Unit(0).Orders = UnitOrder.GoTo;
        Equal(GroundOrderRoute.Finish, orders.RetainedGoto(retained)); // One retry, before treaty filtering.
        ferry.Data.World.Civilizations[1].Treaties[2].Alliance = true;
        Equal(GroundOrderRoute.Continue, orders.RetainedGoto(retained)); Equal(UnitOrder.None, ferry.Data.Unit(0).Orders);
    }

    private static void GroundDefenderFortify()
    {
        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            var f = new GuardFixture(); f.ForeignFort();
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Attack = (byte)(variant == 0 ? 1 : 3) };
            if (variant >= 2)
            {
                f.Data.World.Civilizations[1].Treaties[2].Peace = true;
                f.Data.World.Civilizations[1].Treaties[2].Alliance = true;
                f.Data.World.Civilizations[2].Reputation = (byte)(variant == 2 ? 6 : 7);
            }
            SharedGoals.ResetStanding(ref f.Data.World.Civilizations[1]);
            var goals = new SharedGoals(f.Data.World, f.Data.Rules, new MovementGoalQueries(f.Data.World, f.Data.Rules));
            goals.AddStanding(1, new(10, 10), (int)RuleRole.Defend, 2);
            goals.AddStanding(1, new(12, 10), (int)RuleRole.Defend, 2);
            goals.AddStanding(1, new(14, 10), (int)RuleRole.Defend, 2);
            var context = f.Context(); var route = TacticOrders(f.Data).DefenderFortify(context);
            Equal(variant == 2 ? GroundOrderRoute.Continue : variant == 0 ? GroundOrderRoute.Hold : GroundOrderRoute.PlanMove, route);
            Equal(variant is 1 or 3, context.Hold); Equal((byte)(variant == 2 ? 19 : 70), f.Data.Unit(0).AiTask);
            int retained = ((ReadOnlySpan<CivilizationGoal>)f.Data.World.Civilizations[1].StandingGoals).ToArray().Count(g => g.Role == (int)RuleRole.Defend);
            Equal(variant == 2 ? 3 : 1, retained); // Stronger attack retains LastFortDefender; peace clears all reasons, even with Alliance.
        }
        var alone = new GuardFixture(); alone.ForeignFort(); alone.Data.World.Map.TileAt(new(10, 10)).Features &= ~MapFeatures.Fortification;
        Equal(GroundOrderRoute.Hold, TacticOrders(alone.Data).DefenderFortify(alone.Context()));
        alone.Data.Rules.Units[0] = alone.Data.Rules.Units[0] with { Attack = 2 };
        Equal(GroundOrderRoute.Continue, TacticOrders(alone.Data).DefenderFortify(alone.Context()));
    }

    private static void GroundDirectionAndFinish()
    {
        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            var f = new RecoveryFixture(); var c = f.Context();
            f.Data.Unit(0).LastDirection = 2; f.Data.Unit(0).Orders = UnitOrder.GoTo;
            if (variant == 1) f.Data.Unit(0).Flags.Garrison = true;
            if (variant == 2) c.StayHome = true;
            if (variant == 3) f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Settle };
            Equal(0, TacticOrders(f.Data).Direction(c, 8)); Equal((byte)255, f.Data.Unit(0).LastDirection);
            Equal(variant == 0 ? UnitOrder.Fortifying : variant == 1 ? UnitOrder.Fortified : UnitOrder.None, f.Data.Unit(0).Orders);
            Equal((byte)6, f.Data.Unit(0).MovesUsed); Equal(new MapPoint(10, 10), f.Data.Unit(0).Position);
        }
        var air = new RecoveryFixture(2); air.Data.Unit(0).Orders = UnitOrder.AiIdle; air.Data.Unit(0).MovesUsed = 255;
        Equal(1, TacticOrders(air.Data).Finish(new(0), null)); Equal((byte)2, air.Data.Unit(0).MovesUsed);
        Equal((byte)48, air.Data.Unit(0).AiTask); Equal(UnitOrder.None, air.Data.Unit(0).Orders);
        foreach (var order in new[] { UnitOrder.Road, UnitOrder.Sleeping, UnitOrder.AiGoTo })
        {
            var f = new RecoveryFixture(); f.Data.Unit(0).Orders = order;
            Equal(0, TacticOrders(f.Data).Finish(new(0), null)); Equal(order, f.Data.Unit(0).Orders); Equal((byte)0, f.Data.Unit(0).MovesUsed);
        }
        var absent = new RecoveryFixture(); Throws<InvalidOperationException>(() => TacticOrders(absent.Data).Finish(new(0), null));
        Equal((byte)6, absent.Data.Unit(0).MovesUsed); Equal(UnitOrder.None, absent.Data.Unit(0).Orders);
        var edge = new RecoveryFixture(); edge.Data.Stacks.Relocate(0, new(0, 10), false); var context = edge.Context();
        Equal(0, TacticOrders(edge.Data).Direction(context, 5)); Equal(new MapPoint(-2, 10), context.Position);
        Equal(UnitOrder.Fortifying, edge.Data.Unit(0).Orders); Equal(new MapPoint(0, 10), edge.Data.Unit(0).Position);
        Throws<ArgumentOutOfRangeException>(() => TacticOrders(edge.Data).Direction(context, 9));
    }

    private static void GroundGotoFinishNaval()
    {
        foreach (int variant in new[] { 0, 1, 2 })
        {
            var f = new RecoveryFixture(1); f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Role = RuleRole.NavalSuperiority };
            f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = f.Data.Unit(0).Position;
            f.Data.Unit(0).MovesUsed = (byte)(variant == 0 ? 0 : 1);
            if (variant == 2)
            {
                f.Data.Rules.Units[3] = f.Data.Rules.Units[1] with { Role = RuleRole.SeaTransport };
                f.Data.AddUnit(1, 1, 3, new(10, 10));
            }
            Equal(0, TacticOrders(f.Data).Finish(new(0), null)); Equal(true, f.Data.Unit(0).Flags.GoalReached);
            Equal(UnitOrder.None, f.Data.Unit(0).Orders); Equal((byte)(variant == 2 ? 6 : variant), f.Data.Unit(0).MovesUsed);
        }
    }

    private static void PartisanFortressCounters()
    {
        foreach (var (counter, enemy, spent, next, fortress) in new[]
        { (9, false, 0, 10, false), (10, true, 0, 9, true), (255, false, 0, 0, true), (128, true, 0, 127, false), (15, false, 1, 15, true) })
        {
            var f = new GuardFixture(); f.ForeignFort(); f.Data.Unit(0).Type = new(UnitTypeIds.Partisans);
            f.Data.Unit(0).Counter = (byte)counter; f.Data.Unit(0).MovesUsed = (byte)spent;
            if (enemy) f.Data.AddUnit(1, 2, 2, new(12, 10));
            var c = f.Context(); new PartisanFortress(f.Data.World).Apply(c);
            Equal((byte)next, f.Data.Unit(0).Counter); Equal(fortress, c.Fortress);
            Equal(true, f.Data.World.Map.TileAt(new(10, 10)).Features.HasFlag(MapFeatures.Fortification));
            if (!fortress) Equal(GroundGuardRoute.CityGuard, f.Guard.ForeignFortress(c));
        }
    }

    private static void PartisanFortressCities()
    {
        foreach (bool differentRegion in new[] { false, true })
        {
            var f = new GuardFixture(); f.ForeignFort(18); f.Data.AddCity(1, 2, new(12, 10)); f.Data.AddCity(2, 2, new(14, 10));
            if (differentRegion) f.Data.World.Map.TileAt(new(14, 10)).Region = 1;
            var c = f.Context(); new PartisanFortress(f.Data.World).Apply(c); Equal(differentRegion, c.Fortress);
        }
        foreach (bool ownFirst in new[] { false, true })
        {
            var f = new GuardFixture(); f.ForeignFort();
            f.Data.World.Entities.Cities.At(new(0)).Owner = new((byte)(ownFirst ? 1 : 2));
            f.Data.World.Map.TileAt(new(14, 10)).Owner = (byte)(ownFirst ? 1 : 2);
            f.Data.AddCity(1, ownFirst ? 2 : 1, new(10, 14)); f.Data.AddCity(2, 2, new(12, 10));
            var c = f.Context(); new PartisanFortress(f.Data.World).Apply(c); Equal(true, c.Fortress);
        }
    }

    private static void GroundOrdersActualExecution()
    {
        var f = new OrderFixture(); var d = f.Data;
        d.World.Globals.HumanPlayers = CivilizationSet.Only(2); d.World.Multiplayer.LocalHuman = 2;
        d.Rules.Units[0] = d.Rules.Units[0] with { Role = RuleRole.Defend, Defense = 2, Attack = 1 };
        d.AddCity(d.World.Entities.Cities.End, 1, d.Unit(0).Position); d.Unit(0).Orders = UnitOrder.None;
        var c = new UnitTacticsContext(d.World, d.Rules, new(0));
        var guard = new GroundGuard(d.World, d.Rules, d.Stacks, new(d.World, d.Rules, d.Stacks, new()),
            new(d.World, d.Rules, new MovementGoalQueries(d.World, d.Rules)));
        Equal(GroundGuardRoute.Hold, guard.City(c)); Equal(0, TacticOrders(d).Direction(c, 8));
        Equal(UnitOrder.Fortifying, d.Unit(0).Orders);
        using var fortified = f.Begin(); f.Complete(fortified); Equal(1, fortified.Result);
        Equal(UnitOrder.Fortified, d.Unit(0).Orders); Equal((byte)6, d.Unit(0).MovesUsed);
        var move = new OrderFixture(); move.Data.World.Globals.HumanPlayers = CivilizationSet.Only(2); move.Data.World.Multiplayer.LocalHuman = 2;
        var target = new UnitTacticsContext(move.Data.World, move.Data.Rules, new(0));
        Equal(0, TacticOrders(move.Data).Direction(target, 1)); Equal(UnitOrder.AiGoTo, move.Data.Unit(0).Orders);
        Equal(new MapPoint(18, 12), move.Data.Unit(0).Position);
        using var moved = move.Begin(); move.Complete(moved); Equal(1, moved.MovementResult);
        Equal(new MapPoint(20, 12), move.Data.Unit(0).Position); Equal((byte)3, move.Data.Unit(0).MovesUsed);
        Equal(UnitOrder.AiGoTo, move.Data.Unit(0).Orders); Equal(false, move.Data.Unit(0).Flags.GoalReached);
    }
}
