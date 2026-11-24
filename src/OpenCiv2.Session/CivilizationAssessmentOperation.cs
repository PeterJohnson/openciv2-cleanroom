using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;

namespace OpenCiv2.Session;

/// <summary>A01 assessment with private lifecycle decisions before later F04 upkeep stages.</summary>
public sealed class CivilizationAssessmentOperation(CivilizationAssessment.Operation operation,
    Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private LifecycleDecision? pending;
    private InteractionSpec? intelligenceWindow;
    private OperationStep? completed;
    private bool disposed, faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted)
        { throw new InvalidOperationException("A failed assessment presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var next = operation.ResumeDecision(LifecycleInteraction.ResearchReply(pending, input));
            if (!ReferenceEquals(pending, next))
            { intelligenceWindow = null; }
            pending = next;
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction,
                    Interaction: LifecycleInteraction.Prompt(context, pending, recipient, ref intelligenceWindow, intelligence));
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            context.Effect("civilization.assessment.completed", OwnedList<NamedValue>.Empty);
            return completed = operation.PresentationFailures.Count == 0 ? new()
                : new(CommandStatus.Failed, Code: "civilization_assessment_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() { disposed = true; operation.Dispose(); }
}
