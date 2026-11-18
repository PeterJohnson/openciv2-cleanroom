using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // U01 §§2–4,9–11: independently authored contract/regression cases. No original-peer claim.
    private sealed class StackFixture
    {
        public GameState World { get; } = new(new ProcessContext());
        public StackBudget Budget { get; } = new(50_000_000, 64);
        public List<StackDiagnostic> Reports { get; } = [];
        public List<string> Calls { get; } = [];
        public LocalStacks Local { get; }
        public StackOperations Operations { get; }
        public Action? OnSync, OnFlush;
        public StackFixture(bool network = false, bool observe = true)
        {
            World.Map.Allocate(new(8, 6)); World.Multiplayer.NetworkHost = true;
            World.Globals.Multiplayer = network ? GameMode.Tcp : GameMode.SinglePlayer;
            Local = new(World.Entities.Units, World.Map, World.UnitStacks, Budget, observe ? Reports : null);
            Operations = new(World, Budget, observe ? Reports : null, () => { Calls.Add("sync"); OnSync?.Invoke(); Calls.Add("flush"); OnFlush?.Invoke(); });
        }
        public ref Unit Unit(int slot) => ref World.Entities.Units.Storage[slot];
        public void Live(int slot, Position? position = null, byte owner = 1)
        {
            var p = position ?? new(2, 2); Unit(slot).Token = new((uint)slot + 1); Unit(slot).Owner = new(owner);
            Unit(slot).X = (short)p.X; Unit(slot).Y = (short)p.Y;
            World.Entities.Units.SetEnd(Math.Max(World.Entities.Units.End, slot + 1)); World.Globals.UnitEnd = (short)World.Entities.Units.End;
        }
        public void Chain(int[] members, Position? position = null, bool mark = false)
        {
            for (int i = 0; i < members.Length; ++i)
            {
                int slot = members[i]; Live(slot, position); Unit(slot).Previous = new((short)(i == 0 ? -1 : members[i - 1]));
                Unit(slot).Next = new((short)(i + 1 == members.Length ? -1 : members[i + 1]));
            }
            if (mark) { ref var tile = ref World.Map.TileAt(position ?? new(2, 2)); tile.Features = 0x11; tile.Owner = 1; }
        }
        public void Order(int head, params int[] expected)
        {
            int current = head, previous = -1;
            foreach (int slot in expected)
            {
                Equal(slot, current); Equal(previous, (int)Unit(current).Previous.Value);
                previous = current; current = Unit(current).Next.Value;
            }
            Equal(true, current < 0);
        }
        public void Diagnostics(params StackDiagnostic[] expected) => Equal(new OwnedList<StackDiagnostic>(expected), new(Reports));
    }
    private static void StackTraversal()
    {
        var f = new StackFixture(); f.Chain([4, 2, 7]); f.Unit(4).Previous = new(-8); f.Unit(7).Next = new(-12);
        var before = f.World.Entities.Units.Storage.ToArray();
        Equal(2, f.Local.RawForwardCount(2)); Equal(1, f.Local.Validate(2)); Equal(4, f.Local.First(7)); Equal(7, f.Local.Last(4)); Equal(-12, f.Local.Next(7));
        Equal(4, f.Local.FirstForOwnerAt(1, new(2, 2))); Equal(-1, f.Local.FirstForOwnerAt(2, new(2, 2))); Equal(-1, f.Local.FirstForOwnerAt(1, new(3, 2)));
        foreach (int negative in new[] { -1, -19, int.MinValue })
        { Equal(negative, f.Local.Next(negative)); Equal(negative, f.Local.First(negative)); Equal(negative, f.Local.Last(negative)); Equal(0, f.Local.RawForwardCount(negative)); }
        Equal(new OwnedList<Unit>(before), new(f.World.Entities.Units.Storage.ToArray())); f.Diagnostics();
    }
    private static void StackNonRepairs()
    {
        for (int kind = 0; kind < 3; ++kind)
        {
            var f = new StackFixture(); f.Chain([0, 1]);
            if (kind == 0) f.Unit(1).Token = new(0); else if (kind == 1) f.Unit(1).Previous = new(-1); else f.Unit(1).Owner = new(5);
            var first = f.Unit(0); var second = f.Unit(1);
            Equal(1, f.Local.Validate(0)); Equal(1, f.Local.Validate(1)); Equal(first, f.Unit(0)); Equal(second, f.Unit(1)); f.Diagnostics();
        }
    }
    private static void StackDeadPasses()
    {
        var f = new StackFixture(); f.Chain([0, 1, 2]); f.Unit(1).Token = new(0);
        Equal(3, f.Local.RawForwardCount(0)); Equal(0, f.Local.Validate(0)); f.Diagnostics(new StackDiagnostic(StackProblem.ForwardDead, 1, 2));
        Equal((short)1, f.Unit(0).Next.Value); Equal((short)0, f.Unit(1).Previous.Value); Equal((short)-1, f.Unit(1).Next.Value); Equal((short)-1, f.Unit(2).Previous.Value);
        f = new(); f.Chain([0, 1, 2]); f.Unit(0).Token = f.Unit(1).Token = new(0); Equal(0, f.Local.Validate(2));
        f.Diagnostics(new StackDiagnostic(StackProblem.BackwardDead, 2, 1), new(StackProblem.BackwardDead, 1, 0));
        for (int i = 0; i < 3; ++i) { Equal((short)-1, f.Unit(i).Previous.Value); Equal((short)-1, f.Unit(i).Next.Value); }
    }
    private static void StackPositionCuts()
    {
        foreach (bool backward in new[] { false, true }) foreach (bool x in new[] { false, true })
        {
            var f = new StackFixture(); f.Chain([0, 1, 2]); f.Unit(1).Y = 4; if (x) f.Unit(1).X = 4;
            Equal(0, f.Local.Validate(backward ? 2 : 0));
            var problem = backward ? x ? StackProblem.BackwardX : StackProblem.BackwardY : x ? StackProblem.ForwardX : StackProblem.ForwardY;
            f.Diagnostics(new StackDiagnostic(problem, backward ? 2 : 0, 1), new(problem, 1, backward ? 0 : 2));
            for (int i = 0; i < 3; ++i) { Equal((short)-1, f.Unit(i).Previous.Value); Equal((short)-1, f.Unit(i).Next.Value); }
        }
    }
    private static void StackSingletonDetach()
    {
        var f = new StackFixture(); f.Live(0); f.Unit(0).Flags = 0x407; f.Unit(0).Orders = UnitOrder.Sleeping; f.Unit(0).MovesUsed = 21; f.Unit(0).Home = new(0);
        var expected = f.Unit(0); expected.X = expected.Y = -200;
        ref var tile = ref f.World.Map.TileAt(new(2, 2)); tile.Features = 0x15; tile.Owner = 1; tile.Fertility = 7;
        var expectedTile = tile; expectedTile.Features = 0x14;
        f.Local.Detach(0); Equal(expected, f.Unit(0)); Equal(expectedTile, tile); Equal(false, f.World.UnitStacks.DetachActive); Equal(1, f.World.Entities.Units.End);
    }
    private static void StackMiddlePlacement()
    {
        var f = new StackFixture(); f.Chain([4, 2, 7]); ref var tile = ref f.World.Map.TileAt(new(2, 2)); tile.Features = 0x10; tile.Owner = 5; var before = tile;
        f.Local.Detach(2); f.Order(4, 4, 7); Equal((short)-200, f.Unit(2).X); Equal((short)-1, f.Unit(2).Previous.Value); Equal((short)-1, f.Unit(2).Next.Value);
        f.Local.AttachAtFront(2, new(2, 2)); Equal(2, f.Local.First(7)); f.Order(2, 2, 4, 7); Equal(before, tile); Equal(false, f.World.UnitStacks.AttachActive);
    }
    private static void StackCoordinateWidth()
    {
        var f = new StackFixture(); f.Live(0, new(-200, -200)); f.Live(1); var tile = f.World.Map.TileAt(new(2, 2));
        f.Local.AttachAtFront(0, new(65538, -65534)); Equal((short)2, f.Unit(0).X); Equal((short)2, f.Unit(0).Y);
        Equal((short)-1, f.Unit(0).Next.Value); Equal((short)-1, f.Unit(1).Previous.Value); Equal(tile, f.World.Map.TileAt(new(2, 2)));
        // Equal off-map coordinates form an ordinary owner group.
        f = new(); f.Live(0); f.Live(1); f.Local.Detach(0); f.Local.Detach(1); Equal(0, f.Local.FirstForOwnerAt(1, new(-200, -200)));
        f.Local.AttachAtFront(1, new(-200, -200)); f.Order(1, 1, 0);
    }
    private static void StackOwnerWidth()
    {
        for (int owner = 0; owner < 256; ++owner)
        {
            var f = new StackFixture(); f.Live(0, owner: (byte)owner); ref var tile = ref f.World.Map.TileAt(new(4, 2)); tile.Fertility = 7; tile.Features = 0x10;
            f.Local.Detach(0); Equal((short)(-100 * (unchecked((sbyte)owner) + 1)), f.Unit(0).X); Equal(f.Unit(0).X, f.Unit(0).Y);
            f.Local.AttachAtFront(0, new(4, 2)); Equal((byte)7, tile.Fertility); Equal((byte)0x11, tile.Features); Equal(owner <= 8 ? owner : -1, tile.Owner is byte value ? value : -1);
        }
    }
    private static void StackSelfLoop()
    {
        var f = new StackFixture(); f.Live(0); f.Unit(0).Previous = f.Unit(0).Next = new(0);
        Equal(2048, f.Local.RawForwardCount(0)); Equal(0, f.Local.Validate(0)); f.Diagnostics(new StackDiagnostic(StackProblem.Infinite, 0)); f.Order(0, 0);
        Equal((short)2, f.Unit(0).X); Equal((short)2, f.Unit(0).Y); Equal((byte)1, f.World.Map.TileAt(new(2, 2)).Features);
        Equal(1, (int)f.World.Map.TileAt(new(2, 2)).Owner!); Equal(0, f.World.UnitStacks.Suppression); Equal(false, f.World.UnitStacks.DetachActive); Equal(false, f.World.UnitStacks.AttachActive);
    }
    private static void StackFullRepair()
    {
        var f = new StackFixture(); f.Chain(Enumerable.Range(0, 2048).ToArray()); f.Unit(2047).Next = new(-7);
        Equal(0, f.Local.Validate(0)); f.Diagnostics(new StackDiagnostic(StackProblem.Infinite, 0)); Equal(2048, f.World.Entities.Units.End);
        f.Order(2047, Enumerable.Range(0, 2048).Reverse().ToArray()); Equal((short)-7, f.Unit(0).Next.Value);
        foreach (var unit in f.World.Entities.Units.Scanned) { Equal((short)2, unit.X); Equal((short)2, unit.Y); }
        Equal(false, f.World.UnitStacks.DetachActive); Equal(false, f.World.UnitStacks.AttachActive);
    }
    private static void StackExecutionStops()
    {
        var f = new StackFixture(); f.Live(0); f.Live(1); f.Unit(0).Previous = f.Unit(0).Next = new(0); f.Budget.ValidationDepth = 1;
        try { f.Local.Validate(0); throw new InvalidOperationException("Expected depth stop"); }
        catch (StackResourceStop error) { Equal((int)StackStop.ValidationDepth, (int)error.Reason); }
        f.Diagnostics(new StackDiagnostic(StackProblem.Infinite, 0)); Equal((short)-200, f.Unit(0).X); Equal((short)-1, f.Unit(0).Next.Value);
        Equal(false, f.World.UnitStacks.DetachActive); Equal(true, f.World.UnitStacks.AttachActive); Equal(0, f.World.UnitStacks.Suppression);
        f.Budget.ValidationDepth = 64; Equal(1, f.Local.Validate(1));
        f = new(); f.Chain([0, 1]); f.Unit(0).Previous = new(1); var first = f.Unit(0); var second = f.Unit(1); f.Budget.Steps = 40;
        try { f.Local.Validate(0); throw new InvalidOperationException("Expected step stop"); }
        catch (StackResourceStop error) { Equal((int)StackStop.Steps, (int)error.Reason); }
        Equal(0UL, f.Budget.Steps); Equal(first, f.Unit(0)); Equal(second, f.Unit(1)); f.Diagnostics();
    }
    private static void StackBackingFailures()
    {
        var f = new StackFixture(); f.Chain([1, 0]); f.Unit(0).Next = new(3000);
        Throws<ArgumentOutOfRangeException>(() => f.Local.Detach(0)); Equal((short)3000, f.Unit(1).Next.Value); Equal((short)1, f.Unit(0).Previous.Value);
        Equal((short)3000, f.Unit(0).Next.Value); Equal((short)2, f.Unit(0).X); Equal(true, f.World.UnitStacks.DetachActive);
        foreach (int suppression in new[] { 0, 1 }) foreach (int slot in new[] { -9, 2048, 2049, int.MaxValue })
        {
            f.World.UnitStacks.Suppression = suppression;
            Throws<ArgumentOutOfRangeException>(() => f.Local.Validate(slot)); Throws<ArgumentOutOfRangeException>(() => f.Local.BackedUnit(slot));
        }
        f = new(); f.Live(0); f.World.Entities.Units.SetEnd(0); Equal(1, f.Local.Validate(0)); // Backing is independent of scan bound.
        f.World.UnitStacks.Suppression = 1; f.Unit(0).Next = new(3000);
        Equal(3000, f.Local.Next(0)); Throws<ArgumentOutOfRangeException>(() => f.Local.Last(0));
    }
    private static void StackPassiveDiagnostics()
    {
        var observed = new StackFixture(); var quiet = new StackFixture(observe: false);
        foreach (var f in new[] { observed, quiet }) { f.Chain([0, 1, 2]); f.Unit(0).Token = f.Unit(1).Token = new(0); }
        Equal(observed.Local.Validate(2), quiet.Local.Validate(2)); Equal(2, observed.Reports.Count); Equal(0, quiet.Reports.Count);
        Equal(new OwnedList<Unit>(observed.World.Entities.Units.Storage.ToArray()), new(quiet.World.Entities.Units.Storage.ToArray()));
    }
    private static void StackAnimationLookup()
    {
        var f = new StackFixture(); f.Chain([0]); Equal(0, f.Local.FirstAt(new(2, 2))); Equal(-1, f.Local.FirstAt(new(3, 2)));
        f.World.UnitStacks.AnimationActive = true; Equal(-1, f.Local.FirstAt(new(2, 2)));
        f.World.Map.TileAt(new(2, 2)).Features |= 1; f.World.Map.TileAt(new(2, 2)).Owner = 1; Equal(0, f.Local.FirstAt(new(2, 2)));
        f.Local.Detach(0); Equal(-1, f.Local.FirstAt(new(-200, -200))); f.World.UnitStacks.AnimationActive = false; Equal(0, f.Local.FirstAt(new(-200, -200)));
    }
    private static void StackPlacementSynchronization()
    {
        var f = new StackFixture(true); f.Chain([4, 2, 7], mark: true); f.Unit(2).MovesUsed = 55; f.Unit(2).Home = new(200); f.Unit(2).Orders = UnitOrder.Sleeping;
        var before = f.Unit(2); var tile = f.World.Map.TileAt(new(2, 2));
        f.OnSync = () => { Equal(true, f.World.UnitStacks.RelocateActive); Equal(false, f.World.UnitStacks.DetachActive); Equal(false, f.World.UnitStacks.AttachActive); f.World.Globals.Multiplayer = GameMode.SinglePlayer; };
        f.OnFlush = () => Equal(true, f.World.UnitStacks.RelocateActive);
        f.Operations.RelocateInPlace(2, true); f.Order(2, 2, 4, 7); before.Previous = new(-1); before.Next = new(4); Equal(before, f.Unit(2)); Equal(tile, f.World.Map.TileAt(new(2, 2)));
        Equal("sync,flush", string.Join(',', f.Calls)); Equal(false, f.World.UnitStacks.RelocateActive);
        foreach (bool attach in new[] { false, true })
        {
            f = new(true); f.Live(0, attach ? new(-200, -200) : new(2, 2));
            f.OnSync = () => { Equal(true, attach ? f.World.UnitStacks.AttachActive : f.World.UnitStacks.DetachActive); f.World.Globals.Multiplayer = GameMode.SinglePlayer; };
            f.OnFlush = () => Equal(true, attach ? f.World.UnitStacks.AttachActive : f.World.UnitStacks.DetachActive);
            if (attach) f.Operations.AttachAtFront(0, new(2, 2), true); else f.Operations.Detach(0, true);
            Equal(false, attach ? f.World.UnitStacks.AttachActive : f.World.UnitStacks.DetachActive); Equal("sync,flush", string.Join(',', f.Calls));
        }
    }
    private static void StackBottomGuards()
    {
        var f = new StackFixture(true); f.Chain([4, 2, 7]); f.Operations.MoveToBottom(4, true); f.Order(2, 2, 7, 4); Equal("sync,flush", string.Join(',', f.Calls));
        f = new(true); f.Chain([4, 2, 7]); f.Operations.MoveToBottom(2, false); f.Order(4, 4, 7, 2); Equal(0, f.Calls.Count);
        f = new(true); f.Chain([4, 2, 7]); f.World.Multiplayer.NetworkHost = false; f.World.UnitStacks.BottomActive = true; f.World.UnitStacks.BottomReply = 35; f.Budget.Steps = 0;
        f.Operations.MoveToBottom(7, true); f.Order(4, 4, 2, 7); Equal(true, f.World.UnitStacks.BottomActive); Equal(35, f.World.UnitStacks.BottomReply); Equal(0, f.Calls.Count);
    }
    private static void StackWholeRelocation()
    {
        foreach (bool same in new[] { false, true })
        {
            var f = new StackFixture(true); f.Chain([4, 2, 7], mark: true); if (!same) f.Chain([8, 9], new(6, 2), true);
            f.Operations.RelocateStack(2, same ? new(2, 2) : new(6, 2), true); f.Order(7, same ? [7, 2, 4] : [7, 2, 4, 8, 9]);
            if (!same) { Equal((byte)0x10, f.World.Map.TileAt(new(2, 2)).Features); Equal(1, (int)f.World.Map.TileAt(new(2, 2)).Owner!); }
            foreach (int slot in new[] { 4, 2, 7 }) Equal((short)(same ? 2 : 6), f.Unit(slot).X);
            Equal("sync,flush", string.Join(',', f.Calls)); Equal(false, f.World.UnitStacks.RelocateActive);
        }
        var empty = new StackFixture(true); empty.World.Multiplayer.NetworkHost = false; empty.World.UnitStacks.RelocateActive = true; empty.World.UnitStacks.RelocateReply = 19; empty.Budget.Steps = 0;
        empty.Operations.RelocateStack(-7, new(6, 2), true); Equal("sync,flush", string.Join(',', empty.Calls)); Equal(true, empty.World.UnitStacks.RelocateActive); Equal(19, empty.World.UnitStacks.RelocateReply);
    }
    private static void StackAuthorityAndFailure()
    {
        foreach (bool sync in new[] { false, true }) for (int mode = 0; mode < 7; ++mode)
        {
            var f = new StackFixture(); f.World.Globals.Multiplayer = (GameMode)mode; f.World.Multiplayer.NetworkHost = false; f.Chain([0], mark: true); var before = f.Unit(0);
            if (mode >= 3) { Throws<InvalidOperationException>(() => f.Operations.Relocate(0, new(4, 2), sync)); Equal(before, f.Unit(0)); Equal(false, f.World.UnitStacks.RelocateActive); }
            else { f.Operations.Relocate(0, new(4, 2), sync); Equal((short)4, f.Unit(0).X); }
            Equal(0, f.Calls.Count);
        }
        var failed = new StackFixture(true); failed.Chain([0], mark: true); failed.OnSync = () => throw new IOException("sync failed");
        Throws<IOException>(() => failed.Operations.Detach(0, true)); Equal((short)-200, failed.Unit(0).X); Equal((byte)0x10, failed.World.Map.TileAt(new(2, 2)).Features);
        Equal(true, failed.World.UnitStacks.DetachActive); Equal("sync", string.Join(',', failed.Calls));
        var nested = new StackFixture(); nested.Live(0); nested.Budget.Steps = 2;
        Throws<StackResourceStop>(() => nested.Operations.Relocate(0, new(4, 2), false)); Equal((short)-200, nested.Unit(0).X);
        Equal(true, nested.World.UnitStacks.RelocateActive); Equal(false, nested.World.UnitStacks.DetachActive); Equal(false, nested.World.UnitStacks.AttachActive);
        var missing = new StackFixture(true); missing.Live(0); var noSync = new StackOperations(missing.World, missing.Budget);
        Throws<InvalidOperationException>(() => noSync.Detach(0, true)); Equal((short)-200, missing.Unit(0).X); Equal(true, missing.World.UnitStacks.DetachActive);
    }
    private static void StackRepairAuthority()
    {
        foreach (bool host in new[] { false, true })
        {
            var f = new StackFixture(true); f.World.Multiplayer.NetworkHost = host; f.Live(0); f.Unit(0).Previous = f.Unit(0).Next = new(0);
            if (host) { Equal(0, f.Operations.Validate(0)); f.Order(0, 0); Equal(true, f.World.Map.TileAt(new(2, 2)).Unit); }
            else { var before = f.Unit(0); Throws<InvalidOperationException>(() => f.Operations.Validate(0)); Equal(before, f.Unit(0)); }
            Equal(host, f.World.Multiplayer.NetworkHost); f.Diagnostics(new StackDiagnostic(StackProblem.Infinite, 0)); Equal(0, f.Calls.Count);
        }
    }
    private static void StackOrderingPermutations()
    {
        // A sequence oracle covers all orderings through five units and every selected member.
        static IEnumerable<int[]> Permutations(int[] values)
        {
            if (values.Length == 0) { yield return []; yield break; }
            foreach (int head in values) foreach (var rest in Permutations(values.Where(v => v != head).ToArray())) yield return [head, .. rest];
        }
        for (int length = 1; length <= 5; ++length) foreach (var order in Permutations(Enumerable.Range(0, length).Select(i => i * 2).ToArray()))
            foreach (int selected in order)
            {
                var front = new StackFixture(); front.Chain(order, mark: true); front.Operations.RelocateInPlace(selected, false);
                front.Order(selected, [selected, .. order.Where(i => i != selected)]);
                var bottom = new StackFixture(); bottom.Chain(order, mark: true); bottom.Operations.MoveToBottom(selected, false);
                int[] expected = [.. order.Where(i => i != selected), selected]; bottom.Order(expected[0], expected);
            }
    }
}
