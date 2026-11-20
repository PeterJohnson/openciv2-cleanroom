using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // U01 §§6,9–11: authored basic-deletion expectations. Bounded providers do not implement V01 or N02/N03.
    private sealed class DeletionFixture : IUnitDeletionEffects
    {
        public ProcessContext Process { get; } = new();
        public GameState World { get; }
        public StackBudget Budget { get; } = new(100_000, 64);
        public List<string> Calls { get; } = [];
        public Action? OnRemoved, OnRefresh, OnCheck, OnSync;
        public DeletionFixture()
        {
            World = new(Process); World.Map.Allocate(new(8, 6)); ref var tile = ref World.Map.TileAt(new(2, 2)); tile.Features = MapFeatures.Unit | MapFeatures.Irrigation | MapFeatures.Road; tile.Owner = 1; tile.Fertility = 7;
            Civ.MilitaryCount = 10; Civ.UnitsActive[3] = 10; Civ.UnitsLost[3] = 7; World.UnitTotals[1] = 10;
        }
        public ref Civilization Civ => ref World.Civilizations[1];
        public ref Unit Unit(int slot) => ref World.Entities.Units.Storage[slot];
        public void Role(byte role, int type = 3) => Process.Rules.Definitions.Units[type] = Process.Rules.Definitions.Units[type] with { Role = (RuleRole)role };
        public void Bound(int end) { World.Entities.Units.SetEnd(end); World.Globals.UnitEnd = (short)end; }
        public void Live(int slot)
        {
            Unit(slot).Token = new((uint)slot + 1); Unit(slot).Type = new(3); Unit(slot).Owner = new(1); Unit(slot).Position = new(2, 2);
            Bound(Math.Max(World.Entities.Units.End, slot + 1));
        }
        public UnitDeletion Operation => new(World, Process.Rules.Definitions, Budget, this);
        public void UnitRemoved(MapPoint position) { Calls.Add($"removed:{position.X},{position.Y}"); OnRemoved?.Invoke(); }
        public void RefreshHomeCity(int city) { Calls.Add($"refresh:{city}"); OnRefresh?.Invoke(); }
        public void CheckCivilization(int owner, int killer) { Calls.Add($"check:{owner},{killer}"); OnCheck?.Invoke(); }
        public void SynchronizeDeletion(MapPoint position, CitySlot? home)
        {
            Calls.Add("sync"); OnSync?.Invoke(); Calls.Add($"unit:{position.X},{position.Y}");
            if (home is CitySlot selected && selected.Value > 0) Calls.Add($"home:{selected.Value}");
            Calls.Add("flush:5000");
        }
        public void Trace(string expected) => Equal(expected, string.Join(';', Calls));
    }
    private static void DeletionBookkeeping()
    {
        var f = new DeletionFixture(); f.Live(1); f.Bound(4); ref var unit = ref f.Unit(1);
        unit.Flags = StateConversion.DecodeUnitStatus(0x5703); unit.Orders = UnitOrder.Fortified; unit.Home = new(200); unit.MovesUsed = 33; unit.Damage = 9; unit.Destination = new(57, -12);
        var expected = unit; expected.Token = new(0); expected.Position = new(-200, -200); var globals = f.World.Globals;
        f.Operation.Erase(1, false); Equal(expected, unit); Equal(globals, f.World.Globals); Equal(4, f.World.Entities.Units.End);
        Equal((short)9, f.Civ.MilitaryCount); Equal((byte)9, f.Civ.UnitsActive[3]); Equal((byte)7, f.Civ.UnitsLost[3]); Equal(9, f.World.UnitTotals[1]);
        Equal(MapFeatures.Irrigation | MapFeatures.Road, f.World.Map.TileAt(new(2, 2)).Features); Equal(1, (int)f.World.Map.TileAt(new(2, 2)).Owner!); Equal((byte)7, f.World.Map.TileAt(new(2, 2)).Fertility);
        Equal(false, f.World.UnitDeletion.Active); Equal(false, f.World.UnitStacks.DetachActive); f.Trace("");
    }
    private static void DeletionCounterArithmetic()
    {
        var zero = new DeletionFixture(); zero.Live(0); zero.Civ.MilitaryCount = 0; zero.Civ.UnitsActive[3] = 0; zero.World.UnitTotals[1] = 0;
        zero.Operation.Erase(0, false); Equal((short)0, zero.Civ.MilitaryCount); Equal((byte)0, zero.Civ.UnitsActive[3]); Equal(0, zero.World.UnitTotals[1]);
        for (int role = 0; role < 256; ++role)
        {
            var f = new DeletionFixture(); f.Live(0); f.Civ.MilitaryCount = short.MinValue; f.Civ.UnitsActive[3] = 255; f.World.UnitTotals[1] = int.MinValue; f.Role((byte)role);
            f.Operation.Erase(0, false); Equal(unchecked((sbyte)role) < 5 ? short.MaxValue : short.MinValue, f.Civ.MilitaryCount);
            Equal((byte)254, f.Civ.UnitsActive[3]); Equal(int.MaxValue, f.World.UnitTotals[1]); f.Trace("");
        }
    }
    private static void DeletionScanBounds()
    {
        var f = new DeletionFixture(); for (int i = 0; i < 3; ++i) f.Live(i);
        f.Operation.Erase(0, false); Equal(3, f.World.Entities.Units.End); f.Operation.Erase(1, false); Equal(3, f.World.Entities.Units.End);
        f.Operation.Erase(2, false); Equal(2, f.World.Entities.Units.End); Equal((short)2, f.World.Globals.UnitEnd);
        f = new(); f.Live(0); f.Live(2); f.Bound(1); f.Operation.Erase(2, false);
        Equal(false, f.Unit(2).Token.Occupied); Equal(1, f.World.Entities.Units.End); Equal((short)1, f.World.Globals.UnitEnd);
    }
    private static void DeletionSleepingReferences()
    {
        var f = new DeletionFixture(); f.Live(1); f.Bound(5);
        for (int i = 0; i < 6; ++i) { f.Unit(i).Orders = UnitOrder.Sleeping; f.Unit(i).Carrier = new UnitSlot(1); f.Unit(i).Destination = new(1, 59); f.Unit(i).Flags = StateConversion.DecodeUnitStatus(0x1000); }
        f.Unit(2).Carrier = new UnitSlot(3); f.Unit(3).Orders = UnitOrder.Fortified; f.Operation.Erase(1, false);
        foreach (int i in new[] { 0, 1, 4 })
        { Equal((sbyte)UnitOrder.None, (sbyte)f.Unit(i).Orders); Equal((short)1, f.Unit(i).Destination.X); Equal((short)59, f.Unit(i).Destination.Y); Equal(StateConversion.DecodeUnitStatus((ushort)0x1000), f.Unit(i).Flags); }
        Equal((sbyte)UnitOrder.Sleeping, (sbyte)f.Unit(2).Orders); Equal((sbyte)UnitOrder.Fortified, (sbyte)f.Unit(3).Orders); Equal((sbyte)UnitOrder.Sleeping, (sbyte)f.Unit(5).Orders);
        // The removed exact tail is no longer in the wake scan.
        f = new(); f.Live(0); f.Unit(0).Orders = UnitOrder.Sleeping; f.Unit(0).Carrier = new UnitSlot(0); f.Operation.Erase(0, false); Equal((sbyte)UnitOrder.Sleeping, (sbyte)f.Unit(0).Orders);
    }
    private static void DeletionGuardsAndAuthority()
    {
        var f = new DeletionFixture(); f.World.Globals.Multiplayer = GameMode.Tcp; f.World.UnitDeletion.Active = true; f.World.UnitDeletion.Reply = 83; f.Unit(0).Next = new UnitSlot(3000); f.Budget.Steps = 0;
        f.Operation.Erase(-8, true); f.Operation.Erase(0, true); Equal((short)3000, (f.Unit(0).Next?.Value ?? -1)); Equal(true, f.World.UnitDeletion.Active); Equal(83, f.World.UnitDeletion.Reply); f.Trace("");
        f.World.UnitDeletion.Active = false; Throws<ArgumentOutOfRangeException>(() => f.Operation.Erase(3000, true)); Equal(false, f.World.UnitDeletion.Active);
        foreach (bool notify in new[] { false, true }) for (int mode = 0; mode < 7; ++mode)
        {
            f = new(); f.Live(0); f.World.Globals.Multiplayer = (GameMode)mode; var before = f.Unit(0);
            if (mode >= 3) { Throws<InvalidOperationException>(() => f.Operation.Erase(0, notify)); Equal(before, f.Unit(0)); Equal((byte)10, f.Civ.UnitsActive[3]); }
            else { f.Operation.Erase(0, notify); Equal(false, f.Unit(0).Token.Occupied); }
            Equal(false, f.World.UnitDeletion.Active);
        }
    }
    private static void DeletionNotifications()
    {
        foreach (int home in new[] { -1, 0, 200, 255 })
        {
            var f = new DeletionFixture(); f.Live(0); f.Unit(0).Home = home < 0 ? null : new((ushort)home);
            f.World.Globals.Multiplayer = GameMode.Tcp; f.World.Multiplayer.NetworkHost = true;
            f.OnRemoved = f.OnRefresh = f.OnSync = () => Equal(true, f.World.UnitDeletion.Active);
            f.Operation.Erase(0, true);
            f.Trace("removed:2,2;" + (home < 0 ? "" : $"refresh:{home};") + "sync;unit:2,2;" + (home > 0 ? $"home:{home};" : "") + "flush:5000");
            Equal(false, f.World.UnitDeletion.Active);
        }
    }
    private static void DeletionCapturedSelectors()
    {
        var f = new DeletionFixture(); f.Live(0); f.Unit(0).Home = new(200);
        f.OnRemoved = () =>
        {
            Equal(true, f.World.UnitDeletion.Active); Equal(false, f.Unit(0).Token.Occupied);
            f.Unit(0).Owner = new(2); f.Unit(0).Type = new(4); f.Unit(0).Home = new(7); f.Unit(0).Position = new(4, 4);
            f.Role(5); f.Civ.CityCount = 0; f.Civ.UnitsActive[3] = 0;
        };
        f.OnCheck = () => f.World.Globals.Multiplayer = GameMode.Tcp;
        f.OnSync = () => f.World.Globals.Multiplayer = GameMode.SinglePlayer;
        f.Operation.Erase(0, true); f.Trace("removed:2,2;refresh:200;check:1,-1;sync;unit:2,2;home:200;flush:5000"); Equal(false, f.World.UnitDeletion.Active);
    }
    private static void DeletionSettlementCheck()
    {
        for (int condition = 0; condition < 3; ++condition)
        {
            var f = new DeletionFixture(); f.Live(0); f.Role(5); f.Role(5, 4); f.Civ.UnitsActive[3] = 1; f.Civ.UnitsActive[4] = 5; f.World.Globals.CivsAlive = StateConversion.DecodeCivilizations(0xff);
            if (condition == 1) f.Civ.CityCount = 1; if (condition == 2) f.Civ.UnitsActive[3] = 2;
            f.Operation.Erase(0, false); f.Trace(condition == 0 ? "check:1,-1" : ""); Equal(StateConversion.DecodeCivilizations((byte)0xff), f.World.Globals.CivsAlive); Equal((short)10, f.Civ.MilitaryCount);
        }
    }
    private static void DeletionFailurePrefixes()
    {
        for (int stage = 0; stage < 5; ++stage)
        {
            var f = new DeletionFixture(); f.Live(0); f.Unit(0).Home = new(1); f.World.Globals.Multiplayer = GameMode.Tcp; f.World.Multiplayer.NetworkHost = true;
            Action fail = () => throw new IOException("owning child failed");
            switch (stage)
            {
                case 0: f.Unit(0).Next = new UnitSlot(3000); break;
                case 1: f.OnRemoved = fail; break;
                case 2: f.OnRefresh = fail; break;
                case 3: f.Role(5); f.Civ.UnitsActive[3] = 1; f.OnCheck = fail; break;
                case 4: f.OnSync = fail; break;
            }
            if (stage == 0) Throws<ArgumentOutOfRangeException>(() => f.Operation.Erase(0, true)); else Throws<IOException>(() => f.Operation.Erase(0, true));
            Equal(9, f.World.UnitTotals[1]); Equal(true, f.World.UnitDeletion.Active); Equal(stage == 0, f.Unit(0).Token.Occupied);
            Equal(stage == 0 ? 1 : 0, f.World.Entities.Units.End); Equal(stage == 0, f.World.UnitStacks.DetachActive);
            f.Trace(stage switch { 0 => "", 1 => "removed:2,2", 2 => "removed:2,2;refresh:1", 3 => "removed:2,2;refresh:1;check:1,-1", _ => "removed:2,2;refresh:1;sync" });
        }
        var exhausted = new DeletionFixture(); exhausted.Live(0); exhausted.Budget.Steps = 0;
        Throws<StackResourceStop>(() => exhausted.Operation.Erase(0, false)); Equal(9, exhausted.World.UnitTotals[1]); Equal(true, exhausted.Unit(0).Token.Occupied);
        Equal(true, exhausted.World.UnitDeletion.Active); Equal(false, exhausted.World.UnitStacks.DetachActive);
    }
    private static void DeletionNestedActivity()
    {
        var f = new DeletionFixture(); f.Live(0); f.Live(1); var operation = f.Operation;
        f.OnRemoved = () => { Equal(true, f.World.UnitDeletion.Active); operation.Erase(1, false); Equal(false, f.World.UnitDeletion.Active); throw new IOException("outer child failed"); };
        Throws<IOException>(() => operation.Erase(0, true)); Equal(false, f.World.UnitDeletion.Active); Equal(false, f.Unit(0).Token.Occupied); Equal(false, f.Unit(1).Token.Occupied); Equal(1, f.World.Entities.Units.End);
    }
    private static void DeletionBackingAndSignedOwner()
    {
        var f = new DeletionFixture(); f.Live(0); Throws<ArgumentOutOfRangeException>(() => f.Operation.Erase(2048, false));
        f.Unit(0).Owner = new(255); f.Operation.Erase(0, false); Equal((short)0, f.Unit(0).Position.X); Equal((short)0, f.Unit(0).Position.Y); Equal(10, f.World.UnitTotals[1]);
        f = new(); f.Live(0); f.Unit(0).Owner = new(255); f.Role(5);
        Throws<ArgumentOutOfRangeException>(() => f.Operation.Erase(0, false)); Equal(false, f.Unit(0).Token.Occupied); Equal(true, f.World.UnitDeletion.Active);
        f = new(); f.Live(0); f.Unit(0).Owner = new(8); Throws<ArgumentOutOfRangeException>(() => f.Operation.Erase(0, false)); Equal(true, f.Unit(0).Token.Occupied); Equal(true, f.World.UnitDeletion.Active);
        f = new(); f.Live(0); f.Unit(0).Type = new(255); f.Civ.MilitaryCount = 0;
        Throws<ArgumentOutOfRangeException>(() => f.Operation.Erase(0, false)); Equal(true, f.Unit(0).Token.Occupied); Equal(10, f.World.UnitTotals[1]);
        f = new(); f.Live(0); f.Unit(0).Owner = new(255); f.Unit(0).Type = new(255);
        Throws<ArgumentOutOfRangeException>(() => f.Operation.Erase(0, false)); Equal(false, f.Unit(0).Token.Occupied); Equal(0, f.World.Entities.Units.End);
    }
}
