using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static UnitStackSelection StackSelector(RevelationFixture f, SelectionEffects effects, UnitRevelation? reveal = null)
    {
        reveal ??= new(f.World, f.Rules, f.Stacks, f);
        var selection = new UnitSelection(f.World, f.Rules, new(), reveal, effects);
        return new(f.World, f.Rules, f.Stacks, new(f.World, f.Rules, selection, effects), reveal);
    }
    private static void StackSelectionEntryGates()
    {
        foreach (bool moving in new[] { false, true })
            foreach (int owner in new[] { 1, 2 })
                foreach (int turn in new[] { 1, 2 })
                    foreach (bool full in new[] { false, true })
                    {
                        var f = new RevelationFixture(); f.World.Globals.TurnCiv = (sbyte)turn; f.World.Globals.RevealMap = (byte)(full ? 1 : 0);
                        f.World.MovementMode = moving ? MovementMode.MoveUnits : MovementMode.View;
                        f.AddUnit(0, owner, 0, new(20, 12)); f.World.Globals.ActiveUnit = new(0);
                        f.Unit(0).MovesUsed = 1; f.Unit(0).Orders = UnitOrder.Fortified; f.Unit(0).Flags.Automated = true;
                        var effects = new SelectionEffects(); using var op = StackSelector(f, effects).Begin(new(20, 12), 1, "entry");
                        Equal(null, op.Resume()); Equal(true, op.Completed);
                        bool selected = !moving && owner == 1 && (turn == 1 || full);
                        Equal(selected ? (UnitSlot?)new UnitSlot(0) : null, op.Selected);
                        Equal(selected ? UnitOrder.None : UnitOrder.Fortified, f.Unit(0).Orders);
                        Equal(!selected, f.Unit(0).Flags.Automated);
                        if (!selected) { Equal(0, effects.Calls.Count); }
                    }
        var empty = new RevelationFixture(); empty.World.Globals.TurnCiv = 1;
        foreach (var mode in new[] { MovementMode.View, MovementMode.MoveUnits })
        {
            empty.World.MovementMode = mode;
            using var op = StackSelector(empty, new()).Begin(new(2, 2), 1, "empty");
            Equal(null, op.Resume()); Equal(true, op.Completed); Equal(null, op.Selected);
        }
        var multiple = new RevelationFixture(); multiple.World.Globals.TurnCiv = 2;
        multiple.AddUnit(0, 1, 0, new(2, 2)); multiple.AddUnit(1, 1, 0, new(2, 2)); multiple.World.Globals.ActiveUnit = new(0);
        foreach (var mode in new[] { MovementMode.View, MovementMode.MoveUnits })
        {
            multiple.World.MovementMode = mode;
            using var op = StackSelector(multiple, new()).Begin(new(2, 2), 1, "multiple");
            Equal(mode == MovementMode.MoveUnits, op.Resume() is UnitStackDecision.Choose);
            Equal(mode == MovementMode.View, op.Completed);
        }
    }
    private static void StackSelectionRowsAndWake()
    {
        var f = new RevelationFixture(); f.World.Globals.TurnCiv = 1; f.World.MovementMode = MovementMode.View;
        f.World.Names[1].Adjective = "Alder"; f.Rules.Units[3] = f.Rules.Units[3] with { Identity = new("trade", "Caravan") };
        f.Rules.Commodities[2] = new("cloth", "Cloth"); f.AddCity(0, 1, new(10, 10)); f.World.Entities.Cities.Storage[0].Name = "Home";
        for (int i = 0; i < 12; i++) { f.AddUnit(i, 1, 3, new(20, 12)); f.Unit(i).Orders = UnitOrder.Fortified; f.Unit(i).Flags.Automated = true; }
        f.Unit(11).Counter = 2; f.Unit(11).Home = new(0); f.Unit(11).Flags.Veteran = true; f.Unit(10).Counter = 255;
        f.World.Globals.ActiveUnit = new(1); var effects = new SelectionEffects(); using var op = StackSelector(f, effects).Begin(new(20, 12), 1, "rows");
        var menu = (UnitStackDecision.Choose)op.Resume()!; Equal(menu, op.Resume()); Equal(9, menu.Rows.Count);
        Equal(menu, op.Resume(new UnitStackReply.Choose(-1, true))); Equal(false, op.Completed);
        Equal(UnitOrder.Fortified, f.Unit(11).Orders); Equal(true, f.Unit(11).Flags.Automated);
        Sequence(Enumerable.Range(3, 9).Reverse().Select(i => (ushort)i), menu.Rows.Select(r => r.Unit.Slot.Value));
        Equal(new UnitStackRow(new(new(11), f.Unit(11).Token), "Alder", true, "Caravan", "Home", "Cloth"), menu.Rows[0]);
        Equal("Freight", menu.Rows[1].Cargo); Equal(null, menu.Rows[1].Home);
        // The captured row's raw wake writes survive even a changed identity/owner and cancellation.
        f.Unit(11).Token = default; f.Unit(11).Owner = new(2); f.Unit(11).MovesUsed = 6; f.Unit(11).AuxiliaryTimer = 19;
        var before = f.Unit(11); var next = (UnitStackDecision.Choose)op.Resume(new UnitStackReply.Choose(0, true))!;
        Equal(false, ReferenceEquals(menu, next)); Equal(menu.Rows, next.Rows);
        var expected = before; expected.Orders = UnitOrder.None; expected.Flags.Automated = false;
        Equal(expected, f.Unit(11)); Equal(UnitOrder.Fortified, f.Unit(10).Orders);
        Equal(null, op.Resume(new UnitStackReply.Choose(-1))); Equal(true, op.Completed); Equal(null, op.Selected);
        Equal((UnitSlot?)new UnitSlot(1), f.World.Globals.ActiveUnit); Equal(MovementMode.View, f.World.MovementMode); Equal(0, effects.Calls.Count);
        var invalid = new RevelationFixture(); invalid.World.Globals.TurnCiv = 1;
        for (int i = 0; i < 12; i++) { invalid.AddUnit(i, 1, 0, new(20, 12)); }
        invalid.Unit(0).Next = new(ushort.MaxValue);
        using var bad = StackSelector(invalid, new()).Begin(new(20, 12), 1, "invalid-tail");
        Throws<ArgumentOutOfRangeException>(() => bad.Resume()); Throws<InvalidOperationException>(() => bad.Resume());
    }
    private static void StackSelectionAcceptedContact()
    {
        var f = new ContactFixture(); ContactNeighbors(f); f.World.Globals.TurnCiv = 1; f.World.MovementMode = MovementMode.View;
        f.Data.AddUnit(9, 1, 0, new(20, 12)); f.Data.Unit(10).Orders = UnitOrder.Fortified; f.Data.Unit(10).Flags.Automated = true;
        var effects = new SelectionEffects(); using var op = StackSelector(f.Data, effects, ContactRevelation(f)).Begin(new(20, 12), 1, "accept");
        var menu = (UnitStackDecision.Choose)op.Resume()!; Equal((ushort)10, menu.Rows[1].Unit.Slot.Value);
        var pending = op.Resume(new UnitStackReply.Choose(1)); Equal(true, pending is UnitStackDecision.Contact);
        Equal(pending, op.Resume()); Equal((UnitSlot?)new UnitSlot(10), op.Selected); Equal(UnitOrder.None, f.Data.Unit(10).Orders);
        Equal(false, f.Data.Unit(10).Flags.Automated); Equal(MovementMode.MoveUnits, f.World.MovementMode); Sequence(["focus"], effects.Calls);
        Equal(false, f.Data.Unit(12).Visibility.Contains(1));
        Equal(null, op.Resume(new UnitStackReply.Contact(new(1)))); Equal(true, op.Completed); Equal(true, f.Data.Unit(12).Visibility.Contains(1));
        Sequence(["focus"], effects.Calls); op.Resume(); Sequence(["focus"], effects.Calls); f.F.T.T.Random.RequireChoicesConsumed();
    }
    private static void StackSelectionOriginalSlotTail()
    {
        var f = new ContactFixture(); ContactNeighbors(f); f.World.Globals.TurnCiv = 1;
        f.Data.AddUnit(0, 1, 0, new(2, 2)); f.Data.Unit(0).MovesUsed = 6;
        f.Data.AddUnit(14, 3, 0, new(4, 2));
        var effects = new SelectionEffects(); using var op = StackSelector(f.Data, effects, ContactRevelation(f)).BeginSelected(new(0), new(2, 2), 1, "tail");
        Equal(true, op.Resume() is UnitStackDecision.Contact); Equal((UnitSlot?)new UnitSlot(0), op.Selected);
        Equal((UnitSlot?)new UnitSlot(10), f.World.Globals.ActiveUnit);
        f.Data.AddUnit(13, 1, 0, new(2, 4)); f.Data.Unit(13).MovesUsed = 1; f.World.Globals.ActiveUnit = new(13); f.Data.Unit(0).MovesUsed = 0;
        Equal(true, op.Resume(new UnitStackReply.Contact(new(1))) is UnitStackDecision.Contact);
        Equal((UnitSlot?)new UnitSlot(13), f.World.Globals.ActiveUnit); Sequence(["focus", "flush", "focus"], effects.Calls);
        Equal(true, f.World.Civilizations[1].Treaties[2].Contact); Equal(false, op.Completed);
        Equal(null, op.Resume(new UnitStackReply.Contact(new(1)))); Equal(true, op.Completed);
        Equal(true, f.World.Civilizations[1].Treaties[3].Contact); Equal(true, f.World.Map.TileAt(new(2, 2)).RevealedTo.Contains(1));
        Equal((UnitSlot?)new UnitSlot(13), f.World.Globals.ActiveUnit); f.F.T.T.Random.RequireChoicesConsumed();
    }
    private static void StackSelectionFailurePrefixes()
    {
        var f = new ContactFixture(); ContactNeighbors(f); f.World.Globals.TurnCiv = 1; f.Data.Unit(10).Orders = UnitOrder.Fortified;
        using var op = StackSelector(f.Data, new(), ContactRevelation(f)).BeginSelected(new(10), new(20, 12), 1, "dispose");
        op.Resume(); op.Dispose(); Throws<ObjectDisposedException>(() => op.Resume(new UnitStackReply.Contact(new(1))));
        Equal(UnitOrder.None, f.Data.Unit(10).Orders); Equal((UnitSlot?)new UnitSlot(10), f.World.Globals.ActiveUnit); Equal(false, f.Data.Unit(12).Visibility.Contains(1));
        var broken = new ContactFixture(); ContactNeighbors(broken); broken.World.Globals.TurnCiv = 1;
        broken.World.Events.Loaded = true; NegotiationEvent(broken.World, 1, 2); broken.F.FailEvent = true;
        using var failed = StackSelector(broken.Data, new(), ContactRevelation(broken)).BeginSelected(new(10), new(20, 12), 1, "fail");
        Throws<InvalidOperationException>(() => failed.Resume()); Throws<InvalidOperationException>(() => failed.Resume());
        Equal((UnitSlot?)new UnitSlot(10), broken.World.Globals.ActiveUnit); Equal(MovementMode.MoveUnits, broken.World.MovementMode);
        var display = new ContactFixture(); ContactNeighbors(display); display.World.Globals.TurnCiv = 1; display.F.C.FailClose = true;
        var effects = new SelectionEffects { Fail = "focus" };
        using var cosmetic = StackSelector(display.Data, effects, ContactRevelation(display)).BeginSelected(new(10), new(20, 12), 1, "cosmetic");
        cosmetic.Resume(); cosmetic.Resume(new UnitStackReply.Contact(new(1))); Equal(true, cosmetic.Completed); Equal(2, cosmetic.PresentationFailures.Count);
        Equal(true, display.Data.Unit(12).Visibility.Contains(1));
    }
    private static void StackSelectionPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(255)); var data = new RevelationFixture(runtime.Process, runtime.World);
        var f = new ContactFixture(data: data); ContactNeighbors(f); f.World.Globals.TurnCiv = 1; f.World.MovementMode = MovementMode.View;
        data.AddUnit(9, 1, 0, new(20, 12)); data.Unit(10).Orders = UnitOrder.Fortified; data.Unit(10).Flags.Automated = true;
        ParticipantId recipient = default; var effects = new SelectionEffects();
        runtime.RegisterCommand(new(new(1, "test.stack", "Select stack", AccessClass.Player, new([]), new([])), (_, _, _) => new(),
            _ => new UnitStackSelectionOperation(StackSelector(data, effects, ContactRevelation(f)).Begin(new(20, 12), 1, "session"),
                _ => recipient, _ => throw new InvalidOperationException())));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var player = SessionClient(runtime, 1); recipient = player.Identity.Participant;
        var command = actor.Submit(new("test.stack", new([]))).Correlation.Command; runtime.AdvanceOne(); var first = RequiredInteraction(player, command);
        Equal(null, RequiredResult(actor, command).Interaction); Equal(false, actor.Reply(first, 10).Accepted);
        Equal(true, player.Reply(first, 10).Accepted); runtime.AdvanceOne(); var second = RequiredInteraction(player, command);
        Equal(false, player.Reply(first, 1).Accepted); Equal(UnitOrder.None, data.Unit(10).Orders); Equal(false, data.Unit(10).Flags.Automated);
        Equal(true, player.Reply(second, 1).Accepted); runtime.AdvanceOne(); var meeting = RequiredInteraction(player, command);
        Equal(null, RequiredResult(actor, command).Interaction); Equal(true, player.Reply(meeting, 1).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(true, data.Unit(12).Visibility.Contains(1));
        f.F.T.T.Random.RequireChoicesConsumed();
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var local = new RevelationFixture(); local.World.Globals.TurnCiv = 1;
            local.AddUnit(0, 1, 0, new(2, 2)); local.AddUnit(1, 1, 0, new(2, 2)); local.Unit(1).Orders = UnitOrder.Fortified;
            var context = new OperationContext(local.World, local.Process, f.F.T.T.Random,
                new OpenCiv2.Base.Randomness.PresentationRandom(1, "stack"), new(default, new(1)), default, (_, _) => { }, () => { });
            using var adapter = new UnitStackSelectionOperation(StackSelector(local, new()).Begin(new(2, 2), 1, "cancel"), c => new((ulong)c), _ => throw new InvalidOperationException());
            Equal(CommandStatus.AwaitingInteraction, adapter.Resume(context, new()).Status);
            Equal(CommandStatus.AwaitingInteraction, adapter.Resume(context, new(OperationInputKind.Choice, 9)).Status);
            var done = adapter.Resume(context, new(input)); Equal(CommandStatus.Completed, done.Status); Equal(done, adapter.Resume(context, new(input)));
            Equal(UnitOrder.None, local.Unit(1).Orders); Equal(null, local.World.Globals.ActiveUnit);
        }
    }
}
