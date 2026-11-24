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
            var reply = pending switch
            {
                HutDecision.Contact contact => DiplomaticContactInteraction.Reply(contact.Decision, input),
                HutDecision.Grant grant => ResearchGrantInteraction.ResearchReply(grant.Decision, input),
                HutCityVisit => AttackConfirmationInteraction.Reply(input) is { } selected ? new ResearchReply(selected) : null,
                _ => null
            };
            var previous = pending;
            pending = operation.ResumeDecision(reply);
            if (!ReferenceEquals(previous, pending))
            { intelligenceWindow = null; }
            if (pending is not null)
            {
                context.Publish();
                var prompt = pending is HutCityVisit request
                    ? visit ??= window(request) with { Recipient = recipient(request.Actor) }
                    : pending is HutDecision.Contact contact
                        ? DiplomaticContactInteraction.Prompt(context, contact.Decision, recipient,
                            intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref intelligenceWindow)
                        : ResearchGrantInteraction.Prompt(context, ((HutDecision.Grant)pending).Decision, recipient,
                            intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref intelligenceWindow);
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            context.Effect("world.hut.completed", OwnedList<NamedValue>.Empty);
            return completed = operation.PresentationFailures.Count == 0 ? new()
                : new(CommandStatus.Failed, Code: "hut_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
