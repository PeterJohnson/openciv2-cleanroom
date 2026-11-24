using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Shared private proposal decisions for direct and audience callers.</summary>
internal static class DiplomaticProposalInteraction
{
    public static ResearchReply? Reply(DiplomaticProposalDecision? pending, OperationInput input) => pending switch
    {
        DiplomaticProposalDecision.Treaty treaty => TreatyNegotiationInteraction.Reply(treaty.Decision, input),
        DiplomaticProposalDecision.Demand { Decision: DiplomaticDemandDecision.Technology technology } => TechnologyNegotiationInteraction.Reply(technology.Decision, input),
        DiplomaticProposalDecision.Demand { Decision: DiplomaticDemandDecision.Grant grant } => ResearchGrantInteraction.ResearchReply(grant.Decision, input),
        _ => null
    };
    public static InteractionSpec Prompt(OperationContext context, DiplomaticProposalDecision decision,
        Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence, ref InteractionSpec? window) => decision switch
        {
            DiplomaticProposalDecision.Treaty treaty => TreatyNegotiationInteraction.Prompt(context, treaty.Decision, recipient, intelligence, ref window),
            DiplomaticProposalDecision.Demand { Decision: DiplomaticDemandDecision.Technology technology } => TechnologyNegotiationInteraction.Prompt(context, technology.Decision, recipient, intelligence, ref window),
            DiplomaticProposalDecision.Demand { Decision: DiplomaticDemandDecision.Grant grant } => ResearchGrantInteraction.Prompt(context, grant.Decision, recipient, intelligence, ref window),
            _ => throw new InvalidOperationException("Unknown proposal decision")
        };
}
