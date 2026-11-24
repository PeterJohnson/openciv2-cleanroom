using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Host-owned content, gameplay graph and private presentation for an admitted move.
/// Cursor is read when focus begins, after any retained movement/outcome decisions.</summary>
public sealed record PlayerMovementBinding(SinglePlayerMovement Movement, UnitFocus Focus,
    MovementDispatchInputs Inputs, Func<MapPoint> Cursor, HallOfFame Hall,
    Func<int, ParticipantId> Recipient, Func<CityEspionageVisit, InteractionSpec> Visit,
    Func<EmissaryIntelligence, InteractionSpec> Intelligence,
    Func<CaptureCityVisit, InteractionSpec> Capture, Func<HutCityVisit, InteractionSpec> Hut);

/// <summary>Player admission for U02 followed by actual U04 focus and pending game outcomes.
/// The graph factory is called only after execution-time validation. Its context accessor is
/// valid only within Resume, allowing semantic effects to borrow the current session step.</summary>
public static class PlayerMovementCommands
{
    public const string Move = "unit.move";
    public static void Register(SessionRuntime runtime, Func<Func<OperationContext>, PlayerMovementBinding> bind) =>
        runtime.RegisterCommand(new(new(1, Move, "Move an owned unit one step, including combat, missions and transport.",
            AccessClass.Player, new([new("unit", ValueKind.Unit), new("direction")]),
            new([new("unit", ValueKind.Unit), new("result", ValueKind.Integer)])),
            (actor, request, world) => new(Validate(actor, request, world)), request => new Operation(request, bind)));

    private static UnitTarget Target(CommandRequest request) => ((CommandValue.Unit)CommandDescriptor.Value(request.Arguments, "unit")).Value;
    private static int Direction(CommandRequest request) => ((CommandValue.Integer)CommandDescriptor.Value(request.Arguments, "direction")).Value;
    private static string? Validate(CommandActor actor, CommandRequest request, GameState world)
    {
        if (LocalPlayCommands.ValidatePhase(actor, world) is { } phase)
        { return phase; }
        var unit = Target(request);
        if (!world.Entities.Units.Live(unit.Slot) || world.Entities.Units.At(unit.Slot).Token != unit.Token
            || world.Entities.Units.At(unit.Slot).Owner != actor.Civilization)
        { return "unit_not_available"; }
        return Direction(request) is < 0 or >= MapLimits.Directions ? "direction_not_available" : null;
    }

    public sealed class Operation(CommandRequest request, Func<Func<OperationContext>, PlayerMovementBinding> bind) : IContinuation
    {
        private OperationContext? current;
        private PlayerMovementBinding? binding;
        private MovementDispatcher.Operation? movement;
        private IContinuation? child;
        private PendingOutcomeOperation? outcome;
        private OperationStep? completed;
        private string failure = "", call = "";
        private int stage;
        private bool disposed, faulted;
        private OperationContext Borrow() => current ?? throw new InvalidOperationException("Movement context is only available during a session step");

        public OperationStep Resume(OperationContext context, OperationInput input)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed player movement command cannot resume"); }
            if (completed is not null)
            { return completed; }
            current = context;
            try
            {
                if (binding is null)
                {
                    if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout)
                    { return completed = new(CommandStatus.Cancelled, Code: "cancelled"); }
                    if (Validate(context.Actor, request, context.World) is { } error)
                    { return completed = new(CommandStatus.Failed, Code: error); }
                    binding = bind(Borrow);
                    call = FormattableString.Invariant($"move/{context.Correlation.Session.Value}/{context.Correlation.Command.Value}");
                    context.World.Globals.ActiveUnit = Target(request).Slot;
                    context.World.MovementMode = MovementMode.MoveUnits;
                    movement = binding.Movement.Dispatcher.Begin(Target(request).Slot, Direction(request), new(true, true), binding.Inputs, call);
                    child = new MovementOperation(movement, binding.Recipient, binding.Visit, binding.Intelligence, binding.Capture, binding.Hut);
                    input = new();
                }
                while (true)
                {
                    if (child is not null)
                    {
                        var result = child.Resume(context, input);
                        if (!result.Status.Terminal())
                        { return result; }
                        if (result.Status == CommandStatus.Failed && failure.Length == 0)
                        { failure = result.Code; }
                        child.Dispose();
                        child = null;
                        input = new();
                    }
                    switch (stage++)
                    {
                        case 0:
                            if (context.World.PendingEndReason != EndReason.None)
                            { child = outcome = new(binding.Hall); }
                            break;
                        case 1:
                            if (outcome?.Quit == true)
                            {
                                context.World.PlayerPhaseActive = context.World.TurnLoopActive = false;
                                context.Effect("game.closed", OwnedList<NamedValue>.Empty);
                            }
                            else
                            {
                                child = new UnitFocusOperation(binding.Focus.Begin(binding.Cursor(), 0, context.World.Multiplayer.LocalHuman,
                                    call + "/focus"), binding.Recipient, binding.Intelligence);
                            }
                            break;
                        default:
                            context.World.Globals.TransientFlags.EndOfTurn = context.World.Globals.ActiveUnit is null;
                            var values = new OwnedList<NamedValue>([new("unit", new CommandValue.Unit(Target(request))),
                                new("result", new CommandValue.Integer(movement!.Result!.Value))]);
                            context.Effect("unit.move.completed", values);
                            return completed = new(failure.Length == 0 ? CommandStatus.Completed : CommandStatus.Failed, values, failure);
                    }
                }
            }
            catch { faulted = true; child?.Dispose(); context.Publish(); throw; }
            finally { current = null; }
        }
        public void Dispose() { disposed = true; child?.Dispose(); }
    }
}
