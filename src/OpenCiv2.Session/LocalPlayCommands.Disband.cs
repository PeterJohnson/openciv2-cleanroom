using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public static partial class LocalPlayCommands
{
    public const string Disband = "unit.disband";
    private static void RegisterDisbandCommand(SessionRuntime runtime, HallOfFame hall) =>
        runtime.RegisterCommand(new(new(1, Disband, "Confirm disbanding an owned unit; its city receives shield credit.",
            AccessClass.Player, new([new("unit", ValueKind.Unit)]), new([new("disbanded", ValueKind.Boolean)])),
            (actor, request, world) => new(ValidatePhase(actor, world) ?? ValidateUnit(actor, request, world)),
            request => new DisbandCommand(request, hall)));

    private sealed class DisbandEffects(OperationContext context) : IDisbandEffects
    {
        // The desktop city card is a projection. Each production transaction opens
        // its own current C06 snapshot; no retained engine city window is open here.
        public CitySlot? DisplayedCity => null;
        public void RefreshCity(CitySlot city) => context.Publish();
        public void RedrawArea(MapPoint position, int observer) => context.Publish();
        public void RedrawMaps(int observer) => context.Publish();
    }
    private sealed class DisbandCommand(CommandRequest request, HallOfFame hall) : IContinuation
    {
        private UnitDisbandOperation? disband;
        private PendingOutcomeOperation? outcome;
        private OperationStep? removalResult, completed;
        public OperationStep Resume(OperationContext context, OperationInput input)
        {
            if (completed is not null)
            { return completed; }
            if (disband is null)
            {
                if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout)
                { return new(CommandStatus.Cancelled, Code: "cancelled"); }
                if ((ValidatePhase(context.Actor, context.World) ?? ValidateUnit(context.Actor, request, context.World)) is { } error)
                { return new(CommandStatus.Failed, Code: error); }
                var target = UnitArgument(request);
                context.World.Globals.ActiveUnit = target.Slot;
                context.World.MovementMode = MovementMode.MoveUnits;
                disband = new(EndPlayerPhase.CreateDisband(context, new DisbandEffects(context))
                    .Begin(context.World.Entities.Units.At(target.Slot).Position, "local-disband"),
                    owner => owner == context.Actor.Civilization.Value ? context.Actor.Participant
                        : throw new InvalidOperationException("The authored profile has no foreign disband participant"));
                input = new();
            }
            if (removalResult is null)
            {
                var result = disband.Resume(context, input);
                if (!result.Status.Terminal())
                { return result; }
                removalResult = result;
                if (result.Result?.Single(value => value.Name == "disbanded").Value is CommandValue.Boolean { Value: false })
                { return completed = result; }
                SelectReadyUnit(context);
                if (context.World.PendingEndReason != EndReason.None && !context.World.Entities.Units.Scanned.ToArray()
                    .Any(unit => unit.Token.Occupied && unit.Owner == context.Actor.Civilization))
                {
                    context.World.PlayerPhaseActive = false;
                    outcome = new(hall);
                    input = new();
                }
            }
            if (outcome is not null)
            {
                var result = outcome.Resume(context, input);
                if (!result.Status.Terminal())
                { return result; }
                if (result.Status == CommandStatus.Failed)
                { removalResult = removalResult with { Status = CommandStatus.Failed, Code = result.Code }; }
                if (outcome.Quit)
                {
                    context.World.PlayerPhaseActive = context.World.TurnLoopActive = false;
                    context.Effect("game.closed", OwnedList<NamedValue>.Empty);
                }
            }
            context.Publish();
            return completed = removalResult;
        }
        public void Dispose() { disband?.Dispose(); outcome?.Dispose(); }
    }
}
