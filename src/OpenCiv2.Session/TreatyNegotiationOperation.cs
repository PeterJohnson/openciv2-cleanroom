using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Private V05 treaty payments and solidarity, including retained V03 grants.</summary>
public sealed class TreatyNegotiationOperation(TreatyNegotiation.Operation operation,
    Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private TreatyNegotiationDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed treaty negotiation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(TreatyNegotiationInteraction.Reply(pending, input));
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: TreatyNegotiationInteraction.Prompt(context, pending, recipient, intelligence, ref window));
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var result = new OwnedList<NamedValue>([new("endsAudience", new CommandValue.Boolean(operation.EndsAudience))]);
            context.Effect("diplomacy.treaty.completed", result);
            return completed = operation.PresentationFailures.Count == 0 ? new(Result: result)
                : new(CommandStatus.Failed, result, "treaty_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
