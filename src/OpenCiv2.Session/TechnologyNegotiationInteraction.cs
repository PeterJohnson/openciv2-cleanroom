using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Shared private V05 exchange/grant interactions for direct and favor callers.</summary>
internal static class TechnologyNegotiationInteraction
{
    public static ResearchReply? Reply(TechnologyNegotiationDecision? pending, OperationInput input)
    {
        if (pending is TechnologyNegotiationDecision.Captured { Decision: CapturedTechnologyDecision.Goal goal })
        { return ResearchGoalInteraction.Reply(goal.Decision, input); }
        if (pending is TechnologyNegotiationDecision.Grant grant)
        { return ResearchGrantInteraction.ResearchReply(grant.Decision, input); }
        if (pending is TechnologyNegotiationDecision.Captured { Decision: CapturedTechnologyDecision.Grant acquired })
        { return ResearchGrantInteraction.ResearchReply(acquired.Decision, input); }
        if (pending is TechnologyNegotiationDecision.Emissary emissary)
        { return EmissaryInteraction.Reply(emissary.Decision, input) is { } answer ? new(answer) : null; }
        if (input.Kind == OperationInputKind.Choice && input.Choice is { } choice)
        { return pending is TechnologyNegotiationDecision.Captured && choice == 100 ? new(0, 1) : new(choice); }
        return pending is not TechnologyNegotiationDecision.Captured && input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout
            ? new(0) : null;
    }
    public static InteractionSpec Prompt(OperationContext context, TechnologyNegotiationDecision decision,
        Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence, ref InteractionSpec? window)
    {
        if (decision is TechnologyNegotiationDecision.Captured { Decision: CapturedTechnologyDecision.Goal goal })
        { return ResearchGoalInteraction.Prompt(context, goal.Decision, recipient); }
        if (decision is TechnologyNegotiationDecision.Emissary emissary)
        { return EmissaryInteraction.Prompt(emissary.Decision, recipient, intelligence, ref window); }
        if (decision is TechnologyNegotiationDecision.Grant grant)
        { return ResearchGrantInteraction.Prompt(context, grant.Decision, recipient, intelligence, ref window); }
        if (decision is TechnologyNegotiationDecision.Captured { Decision: CapturedTechnologyDecision.Grant acquired })
        { return ResearchGrantInteraction.Prompt(context, acquired.Decision, recipient, intelligence, ref window); }
        if (decision is TechnologyNegotiationDecision.Captured { Decision: CapturedTechnologyChoice choice })
        {
            var options = choice.Options.Select(a => new InteractionOption(a.Identity, a.Name)).ToList();
            if (options.Count == 0)
            { options.Add(new(choice.Recommendation, context.Process.Rules.Definitions.Advances[choice.Recommendation].Identity.Name)); }
            options.Add(new(100, "Research goal"));
            return new("Choose technology to receive", recipient(choice.Civilization), null, new(options.Select(o => o.Value)), Cancellable: false)
            { Options = new(options) };
        }
        if (decision is TechnologyNegotiationDecision.Sale sale)
        {
            var offer = sale.Offer;
            return new($"Buy {offer.Advance.Name} from the {offer.Counterpart.Plural} for {offer.Price} gold?",
                recipient(offer.Recipient), null, new([0, 1]))
            { Options = new([new(0, "Decline"), new(1, "Buy technology")]) };
        }
        var exchange = ((TechnologyNegotiationDecision.Exchange)decision).Offer;
        List<InteractionOption> choices = [new(0, "Decline"), new(1, $"Offer {exchange.Best.Name}")];
        if (exchange.Alternative is { } alternate)
        { choices.Add(new(2, $"Offer {alternate.Name}")); }
        return new($"The {exchange.Counterpart.Plural} propose a technology exchange, offering {exchange.Wanted.Name}.",
            recipient(exchange.Recipient), null, new(choices.Select(c => c.Value)))
        { Options = new(choices) };
    }
}
