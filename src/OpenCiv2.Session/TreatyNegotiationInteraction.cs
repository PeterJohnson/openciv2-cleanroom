using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Private treaty choices shared by direct negotiation and proposal responses.</summary>
internal static class TreatyNegotiationInteraction
{
    public static ResearchReply? Reply(TreatyNegotiationDecision? pending, OperationInput input)
    {
        if (pending is TreatyNegotiationDecision.Grant grant)
        { return ResearchGrantInteraction.ResearchReply(grant.Decision, input); }
        if (input.Kind == OperationInputKind.Choice && input.Choice is { } choice)
        { return new(choice); }
        return input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout ? new(0) : null;
    }
    public static InteractionSpec Prompt(OperationContext context, TreatyNegotiationDecision decision,
        Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence, ref InteractionSpec? window)
    {
        if (decision is TreatyNegotiationDecision.Grant grant)
        { return ResearchGrantInteraction.Prompt(context, grant.Decision, recipient, intelligence, ref window); }
        var (player, title) = decision switch
        {
            TreatyNegotiationDecision.Technology t => (t.Recipient, $"Give {t.Advance.Name} to the {t.Partner.Plural} to negotiate {(t.Alliance ? "an alliance" : "peace")}?"),
            TreatyNegotiationDecision.Gold g => (g.Recipient, $"Pay the {g.Partner.Plural} {g.Price} gold to negotiate {(g.Alliance ? "an alliance" : "peace")}?"),
            TreatyNegotiationDecision.Solidarity s => (s.Recipient, $"Join the {s.Partner.Plural} in war against the {s.Target.Plural}?"),
            _ => throw new InvalidOperationException("Unknown treaty decision")
        };
        return new(title, recipient(player), null, new([0, 1])) { Options = new([new(0, "Decline"), new(1, "Accept")]) };
    }
}
