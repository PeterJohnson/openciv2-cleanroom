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
            pending = operation.Resume(Reply(input));
            if (pending is not null)
            { context.Publish(); return new(CommandStatus.AwaitingInteraction, Interaction: Prompt(context, pending)); }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var result = new OwnedList<NamedValue>([new("endsAudience", new CommandValue.Boolean(operation.EndsAudience))]);
            context.Effect("diplomacy.proposal.completed", result);
            return completed = operation.PresentationFailures.Count == 0 ? new(Result: result)
                : new(CommandStatus.Failed, result, "proposal_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private ResearchReply? Reply(OperationInput input) => pending switch
    {
        DiplomaticProposalDecision.Treaty treaty => TreatyNegotiationInteraction.Reply(treaty.Decision, input),
        DiplomaticProposalDecision.Demand { Decision: DiplomaticDemandDecision.Technology technology } => TechnologyNegotiationInteraction.Reply(technology.Decision, input),
        DiplomaticProposalDecision.Demand { Decision: DiplomaticDemandDecision.Grant grant } => ResearchGrantInteraction.ResearchReply(grant.Decision, input),
        _ => null
    };
    private InteractionSpec Prompt(OperationContext context, DiplomaticProposalDecision decision) => decision switch
    {
        DiplomaticProposalDecision.Treaty treaty => TreatyNegotiationInteraction.Prompt(context, treaty.Decision, recipient, intelligence, ref window),
        DiplomaticProposalDecision.Demand { Decision: DiplomaticDemandDecision.Technology technology } => TechnologyNegotiationInteraction.Prompt(context, technology.Decision, recipient, intelligence, ref window),
        DiplomaticProposalDecision.Demand { Decision: DiplomaticDemandDecision.Grant grant } => ResearchGrantInteraction.Prompt(context, grant.Decision, recipient, intelligence, ref window),
        _ => throw new InvalidOperationException("Unknown proposal decision")
    };
    public void Dispose() => operation.Dispose();
}
