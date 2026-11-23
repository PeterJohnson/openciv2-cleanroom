using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

internal static class LifecycleInteraction
{
    internal static int? Reply(LifecycleDecision? decision, OperationInput input) => decision switch
    {
        ResearchGrantDecision grant => ResearchGrantInteraction.Reply(grant, input),
        EspionagePretext => EspionageInteraction.Reply(input),
        _ => null
    };
    internal static ResearchReply? ResearchReply(LifecycleDecision decision, OperationInput input) =>
        Reply(decision, input) is { } answer ? new(answer) : null;
    internal static InteractionSpec Prompt(OperationContext context, LifecycleDecision decision,
        Func<int, ParticipantId> recipient, ref InteractionSpec? intelligenceWindow,
        Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) => decision switch
        {
            EspionagePretext pretext => EspionageInteraction.Prompt(pretext, recipient(pretext.Victim.Civilization)),
            ResearchGrantDecision grant => ResearchGrantInteraction.Prompt(context, grant, recipient,
                intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref intelligenceWindow),
            _ => throw new InvalidOperationException("Unknown lifecycle decision")
        };
}
