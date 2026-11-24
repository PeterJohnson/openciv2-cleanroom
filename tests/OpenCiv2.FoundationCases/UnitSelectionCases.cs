using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // U04 activation: actual W03 revelation, retained V04 contact and private Session routing.
    private sealed class SelectionEffects : IUnitSelectionEffects
    {
        public List<string> Calls { get; } = [];
        public string? Fail;
        public void Focus(UnitTarget unit, MapPoint position, int owner) { Call("focus"); Equal(1, owner); }
        public void FlushInput() => Call("flush");
        public void RedrawMaps() => Call("empty");
        private void Call(string name) { Calls.Add(name); if (Fail == name) { throw new IOException(name); } }
    }
    private static void SelectionLossAndSearchOrigin()
    {
        foreach (bool lost in new[] { false, true })
        {
            var f = new RevelationFixture(); f.World.Globals.TurnCiv = 1;
            f.AddUnit(0, 1, 0, new(20, 12)); f.AddUnit(1, 1, 0, new(2, 2));
            f.Unit(0).MovesUsed = f.Unit(1).MovesUsed = 1; f.World.Globals.ActiveUnit = new(0);
            var combat = new CombatWorkspace { ActiveUnitLost = lost }; var effects = new SelectionEffects();
            var selection = new UnitSelection(f.World, f.Rules, combat, new(f.World, f.Rules, f.Stacks, f), effects);
            using var op = selection.BeginNext(new(2, 2), 0, 1, "origin");
            Equal(null, op.Resume()); Equal(true, op.Completed); Equal((UnitSlot?)new UnitSlot((ushort)(lost ? 1 : 0)), op.Selected);
            Equal(op.Selected, f.World.Globals.ActiveUnit); Equal(false, combat.ActiveUnitLost); Sequence(["focus", "flush"], effects.Calls);
            Equal(0, f.PublishedOrigins.Count); Equal(null, op.Resume(new(0))); Equal(2, effects.Calls.Count);
        }
    }
    private static void SelectionTimersAndEmpty()
    {
        foreach (var order in new[] { UnitOrder.None, UnitOrder.GoTo, UnitOrder.AiGoTo, UnitOrder.Fortifying, UnitOrder.Road, unchecked((UnitOrder)255) })
        {
            var f = new RevelationFixture(); f.World.Globals.TurnCiv = 1; f.AddUnit(0, 1, 0, new(20, 12));
            f.Unit(0).Orders = order; f.Unit(0).AuxiliaryTimer = 19;
            bool focus = order == UnitOrder.None || unchecked((sbyte)order) > 10;
            var effects = new SelectionEffects(); var combat = new CombatWorkspace();
            using var op = new UnitSelection(f.World, f.Rules, combat, new(f.World, f.Rules, f.Stacks, f), effects)
                .BeginNext(new(20, 12), 0, 1, "timer");
            Equal(null, op.Resume()); Equal(true, op.Completed); Equal((UnitSlot?)new UnitSlot(0), op.Selected);
            Equal((byte)(focus ? 0 : 19), f.Unit(0).AuxiliaryTimer); Equal(order, f.Unit(0).Orders);
            Equal(true, f.World.Map.TileAt(new(20, 14)).RevealedTo.Contains(1));
            Sequence(focus ? ["focus", "flush"] : new[] { "flush" }, effects.Calls);
        }
        var empty = new RevelationFixture(); empty.World.Globals.TurnCiv = 1; empty.AddUnit(0, 1, 0, new(20, 12));
        empty.Unit(0).Orders = UnitOrder.Fortified; var presentation = new SelectionEffects { Fail = "empty" };
        var state = new CombatWorkspace { ActiveUnitLost = true }; empty.World.Globals.ActiveUnit = new(0);
        using var none = new UnitSelection(empty.World, empty.Rules, state, new(empty.World, empty.Rules, empty.Stacks, empty), presentation)
            .BeginNext(new(20, 12), 0, 1, "empty");
        Equal(null, none.Resume()); Equal(true, none.Completed); Equal(null, none.Selected); Equal(null, empty.World.Globals.ActiveUnit);
        Equal(false, state.ActiveUnitLost); Sequence(["empty"], presentation.Calls); Equal(1, none.PresentationFailures.Count);
    }
    private static UnitSelection.Operation SelectContact(ContactFixture f, CombatWorkspace combat, SelectionEffects effects)
    {
        ContactNeighbors(f); f.World.Globals.TurnCiv = 1; f.World.Globals.ActiveUnit = new(10); f.Data.Unit(10).AuxiliaryTimer = 19;
        return new UnitSelection(f.World, f.Data.Rules, combat, ContactRevelation(f), effects).BeginNext(new(20, 12), 0, 1, "activation");
    }
    private static void SelectionRetainsActualContact()
    {
        foreach (bool simultaneous in new[] { false, true })
        {
            var f = new ContactFixture(); var combat = new CombatWorkspace { ActiveUnitLost = true }; var effects = new SelectionEffects();
            using var op = SelectContact(f, combat, effects);
            var pending = op.Resume(); Equal(true, pending is DiplomaticContactDecision.Audience { Value: DiplomaticAudienceDecision.Emissary });
            Equal((UnitSlot?)new UnitSlot(10), op.Selected); Equal((byte)0, f.Data.Unit(10).AuxiliaryTimer); Equal(false, combat.ActiveUnitLost);
            Sequence(["focus"], effects.Calls); Equal(false, f.Data.Unit(12).Visibility.Contains(1)); Equal(pending, op.Resume());
            f.World.Multiplayer.Saved.Simultaneous = simultaneous; f.World.Globals.ActiveUnit = new(12); combat.ActiveUnitLost = true;
            Equal(null, op.Resume(new(1))); Equal(true, op.Completed); Equal((UnitSlot?)new UnitSlot(12), f.World.Globals.ActiveUnit);
            Equal((UnitSlot?)new UnitSlot(10), op.Selected); Equal(true, combat.ActiveUnitLost); Equal(true, f.Data.Unit(12).Visibility.Contains(1));
            Sequence(simultaneous ? ["focus"] : new[] { "focus", "flush" }, effects.Calls);
            f.F.T.T.Random.RequireChoicesConsumed();
        }
    }
    private static void SelectionFailuresAndDisposal()
    {
        var held = new ContactFixture(); var effects = new SelectionEffects(); var combat = new CombatWorkspace { ActiveUnitLost = true };
        using var op = SelectContact(held, combat, effects); op.Resume(); op.Dispose();
        Throws<ObjectDisposedException>(() => op.Resume(new(1))); Sequence(["focus"], effects.Calls);
        Equal((byte)0, held.Data.Unit(10).AuxiliaryTimer); Equal(false, combat.ActiveUnitLost); Equal(false, held.Data.Unit(12).Visibility.Contains(1));
        var broken = new ContactFixture(); var brokenEffects = new SelectionEffects(); var brokenCombat = new CombatWorkspace { ActiveUnitLost = true };
        using var failed = SelectContact(broken, brokenCombat, brokenEffects); broken.World.Events.Loaded = true;
        NegotiationEvent(broken.World, 1, 2); broken.F.FailEvent = true;
        Throws<InvalidOperationException>(() => failed.Resume()); Throws<InvalidOperationException>(() => failed.Resume());
        Equal((byte)0, broken.Data.Unit(10).AuxiliaryTimer); Equal(false, brokenCombat.ActiveUnitLost); Sequence(["focus"], brokenEffects.Calls);
        Equal(false, failed.Completed); Equal(false, broken.World.Civilizations[1].Treaties[2].Contact);
        foreach (string fail in new[] { "focus", "flush" })
        {
            var f = new ContactFixture(); var display = new SelectionEffects { Fail = fail }; f.F.C.FailClose = true;
            using var retained = SelectContact(f, new(), display); retained.Resume(); retained.Resume(new(1));
            Equal(true, retained.Completed); Equal(2, retained.PresentationFailures.Count); Sequence(["focus", "flush"], display.Calls);
            Equal(true, f.Data.Unit(12).Visibility.Contains(1));
        }
    }
    private static void SelectionPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(255)); var data = new RevelationFixture(runtime.Process, runtime.World);
        var f = new ContactFixture(data: data); var effects = new SelectionEffects(); ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.activate", "Activate next unit", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new UnitSelectionOperation(SelectContact(f, new(), effects), _ => recipient, _ => throw new InvalidOperationException())));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var player = SessionClient(runtime, 1); recipient = player.Identity.Participant;
        var command = actor.Submit(new("test.activate", new([]))).Correlation.Command; runtime.AdvanceOne(); var prompt = RequiredInteraction(player, command);
        Equal(null, RequiredResult(actor, command).Interaction); Equal(false, actor.Reply(prompt, 1).Accepted);
        Sequence(["focus"], effects.Calls); Equal(true, player.Reply(prompt, 1).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(false, player.Reply(prompt, 1).Accepted);
        Sequence(["focus", "flush"], effects.Calls); Equal(true, f.Data.Unit(12).Visibility.Contains(1)); f.F.T.T.Random.RequireChoicesConsumed();
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var cancel = new ContactFixture(); var display = new SelectionEffects { Fail = "flush" };
            var context = new OperationContext(cancel.World, cancel.Data.Process, cancel.F.T.T.Random,
                new OpenCiv2.Base.Randomness.PresentationRandom(1, "selection"), new(default, new(1)), default, (_, _) => { }, () => { });
            using var adapter = new UnitSelectionOperation(SelectContact(cancel, new(), display), c => new((ulong)c), _ => throw new InvalidOperationException());
            Equal(CommandStatus.AwaitingInteraction, adapter.Resume(context, new()).Status);
            var done = adapter.Resume(context, new(input)); Equal(CommandStatus.Failed, done.Status); Equal("unit_activation_presentation_failed", done.Code);
            Equal(done, adapter.Resume(context, new(input))); Sequence(["focus", "flush"], display.Calls);
        }
    }
}
