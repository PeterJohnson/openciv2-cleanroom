using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>U06 delivery/survivor child with private lifecycle and actual A04/U05/C09 decision routing.
/// The owning combat command supplies admission and the surrounding combat stages.</summary>
public sealed class CombatContinuationOperation(CombatContinuation.Operation operation, Func<int, ParticipantId> recipient,
    Func<CaptureCityVisit, InteractionSpec> window) : IContinuation
{
    private CombatContinuationDecision? pending;
    private InteractionSpec? windowPrompt;
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
                var prompt = CombatContinuationInteraction.Prompt(context, pending, recipient, window, ref windowPrompt);
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
        bool technology = pending is CombatContinuationDecision.Response { Decision: ParadropDecision.Capture { Decision: CityCaptureDecision.Technology } };
        return input.Kind switch
        {
            OperationInputKind.Choice when input.Choice is { } choice => technology && choice == 100 ? new(0, 1) : new(choice),
            OperationInputKind.Cancel or OperationInputKind.Timeout when !technology => new(0),
            _ => null
        };
    }
    internal static InteractionSpec Prompt(OperationContext context, CombatContinuationDecision pending, Func<int, ParticipantId> recipient,
        Func<CaptureCityVisit, InteractionSpec> window, ref InteractionSpec? windowPrompt)
    {
        return pending switch
        {
            CombatContinuationDecision.Incident incident => EspionageInteraction.Prompt(incident.Question, recipient(incident.Question.Victim.Civilization)),
            CombatContinuationDecision.Response { Decision: ParadropDecision.Attack attack } => AttackConfirmationInteraction.Prompt(attack.Warning, recipient(attack.Warning.Actor.Civilization)),
            CombatContinuationDecision.Response { Decision: ParadropDecision.Capture capture } => CityCaptureInteraction.Prompt(context, capture.Decision, recipient, window, ref windowPrompt),
            _ => throw new InvalidOperationException("Unknown combat continuation decision")
        };
    }
}
