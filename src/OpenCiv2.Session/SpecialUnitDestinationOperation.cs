using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;

namespace OpenCiv2.Session;

/// <summary>A07 destination choice with private nested U01/V01/V06 lifecycle decisions.</summary>
public sealed class SpecialUnitDestinationOperation(SpecialUnitDestinations.Operation operation,
    Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private LifecycleDecision? pending;
    private InteractionSpec? intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("Failed destination presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(pending is null ? null : LifecycleInteraction.Reply(pending, input));
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction,
                    Interaction: LifecycleInteraction.Prompt(context, pending, recipient, ref intelligenceWindow, intelligence));
            }
            if (operation.Result is not { } result)
            { context.Publish(); return new(CommandStatus.Pending); }
            var values = new OwnedList<NamedValue>([new("route", new CommandValue.Integer((int)result.Route)),
                new("direction", new CommandValue.Integer(result.Direction))]);
            context.Effect("ai.special_destination", values);
            return completed = new(Result: values);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
