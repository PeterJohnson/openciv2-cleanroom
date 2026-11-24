using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Admitted V01 forced removal with independently routed spy/recreation decisions.</summary>
public sealed class CivilizationRemovalOperation(CivilizationRemoval.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed removal presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var previous = operation.Pending;
            var pending = operation.Resume(LifecycleInteraction.ResearchReply(previous, input));
            if (!ReferenceEquals(previous, pending))
            { window = null; }
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: LifecycleInteraction.Prompt(context, pending, recipient, ref window, intelligence));
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            context.Effect("civilization.removal.completed", new([]));
            return completed = operation.PresentationFailures.Count == 0 ? new()
                : new(CommandStatus.Failed, Code: "civilization_removal_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
