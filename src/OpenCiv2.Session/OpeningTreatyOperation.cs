using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V05 opening treaty offers with private emissary and concession-grant decisions.</summary>
public sealed class OpeningTreatyOperation(OpeningTreaties.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private OpeningTreatyDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed opening treaty cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var previous = pending;
            pending = operation.Resume(OpeningTreatyInteraction.Reply(pending, input));
            if (!ReferenceEquals(previous, pending))
            { window = null; }
            if (pending is not null)
            { context.Publish(); return new(CommandStatus.AwaitingInteraction, Interaction: OpeningTreatyInteraction.Prompt(context, pending, recipient, intelligence, ref window)); }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var outcome = operation.Result!;
            var result = new OwnedList<NamedValue>([new("treatyMade", new CommandValue.Boolean(outcome.TreatyMade)),
                new("conceded", new CommandValue.Boolean(outcome.Conceded)), new("senateBlocked", new CommandValue.Boolean(outcome.SenateBlocked)),
                new("endsAudience", new CommandValue.Boolean(outcome.EndsAudience))]);
            context.Effect("diplomacy.opening-treaty.completed", result);
            return completed = operation.PresentationFailures.Count == 0 ? new(Result: result)
                : new(CommandStatus.Failed, result, "opening_treaty_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}

internal static class OpeningTreatyInteraction
{
    private static bool ZeroAccepts(OpeningTreatyOfferKind kind) => kind is OpeningTreatyOfferKind.Alliance
        or OpeningTreatyOfferKind.GrantCease or OpeningTreatyOfferKind.WallCease or OpeningTreatyOfferKind.Ceasefire or OpeningTreatyOfferKind.Peace;
    public static ResearchReply? Reply(OpeningTreatyDecision? pending, OperationInput input)
    {
        if (pending is OpeningTreatyDecision.Grant grant)
        { return ResearchGrantInteraction.ResearchReply(grant.Value, input); }
        if (pending is OpeningTreatyDecision.Emissary emissary)
        { return EmissaryInteraction.Reply(emissary.Value, input) is { } answer ? new(answer) : null; }
        if (input.Kind == OperationInputKind.Choice && input.Choice is { } choice)
        { return new(choice); }
        return input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout
            ? new(pending is OpeningTreatyDecision.Offer offer && ZeroAccepts(offer.Value.Kind) ? 1 : 0) : null;
    }
    public static InteractionSpec Prompt(OperationContext context, OpeningTreatyDecision decision, Func<int, ParticipantId> recipient,
        Func<EmissaryIntelligence, InteractionSpec> intelligence, ref InteractionSpec? window)
    {
        if (decision is OpeningTreatyDecision.Grant grant)
        { return ResearchGrantInteraction.Prompt(context, grant.Value, recipient, intelligence, ref window); }
        if (decision is OpeningTreatyDecision.Emissary emissary)
        { return EmissaryInteraction.Prompt(emissary.Value, recipient, intelligence, ref window); }
        var offer = ((OpeningTreatyDecision.Offer)decision).Value;
        string title = offer.Kind switch
        {
            OpeningTreatyOfferKind.Alliance => $"Accept an alliance with the {offer.Partner.Plural}?",
            OpeningTreatyOfferKind.Solidarity => $"Prove solidarity by declaring war on the {offer.Target!.Plural}?",
            OpeningTreatyOfferKind.GrantCease or OpeningTreatyOfferKind.WallCease or OpeningTreatyOfferKind.Ceasefire => $"Accept a ceasefire with the {offer.Partner.Plural}?",
            OpeningTreatyOfferKind.Peace => $"Accept peace with the {offer.Partner.Plural}?",
            OpeningTreatyOfferKind.Technology => $"Accept {offer.Advance!.Name} and a ceasefire from the {offer.Partner.Plural}?",
            OpeningTreatyOfferKind.Gold => $"Accept {offer.Gold} gold and a ceasefire from the {offer.Partner.Plural}?",
            OpeningTreatyOfferKind.GoldForPeace => $"Accept {offer.Gold} gold for peace with the {offer.Partner.Plural}?",
            OpeningTreatyOfferKind.Grovel => $"Accept the {offer.Partner.Plural}' treasury and available technologies for a ceasefire?",
            _ => throw new InvalidOperationException("Unknown opening treaty offer")
        };
        int accept = ZeroAccepts(offer.Kind) ? 0 : 1, decline = 1 - accept;
        return new(title, recipient(offer.Recipient), null, new([decline, accept]))
        { Options = new([new(decline, "Decline"), new(accept, "Accept")]) };
    }
}
