using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Private decisions from W06 orders and their actual ransom, pillage and lifecycle children.</summary>
public sealed class BarbarianOrderOperation(BarbarianOrders.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private BarbarianOrderDecision? pending;
    private InteractionSpec? intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("Failed barbarian order presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var answer = BarbarianOrderInteraction.Reply(pending, input);
            var previous = pending;
            pending = operation.ResumeDecision(answer);
            if (!ReferenceEquals(previous, pending))
            { intelligenceWindow = null; }
            if (pending is not null)
            {
                context.Publish();
                var prompt = BarbarianOrderInteraction.Prompt(context, pending, recipient, ref intelligenceWindow, intelligence);
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (operation.Result is null)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var result = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(operation.Result.Value))]);
            context.Effect("unit.barbarian-orders.completed", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}

internal static class BarbarianOrderInteraction
{
    internal static ResearchReply? Reply(BarbarianOrderDecision? pending, OperationInput input) => pending switch
    {
        BarbarianOrderDecision.Lifecycle life => LifecycleInteraction.ResearchReply(life.Decision, input),
        BarbarianOrderDecision.Ransom ransom => BarbarianRansomInteraction.Reply(ransom.Decision, input),
        BarbarianOrderDecision.Pillage => LifecycleInteraction.Selection(AttackConfirmationInteraction.Reply(input)),
        _ => null
    };
    internal static InteractionSpec Prompt(OperationContext context, BarbarianOrderDecision pending, Func<int, ParticipantId> recipient,
        ref InteractionSpec? intelligenceWindow, Func<EmissaryIntelligence, InteractionSpec>? intelligence) => pending switch
        {
            BarbarianOrderDecision.Lifecycle life => LifecycleInteraction.Prompt(context, life.Decision, recipient,
                ref intelligenceWindow, intelligence),
            BarbarianOrderDecision.Ransom ransom => BarbarianRansomInteraction.Prompt(context, ransom.Decision, recipient,
                ref intelligenceWindow, intelligence),
            BarbarianOrderDecision.Pillage pillage => AttackConfirmationInteraction.Prompt(pillage.Warning, recipient(pillage.Warning.Actor.Civilization)),
            _ => throw new InvalidOperationException("Unknown barbarian order decision")
        };
}
