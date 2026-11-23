using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V05 private reception decision and required intelligence-window continuation.</summary>
public sealed class EmissaryOperation(EmissaryReception.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private EmissaryDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed emissary presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(pending is null ? null : EmissaryInteraction.Reply(pending, input));
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: EmissaryInteraction.Prompt(pending, recipient, intelligence, ref window));
            }
            var result = new OwnedList<NamedValue>([new("accepted", new CommandValue.Boolean(operation.Accepted!.Value))]);
            context.Effect("diplomacy.emissary.completed", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}

internal static class EmissaryInteraction
{
    internal static int? Reply(EmissaryDecision pending, OperationInput input) => input.Kind switch
    {
        OperationInputKind.Choice => input.Choice,
        OperationInputKind.Cancel or OperationInputKind.Timeout => pending is EmissaryDecision.Receive ? 1 : 0,
        _ => null
    };
    internal static InteractionSpec Prompt(EmissaryDecision pending, Func<int, ParticipantId> recipient,
        Func<EmissaryIntelligence, InteractionSpec> intelligence, ref InteractionSpec? window)
    {
        if (pending is EmissaryDecision.Intelligence report)
        { return window ??= intelligence(report.Visit) with { Recipient = recipient(report.Visit.Observer) }; }
        window = null;
        var offer = ((EmissaryDecision.Receive)pending).Offer;
        List<InteractionOption> options = [new(0, "Receive emissary"), new(1, "Refuse")];
        if (offer.AllowIntelligence)
        { options.Add(new(2, "Intelligence report")); }
        return new($"Receive an emissary from {offer.Counterpart.Title} {offer.Counterpart.Leader} of the {offer.Counterpart.Plural}?",
            recipient(offer.Actor.Civilization), null, new(options.Select(o => o.Value)))
        { Options = new(options) };
    }
}
