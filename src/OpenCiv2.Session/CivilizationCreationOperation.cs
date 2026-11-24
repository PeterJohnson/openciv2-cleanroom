using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Trusted local V01 creation with bounded placement and private catch-up and placement contact choices.</summary>
public sealed class CivilizationCreationOperation(CivilizationCreation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private InteractionSpec? intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed creation presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var previous = operation.Pending;
            var result = operation.AdvanceDecision(64, LifecycleInteraction.ResearchReply(previous, input));
            if (!ReferenceEquals(previous, operation.Pending))
            { intelligenceWindow = null; }
            if (operation.Pending is { } decision)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: LifecycleInteraction.Prompt(context,
                    decision, recipient, ref intelligenceWindow, intelligence));
            }
            if (result is null)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var values = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(result.Value))]);
            context.Effect("civilization.creation.completed", values);
            return completed = operation.PresentationFailures.Count == 0 ? new(Result: values)
            : new(CommandStatus.Failed, values, "civilization_creation_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
