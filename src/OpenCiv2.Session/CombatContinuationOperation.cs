using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>U06 delivery/survivor child with private lifecycle and actual A04/U05/C09 decision routing.
/// The owning combat command supplies admission and the surrounding combat stages.</summary>
public sealed class CombatContinuationOperation(CombatContinuation.Operation operation, Func<int, ParticipantId> recipient,
    Func<CaptureCityVisit, InteractionSpec> window, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private CombatContinuationDecision? pending;
    private InteractionSpec? windowPrompt, intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed combat continuation presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(pending is null ? null : CombatContinuationInteraction.Reply(pending, input));
            if (pending is not null)
            {
                context.Publish();
                var prompt = CombatContinuationInteraction.Prompt(context, pending, recipient, window, ref windowPrompt, ref intelligenceWindow, intelligence);
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending); }
            var result = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(operation.Result!.Value))]);
            context.Effect("combat.continuation.completed", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}

internal static class CombatContinuationInteraction
{
    internal static ResearchReply? Reply(CombatContinuationDecision pending, OperationInput input)
    {
        if (pending is CombatContinuationDecision.Lifecycle lifecycle)
        { return LifecycleInteraction.ResearchReply(lifecycle.Decision, input); }
        if (pending is CombatContinuationDecision.Response { Decision: ParadropDecision.Capture capture })
        { return CityCaptureInteraction.Reply(capture.Decision, input); }
        return input.Kind switch
        {
            OperationInputKind.Choice when input.Choice is { } choice => new(choice),
            OperationInputKind.Cancel or OperationInputKind.Timeout => new(0),
            _ => null
        };
    }
    internal static InteractionSpec Prompt(OperationContext context, CombatContinuationDecision pending, Func<int, ParticipantId> recipient,
        Func<CaptureCityVisit, InteractionSpec> window, ref InteractionSpec? windowPrompt, ref InteractionSpec? intelligenceWindow, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null)
    {
        return pending switch
        {
            CombatContinuationDecision.Lifecycle incident => LifecycleInteraction.Prompt(context, incident.Decision, recipient, ref intelligenceWindow, intelligence),
            CombatContinuationDecision.Response { Decision: ParadropDecision.Attack attack } => AttackConfirmationInteraction.Prompt(attack.Warning, recipient(attack.Warning.Actor.Civilization)),
            CombatContinuationDecision.Response { Decision: ParadropDecision.Capture capture } => CityCaptureInteraction.Prompt(context, capture.Decision, recipient, window, ref windowPrompt, ref intelligenceWindow, intelligence),
            _ => throw new InvalidOperationException("Unknown combat continuation decision")
        };
    }
}
