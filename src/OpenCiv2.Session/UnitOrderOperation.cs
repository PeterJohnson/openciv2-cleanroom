using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Private nested movement interactions for U04 execution of an already assigned order.
/// Player admission, active-unit scheduling and AI order choice belong to callers.</summary>
public sealed class UnitOrderOperation(UnitOrderExecution.Operation operation, Func<int, ParticipantId> recipient,
    Func<CityEspionageVisit, InteractionSpec> visit, Func<EmissaryIntelligence, InteractionSpec> intelligence,
    Func<CaptureCityVisit, InteractionSpec> capture, Func<HutCityVisit, InteractionSpec> hut) : IContinuation
{
    private MovementDecision? pending;
    private readonly MovementInteraction interaction = new(recipient, visit, intelligence, capture, hut);
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("Failed unit order presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var answer = interaction.Reply(pending, input);
            pending = operation.Resume(answer);
            if (pending is not null)
            {
                context.Publish();
                var prompt = interaction.Prompt(context, pending);
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var values = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(operation.Result!.Value))]);
            context.Effect("unit-orders.completed", values);
            return completed = new(Result: values);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
