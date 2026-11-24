using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;

namespace OpenCiv2.Session;

/// <summary>V04 maintenance with private retained research, emissary and war-ally decisions.</summary>
public sealed class DiplomacyTurnOperation(DiplomacyTurn.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private DiplomacyTurnDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;

    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed diplomacy turn presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            int? answer = pending switch
            {
                DiplomacyTurnDecision.Research research => ResearchGrantInteraction.Reply(research.Decision, input),
                DiplomacyTurnDecision.WarAlly ally => WarAllyInteraction.Reply(ally.Decision, input),
                DiplomacyTurnDecision.Review review => RelationReviewInteraction.Reply(review.Decision, input),
                _ => null
            };
            pending = operation.Resume(answer);
            if (pending is not null)
            {
                context.Publish();
                var prompt = pending switch
                {
                    DiplomacyTurnDecision.Research research => ResearchGrantInteraction.Prompt(context, research.Decision, recipient, intelligence, ref window),
                    DiplomacyTurnDecision.WarAlly ally => WarAllyInteraction.Prompt(context, ally.Decision, recipient, intelligence, ref window),
                    DiplomacyTurnDecision.Review review => RelationReviewInteraction.Prompt(review.Decision, recipient, intelligence, ref window),
                    _ => throw new InvalidOperationException("Unknown diplomacy decision")
                };
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var result = new OwnedList<NamedValue>([new("completed", new CommandValue.Boolean(true))]);
            context.Effect("diplomacy.turn.completed", result);
            return completed = operation.PresentationFailures.Count == 0 ? new(Result: result)
                : new(CommandStatus.Failed, result, "diplomacy_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}

internal static class RelationReviewInteraction
{
    internal static int? Reply(RelationReviewDecision pending, OperationInput input) => pending is RelationReviewDecision.Emissary emissary
        ? EmissaryInteraction.Reply(emissary.Decision, input)
        : input.Kind == OperationInputKind.Choice ? input.Choice
        : input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout ? 0 : null;

    internal static InteractionSpec Prompt(RelationReviewDecision pending, Func<int, ParticipantId> recipient,
        Func<EmissaryIntelligence, InteractionSpec> intelligence, ref InteractionSpec? window)
    {
        if (pending is RelationReviewDecision.Emissary emissary)
        { return EmissaryInteraction.Prompt(emissary.Decision, recipient, intelligence, ref window); }
        var request = ((RelationReviewDecision.Border)pending).Request;
        string location = request.Cities < 2 ? request.City : $"{request.City} and other cities";
        return request.Repeated
            ? new($"The {request.Owner.Plural} demand that your forces withdraw from near {location}.",
                recipient(request.Recipient), null, new([0, 1]))
            { Options = new([new(0, "Withdraw"), new(1, "Refuse and fight")]) }
            : new($"The {request.Owner.Plural} warn that your forces are intruding near {location}.",
                recipient(request.Recipient), null, new([0]))
            { Options = new([new(0, "Continue")]) };
    }
}

internal static class WarAllyInteraction
{
    internal static int? Reply(WarAllyDecision pending, OperationInput input) => pending switch
    {
        WarAllyDecision.Emissary emissary => EmissaryInteraction.Reply(emissary.Decision, input),
        WarAllyDecision.Grant grant => ResearchGrantInteraction.Reply(grant.Decision, input),
        _ => input.Kind == OperationInputKind.Choice ? input.Choice
            : input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout ? 0 : null
    };

    internal static InteractionSpec Prompt(OperationContext context, WarAllyDecision pending, Func<int, ParticipantId> recipient,
        Func<EmissaryIntelligence, InteractionSpec> intelligence, ref InteractionSpec? window)
    {
        if (pending is WarAllyDecision.Emissary emissary)
        { return EmissaryInteraction.Prompt(emissary.Decision, recipient, intelligence, ref window); }
        if (pending is WarAllyDecision.Grant grant)
        { return ResearchGrantInteraction.Prompt(context, grant.Decision, recipient, intelligence, ref window); }
        var offer = ((WarAllyDecision.Offer)pending).Request;
        string payment = offer.Paid ? $" They offer you {offer.Payment} gold"
            + (offer.AdvanceName is { } advance ? $" and {advance}" : "") + "." : "";
        string alliance = offer.AgainstAlly ? $" This ends your alliance with the {offer.Enemy.Plural}." : "";
        return new($"The {offer.Sender.Plural} request an alliance and war against the {offer.Enemy.Plural}.{payment}{alliance}",
            recipient(offer.Recipient), null, new([0, 1]))
        { Options = new([new(0, "Decline"), new(1, "Ally and declare war")]) };
    }
}
