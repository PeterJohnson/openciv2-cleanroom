using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;

namespace OpenCiv2.Session;

public delegate void CheckpointDecoder(ReadOnlySpan<byte> bytes, GameState world, ProcessContext process);
public delegate IContinuation ContinuationDecoder(ReadOnlySpan<byte> bytes, CommandRequest request, CommandUpdate update);
/// <summary>Trusted, synchronous, non-reentrant owner codec. Encode only observes its operands; Decode
/// writes only supplied staged state. No services, live session references or external side effects.</summary>
public sealed record CheckpointProfile(string Id, Func<GameState, ProcessContext, OwnedList<byte>> Encode, CheckpointDecoder Decode);
/// <summary>Persists explicit operation phase/locals. Decode must not repeat accepted effects or run
/// the execution factory; on failure it owns cleanup of partially constructed resources.</summary>
public sealed record ContinuationCodec(string Id, uint Version, Func<IContinuation, OwnedList<byte>> Encode, ContinuationDecoder Decode);

public sealed partial class SessionRuntime
{
    private static void CheckpointRequire(bool condition, string message) { if (!condition) throw new ArgumentException(message); }
    private static void ValidateProfile(CheckpointProfile profile, string contentHash)
    {
        ArgumentNullException.ThrowIfNull(profile);
        CheckpointRequire(profile.Id is { Length: > 0 and <= 128 } && profile.Encode is not null && profile.Decode is not null, "Checkpoint requires a named owner codec");
        CheckpointRequire(contentHash is { Length: 64 } && contentHash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'), "Invalid checkpoint content hash");
    }
    private static ContinuationCodec OperationCodec(CommandDefinition definition)
    {
        var codec = definition.Checkpoint;
        CheckpointRequire(codec is not null && codec.Id is { Length: > 0 and <= LocalCheckpointCodec.MaximumText } && codec.Version != 0 && codec.Encode is not null && codec.Decode is not null, "Operation has no valid checkpoint codec");
        return codec!;
    }
    private static void LocalBoundary(RuntimeState current)
    {
        CheckpointRequire(current.Active && !current.Disposed && !current.Advancing && current.Activities == 0 && current.Participants.Count == 1 && current.Gameplay is not null && current.Presentation is not null, "Checkpoint requires an active local participant between steps");
        var participant = current.Participants.Values.Single();
        CheckpointRequire(participant.Civilization.Value == 1 && participant.Connection is null, "Checkpoint requires local civilization 1");
        CheckpointRequire(current.Commands.Count <= 1 && current.Ready.Count == 0 && current.Requests.Count == 0 && current.Replies.Count == 0 && current.Received.Count == 0, "Checkpoint excludes extra commands, queued inputs and peer work");
        foreach (var record in current.Commands.Values)
            CheckpointRequire(record.Update.Status == CommandStatus.AwaitingInteraction && record.Update.Interaction is { } interaction && record.Continuation is not null && record.Executor is null && record.ReplyTo is null && !record.ResultApplication && record.Resolved.Count == 0 && record.Actor.Source is null && record.Actor.Participant == participant.Id && record.Actor.Civilization == participant.Civilization && interaction.Recipient == participant.Id, "Checkpoint supports only an owned local suspended decision");
    }
    public byte[] Checkpoint(CheckpointProfile profile, string contentHash, ulong replayLimit = RandomCheckpointCodec.DefaultReplayLimit)
    {
        CheckAlive(); ValidateProfile(profile, contentHash); LocalBoundary(state); var current = state; current.Checkpointing = true;
        try
        {
            CheckpointCommand? command = null;
            if (current.Commands.Values.SingleOrDefault() is { } record)
            {
                var codec = OperationCodec(record.Definition);
                command = new(record.Request, record.Definition.Descriptor.Version, record.Actor.Controller, record.Actor.Access, Snapshot(record), codec.Id, codec.Version, codec.Encode(record.Continuation!));
            }
            var counters = new CheckpointCounters(current.NextSession, current.NextParticipant, current.NextController, current.NextCommand, current.NextInteraction, current.Revision);
            var checkpoint = new LocalCheckpoint(profile.Id, contentHash, current.Identity, current.Session, current.Participants.Values.Single().Id, counters,
                profile.Encode(current.World, current.Process), current.Gameplay!.Capture(), current.Presentation!.Capture(), command);
            return LocalCheckpointCodec.Encode(checkpoint, replayLimit);
        }
        finally { current.Checkpointing = false; }
    }
    /// <summary>M1-01 staged restore. All recoverable admission failures retain the destination.
    /// Successful replacement invalidates borrowed world references and old clients. Continuation
    /// cleanup is release-only: nonfatal disposal errors cannot undo an installed checkpoint.</summary>
    public void RestoreCheckpoint(ReadOnlySpan<byte> bytes, CheckpointProfile profile, string contentHash, ulong replayLimit = RandomCheckpointCodec.DefaultReplayLimit)
    {
        CheckAlive(); ValidateProfile(profile, contentHash); var previous = state;
        CheckpointRequire(!previous.Advancing && previous.Activities == 0, "Cannot restore during a command step or owner workflow");
        CheckpointRequire(previous.Ready.Count == 0 && previous.Requests.Count == 0 && previous.Replies.Count == 0 && previous.Received.Count == 0, "Cannot replace queued destination work");
        CheckpointRequire(previous.Commands.Values.All(r => r.Executor is null && r.ReplyTo is null && r.Actor.Source is null), "Cannot replace remote destination work");
        CheckpointRequire(previous.Participants.Values.All(p => p.Connection is null), "Cannot replace connected destination participants");
        previous.Checkpointing = true; RuntimeState? prepared = null; IContinuation? continuation = null;
        try
        {
            var checkpoint = LocalCheckpointCodec.Decode(bytes, replayLimit);
            CheckpointRequire(checkpoint.Profile == profile.Id && checkpoint.ContentHash == contentHash, "Checkpoint profile/content mismatch");
            // No staged map shares the destination fallback. Process replacement transfers that binding
            // together with the staged world, so even a failing decoder cannot mutate the old fallback.
            var process = new ProcessContext(); var world = new GameState(process);
            prepared = new(previous.Process, previous.Identity) { World = world, Session = checkpoint.Session, Active = true, Checkpointing = true };
            foreach (var (name, definition) in previous.Definitions) prepared.Definitions.Add(name, definition);
            prepared.Participants.Add(checkpoint.Participant.Value, new(checkpoint.Participant, new(1)));
            var counters = checkpoint.Counters; prepared.NextSession = counters.Session; prepared.NextParticipant = counters.Participant;
            prepared.NextController = counters.Controller; prepared.NextCommand = counters.Command; prepared.NextInteraction = counters.Interaction; prepared.Revision = counters.Revision;
            prepared.Gameplay = new(0, checkpoint.Gameplay.Context); prepared.Gameplay.Restore(checkpoint.Gameplay, replayLimit);
            prepared.Presentation = new(0, checkpoint.Presentation.Context); prepared.Presentation.Restore(checkpoint.Presentation, replayLimit);
            profile.Decode(checkpoint.World.ToArray(), world, process);
            if (checkpoint.Command is { } saved)
            {
                CheckpointRequire(prepared.Definitions.TryGetValue(saved.Request.Name, out var definition), "Unknown checkpoint operation");
                var codec = OperationCodec(definition!);
                CheckpointRequire(saved.DescriptorVersion == definition!.Descriptor.Version && saved.Codec == codec.Id && saved.CodecVersion == codec.Version, "Unknown checkpoint operation codec/version");
                CheckpointRequire(CommandDescriptor.ValidateValues(definition.Descriptor.Arguments, saved.Request.Arguments) is null, "Invalid checkpoint arguments");
                CheckpointRequire(Permitted(saved.Access, definition.Descriptor.Access), "Checkpoint actor lacks descriptor grant");
                var actor = new CommandActor(checkpoint.Participant, new(1), saved.Controller, saved.Access);
                var record = new CommandRecord(actor, saved.Request, definition, saved.Update); record.Effects.AddRange(saved.Update.Committed);
                continuation = codec.Decode(saved.Locals.ToArray(), saved.Request, saved.Update);
                CheckpointRequire(continuation is not null, "Operation codec returned no continuation"); record.Continuation = continuation;
                prepared.Commands.Add(saved.Update.Correlation.Command.Value, record);
            }
            LocalBoundary(prepared);
            prepared.NextSession = Math.Max(prepared.NextSession, previous.NextSession); prepared.NextParticipant = Math.Max(prepared.NextParticipant, previous.NextParticipant);
            prepared.NextController = Math.Max(prepared.NextController, previous.NextController); prepared.NextCommand = Math.Max(prepared.NextCommand, previous.NextCommand); prepared.NextInteraction = Math.Max(prepared.NextInteraction, previous.NextInteraction);
            // Prepare traversal storage too: installation itself has no allocation or callbacks.
            var oldLeases = previous.Leases.Values.ToArray(); var oldCommands = previous.Commands.Values.ToArray();
            previous.Process.SwapContents(process);
            foreach (var lease in oldLeases) lease.Active = false;
            previous.Active = false; state = prepared; continuation = null;
            // Release-only callbacks run after installation, with reentry still blocked.
            foreach (var record in oldCommands)
            {
                var abandoned = record.Continuation; record.Continuation = null; ReleaseCheckpointContinuation(abandoned);
            }
        }
        finally
        {
            try { ReleaseCheckpointContinuation(continuation); }
            finally { previous.Checkpointing = false; if (prepared is not null) prepared.Checkpointing = false; }
        }
    }
    private static void ReleaseCheckpointContinuation(IContinuation? continuation)
    {
        try { continuation?.Dispose(); }
        catch (Exception error) when (error is not OutOfMemoryException) { /* Release-only owner contract; preserve primary failure or installed success. */ }
    }
}
