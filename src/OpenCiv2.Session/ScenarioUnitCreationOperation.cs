using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Private nested contact choices while an S02 CreateUnit action completes.</summary>
public sealed class ScenarioUnitCreationOperation(ScenarioUnitActions.CreateOperation operation,
    Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private DiplomaticContactDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("Failed scenario creation presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var previous = pending;
            pending = operation.Resume(DiplomaticContactInteraction.Reply(pending, input));
            if (!ReferenceEquals(previous, pending))
            { window = null; }
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: DiplomaticContactInteraction.Prompt(context, pending, recipient, intelligence, ref window));
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var result = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(operation.Result!.Value))]);
            context.Effect("scenario.unit_creation.completed", result);
            return completed = operation.PresentationFailures.Count == 0 ? new(Result: result)
                : new(CommandStatus.Failed, Result: result, Code: "scenario_creation_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
