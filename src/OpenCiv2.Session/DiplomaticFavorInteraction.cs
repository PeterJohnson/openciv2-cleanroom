using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Shared private favor decisions for direct and audience callers.</summary>
internal static class DiplomaticFavorInteraction
{
    public static ResearchReply? Reply(DiplomaticFavorDecision? pending, OperationInput input)
    {
        if (pending is DiplomaticFavorDecision.Technology technology)
        { return TechnologyNegotiationInteraction.Reply(technology.Decision, input); }
        if (pending is DiplomaticFavorDecision.Grant grant)
        { return ResearchGrantInteraction.ResearchReply(grant.Decision, input); }
        if (input.Kind == OperationInputKind.Choice && input.Choice is { } choice)
        { return new(choice); }
        return input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout ? new(0) : null;
    }
    public static InteractionSpec Prompt(OperationContext context, DiplomaticFavorDecision decision,
        Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence, ref InteractionSpec? window)
    {
        if (decision is DiplomaticFavorDecision.Technology technology)
        { return TechnologyNegotiationInteraction.Prompt(context, technology.Decision, recipient, intelligence, ref window); }
        if (decision is DiplomaticFavorDecision.Grant grant)
        { return ResearchGrantInteraction.Prompt(context, grant.Decision, recipient, intelligence, ref window); }
        if (decision is DiplomaticFavorDecision.Menu menu)
        {
            return new($"Ask the {menu.Partner.Plural} for a favor", recipient(menu.Recipient), null, new([0, 1, 2, 3]))
            { Options = new([new(0, "Leave"), new(1, "Exchange technology"), new(2, "Join a war"), new(3, "Exchange maps")]) };
        }
        if (decision is DiplomaticFavorDecision.Target target)
        {
            List<InteractionOption> choices = [new(0, "Back"), .. target.Options.Select(p => new InteractionOption(p.Civilization, p.Plural))];
            return new("Choose a civilization to oppose", recipient(target.Recipient), null, new(choices.Select(c => c.Value))) { Options = new(choices) };
        }
        var offer = ((DiplomaticFavorDecision.War)decision).Offer;
        string price = offer.Gold is { } gold ? gold + " gold" : offer.First!.Name + " and " + offer.Second!.Name;
        return new($"Pay the {offer.Partner.Plural} {price} to declare war on the {offer.Target.Plural}?", recipient(offer.Recipient), null, new([0, 1]))
        { Options = new([new(0, "Decline"), new(1, "Accept")]) };
    }
}
