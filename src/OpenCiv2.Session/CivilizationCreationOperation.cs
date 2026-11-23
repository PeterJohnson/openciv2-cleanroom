using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Trusted local V01 creation with bounded placement and private catch-up research choices.</summary>
public sealed class CivilizationCreationOperation(CivilizationCreation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private InteractionSpec? intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed creation presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var result = operation.Advance(64, ResearchGrantInteraction.Reply(operation.Pending, input));
            if (operation.Pending is { } decision)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: ResearchGrantInteraction.Prompt(context,
                    decision, recipient, intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref intelligenceWindow));
            }
            if (result is null)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var values = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(result.Value))]);
            context.Effect("civilization.creation.completed", values);
            return completed = new(Result: values);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
