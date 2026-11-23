using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>W06 private city visit after actual hut creation and bounded redirect continuation.</summary>
public sealed class HutOperation(HutEntry.Operation operation, Func<int, ParticipantId> recipient,
    Func<HutCityVisit, InteractionSpec> window) : IContinuation
{
    private HutCityVisit? pending;
    private InteractionSpec? visit;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed hut presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(pending is null ? null : AttackConfirmationInteraction.Reply(input));
            if (pending is { } request)
            {
                context.Publish();
                visit ??= window(request) with { Recipient = recipient(request.Actor) };
                return new(CommandStatus.AwaitingInteraction, Interaction: visit);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending); }
            context.Effect("world.hut.completed", OwnedList<NamedValue>.Empty);
            return completed = new();
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
