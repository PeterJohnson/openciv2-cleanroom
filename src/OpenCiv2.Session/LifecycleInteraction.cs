using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

internal static class LifecycleInteraction
{
    internal static ResearchReply? Selection(int? value) => value is { } selected ? new(selected) : null;
    internal static ResearchReply? ResearchReply(LifecycleDecision? decision, OperationInput input) => decision switch
    {
        ResearchGrantDecision grant => ResearchGrantInteraction.ResearchReply(grant, input),
        EspionagePretext => EspionageInteraction.Reply(input) is { } value ? new(value) : null,
        LifecycleContact contact => DiplomaticContactInteraction.Reply(contact.Decision, input),
        _ => null
    };
    internal static InteractionSpec Prompt(OperationContext context, LifecycleDecision decision,
        Func<int, ParticipantId> recipient, ref InteractionSpec? intelligenceWindow,
        Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) => decision switch
        {
            LifecycleContact contact => DiplomaticContactInteraction.Prompt(context, contact.Decision, recipient,
                intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref intelligenceWindow),
            EspionagePretext pretext => EspionageInteraction.Prompt(pretext, recipient(pretext.Victim.Civilization)),
            ResearchGrantDecision grant => ResearchGrantInteraction.Prompt(context, grant, recipient,
                intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref intelligenceWindow),
            _ => throw new InvalidOperationException("Unknown lifecycle decision")
        };
}
