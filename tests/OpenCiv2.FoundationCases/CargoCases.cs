using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // U01 §§4,6,7: specification examples use actual placement, grouping and deletion children.
    private static readonly MapPoint CargoPoint = new(10, 10);
    private static RevelationFixture CargoFixture(params int[] types)
    {
        var f = new RevelationFixture();
        f.Rules.Units[1] = f.Rules.Units[1] with { Capacity = 2 };
        for (int i = types.Length - 1; i >= 0; --i) f.AddUnit(i, 1, types[i], CargoPoint);
        return f;
    }
    private static string Chain(RevelationFixture f, int slot)
    {
        List<int> chain = [];
        for (int current = f.Stacks.First(slot); current >= 0; current = f.Stacks.Next(current)) chain.Add(current);
        return string.Join(',', chain);
    }
    private static void CargoStableShipsAndStaging()
    {
        var f = CargoFixture(0, 1, 0, 1);
        f.Stacks.ShipsFirst(0, f.Rules, false);
        Equal("1,3,0,2", Chain(f, 0));
        f = CargoFixture(0, 1, 0, 1);
        f.AddUnit(4, 1, 0, UnitStaging.Ships(1));
        f.Stacks.ShipsFirst(0, f.Rules, false);
        Equal("4,1,3,0,2", Chain(f, 0)); // Existing staging occupants join the restored group.
        f = CargoFixture(0, 1, 0, 1); f.Unit(3).Owner = new(2);
        f.Stacks.ShipsFirst(0, f.Rules, false);
        Equal("0,2", Chain(f, 0)); Equal("3", Chain(f, 3)); Equal(CargoPoint, f.Unit(3).Position);
        Equal(UnitStaging.Ships(1), f.Unit(1).Position); // Restored foreign ship belongs to its own owner's stack.
        Equal(new MapPoint(-400, -400), UnitStaging.Lifted(1));
        Equal(new MapPoint(-600, -600), UnitStaging.Ships(1));
        f.Stacks.ShipsFirst(-1, f.Rules, false);
    }
    private static void CargoHumanPriorityAndSleep()
    {
        var f = CargoFixture(1, 0, 0, 0);
        for (int i = 1; i <= 3; ++i) { f.Unit(i).Orders = UnitOrder.Sleeping; f.Unit(i).Destination = new(99, 47); }
        f.Unit(2).Carrier = new(0); f.Unit(3).Carrier = new(8);
        f.Unit(0).Flags.Aboard = true; f.Unit(3).Flags.Aboard = true;
        Equal(0, new UnitCargo(f.World, f.Rules, f.Stacks).Gather(0, false, false));
        Equal(true, f.Unit(1).Flags.Aboard && f.Unit(2).Flags.Aboard && f.Unit(0).Flags.Aboard);
        Equal(false, f.Unit(3).Flags.Aboard); Equal((UnitSlot?)new UnitSlot(0), f.Unit(1).Carrier);
        Equal(new MapPoint(0, 47), f.Unit(1).Destination); Equal((UnitSlot?)new UnitSlot(8), f.Unit(3).Carrier);
        f = CargoFixture(1, 0, 0); f.World.Map.TileAt(CargoPoint).Terrain = Terrain.Ocean;
        f.Unit(1).Orders = UnitOrder.Sleeping; f.Unit(1).Carrier = new(8);
        f.Unit(2).Orders = UnitOrder.GoTo; f.Unit(2).Destination = new(30, 77);
        Equal(1, new UnitCargo(f.World, f.Rules, f.Stacks).Gather(0, false, false));
        Equal(false, f.Unit(1).Flags.Aboard); Equal(true, f.Unit(2).Flags.Aboard);
        Equal(UnitOrder.Sleeping, f.Unit(2).Orders); Equal(new MapPoint(0, 77), f.Unit(2).Destination);
        var unit = new Unit { Orders = UnitOrder.GoTo, Carrier = new(8), Destination = new(55, 77) };
        UnitOrders.SetSleep(ref unit); Equal((UnitSlot?)null, unit.Carrier); Equal(new MapPoint(-1, 77), unit.Destination);
        unit.Carrier = new(3); UnitOrders.SetSleep(ref unit); Equal((UnitSlot?)new UnitSlot(3), unit.Carrier);
    }
    private static void CargoAiRegionalEligibility()
    {
        var f = CargoFixture(1, 0, 0, 0, 3);
        f.Rules.Units[1] = f.Rules.Units[1] with { Capacity = 5 };
        f.World.Map.TileAt(CargoPoint).Region = 6;
        for (int i = 1; i < 5; ++i) f.Unit(i).Owner = new(2);
        f.Unit(2).Owner = new(3); f.Unit(3).Orders = UnitOrder.Fortified;
        f.World.Civilizations[2].ContinentPolicy[6] = 1;
        Equal(3, new UnitCargo(f.World, f.Rules, f.Stacks).Gather(0, false, false));
        Equal(false, f.Unit(1).Flags.Aboard || f.Unit(3).Flags.Aboard);
        Equal(true, f.Unit(2).Flags.Aboard && f.Unit(4).Flags.Aboard);
        f.World.Map.TileAt(CargoPoint).Terrain = Terrain.Ocean;
        Equal(1, new UnitCargo(f.World, f.Rules, f.Stacks).Gather(0, false, false));
        for (int i = 1; i < 5; ++i) Equal(true, f.Unit(i).Flags.Aboard);
        for (int i = 0; i < 5; ++i) f.Unit(i).Position = new(-80, -80);
        f.Unit(3).Orders = UnitOrder.Fortified;
        Equal(2, new UnitCargo(f.World, f.Rules, f.Stacks).Gather(0, false, false)); // Off-map bypasses policy, not fortification.
    }
    private static void CargoAircraftAndSignedCapacity()
    {
        var f = CargoFixture(1, 2, 2, 0);
        f.Rules.Units[1] = f.Rules.Units[1] with { Capacity = 0, Abilities = new() { Submarine = true } };
        f.Rules.Units[4] = f.Rules.Units[2] with { Abilities = new() { DestroyedAfterAttack = true } };
        f.Unit(2).Type = new(4); f.Unit(2).Orders = UnitOrder.Fortified;
        Equal(19, new UnitCargo(f.World, f.Rules, f.Stacks).Gather(0, false, false));
        Equal(false, f.Unit(1).Flags.Aboard || f.Unit(3).Flags.Aboard);
        Equal(true, f.Unit(2).Flags.Aboard); Equal(UnitOrder.None, f.Unit(2).Orders);
        f.Rules.Units[1] = f.Rules.Units[1] with { Capacity = 255, Abilities = new() { CarryAircraft = true } };
        f.Unit(1).Orders = UnitOrder.Sleeping; f.Unit(1).Carrier = new(9);
        Equal(18, new UnitCargo(f.World, f.Rules, f.Stacks).Gather(0, false, false));
        Equal(UnitOrder.Sleeping, f.Unit(1).Orders); Equal((UnitSlot?)new UnitSlot(9), f.Unit(1).Carrier);
        f = CargoFixture(1, 0, 0, 0); f.World.Map.TileAt(CargoPoint).Terrain = Terrain.Ocean;
        f.Rules.Units[1] = f.Rules.Units[1] with { Capacity = 128 };
        Equal(-131, new UnitCargo(f.World, f.Rules, f.Stacks).Gather(0, false, false));
        f.Unit(0).Owner = new(0);
        Equal(17, new UnitCargo(f.World, f.Rules, f.Stacks).Gather(0, false, false));
    }
    private static void CargoLiftAndRepeatedQueries()
    {
        var f = CargoFixture(1, 0, 0);
        f.Unit(1).Orders = f.Unit(2).Orders = UnitOrder.Sleeping;
        var cargo = new UnitCargo(f.World, f.Rules, f.Stacks);
        cargo.Lift(0, false);
        Equal("2,1,0", Chain(f, 0));
        for (int i = 0; i < 3; ++i) { Equal(UnitStaging.Lifted(1), f.Unit(i).Position); Equal(false, f.Unit(i).Flags.Aboard); }
        Equal((UnitSlot?)new UnitSlot(0), f.Unit(2).Carrier);
        Equal(false, f.World.Map.TileAt(CargoPoint).Features.HasFlag(MapFeatures.Unit));
        Equal(false, f.World.UnitStacks.CargoActive || f.World.UnitStacks.LiftActive);
        cargo.Lift(-1, false);
        f = CargoFixture(1, 1, 0, 0); f.World.Map.TileAt(CargoPoint).Terrain = Terrain.Ocean;
        f.Rules.Units[4] = f.Rules.Units[1] with { Capacity = 3 }; f.Unit(1).Type = new(4);
        cargo = new(f.World, f.Rules, f.Stacks);
        Equal(3, cargo.MaximumRemaining(0, false)); // First ship binds both; the second clears their aboard flags but cannot take them.
        Equal(false, f.Unit(2).Flags.Aboard || f.Unit(3).Flags.Aboard);
        Equal((UnitSlot?)new UnitSlot(0), f.Unit(2).Carrier);
        f = CargoFixture(1, 0, 0); f.Unit(2).Owner = new(2);
        f.Unit(1).Orders = UnitOrder.Sleeping;
        new UnitCargo(f.World, f.Rules, f.Stacks).Lift(0, false);
        Equal("1,0", Chain(f, 0)); Equal(UnitStaging.Lifted(2), f.Unit(2).Position);
    }
    private sealed class LossPresentation : IUnitLossEffects
    {
        public List<string> Calls { get; } = [];
        public bool FailRedraw;
        public void UnitsChangedAt(MapPoint p) => Calls.Add($"changed:{p.X},{p.Y}");
        public void RedrawTile(MapPoint p)
        {
            Calls.Add($"draw:{p.X},{p.Y}");
            if (FailRedraw) throw new IOException("view unavailable");
        }
        public void SynchronizeLoss(MapPoint? redraw) => throw new InvalidOperationException("This fixture has no transport");
    }
    private static UnitLoss Loss(RevelationFixture f, LossPresentation presentation) => new(f.World, f.Rules, f.Stacks,
        new(f.World, f.Rules, f.Stacks), new(f.World, f.Rules, new(100000, 64), f), presentation);
    private static void CargoLossSeaLandAndStaging()
    {
        var f = CargoFixture(1, 0, 0); f.World.Map.TileAt(CargoPoint).Terrain = Terrain.Ocean;
        f.Rules.Units[1] = f.Rules.Units[1] with { Capacity = 1 };
        var effects = new LossPresentation(); Loss(f, effects).DestroyAndRedraw(0, true);
        for (int i = 0; i < 3; ++i) Equal(false, f.Unit(i).Token.Occupied);
        Equal(2, f.World.Entities.Units.End); Equal("changed:10,10;draw:10,10", string.Join(';', effects.Calls));
        Equal(0, f.Removed.Count); Equal(false, f.World.UnitDeletion.Active || f.World.UnitDeletion.CargoActive || f.World.UnitDeletion.DestroyActive);
        f = CargoFixture(1, 0, 0); f.Unit(1).Orders = UnitOrder.Sleeping; f.Unit(1).Carrier = new(0);
        effects = new(); Loss(f, effects).DestroyAndRedraw(0, true);
        Equal(false, f.Unit(0).Token.Occupied); Equal(true, f.Unit(1).Token.Occupied && f.Unit(2).Token.Occupied);
        Equal(UnitOrder.None, f.Unit(1).Orders); Equal("draw:10,10", string.Join(';', effects.Calls));
        f = CargoFixture(1, 0, 0); f.Stacks.RelocateStack(0, new(-400, -99), false);
        effects = new(); Loss(f, effects).EraseWithCargo(0, false);
        Equal(0, effects.Calls.Count); Equal(false, f.Unit(2).Token.Occupied); // X alone selects lifted-stack loss.
        f = CargoFixture(1, 0); f.Stacks.RelocateStack(0, new(-80, -99), false);
        effects = new(); Loss(f, effects).EraseWithCargo(0, false);
        Equal("changed:-80,-99", string.Join(';', effects.Calls));
        f = CargoFixture(0, 1); f.World.Map.TileAt(CargoPoint).Terrain = Terrain.Ocean;
        Loss(f, new()).EraseWithCargo(0, false); Equal(true, f.Unit(1).Token.Occupied);
    }
    private static void CargoFailureAndAuthority()
    {
        var f = CargoFixture(1, 0); var effects = new LossPresentation { FailRedraw = true };
        Throws<IOException>(() => Loss(f, effects).DestroyAndRedraw(0, false));
        Equal(false, f.Unit(0).Token.Occupied); Equal(true, f.World.UnitDeletion.DestroyActive); Equal(false, f.World.UnitDeletion.CargoActive);
        f = CargoFixture(1, 0); f.World.Globals.Multiplayer = GameMode.Tcp;
        var cargo = new UnitCargo(f.World, f.Rules, f.Stacks);
        Throws<InvalidOperationException>(() => cargo.Gather(0, false, false));
        Throws<InvalidOperationException>(() => Loss(f, new()).EraseWithCargo(0, false));
        Equal(true, f.Unit(0).Token.Occupied); Equal(false, f.World.UnitStacks.CargoActive || f.World.UnitDeletion.CargoActive);
        f = CargoFixture(1, 0); f.Unit(1).Orders = UnitOrder.Sleeping; f.Unit(1).Type = new(255);
        Throws<IndexOutOfRangeException>(() => new UnitCargo(f.World, f.Rules, f.Stacks).Gather(0, true, false));
        Equal(UnitStaging.Ships(1), f.Unit(0).Position); Equal(true, f.World.UnitStacks.CargoActive);
    }
}
