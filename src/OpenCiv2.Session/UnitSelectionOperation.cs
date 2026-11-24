using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>U04 activation's private W03/V04 decisions. Selection admission and
/// the surrounding human/AI phase belong to the caller.</summary>
public sealed class UnitSelectionOperation(UnitSelection.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private DiplomaticContactDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("Failed unit activation presentation cannot resume"); }
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
                return new(CommandStatus.AwaitingInteraction,
                    Interaction: DiplomaticContactInteraction.Prompt(context, pending, recipient, intelligence, ref window));
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            context.Effect("unit.activation.completed", new([]));
            return completed = operation.PresentationFailures.Count == 0 ? new()
                : new(CommandStatus.Failed, Code: "unit_activation_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
