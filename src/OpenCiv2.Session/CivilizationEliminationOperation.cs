using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Trusted V01 destruction continuation. The owning city/combat operation admits the removal;
/// the seat resolver routes each human victim's pretext, including nested last-settler checks.</summary>
public sealed class CivilizationEliminationOperation(CivilizationElimination.Operation operation,
    Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private LifecycleDecision? pending;
    private OperationStep? completed;
    private InteractionSpec? intelligenceWindow;

    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed lifecycle presentation cannot resume"); }
        try
        { return Advance(context, input); }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private OperationStep Advance(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        { return completed; }
        LifecycleDecision? decision;
        decision = operation.ResumeDecision(LifecycleInteraction.ResearchReply(pending, input));
        if (!ReferenceEquals(pending, decision))
        { intelligenceWindow = null; }
        pending = decision;
        if (decision is not null)
        {
            context.Publish();
            return new(CommandStatus.AwaitingInteraction,
                Interaction: LifecycleInteraction.Prompt(context, decision, recipient, ref intelligenceWindow, intelligence));
        }
        if (operation.Result is null)
        {
            context.Publish();
            return new(CommandStatus.Pending) { ReadyToContinue = true };
        }
        var result = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(operation.Result.Value))]);
        context.Effect("civilization.elimination.completed", result);
        return completed = operation.PresentationFailures.Count == 0 ? new(Result: result)
            : new(CommandStatus.Failed, result, "civilization_elimination_presentation_failed");
    }

    public void Dispose() => operation.Dispose();
}
