using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Private V05 treaty payments and solidarity, including retained V03 grants.</summary>
public sealed class TreatyNegotiationOperation(TreatyNegotiation.Operation operation,
    Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private TreatyNegotiationDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed treaty negotiation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(Reply(input));
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: Prompt(context, pending));
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var result = new OwnedList<NamedValue>([new("endsAudience", new CommandValue.Boolean(operation.EndsAudience))]);
            context.Effect("diplomacy.treaty.completed", result);
            return completed = operation.PresentationFailures.Count == 0 ? new(Result: result)
                : new(CommandStatus.Failed, result, "treaty_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private ResearchReply? Reply(OperationInput input)
    {
        if (pending is TreatyNegotiationDecision.Grant grant)
        { return ResearchGrantInteraction.ResearchReply(grant.Decision, input); }
        if (input.Kind == OperationInputKind.Choice && input.Choice is { } choice)
        { return new(choice); }
        return input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout ? new(0) : null;
    }
    private InteractionSpec Prompt(OperationContext context, TreatyNegotiationDecision decision)
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
    public void Dispose() => operation.Dispose();
}
