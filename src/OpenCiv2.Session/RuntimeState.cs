using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Session;

internal sealed class Lease(ClientIdentity identity)
{
    public ClientIdentity Identity { get; } = identity;
    public bool Active = true;
    public Queue<CommandUpdate> Updates { get; } = new();
    public Dictionary<CommandId, int> DeliveredEffects { get; } = new();
}
internal sealed class CommandRecord(CommandActor actor, CommandRequest request, CommandDefinition definition, CommandUpdate update)
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
internal sealed record Work(CommandId Command, OperationInput Input);
internal sealed class RuntimeState(ProcessContext process, RuntimeId identity)
{
    public ProcessContext Process { get; } = process;
    public GameState World = new(process);
    public RuntimeId Identity { get; } = identity;
    public bool Active, Advancing, Disposed, Checkpointing;
    public int Activities;
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
    public void RequireActive()
    {
        RequireOutsideCheckpoint();
        if (!Active || Disposed) throw new AccessException(AccessFailure.SessionEnded);
    }
    public void RequireOutsideCheckpoint()
    {
        if (Checkpointing) throw new InvalidOperationException("Checkpoint codecs must not reenter the session");
    }
    public bool Pending => Commands.Values.Any(c => !c.Update.Status.Terminal());
    public void RequireQuiescent()
    {
        RequireOutsideCheckpoint();
        ObjectDisposedException.ThrowIf(Disposed, this);
        if (Advancing || Activities != 0 || Pending || Requests.Count != 0 || Replies.Count != 0)
            throw new InvalidOperationException("Finish owner workflows/commands and transfer required outboxes before replacing a session");
    }
    public void Publish()
    {
        if (Revision == ulong.MaxValue) throw new OverflowException("World observation revision exhausted");
        ++Revision;
    }
    public static ulong Assign(ref ulong next)
    {
        if (next == ulong.MaxValue) throw new OverflowException("Session identity space exhausted");
        return next++;
    }
}
