using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;

namespace OpenCiv2.Session;

/// <summary>Trusted C09 child; the parent owns capture admission and subsequent city/tile takeover.</summary>
public sealed class CapturePopulationOperation(CapturePopulationLoss.Operation operation,
    Func<int, ParticipantId> recipient) : IContinuation
{
    private bool pending, faulted;
    private OperationStep? completed;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        {
            throw new InvalidOperationException("A failed population continuation cannot resume");
        }

        try
        { return Advance(context, input); }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private OperationStep Advance(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        {
            return completed;
        }

        var decision = operation.Resume(pending ? EspionageInteraction.Reply(input) : null);
        pending = decision is not null;
        if (decision is not null)
        {
            context.Publish();
            return new(CommandStatus.AwaitingInteraction,
                Interaction: EspionageInteraction.Prompt(decision, recipient(decision.Victim.Civilization)));
        }
        if (!operation.Completed)
        { context.Publish(); return new(CommandStatus.Pending); }
        var result = new OwnedList<NamedValue>([new("city", new CommandValue.Integer(operation.RemainingCity?.Value ?? -1))]);
        context.Effect("city.capture.population.completed", result);
        return completed = new(Result: result);
    }
    public void Dispose() => operation.Dispose();
}
