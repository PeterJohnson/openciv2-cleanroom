using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Private U04 confirmation and actual nested lifecycle choices. Cancellation
/// declines only the current confirmation; accepted removal is never rolled back.</summary>
public sealed class UnitDisbandOperation(UnitDisband.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private DisbandDecision? pending;
    private InteractionSpec? intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed disband continuation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            ResearchReply? reply = input.Kind switch
            {
                OperationInputKind.Choice => new(input.Choice!.Value),
                OperationInputKind.Cancel or OperationInputKind.Timeout => new(0),
                _ => null
            };
            if (pending is DisbandDecision.Lifecycle child)
            { reply = LifecycleInteraction.ResearchReply(child.Decision, input); }
            var previous = pending;
            pending = operation.ResumeDecision(pending is null ? null : reply);
            if (!ReferenceEquals(previous, pending))
            { intelligenceWindow = null; }
            if (pending is not null)
            {
                var prompt = pending switch
                {
                    DisbandDecision.Unit unit => Confirm(unit.Actor, new CommandValue.Unit(unit.Target), unit.Name),
                    DisbandDecision.City city => Confirm(city.Actor, new CommandValue.City(city.Target), city.Name),
                    DisbandDecision.Lifecycle nested => LifecycleInteraction.Prompt(context, nested.Decision, recipient, ref intelligenceWindow, intelligence),
                    _ => throw new InvalidOperationException("Unknown disband decision")
                };
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var result = new OwnedList<NamedValue>([new("disbanded", new CommandValue.Boolean(operation.Disbanded))]);
            context.Effect("unit.disband.completed", result);
            return completed = operation.PresentationFailures.Count == 0 ? new(Result: result)
                : new(CommandStatus.Failed, result, "disband_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private InteractionSpec Confirm(int actor, CommandValue target, string name) =>
        new($"Disband {name}?", recipient(actor), target, new([0, 1]))
        { Options = new([new(0, "Keep"), new(1, "Disband")]) };
    public void Dispose() => operation.Dispose();
}
