using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void FocusExistingUnit()
    {
        foreach (var mode in new[] { MovementMode.View, MovementMode.MoveUnits })
        {
            var f = new RevelationFixture(); f.World.Globals.TurnCiv = 1;
            f.AddUnit(0, 1, 0, new(20, 12)); f.World.Globals.ActiveUnit = new(0);
            f.World.MovementMode = mode; f.Unit(0).AuxiliaryTimer = 19;
            var combat = new CombatWorkspace { ActiveUnitLost = true }; var effects = new SelectionEffects();
            var selection = new UnitSelection(f.World, f.Rules, combat, new(f.World, f.Rules, f.Stacks, f), effects);
            using var op = new UnitFocus(f.World, f.Rules, selection, effects).Begin(new(2, 2), 0, 1, "existing");
            Equal(null, op.Resume()); Equal(true, op.Completed); Equal(mode == MovementMode.View, op.InitialFocus);
            Equal((UnitTarget?)new UnitTarget(new(0), f.Unit(0).Token), op.Focused);
            Equal(MovementMode.MoveUnits, f.World.MovementMode); Equal((byte)19, f.Unit(0).AuxiliaryTimer);
            Equal(true, combat.ActiveUnitLost); Equal(0, f.PublishedOrigins.Count); Sequence(["focus"], effects.Calls);
            op.Resume(); Sequence(["focus"], effects.Calls);
        }
    }
    private static UnitFocus.Operation FocusContact(ContactFixture f, SelectionEffects effects)
    {
        ContactNeighbors(f); f.World.Globals.TurnCiv = 1; f.World.Globals.ActiveUnit = null;
        f.World.MovementMode = MovementMode.View;
        var selection = new UnitSelection(f.World, f.Data.Rules, new(), ContactRevelation(f), effects);
        return new UnitFocus(f.World, f.Data.Rules, selection, effects).Begin(new(20, 12), 0, 1, "focus");
    }
    private static void FocusRetainsAndRevalidates()
    {
        foreach (int replacement in new[] { 10, 13, 12, -1 })
        {
            var f = new ContactFixture(); var effects = new SelectionEffects(); using var op = FocusContact(f, effects);
            var pending = op.Resume(); Equal(true, pending is DiplomaticContactDecision.Audience);
            Equal(MovementMode.MoveUnits, f.World.MovementMode); Equal(false, op.InitialFocus);
            Equal(false, op.Completed); Equal(null, op.Focused); Equal(pending, op.Resume()); Sequence(["focus"], effects.Calls);
            f.Data.AddUnit(13, 1, 0, new(2, 2)); f.Data.Unit(13).MovesUsed = 1;
            f.World.Globals.ActiveUnit = replacement < 0 ? null : new((ushort)replacement);
            Equal(null, op.Resume(new(1))); Equal(true, op.Completed);
            bool eligible = replacement is 10 or 13;
            Equal(eligible ? (UnitTarget?)new UnitTarget(new((ushort)replacement), f.Data.Unit(replacement).Token) : null, op.Focused);
            Equal(replacement < 0 ? null : (UnitSlot?)new UnitSlot((ushort)replacement), f.World.Globals.ActiveUnit);
            Sequence(eligible ? ["focus", "flush", "focus"] : new[] { "focus", "flush", "empty" }, effects.Calls);
            Equal(true, f.Data.Unit(12).Visibility.Contains(1)); f.F.T.T.Random.RequireChoicesConsumed();
        }
    }
    private static void FocusFailurePrefixes()
    {
        var held = new ContactFixture(); var effects = new SelectionEffects(); using var op = FocusContact(held, effects);
        op.Resume(); op.Dispose(); Throws<ObjectDisposedException>(() => op.Resume(new(1)));
        Equal(MovementMode.MoveUnits, held.World.MovementMode); Equal((UnitSlot?)new UnitSlot(10), held.World.Globals.ActiveUnit);
        Equal(false, held.Data.Unit(12).Visibility.Contains(1)); Sequence(["focus"], effects.Calls);
        var broken = new ContactFixture(); using var failed = FocusContact(broken, new());
        broken.World.Events.Loaded = true; NegotiationEvent(broken.World, 1, 2); broken.F.FailEvent = true;
        Throws<InvalidOperationException>(() => failed.Resume()); Throws<InvalidOperationException>(() => failed.Resume());
        Equal(false, failed.Completed); Equal(MovementMode.MoveUnits, broken.World.MovementMode);
        Equal((UnitSlot?)new UnitSlot(10), broken.World.Globals.ActiveUnit);
        var empty = new RevelationFixture(); empty.World.Globals.TurnCiv = 1;
        var display = new SelectionEffects { Fail = "empty" };
        using var none = new UnitFocus(empty.World, empty.Rules,
            new(empty.World, empty.Rules, new(), new(empty.World, empty.Rules, empty.Stacks, empty), display), display)
            .Begin(new(2, 2), 0, 1, "empty");
        none.Resume(); Equal(true, none.Completed); Equal(null, none.Focused); Equal(false, none.InitialFocus);
        Equal(2, none.PresentationFailures.Count); Sequence(["empty", "empty"], display.Calls);
    }
    private static void FocusPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(255)); var data = new RevelationFixture(runtime.Process, runtime.World);
        var f = new ContactFixture(data: data); var effects = new SelectionEffects(); ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.focus", "Focus unit", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new UnitFocusOperation(FocusContact(f, effects), _ => recipient, _ => throw new InvalidOperationException())));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var player = SessionClient(runtime, 1); recipient = player.Identity.Participant;
        var command = actor.Submit(new("test.focus", new([]))).Correlation.Command; runtime.AdvanceOne(); var prompt = RequiredInteraction(player, command);
        Equal(null, RequiredResult(actor, command).Interaction); Equal(false, actor.Reply(prompt, 1).Accepted);
        Equal(true, player.Reply(prompt, 1).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(false, player.Reply(prompt, 1).Accepted);
        Sequence(["focus", "flush", "focus"], effects.Calls); f.F.T.T.Random.RequireChoicesConsumed();
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var cancel = new ContactFixture(); var display = new SelectionEffects { Fail = "focus" };
            var context = new OperationContext(cancel.World, cancel.Data.Process, cancel.F.T.T.Random,
                new OpenCiv2.Base.Randomness.PresentationRandom(1, "focus"), new(default, new(1)), default, (_, _) => { }, () => { });
            using var adapter = new UnitFocusOperation(FocusContact(cancel, display), c => new((ulong)c), _ => throw new InvalidOperationException());
            Equal(CommandStatus.AwaitingInteraction, adapter.Resume(context, new()).Status);
            var done = adapter.Resume(context, new(input)); Equal(CommandStatus.Failed, done.Status); Equal("unit_focus_presentation_failed", done.Code);
            Equal(done, adapter.Resume(context, new(input))); Sequence(["focus", "flush", "focus"], display.Calls);
        }
    }
    private static void LocalSelectionFocusReplay()
    {
        foreach (bool console in new[] { false, true })
            foreach (bool allSpent in new[] { false, true })
            {
                var recorder = new LocalReplayRecorder(new string('f', 64)); using var game = new LocalGame(recorder);
                var stored = ClassicFiles.DecodeStoredSave(game.SaveClassic());
                var units = stored.Units.ToArray();
                for (int i = 0; i < units.Length; i++)
                { units[i] = units[i] with { MovesUsed = (byte)(i == 0 || allSpent ? 6 : 0), Orders = i == 0 ? UnitOrder.Fortified : UnitOrder.None }; }
                Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(stored with { Units = new(units) })));
                var requested = game.Player.OwnUnits().Records.Single(u => u.Slot.Value == 0);
                var target = new UnitTarget(requested.Slot, requested.Token);
                RunLocal(game, console, $"unit.select unit={target.Slot.Value}:{target.Token.Value}", LocalPlayCommands.Select,
                    new NamedValue("unit", new CommandValue.Unit(target)));
                var active = game.Player.ObserveMap()!.Annotations!.ActiveUnit;
                Equal(allSpent, active is null); if (active is { } other) { Equal((ushort)1, other.Slot.Value); }
                var saved = game.SaveClassic(); var after = ClassicFiles.DecodeStoredSave(saved);
                Equal(UnitOrder.None, after.Units[0].Orders); Equal((byte)6, after.Units[0].MovesUsed);
                BytesEqual(saved, LocalReplay.Run(recorder.Capture()));
                Equal(LocalLoadResult.Loaded, game.LoadClassic(saved));
                var reloaded = game.SaveClassic(); var restored = ClassicFiles.DecodeStoredSave(reloaded);
                // Reload allocates fresh command identities; all other stored bytes must survive.
                var expectedUnits = after.Units.ToArray();
                for (int i = 0; i < expectedUnits.Length; i++)
                {
                    Equal(false, expectedUnits[i].Token == restored.Units[i].Token);
                    expectedUnits[i] = expectedUnits[i] with { Token = restored.Units[i].Token };
                }
                BytesEqual(ClassicFiles.EncodeStoredSave(after with { Units = new(expectedUnits) }), reloaded);
                BytesEqual(reloaded, LocalReplay.Run(recorder.Capture()));
            }
    }
}
