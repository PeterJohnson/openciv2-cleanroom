using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Trusted V01 child. Once admitted, bounded creation/partition work completes despite cancellation.</summary>
public sealed class CivilWarOperation(CivilWar.Operation operation, Func<int, ParticipantId>? recipient = null,
    Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private OperationStep? completed;
    private InteractionSpec? intelligenceWindow;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed civil-war presentation cannot resume"); }
        if (completed is not null)
        {
            return completed;
        }

        int? result;
        try
        {
            var previous = operation.Pending;
            result = operation.AdvanceDecision(answer: LifecycleInteraction.ResearchReply(previous, input));
            if (!ReferenceEquals(previous, operation.Pending))
            { intelligenceWindow = null; }
            if (operation.Pending is { } decision)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: LifecycleInteraction.Prompt(context,
                    decision, recipient ?? (_ => throw new InvalidOperationException("Civil-war decisions require participant routing")),
                    ref intelligenceWindow, intelligence));
            }
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
        if (result is null)
        { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
        var values = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(result.Value))]);
        context.Effect("civilization.civil-war.completed", values);
        return completed = operation.PresentationFailures.Count == 0 ? new(Result: values)
            : new(CommandStatus.Failed, values, "civilization_civil_war_presentation_failed");
    }
    public void Dispose() => operation.Dispose();
}
