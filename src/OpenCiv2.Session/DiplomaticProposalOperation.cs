using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V05 proposal response with private treaty, allied exchange and grant decisions.</summary>
public sealed class DiplomaticProposalOperation(DiplomaticProposals.Operation operation,
    Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private DiplomaticProposalDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed proposal cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(DiplomaticProposalInteraction.Reply(pending, input));
            if (pending is not null)
            { context.Publish(); return new(CommandStatus.AwaitingInteraction, Interaction: DiplomaticProposalInteraction.Prompt(context, pending, recipient, intelligence, ref window)); }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var result = new OwnedList<NamedValue>([new("endsAudience", new CommandValue.Boolean(operation.EndsAudience))]);
            context.Effect("diplomacy.proposal.completed", result);
            return completed = operation.PresentationFailures.Count == 0 ? new(Result: result)
                : new(CommandStatus.Failed, result, "proposal_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
