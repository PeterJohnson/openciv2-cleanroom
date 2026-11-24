using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;

namespace OpenCiv2.Session;

/// <summary>Required P06 window closure and independent nested incident routing for C09.</summary>
public sealed class CaptureProductionOperation(CaptureProduction.Operation operation,
    Func<int, ParticipantId> recipient, Func<CaptureCityVisit, InteractionSpec> window, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private CaptureProductionDecision? pending;
    private InteractionSpec? prompt;
    private OperationStep? completed;
    private InteractionSpec? intelligenceWindow;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed capture window cannot resume"); }
        try
        { return Advance(context, input); }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private OperationStep Advance(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        { return completed; }
        int? reply = input.Kind switch
        {
            OperationInputKind.Choice => input.Choice,
            OperationInputKind.Cancel or OperationInputKind.Timeout => 0,
            _ => null
        };
        ResearchReply? answer = LifecycleInteraction.Selection(reply);
        if (pending is CaptureProductionDecision.Lifecycle lifecycle)
        { answer = LifecycleInteraction.ResearchReply(lifecycle.Decision, input); }
        var previous = pending;
        pending = operation.ResumeDecision(pending is null ? null : answer);
        if (!ReferenceEquals(previous, pending))
        { intelligenceWindow = null; }
        if (pending is not null)
        {
            context.Publish();
            var interaction = pending switch
            {
                CaptureProductionDecision.Lifecycle incident => LifecycleInteraction.Prompt(context, incident.Decision, recipient, ref intelligenceWindow, intelligence),
                CaptureProductionDecision.Window visit => prompt ??= window(visit.Request) with { Recipient = recipient(visit.Request.Owner) },
                _ => throw new InvalidOperationException("Unknown capture production decision")
            };
            return new(CommandStatus.AwaitingInteraction, Interaction: interaction);
        }
        if (!operation.Completed)
        { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
        context.Effect("city.capture.production.completed", new([]));
        return completed = new();
    }
    public void Dispose() => operation.Dispose();
}
