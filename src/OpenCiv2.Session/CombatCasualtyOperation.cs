using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>U06 casualty continuation: private victim routing for real nested lifecycle incidents.
/// The full combat parent owns sequencing with results, promotion and city consequences.</summary>
public sealed class CombatCasualtyOperation(CombatCasualties.Operation operation, Func<int, ParticipantId> recipient) : IContinuation
{
    private EspionagePretext? pending;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed casualty presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(pending is null ? null : EspionageInteraction.Reply(input));
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: EspionageInteraction.Prompt(pending, recipient(pending.Victim.Civilization)));
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending); }
            context.Effect("combat.casualties.completed", new([]));
            return completed = new();
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
