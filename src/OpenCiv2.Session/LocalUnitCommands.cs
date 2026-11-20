using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Session;

/// <summary>Optional production command registration for the P5 application seam. U01 §3 supplies
/// the mechanic; the client contract admits only a coherent, wholly owned local stack.</summary>
public static class LocalUnitCommands
{
    public const string StackBottomName = "unit.stack-bottom";

    /// <summary>
    /// Builds the bounded local stack-reorder command with admission checks and a confirmation continuation.
    /// </summary>
    public static CommandDefinition StackBottom() => new(
        new(
            1,
            StackBottomName,
            "Move an own unit to the bottom of its local stack after confirmation.",
            AccessClass.Player,
            new([new("unit", ValueKind.Unit)]),
            new([new("unit", ValueKind.Unit), new("changed", ValueKind.Boolean)])),
        (actor, request, world) => new(Validate(actor, Target(request), world)),
        request => new BottomContinuation(Target(request)));

    private static UnitTarget Target(CommandRequest request) =>
        ((CommandValue.Unit)CommandDescriptor.Value(request.Arguments, "unit")).Value;

    /// <summary>
    /// Checks target lifetime, ownership and reciprocal stack links without invoking mutating repair.
    /// </summary>
    /// <remarks>
    /// Both directions use finite traversal limits. A stale target, mixed owner, broken link or cyclic stack
    /// rejects the request.
    /// </remarks>
    private static string? Validate(CommandActor actor, UnitTarget target, GameState world)
    {
        if (world.Globals.Multiplayer != GameMode.SinglePlayer || actor.Source is not null)
        {
            return "local_session_required";
        }

        var units = world.Entities.Units;
        int slot = target.Slot.Value;
        if (slot >= units.End || !target.Token.Occupied || units.Storage[slot].Token != target.Token
            || units.Storage[slot].Owner != actor.Civilization)
        {
            return "unit_not_available";
        }

        var origin = units.Storage[slot];
        if (!world.Map.Allocated || !world.Map.Geometry.Canonical(origin.Position))
        {
            return "stack_not_available";
        }
        // Admission observes raw links without invoking U01's mutating repair. No other owner's
        // fields are returned. Both walks terminate within the finite pool even for cyclic input.
        bool Member(int index) => index >= 0 && index < units.End && units.Storage[index].Token.Occupied &&
            units.Storage[index].Owner == actor.Civilization && units.Storage[index].Position.X == origin.Position.X
            && units.Storage[index].Position.Y == origin.Position.Y;
        int head = slot;
        for (int remaining = EntityLimits.Units; ; --remaining)
        {
            if (remaining == 0 || !Member(head))
            {
                return "stack_not_available";
            }

            int previous = (units.Storage[head].Previous?.Value ?? -1);
            if (previous < 0)
            {
                break;
            }

            if (!Member(previous) || (units.Storage[previous].Next?.Value ?? -1) != head)
            {
                return "stack_not_available";
            }

            head = previous;
        }
        int current = head, last = -1, count = 0;
        bool found = false;
        while (current >= 0)
        {
            // The primitive's >=2048 repair threshold is outside this coherent-stack command profile.
            if (++count >= EntityLimits.Units || !Member(current)
                || (last >= 0 && (units.Storage[current].Previous?.Value ?? -1) != last))
            {
                return "stack_not_available";
            }

            found |= current == slot;
            last = current;
            current = (units.Storage[current].Next?.Value ?? -1);
        }
        return found ? null : "stack_not_available";
    }

    /// <summary>
    /// Retains only the target and confirmation phase, revalidating the live stack before applying the choice.
    /// </summary>
    private sealed class BottomContinuation(UnitTarget target) : IContinuation
    {
        private bool awaiting;

        public OperationStep Resume(OperationContext context, OperationInput input)
        {
            if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout
                || input.Kind == OperationInputKind.Choice && input.Choice == 0)
            {
                return new(CommandStatus.Cancelled, Code: "cancelled");
            }

            if (Validate(context.Actor, target, context.World) is { } error)
            {
                return new(CommandStatus.Failed, Code: error);
            }

            if (!awaiting && input.Kind == OperationInputKind.Begin)
            {
                awaiting = true;
                return new(
                    CommandStatus.AwaitingInteraction,
                    Interaction: new("Move this unit to the bottom of its stack?", null,
                        new CommandValue.Unit(target), new([0, 1])));
            }
            if (!awaiting || input.Kind != OperationInputKind.Choice || input.Choice != 1)
            {
                return new(CommandStatus.Failed, Code: "unexpected_input");
            }

            bool changed = context.World.Entities.Units.Storage[target.Slot.Value].Next is not null;
            using var operation = new UnitOperation(
                context,
                new(UnitOperationKind.Bottom, new([target.Slot.Value, 0, 0, 0, 0, 0])),
                false,
                new(100000, 64),
                new());
            if (operation.Poll() is not { Cancelled: false, TimedOut: false })
            {
                return new(CommandStatus.Failed, Code: "unit_operation_incomplete");
            }

            var result = new OwnedList<NamedValue>(
                [new("unit", new CommandValue.Unit(target)), new("changed", new CommandValue.Boolean(changed))]);
            if (changed)
            {
                context.Effect("unit.stack-reordered", result);
            }

            return new(CommandStatus.Completed, result);
        }
    }
}
