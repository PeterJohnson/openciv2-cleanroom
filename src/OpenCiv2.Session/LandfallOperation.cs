using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>U05 passenger confirmation. The owning U02 continuation consumes Restart and the updated movement state.</summary>
public sealed class LandfallOperation(TransportMovement.LandfallOperation operation, Func<int, ParticipantId> recipient) : IContinuation
{
    private LandfallChoice? pending;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed landfall presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            int? reply = input.Kind switch
            {
                OperationInputKind.Choice => input.Choice,
                OperationInputKind.Cancel or OperationInputKind.Timeout => 0,
                _ => null
            };
            pending = operation.Resume(pending is null ? null : reply);
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: new($"Disembark from {pending.ShipName}?", recipient(pending.Actor),
                    new CommandValue.Unit(pending.Ship), new([0, 1]))
                { Options = new([new(0, "Stay aboard"), new(1, "Disembark")]) });
            }
            var result = new OwnedList<NamedValue>([new("restart", new CommandValue.Boolean(operation.Restart))]);
            context.Effect("movement.landfall.completed", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
