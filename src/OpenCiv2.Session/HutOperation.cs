using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>W06 private city visit after actual hut creation and bounded redirect continuation.</summary>
public sealed class HutOperation(HutEntry.Operation operation, Func<int, ParticipantId> recipient,
    Func<HutCityVisit, InteractionSpec> window, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private HutDecision? pending;
    private InteractionSpec? visit, intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed hut presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(pending is HutDecision.Grant grant ? ResearchGrantInteraction.Reply(grant.Decision, input)
                : pending is null ? null : AttackConfirmationInteraction.Reply(input));
            if (pending is not null)
            {
                context.Publish();
                var prompt = pending is HutCityVisit request
                    ? visit ??= window(request) with { Recipient = recipient(request.Actor) }
                    : ResearchGrantInteraction.Prompt(context, ((HutDecision.Grant)pending).Decision, recipient,
                        intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref intelligenceWindow);
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            context.Effect("world.hut.completed", OwnedList<NamedValue>.Empty);
            return completed = new();
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
