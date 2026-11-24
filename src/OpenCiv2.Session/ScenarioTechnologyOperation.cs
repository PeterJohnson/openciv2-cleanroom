using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>S02 GiveTechnology retains actual V03 government/emissary choices for their private recipients.</summary>
public sealed class ScenarioTechnologyOperation(ScenarioCivilizationActions.GrantOperation operation,
    Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed scenario technology presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var previous = operation.Pending;
            var pending = operation.Resume(ResearchGrantInteraction.Reply(previous, input));
            if (!ReferenceEquals(previous, pending))
            { window = null; }
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: ResearchGrantInteraction.Prompt(context, pending,
                    recipient, intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref window));
            }
            if (operation.Result is null)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var values = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(operation.Result.Value))]);
            context.Effect("scenario.technology.completed", values);
            return completed = operation.PresentationFailures.Count == 0 ? new(Result: values)
                : new(CommandStatus.Failed, values, "scenario_technology_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
