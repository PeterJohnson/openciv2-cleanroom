using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static UnitTacticsContext NavalCargoContext(RevelationFixture f) => new(f.World, f.Rules, new(0));
    private static NavalCargoResult ScanNavalCargo(RevelationFixture f, UnitTacticsContext c) => new NavalCargo(f.World, f.Rules, f.Stacks).Scan(c);

    private static void NavalCargoActualGuardQuota()
    {
        foreach (int variant in Enumerable.Range(0, 3))
        {
            var f = CargoFixture(1, 4, 4, 4, 4, 4, 4);
            f.Rules.Units[1] = f.Rules.Units[1] with { Role = RuleRole.SeaTransport };
            f.Rules.Units[4] = f.Rules.Units[0] with { Role = RuleRole.Defend };
            f.AddCity(0, 1, CargoPoint, variant == 2 ? 1 : 8);
            f.World.Civilizations[1].ContinentPolicy[0] = (byte)(variant == 1 ? 4 : 0);
            if (variant == 2)
            { BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Palace, true); f.World.Civilizations[1].CityCount = 12; }
            for (int i = 1; i <= 6; ++i) f.Unit(i).Orders = UnitOrder.Fortified;
            f.Unit(0).Orders = UnitOrder.GoTo;
            var c = NavalCargoContext(f); Equal(null, c.DesiredDefenders);
            var guard = new GroundGuard(f.World, f.Rules, f.Stacks, new(f.World, f.Rules, f.Stacks, new()),
                new(f.World, f.Rules, new MovementGoalQueries(f.World, f.Rules)));
            guard.City(c); // Actual A04 producer; this test does not substitute for the full chooser's routing.
            int quota = variant == 2 ? 4 : 3; Equal(quota, c.DesiredDefenders);
            var cargo = ScanNavalCargo(f, c);
            Equal(6, cargo.ReservedDefenders); Equal(variant == 1 ? 3 : 1, cargo.Released);
            Equal(default(UnitRoleSet).With((int)RuleRole.Defend), cargo.Roles);
            for (int i = 1; i <= 6; ++i)
            { Equal(i == quota + 1 || variant == 1 && i > quota ? UnitOrder.None : UnitOrder.Fortified, f.Unit(i).Orders); }
            Equal(UnitOrder.GoTo, f.Unit(0).Orders); Equal(new UnitSlot(0), c.Unit);
        }
    }

    private static void NavalCargoCompleteStackAndRoleSlots()
    {
        var f = CargoFixture(1, 0, 4, 2, 5, 6);
        f.Rules.Units[1] = f.Rules.Units[1] with { Capacity = 0 };
        f.Rules.Units[4] = f.Rules.Units[0] with { Role = (RuleRole)255 };
        f.Rules.Units[5] = f.Rules.Units[0] with { Role = (RuleRole)32 };
        f.Rules.Units[6] = f.Rules.Units[0] with { Role = RuleRole.Settle };
        f.Unit(5).Flags.Colonist = true;
        for (int i = 1; i <= 5; ++i)
        { f.Unit(i).Orders = UnitOrder.Sleeping; f.Unit(i).MovesUsed = 6; f.Unit(i).Carrier = new(9); f.Unit(i).Destination = new(9, 77); }
        f.Unit(1).Orders = UnitOrder.Fortifying; f.Unit(2).Owner = new(2); f.Unit(0).Orders = UnitOrder.GoTo;
        var cargo = ScanNavalCargo(f, NavalCargoContext(f));
        Equal(4, cargo.Released); Equal(0, cargo.ReservedDefenders);
        Equal(default(UnitRoleSet).With(0).With(31).With(5), cargo.Roles);
        Equal(false, cargo.Roles.Contains(21)); // Actual Settle remains distinct from effective AIColonist role.
        Equal(true, cargo.Roles.Contains(-1)); Equal(true, cargo.Roles.Contains(32));
        for (int i = 1; i <= 5; ++i)
        {
            Equal(i == 3 ? UnitOrder.Sleeping : UnitOrder.None, f.Unit(i).Orders);
            Equal((byte)6, f.Unit(i).MovesUsed); Equal(new UnitSlot(9), f.Unit(i).Carrier); Equal(new MapPoint(9, 77), f.Unit(i).Destination);
        }
        Equal(UnitOrder.GoTo, f.Unit(0).Orders); Equal("0,1,2,3,4,5", Chain(f, 0));
    }

    private static void NavalCargoAwayReservationAndReset()
    {
        var f = CargoFixture(1, 4, 0, 4); f.Rules.Units[4] = f.Rules.Units[0] with { Role = RuleRole.Defend };
        f.World.Map.TileAt(CargoPoint).Terrain = Terrain.Ocean;
        f.Unit(1).Orders = UnitOrder.Sleeping; f.Unit(3).Orders = UnitOrder.Fortified;
        var c = NavalCargoContext(f); c.DesiredDefenders = 2;
        var first = ScanNavalCargo(f, c);
        Equal(1, first.ReservedDefenders); Equal(1, first.Released); Equal(default(UnitRoleSet).With(0), first.Roles);
        Equal(UnitOrder.Fortifying, f.Unit(1).Orders); Equal(UnitOrder.Fortified, f.Unit(3).Orders);
        Equal(first, ScanNavalCargo(f, c)); // Each scan resets all three outputs.
        c.DesiredDefenders = -1;
        var negative = ScanNavalCargo(f, c);
        Equal(0, negative.ReservedDefenders); Equal(2, negative.Released);
        Equal(default(UnitRoleSet).With(0).With(1), negative.Roles);
        Equal(UnitOrder.None, f.Unit(1).Orders); Equal(UnitOrder.Fortified, f.Unit(3).Orders);
        UnitOrders.WakeByRole(f.World, f.Rules, f.Stacks, 0, negative.Roles, false);
        Equal(UnitOrder.Fortified, f.Unit(3).Orders); // Role wake applies only to members still sleeping.
    }

    private static void NavalCargoUnreachedQuotaAndFailurePrefix()
    {
        var f = CargoFixture(1, 0, 4); f.Rules.Units[4] = f.Rules.Units[0] with { Role = RuleRole.Defend };
        f.Unit(1).Orders = f.Unit(2).Orders = UnitOrder.Sleeping;
        var c = NavalCargoContext(f); c.NoMoves = true;
        Throws<InvalidOperationException>(() => ScanNavalCargo(f, c));
        Equal(UnitOrder.None, f.Unit(1).Orders); Equal(UnitOrder.Sleeping, f.Unit(2).Orders);
        Equal(null, c.DesiredDefenders);
        f.Unit(2).Orders = UnitOrder.Fortified;
        var cargo = ScanNavalCargo(f, c); // Fortified away from home never reads the missing quota.
        Equal(1, cargo.Released); Equal(0, cargo.ReservedDefenders); Equal(UnitOrder.Fortified, f.Unit(2).Orders);
        var guard = new GuardFixture(); var ordinary = guard.Context(); guard.Guard.City(ordinary);
        Equal(null, ordinary.DesiredDefenders); // A lone defender does not produce a quota.
    }

    private static void NavalCargoRoleWakeOrderAndPreservation()
    {
        foreach (int role in new[] { 33, 255 })
        {
            var f = CargoFixture(1, 0, 4, 5, 0);
            f.Rules.Units[4] = f.Rules.Units[2] with { Role = (RuleRole)role };
            f.Rules.Units[5] = f.Rules.Units[0] with { Role = (RuleRole)(role & 31) };
            for (int i = 0; i < 5; ++i)
            { f.Unit(i).Orders = UnitOrder.Sleeping; f.Unit(i).Carrier = new(8); f.Unit(i).Destination = new(8, 77); f.Unit(i).MovesUsed = 6; }
            f.Unit(2).Owner = new(2); f.Unit(4).Orders = UnitOrder.GoTo;
            var roles = default(UnitRoleSet).With(unchecked((sbyte)role));
            UnitOrders.WakeByRole(f.World, f.Rules, f.Stacks, 0, roles, true);
            Equal(UnitOrder.None, f.Unit(2).Orders); Equal(UnitOrder.Sleeping, f.Unit(3).Orders);
            UnitOrders.WakeByRole(f.World, f.Rules, f.Stacks, 0, roles, false);
            Equal(UnitOrder.None, f.Unit(3).Orders); Equal(UnitOrder.GoTo, f.Unit(4).Orders);
            Equal(UnitOrder.Sleeping, f.Unit(0).Orders); Equal(UnitOrder.Sleeping, f.Unit(1).Orders);
            for (int i = 0; i < 5; ++i)
            { Equal(new UnitSlot(8), f.Unit(i).Carrier); Equal(new MapPoint(8, 77), f.Unit(i).Destination); Equal((byte)6, f.Unit(i).MovesUsed); }
        }
    }
}
