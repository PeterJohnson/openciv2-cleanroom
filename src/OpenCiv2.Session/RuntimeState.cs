using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Session;

/// <summary>
/// A controller grant with its private update queue and per-command effect-delivery cursor.
/// </summary>
internal sealed class Lease(ClientIdentity identity)
{
    private const int CityProductionEventLimit = 32;
    private readonly Queue<CityProductionEvent>? productionEvents =
        identity.Kind == ControllerKind.Automation ? new() : null;
    private int droppedProductionEvents;

    public ClientIdentity Identity { get; } = identity;
    public bool Active = true;
    public Queue<CommandUpdate> Updates { get; } = new();
    public Dictionary<CommandId, int> DeliveredEffects { get; } = new();

    public void EnqueueProduction(CityProductionEvent notification)
    {
        if (productionEvents is null)
        {
            return;
        }
        if (productionEvents.Count == CityProductionEventLimit)
        {
            productionEvents.Dequeue();
            if (droppedProductionEvents < int.MaxValue)
            {
                ++droppedProductionEvents;
            }
        }
        productionEvents.Enqueue(notification);
    }

    public CityProductionEvents TakeProductionEvents()
    {
        if (productionEvents is not { Count: > 0 })
        {
            return CityProductionEvents.Empty;
        }
        var result = new CityProductionEvents(new(productionEvents), droppedProductionEvents);
        productionEvents.Clear();
        droppedProductionEvents = 0;
        return result;
    }
}
/// <summary>
/// Retained command lifecycle state, including authority routing, continuation ownership and committed effects.
/// </summary>
/// <remarks>
/// Effects remain available after failure or cancellation. A continuation is detached before disposal to
/// prevent double release.
/// </remarks>
internal sealed class CommandRecord(
    CommandActor actor,
    CommandRequest request,
    CommandDefinition definition,
    CommandUpdate update)
{
    public CommandActor Actor { get; } = actor;
    public CommandRequest Request { get; } = request;
    public CommandDefinition Definition { get; } = definition;
    public CommandUpdate Update = update;
    public IContinuation? Continuation;
    public ConnectionId? Executor;
    public (ConnectionId Source, CommandCorrelation Correlation)? ReplyTo;
    public bool ResultApplication;
    public OwnedList<NamedValue> Resolved = OwnedList<NamedValue>.Empty;
    public List<Effect> Effects { get; } = [];
}
/// <summary>
/// One queued continuation input; admission and execution occur at separate points in the session lane.
/// </summary>
internal sealed record Work(CommandId Command, OperationInput Input);
/// <summary>
/// Mutable bookkeeping behind one trusted runtime, accessed only on its cooperative mutation lane.
/// </summary>
/// <remarks>
/// Checkpoint restore stages a replacement instance. Counters survive replacement so old controller and command
/// identities are not reused.
/// </remarks>
internal sealed class RuntimeState(ProcessContext process, RuntimeId identity)
{
    public ProcessContext Process { get; } = process;
    public GameState World = new(process);
    public RuntimeId Identity { get; } = identity;
    public bool Active, Advancing, Disposed, Checkpointing;
    public int Activities;
    public bool LocalPlay;
    public SessionId Session;
    public ulong NextSession = 1, NextParticipant = 1, NextController = 1, Revision;
    public ulong NextCommand = 1, NextInteraction = 1;
    public SortedDictionary<ulong, Participant> Participants { get; } = new();
    public SortedDictionary<ulong, Lease> Leases { get; } = new();
    public GameplayRandom? Gameplay;
    public PresentationRandom? Presentation;
    public SortedDictionary<string, CommandDefinition> Definitions { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<ulong, CommandRecord> Commands { get; } = new();
    public Queue<Work> Ready { get; } = new();
    public List<RoutedRequest> Requests { get; } = [];
    public List<RoutedReply> Replies { get; } = [];
    public Dictionary<(ConnectionId, CommandCorrelation), CommandId> Received { get; } = new();

    /// <summary>
    /// Rejects client work during checkpoint callbacks or after the client session has ended.
    /// </summary>
    public void RequireActive()
    {
        RequireOutsideCheckpoint();
        if (!Active || Disposed)
        {
            throw new AccessException(AccessFailure.SessionEnded);
        }
    }

    /// <summary>
    /// Prevents codec callbacks from observing or mutating the session during checkpoint staging.
    /// </summary>
    public void RequireOutsideCheckpoint()
    {
        if (Checkpointing)
        {
            throw new InvalidOperationException("Checkpoint codecs must not reenter the session");
        }
    }

    public bool Pending => Commands.Values.Any(c => !c.Update.Status.Terminal());

    /// <summary>
    /// Requires no in-flight steps, pinned workflows, pending commands or untransferred network output.
    /// </summary>
    public void RequireQuiescent()
    {
        RequireOutsideCheckpoint();
        ObjectDisposedException.ThrowIf(Disposed, this);
        if (Advancing || Activities != 0 || Pending || Requests.Count != 0 || Replies.Count != 0)
        {
            throw new InvalidOperationException(
                "Finish owner workflows/commands and transfer required outboxes before replacing a session");
        }
    }

    /// <summary>
    /// Increments the world observation revision, failing instead of wrapping into an older revision.
    /// </summary>
    public void Publish()
    {
        if (Revision == ulong.MaxValue)
        {
            throw new OverflowException("World observation revision exhausted");
        }

        ++Revision;
    }

    /// <summary>
    /// Consumes a monotonically increasing identity and refuses to wrap or issue the exhausted sentinel.
    /// </summary>
    public static ulong Assign(ref ulong next)
    {
        if (next == ulong.MaxValue)
        {
            throw new OverflowException("Session identity space exhausted");
        }

        return next++;
    }
}
