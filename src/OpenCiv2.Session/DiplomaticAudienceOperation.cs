using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V05 local audience with private routing through actual nested gameplay decisions.</summary>
public sealed class DiplomaticAudienceOperation(DiplomaticAudience.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private DiplomaticAudienceDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed audience cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var previous = pending;
            pending = operation.Resume(DiplomaticAudienceInteraction.Reply(pending, input));
            if (!ReferenceEquals(previous, pending))
            { window = null; }
            if (pending is not null)
            { context.Publish(); return new(CommandStatus.AwaitingInteraction, Interaction: DiplomaticAudienceInteraction.Prompt(context, pending, recipient, intelligence, ref window)); }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var result = new OwnedList<NamedValue>([new("scenarioDenied", new CommandValue.Boolean(operation.ScenarioDenied))]);
            context.Effect("diplomacy.audience.completed", result);
            return completed = operation.PresentationFailures.Count == 0 ? new(Result: result)
                : new(CommandStatus.Failed, result, "audience_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}

internal static class DiplomaticAudienceInteraction
{
    public static ResearchReply? Reply(DiplomaticAudienceDecision? pending, OperationInput input) => pending switch
    {
        DiplomaticAudienceDecision.Emissary d => EmissaryInteraction.Reply(d.Value, input) is { } answer ? new(answer) : null,
        DiplomaticAudienceDecision.Technology d => TechnologyNegotiationInteraction.Reply(d.Value, input),
        DiplomaticAudienceDecision.Grant d => ResearchGrantInteraction.ResearchReply(d.Value, input),
        DiplomaticAudienceDecision.Treaty d => OpeningTreatyInteraction.Reply(d.Value, input),
        DiplomaticAudienceDecision.Conversation d => DiplomaticConversationInteraction.Reply(d.Value, input),
        _ => input.Kind == OperationInputKind.Choice && input.Choice is { } choice ? new(choice)
            : input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout ? new(0) : null
    };
    public static InteractionSpec Prompt(OperationContext context, DiplomaticAudienceDecision decision,
        Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence, ref InteractionSpec? window)
    {
        switch (decision)
        {
            case DiplomaticAudienceDecision.Emissary d:
                return EmissaryInteraction.Prompt(d.Value, recipient, intelligence, ref window);
            case DiplomaticAudienceDecision.Technology d:
                return TechnologyNegotiationInteraction.Prompt(context, d.Value, recipient, intelligence, ref window);
            case DiplomaticAudienceDecision.Grant d:
                return ResearchGrantInteraction.Prompt(context, d.Value, recipient, intelligence, ref window);
            case DiplomaticAudienceDecision.Treaty d:
                return OpeningTreatyInteraction.Prompt(context, d.Value, recipient, intelligence, ref window);
            case DiplomaticAudienceDecision.Conversation d:
                return DiplomaticConversationInteraction.Prompt(context, d.Value, recipient, intelligence, ref window);
            case DiplomaticAudienceDecision.Offer d:
                var offer = d.Value;
                string title = offer.Kind switch
                {
                    AudienceOfferKind.JoinWar => $"Join the {offer.Partner.Plural} in war against the {offer.Target!.Plural}?",
                    AudienceOfferKind.Tribute => $"Pay {offer.Gold} gold in tribute to the {offer.Partner.Plural}?",
                    AudienceOfferKind.Technology => $"Give {offer.Advance!.Name} to the {offer.Partner.Plural}?",
                    AudienceOfferKind.OverABarrel => $"The {offer.Partner.Plural} also demand {offer.Advance!.Name}. Accept?",
                    _ => throw new InvalidOperationException("Unknown audience offer")
                };
                return new(title, recipient(offer.Recipient), null, new([0, 1]))
                { Options = new([new(0, "Decline"), new(1, "Accept")]) };
            default:
                throw new InvalidOperationException("Unknown audience decision");
        }
    }
}
