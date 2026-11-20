using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Query;

namespace OpenCiv2.Session;

/// <summary>
/// A seat in the current client session, with its observer civilization and optional peer connection.
/// </summary>
public sealed record Participant(ParticipantId Id, CivilizationSlot Civilization, ConnectionId? Connection = null);
/// <summary>
/// Independent seeds for gameplay and presentation-content randomness when starting a client session.
/// </summary>
public readonly record struct Seeds(ulong Gameplay = 0, ulong PresentationContent = 0);
/// <summary>Explicit scoped world-storage pin. Dispose before replacing a session; no finalizer-driven
/// completion.</summary>
public sealed class SessionActivity : IDisposable
{
    private RuntimeState? state;

    /// <summary>
    /// Pins the current runtime state until this activity is disposed.
    /// </summary>
    internal SessionActivity(RuntimeState state)
    {
        ++state.Activities;
        this.state = state;
    }

    /// <summary>
    /// Releases this pin once; repeated disposal is harmless.
    /// </summary>
    public void Dispose()
    {
        if (state is { } current)
        {
            state = null;
            --current.Activities;
        }
    }
}

/// <summary>Trusted engine owner, never a player capability. One cooperative mutation lane; not thread safe.</summary>
public sealed partial class SessionRuntime : IDisposable
{
    private RuntimeState state;

    /// <summary>
    /// Creates an inactive runtime over the supplied process context and a nonzero runtime identity.
    /// </summary>
    /// <remarks>
    /// The trusted host owns this runtime and serializes access. Construction does not start a client session
    /// or run game setup.
    /// </remarks>
    public SessionRuntime(ProcessContext process, RuntimeId identity)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (identity.Value == 0)
        {
            throw new ArgumentException("Runtime identity must be nonzero", nameof(identity));
        }

        state = new(process, identity);
    }

    /// <summary>
    /// Gets mutable engine state for trusted owner workflows.
    /// </summary>
    /// <remarks>
    /// References into this world become stale when the owner replaces it. Player code uses scoped observations
    /// instead.
    /// </remarks>
    public GameState World
    {
        get
        {
            CheckAlive();
            return state.World;
        }
    }

    /// <summary>
    /// Gets process-owned rules, preferences and identity generators shared by this runtime.
    /// </summary>
    public ProcessContext Process
    {
        get
        {
            CheckAlive();
            return state.Process;
        }
    }

    public bool Active => state.Active && !state.Disposed;

    public GameplayRandom GameplayRandom
    {
        get
        {
            state.RequireActive();
            return state.Gameplay!;
        }
    }

    public PresentationRandom PresentationRandom
    {
        get
        {
            state.RequireActive();
            return state.Presentation!;
        }
    }

    public bool HasPendingCommands => state.Pending;

    private void CheckAlive()
    {
        state.RequireOutsideCheckpoint();
        ObjectDisposedException.ThrowIf(state.Disposed, this);
    }

    /// <summary>
    /// Revokes clients and releases all retained continuations, rethrowing the first cleanup failure afterward.
    /// </summary>
    /// <remarks>
    /// The host must transfer required network output before teardown. Disposal does not resume commands to
    /// cancel them.
    /// </remarks>
    public void Dispose()
    {
        state.RequireOutsideCheckpoint();
        if (state.Disposed)
        {
            return;
        }
        // Owner teardown is not command cancellation or transfer of outstanding replies.
        foreach (var lease in state.Leases.Values)
        {
            lease.Active = false;
        }

        state.Leases.Clear();
        state.Active = false;
        state.Disposed = true;
        Exception? first = null;
        foreach (var record in state.Commands.Values)
        {
            var continuation = record.Continuation;
            record.Continuation = null;
            try
            {
                continuation?.Dispose();
            }
            catch (Exception error)
            {
                first ??= error;
            }
        }
        if (first is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
        }
    }

    /// <summary>
    /// Starts a new client session with independently seeded gameplay and presentation contexts.
    /// </summary>
    /// <remarks>
    /// Existing owner workflows, commands and required outboxes must be quiescent. This changes client
    /// identity, not the world.
    /// </remarks>
    public SessionId BeginClientSession(Seeds seeds)
        =>
            BeginClientSession(
                new GameplayRandom(seeds.Gameplay, "session"),
                new PresentationRandom(seeds.PresentationContent, "session"));

    /// <summary>Owner-supplied domain contexts; their context names and controlled tapes are retained.</summary>
    public SessionId BeginClientSession(GameplayRandom gameplay, PresentationRandom presentation)
    {
        state.RequireQuiescent();
        ArgumentNullException.ThrowIfNull(gameplay);
        ArgumentNullException.ThrowIfNull(presentation);
        var session = new SessionId(RuntimeState.Assign(ref state.NextSession));
        EndClientSession();
        state.Session = session;
        state.Gameplay = gameplay;
        state.Presentation = presentation;
        state.Revision = 0;
        state.Active = true;
        return session;
    }

    /// <summary>
    /// Revokes all clients and clears participant and command bookkeeping after outstanding work has drained.
    /// </summary>
    /// <remarks>
    /// World state, process preferences and identity generators survive. Pending commands, pins or outboxes
    /// prevent this transition.
    /// </remarks>
    public void EndClientSession()
    {
        state.RequireQuiescent();
        foreach (var lease in state.Leases.Values)
        {
            lease.Active = false;
        }

        state.Leases.Clear();
        state.Participants.Clear();
        state.Commands.Clear();
        state.Ready.Clear();
        state.Received.Clear();
        state.Active = false;
    }

    /// <summary>
    /// Starts a standalone owner workflow only when the active session has no outstanding work.
    /// </summary>
    public SessionActivity BeginActivity()
    {
        state.RequireActive();
        state.RequireQuiescent();
        return new(state);
    }

    /// <summary>
    /// Pins the current world against owner replacement without requiring an active client session.
    /// </summary>
    public SessionActivity PinWorld()
    {
        CheckAlive();
        return new(state);
    }

    /// <summary>
    /// Adds a seat for a valid civilization and assigns a new participant identity within this runtime.
    /// </summary>
    public ParticipantId AddParticipant(CivilizationSlot civilization, ConnectionId? connection = null)
    {
        state.RequireActive();
        AdmitCivilization(civilization);
        var id = new ParticipantId(RuntimeState.Assign(ref state.NextParticipant));
        state.Participants.Add(id.Value, new(id, civilization, connection));
        return id;
    }

    /// <summary>
    /// Looks up an existing seat in the active session; an unknown identity is an error.
    /// </summary>
    public Participant Participant(ParticipantId id)
    {
        state.RequireActive();
        return state.Participants[id.Value];
    }

    /// <summary>
    /// Revokes a seat's existing clients before changing its civilization or connection.
    /// </summary>
    public void ReassignParticipant(ParticipantId id, CivilizationSlot civilization, ConnectionId? connection = null)
    {
        state.RequireActive();
        AdmitCivilization(civilization);
        var participant = state.Participants[id.Value];
        RevokeParticipant(id);
        state.Participants[id.Value] = participant with { Civilization = civilization, Connection = connection };
    }

    /// <summary>
    /// Revokes all clients for an existing seat and removes that seat from the session.
    /// </summary>
    public void RemoveParticipant(ParticipantId id)
    {
        state.RequireActive();
        _ = state.Participants[id.Value];
        RevokeParticipant(id);
        state.Participants.Remove(id.Value);
    }

    /// <summary>
    /// Revokes clients bound through a peer connection while retaining their participant seats.
    /// </summary>
    public void Disconnect(ConnectionId connection)
    {
        state.RequireActive();
        foreach (var participant in state.Participants.Values.Where(p => p.Connection == connection).ToArray())
        {
            RevokeParticipant(participant.Id);
            state.Participants[participant.Id.Value] = participant with { Connection = null };
        }
    }

    /// <summary>
    /// Issues a revocable client scoped to one participant, observer civilization and access class.
    /// </summary>
    /// <remarks>
    /// A new binding receives visible pending command state. It does not gain mutable world access or ownership
    /// of the runtime.
    /// </remarks>
    public IPlayerClient Bind(ParticipantId participant, ControllerKind kind, AccessClass access = AccessClass.Player)
    {
        state.RequireActive();
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(access))
        {
            throw new ArgumentException("Unknown controller kind or access class");
        }

        var observer = state.Participants[participant.Value].Civilization;
        var controller = new ControllerId(RuntimeState.Assign(ref state.NextController));
        var lease = new Lease(new(state.Identity, state.Session, participant, controller, observer, kind, access));
        RestorePendingUpdates(state, lease);
        state.Leases.Add(controller.Value, lease);
        return new PlayerClient(state, lease);
    }

    /// <summary>
    /// Invalidates one controller binding; an unknown controller is already effectively revoked.
    /// </summary>
    public void Revoke(ControllerId controller)
    {
        CheckAlive();
        if (state.Leases.Remove(controller.Value, out var lease))
        {
            lease.Active = false;
        }
    }

    /// <summary>
    /// Invalidates every controller attached to a seat before that seat changes or disappears.
    /// </summary>
    private void RevokeParticipant(ParticipantId participant)
    {
        foreach (var id in state.Leases.Where(p => p.Value.Identity.Participant == participant).Select(p => p.Key)
            .ToArray())
        {
            state.Leases[id].Active = false;
            state.Leases.Remove(id);
        }
    }

    private static void AdmitCivilization(CivilizationSlot civilization)
    {
        if (civilization.Value >= EntityLimits.Civilizations)
        {
            throw new ArgumentException("Participant civilization must be 0..7");
        }
    }

    /// <summary>
    /// Advances the observation revision after an owner mutation; no gameplay operation is performed.
    /// </summary>
    public void Publish()
    {
        state.RequireActive();
        state.Publish();
    }

    /// <summary>
    /// Validates a weak, revocable capability before projecting owned observations or accepting command input.
    /// </summary>
    /// <remarks>
    /// Weak references ensure a retained UI or script client cannot keep an abandoned runtime or lease alive.
    /// </remarks>
    private sealed partial class PlayerClient(RuntimeState runtime, Lease binding) : IPlayerClient
    {
        private readonly WeakReference<RuntimeState> state = new(runtime);
        private readonly WeakReference<Lease> lease = new(binding);

        /// <summary>
        /// Resolves the runtime and lease, distinguishing an ended session from a revoked controller.
        /// </summary>
        private (RuntimeState State, Lease Lease) Checked()
        {
            if (!state.TryGetTarget(out var current) || !current.Active || current.Disposed)
            {
                throw new AccessException(AccessFailure.SessionEnded);
            }

            current.RequireOutsideCheckpoint();
            if (!lease.TryGetTarget(out var grant) || !grant.Active || grant.Identity.Session != current.Session)
            {
                throw new AccessException(AccessFailure.Revoked);
            }

            return (current, grant);
        }

        public ClientIdentity Identity => Checked().Lease.Identity;

        public LiveRecords<OwnUnit> OwnUnits()
        {
            var (current, grant) = Checked();
            return LiveProjection.OwnUnits(
                current.World.Entities,
                new(current.Identity, current.Session, current.Revision, grant.Identity.Observer));
        }

        public LiveRecords<OwnCity> OwnCities()
        {
            var (current, grant) = Checked();
            return LiveProjection.OwnCities(
                current.World.Entities,
                new(current.Identity, current.Session, current.Revision, grant.Identity.Observer));
        }

        public MapObservation? ObserveMap()
        {
            var (current, grant) = Checked();
            return MapProjection.Observe(current.World, grant.Identity, current.Revision, current.Process.Rules);
        }
    }
}
