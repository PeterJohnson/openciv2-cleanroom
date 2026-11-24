using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;

namespace OpenCiv2.Session;

/// <summary>Private nested V06 decisions during A04 planning. The owning chooser applies the returned order route.</summary>
public sealed class GroundMovementOperation(GroundMovementPlanner.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private UnitMissionDecision? pending;
    private InteractionSpec? intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("Failed ground planner presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.ResumeDecision(UnitMissionInteraction.Reply(pending, input));
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: UnitMissionInteraction.Prompt(context, pending, recipient, ref intelligenceWindow, intelligence));
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            List<NamedValue> values = [new("retainOrders", new CommandValue.Boolean(operation.Result!.Direction is null))];
            if (operation.Result.Direction is { } direction)
            { values.Add(new("direction", new CommandValue.Integer(direction))); }
            var result = new OwnedList<NamedValue>(values);
            context.Effect("ai.ground-planned", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
