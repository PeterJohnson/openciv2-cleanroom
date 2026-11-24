using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V05 private conversation menu and actual nested favor/gift/proposal decisions.</summary>
public sealed class DiplomaticConversationOperation(DiplomaticConversation.Operation operation,
    Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private DiplomaticConversationDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed conversation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var previous = pending;
            pending = operation.Resume(DiplomaticConversationInteraction.Reply(pending, input));
            if (!ReferenceEquals(previous, pending))
            { window = null; }
            if (pending is not null)
            { context.Publish(); return new(CommandStatus.AwaitingInteraction, Interaction: DiplomaticConversationInteraction.Prompt(context, pending, recipient, intelligence, ref window)); }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            context.Effect("diplomacy.conversation.completed", new([]));
            return completed = operation.PresentationFailures.Count == 0 ? new()
                : new(CommandStatus.Failed, Code: "conversation_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}

internal static class DiplomaticConversationInteraction
{
    public static ResearchReply? Reply(DiplomaticConversationDecision? pending, OperationInput input) => pending switch
    {
        DiplomaticConversationDecision.Proposal proposal => DiplomaticProposalInteraction.Reply(proposal.Decision, input),
        DiplomaticConversationDecision.Favor favor => DiplomaticFavorInteraction.Reply(favor.Decision, input),
        DiplomaticConversationDecision.Gift gift => DiplomaticGiftInteraction.Reply(gift.Decision, input),
        DiplomaticConversationDecision.Emissary emissary => EmissaryInteraction.Reply(emissary.Decision, input) is { } answer ? new(answer) : null,
        _ => input.Kind == OperationInputKind.Choice && input.Choice is { } choice ? new(choice)
            : input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout ? new(0) : null
    };
    public static InteractionSpec Prompt(OperationContext context, DiplomaticConversationDecision decision,
        Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence, ref InteractionSpec? window)
    {
        switch (decision)
        {
            case DiplomaticConversationDecision.Proposal proposal:
                return DiplomaticProposalInteraction.Prompt(context, proposal.Decision, recipient, intelligence, ref window);
            case DiplomaticConversationDecision.Favor favor:
                return DiplomaticFavorInteraction.Prompt(context, favor.Decision, recipient, intelligence, ref window);
            case DiplomaticConversationDecision.Gift gift:
                return DiplomaticGiftInteraction.Prompt(context, gift.Decision, recipient, intelligence, ref window);
            case DiplomaticConversationDecision.Emissary emissary:
                return EmissaryInteraction.Prompt(emissary.Decision, recipient, intelligence, ref window);
            case DiplomaticConversationDecision.Intelligence report:
                return window ??= intelligence(report.Visit) with { Recipient = recipient(report.Visit.Observer) };
            case DiplomaticConversationDecision.Menu menu:
                window = null;
                string[] labels = ["End audience", "Propose an alliance", "Propose peace", "Request aid",
                    "Demand tribute", "Demand withdrawal", "Cancel alliance", "Request a favor", "Offer a gift"];
                return new($"Speak with the {menu.Partner.Plural}", recipient(menu.Recipient), null, menu.Options)
                { Options = new(menu.Options.Select(value => new InteractionOption(value, labels[value]))) };
            default:
                throw new InvalidOperationException("Unknown conversation decision");
        }
    }
}
