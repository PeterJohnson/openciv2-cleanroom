using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // Session player admission around actual U02 movement, U04 focus and V08 outcomes.
    private sealed class PlayerMoveFixture
    {
        public SinglePlayerMovementFixture F { get; }
        public SelectionEffects Effects { get; } = new();
        public PlayerMovementBinding Binding { get; }
        public Func<OperationContext>? Borrowed;
        public int Bindings, CursorReads;
        public PlayerMoveFixture(RevelationFixture? data = null, Func<int, ParticipantId>? recipient = null)
        {
            F = new(data: data); var world = F.World;
            world.Globals.TurnCiv = 1; world.PlayerPhaseActive = world.TurnLoopActive = true;
            var selection = new UnitSelection(world, F.Data.Rules, F.Combat.Services.State, F.Life.Services.Revelation, Effects);
            var focus = new UnitFocus(world, F.Data.Rules, selection, Effects);
            Binding = new(F.Services, focus, F.F.Inputs, () => { ++CursorReads; return F.Data.Unit(0).Position; }, new(new BufferedHallStorage()),
                recipient ?? (c => new((ulong)c)),
                visit => new("Visit", new(1), new CommandValue.City(visit.City), new([0])),
                _ => throw new InvalidOperationException("No intelligence"),
                visit => new("Capture", new(1), new CommandValue.City(visit.City), new([0])),
                visit => new("Hut", new(1), new CommandValue.City(visit.City), new([0])));
            F.F.Relocation.Step = F.F.Relocation.Step with { From = new(18, 12), To = new(20, 12) };
        }
        public PlayerMovementBinding Bind(Func<OperationContext> context)
        { Borrowed = context; ++Bindings; Equal(F.World, context().World); return Binding; }
        public CommandRequest Request(int direction = 1) => new(PlayerMovementCommands.Move, new([
            new("unit", new CommandValue.Unit(new(new(0), F.Data.Unit(0).Token))), new("direction", new CommandValue.Integer(direction))]));
        public OperationContext Context() => F.Life.Diplomacy.Events.Context() with { Actor = new(new(1), new(1)) };
        public PlayerMovementCommands.Operation Begin() => new(Request(), Bind);
    }

    private static void PlayerMovementAdmissionAndRecheck()
    {
        using var runtime = new SessionRuntime(new(), new(277));
        var f = new PlayerMoveFixture(new(runtime.Process, runtime.World)); PlayerMovementCommands.Register(runtime, f.Bind);
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var outsider = SessionClient(runtime, 2);
        Equal(CommandStatus.Rejected, outsider.Submit(f.Request()).Status);
        Equal("direction_not_available", actor.Submit(f.Request(8)).Code); Equal("direction_not_available", actor.Submit(f.Request(-1)).Code);
        f.F.World.PlayerPhaseActive = false; Equal("player_phase_required", actor.Submit(f.Request()).Code); f.F.World.PlayerPhaseActive = true;
        Equal(0, f.Bindings);
        var request = f.Request(); var command = actor.Submit(request).Correlation.Command;
        f.F.Data.Unit(0).Token = new(999); runtime.AdvanceOne();
        Equal(CommandStatus.Failed, RequiredResult(actor, command).Status); Equal("unit_not_available", RequiredResult(actor, command).Code);
        Equal(0, f.Bindings); Equal(new MapPoint(18, 12), f.F.Data.Unit(0).Position);
        command = actor.Submit(f.Request()).Correlation.Command; runtime.AdvanceOne();
        var result = RequiredResult(actor, command); Equal(CommandStatus.Completed, result.Status);
        Equal(1, ((CommandValue.Integer)result.Result.Single(v => v.Name == "result").Value).Value);
        Equal(new MapPoint(20, 12), f.F.Data.Unit(0).Position); Equal(1, f.Bindings); Equal(1, f.CursorReads);
        Throws<InvalidOperationException>(() => f.Borrowed!()); Equal(false, f.F.World.UnitMovementActive); f.F.Consumed();
    }

    private static void PlayerMovementPrivateMission()
    {
        using var runtime = new SessionRuntime(new(), new(278)); ParticipantId owner = default;
        var f = new PlayerMoveFixture(new(runtime.Process, runtime.World), _ => owner); f.F.AtBirch(spy: true);
        PlayerMovementCommands.Register(runtime, f.Bind); runtime.BeginClientSession(new());
        var actor = SessionClient(runtime); var other = SessionClient(runtime, 2); owner = actor.Identity.Participant;
        var command = actor.Submit(f.Request()).Correlation.Command; runtime.AdvanceOne();
        var menu = RequiredInteraction(actor, command); Equal(false, other.Reply(menu, 0).Accepted);
        f.F.World.Civilizations[1].Treaties[2].Embassy = true;
        Equal(true, actor.Reply(menu, (int)CityMission.Embassy).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Pending, RequiredResult(actor, command).Status); Equal(0, f.CursorReads); runtime.AdvanceOne();
        var visit = RequiredInteraction(actor, command); Equal(null, other.Result(command));
        Equal(true, actor.Reply(visit, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(false, actor.Reply(visit, 0).Accepted);
        Equal(new MapPoint(10, 8), f.F.Data.Unit(0).Position); Equal(true, f.F.F.Capture.City.Flags.Investigated);
        Equal((byte)1, f.F.Data.Unit(0).MovesUsed); Equal(1, f.CursorReads); Equal(1, f.Bindings); f.F.Consumed();
    }

    private static void PlayerMovementFocusContact()
    {
        using var runtime = new SessionRuntime(new(), new(279)); ParticipantId actorSeat = default, neighborSeat = default;
        var f = new PlayerMoveFixture(new(runtime.Process, runtime.World), c => c == 3 ? neighborSeat : actorSeat);
        var world = f.F.World; f.F.Data.Unit(0).MovesUsed = 3;
        f.F.Data.AddUnit(2, 1, 4, new(4, 4)); f.F.Data.AddUnit(3, 3, 4, new(5, 5));
        world.Globals.CivsAlive = world.Globals.CivsAlive.With(3); world.Globals.HumanPlayers = CivilizationSet.Only(1).With(3);
        world.Multiplayer.LocalHuman = 3; world.Events.Loaded = true;
        var permission = NegotiationEvent(world, 1, 3, deny: true); permission.Values.Actions = new() { Text = true, JustOnce = true };
        permission.SetString(Event.TextBegin, new("Next unit contact"));
        PlayerMovementCommands.Register(runtime, f.Bind); runtime.BeginClientSession(new());
        var actor = SessionClient(runtime); var neighbor = SessionClient(runtime, 3);
        actorSeat = actor.Identity.Participant; neighborSeat = neighbor.Identity.Participant;
        var command = actor.Submit(f.Request()).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(neighbor, command); Equal(false, actor.Reply(prompt, 0).Accepted);
        Equal(null, RequiredResult(actor, command).Interaction); Equal(new MapPoint(20, 12), f.F.Data.Unit(0).Position);
        Equal((UnitSlot?)new UnitSlot(2), world.Globals.ActiveUnit); Equal(false, world.UnitMovementActive);
        Equal(false, permission.Values.Actions.Consumed); Equal(true, neighbor.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(true, permission.Values.Actions.Consumed);
        Equal((byte)6, f.F.Data.Unit(0).MovesUsed); Equal(1, f.CursorReads); Equal(false, world.Globals.TransientFlags.EndOfTurn); f.F.Consumed();
    }

    private static void PlayerMovementCosmeticAndEmptyFocus()
    {
        foreach (bool movementFails in new[] { false, true })
        {
            var f = new PlayerMoveFixture(); f.F.Data.Unit(0).MovesUsed = 3;
            if (movementFails) f.F.F.Relocation.FailCosmetic = true;
            else f.Effects.Fail = "empty";
            using var operation = f.Begin(); var result = operation.Resume(f.Context(), new());
            Equal(CommandStatus.Failed, result.Status); Equal(movementFails ? "movement_presentation_failed" : "unit_focus_presentation_failed", result.Code);
            Equal(new MapPoint(20, 12), f.F.Data.Unit(0).Position); Equal((byte)6, f.F.Data.Unit(0).MovesUsed);
            Equal(null, f.F.World.Globals.ActiveUnit); Equal(true, f.F.World.Globals.TransientFlags.EndOfTurn);
            Equal(result, operation.Resume(f.Context(), new())); Equal(1, f.CursorReads); f.F.Consumed();
        }
    }

    private static void PlayerMovementCancellationAndDisposal()
    {
        foreach (var reason in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var f = new PlayerMoveFixture(); using var operation = f.Begin();
            Equal(CommandStatus.Cancelled, operation.Resume(f.Context(), new(reason)).Status);
            Equal(0, f.Bindings); Equal(new MapPoint(18, 12), f.F.Data.Unit(0).Position);
            f = new(); f.F.AtBirch(spy: true); using var mission = f.Begin();
            Equal(CommandStatus.AwaitingInteraction, mission.Resume(f.Context(), new()).Status);
            Equal(CommandStatus.Completed, mission.Resume(f.Context(), new(reason)).Status);
            Equal(false, f.F.World.UnitMovementActive); Equal(1, f.CursorReads); f.F.Consumed();
        }
        var disposed = new PlayerMoveFixture(); disposed.F.AtBirch(spy: true); using var waiting = disposed.Begin();
        Equal(CommandStatus.AwaitingInteraction, waiting.Resume(disposed.Context(), new()).Status); Equal(true, disposed.F.World.UnitMovementActive);
        waiting.Dispose(); Equal(false, disposed.F.World.UnitMovementActive); Equal(0, disposed.CursorReads);
        Throws<ObjectDisposedException>(() => waiting.Resume(disposed.Context(), new())); Throws<InvalidOperationException>(() => disposed.Borrowed!());
        var failed = new PlayerMoveFixture(); failed.F.Data.Stacks.Relocate(0, new(38, 12), false); failed.F.F.FailOffMap = true;
        using var rejected = failed.Begin(); Throws<InvalidOperationException>(() => rejected.Resume(failed.Context(), new()));
        Equal(false, failed.F.World.UnitMovementActive); Equal((byte)0, failed.F.Data.Unit(0).MovesUsed); Equal(0, failed.CursorReads);
        Throws<InvalidOperationException>(() => rejected.Resume(failed.Context(), new())); Throws<InvalidOperationException>(() => failed.Borrowed!());
    }

    private static void PlayerMovementPendingOutcome()
    {
        foreach (bool quit in new[] { false, true })
        {
            var f = new PlayerMoveFixture(); f.F.World.PendingEndReason = EndReason.Conquest; f.F.World.Globals.Status.Scored = true;
            using var operation = f.Begin(); var context = f.Context(); var step = operation.Resume(context, new());
            Equal(CommandStatus.AwaitingInteraction, step.Status); Equal(0, f.CursorReads);
            Equal(new MapPoint(20, 12), f.F.Data.Unit(0).Position); Equal(false, f.F.World.UnitMovementActive);
            step = operation.Resume(context, new(OperationInputKind.Choice, 0));
            Equal(true, step.Interaction!.Prompt.StartsWith("Power history", StringComparison.Ordinal));
            step = operation.Resume(context, new(OperationInputKind.Choice, 0)); Equal("Keep playing?", step.Interaction!.Prompt);
            step = operation.Resume(context, new(OperationInputKind.Choice, quit ? 0 : 1));
            Equal(CommandStatus.Completed, step.Status); Equal(!quit, f.F.World.PlayerPhaseActive); Equal(!quit, f.F.World.TurnLoopActive);
            Equal(quit ? 0 : 1, f.CursorReads); Equal(EndReason.None, f.F.World.PendingEndReason);
            Equal(1, ((CommandValue.Integer)step.Result!.Single(v => v.Name == "result").Value).Value); f.F.Consumed();
        }
    }
}
