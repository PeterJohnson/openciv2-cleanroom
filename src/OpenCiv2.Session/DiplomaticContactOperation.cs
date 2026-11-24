using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V04 local contact with private nested audience, grant and ally-help decisions.</summary>
public sealed class DiplomaticContactOperation(DiplomaticContact.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private DiplomaticContactDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed contact cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var previous = pending;
            pending = operation.Resume(DiplomaticContactInteraction.Reply(pending, input));
            if (!ReferenceEquals(previous, pending))
            { window = null; }
            if (pending is not null)
            { context.Publish(); return new(CommandStatus.AwaitingInteraction, Interaction: DiplomaticContactInteraction.Prompt(context, pending, recipient, intelligence, ref window)); }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            context.Effect("diplomacy.contact.completed", new([]));
            return completed = operation.PresentationFailures.Count == 0 ? new()
                : new(CommandStatus.Failed, Code: "contact_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}

internal static class DiplomaticContactInteraction
{
    public static ResearchReply? Reply(DiplomaticContactDecision? pending, OperationInput input) => pending switch
    {
        DiplomaticContactDecision.Scenario d => LifecycleInteraction.ResearchReply(d.Value, input),
        DiplomaticContactDecision.Audience d => DiplomaticAudienceInteraction.Reply(d.Value, input),
        DiplomaticContactDecision.Grant d => ResearchGrantInteraction.ResearchReply(d.Value, input),
        DiplomaticContactDecision.Ally d => AllyHelpInteraction.Reply(d.Value, input) is { } reply ? new(reply) : null,
        _ => null
    };
    public static InteractionSpec Prompt(OperationContext context, DiplomaticContactDecision decision,
        Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence, ref InteractionSpec? window) => decision switch
        {
            DiplomaticContactDecision.Scenario d => LifecycleInteraction.Prompt(context, d.Value, recipient, ref window, intelligence),
            DiplomaticContactDecision.Audience d => DiplomaticAudienceInteraction.Prompt(context, d.Value, recipient, intelligence, ref window),
            DiplomaticContactDecision.Grant d => ResearchGrantInteraction.Prompt(context, d.Value, recipient, intelligence, ref window),
            DiplomaticContactDecision.Ally d => AllyHelpInteraction.Prompt(d.Value, recipient, intelligence, ref window),
            _ => throw new InvalidOperationException("Unknown contact decision")
        };
}
