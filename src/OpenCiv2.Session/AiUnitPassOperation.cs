using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Private decisions and cooperative continuation of the actual A01 unit passes.</summary>
public sealed class AiUnitPassOperation(AiUnitPasses.Operation operation, Func<int, ParticipantId> recipient,
    Func<CityEspionageVisit, InteractionSpec> visit, Func<EmissaryIntelligence, InteractionSpec> intelligence,
    Func<CaptureCityVisit, InteractionSpec> capture, Func<HutCityVisit, InteractionSpec> hut) : IContinuation
{
    private AutomatedOrderDecision? pending;
    private readonly AutomatedOrderInteraction interaction = new(recipient, visit, intelligence, capture, hut);
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        {
            throw new InvalidOperationException("Failed AI unit pass presentation cannot resume");
        }

        if (completed is not null)
        {
            return completed;
        }

        try
        {
            pending = operation.Resume(interaction.Reply(pending, input));
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: interaction.Prompt(context, pending));
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            context.Effect("ai.unit-passes.completed", new([]));
            return completed = new();
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
