using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;

namespace OpenCiv2.Session;

public sealed partial class SessionRuntime
{
    private static bool Permitted(AccessClass grant, AccessClass needed) =>
        needed == AccessClass.Player || grant == AccessClass.Developer;

    /// <summary>
    /// Checks whether a binding may observe the initiating participant's command and effects.
    /// </summary>
    private static bool Owner(Lease lease, CommandRecord record) =>
        lease.Identity.Participant == record.Actor.Participant
        && lease.Identity.Observer == record.Actor.Civilization
        && Permitted(lease.Identity.Access, record.Definition.Descriptor.Access);

    private static bool Recipient(Lease lease, Interaction interaction) =>
        lease.Identity.Participant == interaction.Recipient && lease.Identity.Observer == interaction.Observer;

    /// <summary>
    /// Allows an interaction recipient to answer without granting access to another participant's effects.
    /// </summary>
    private static bool DecisionAccess(Lease lease, CommandRecord record) =>
        record.Update.Interaction is { } interaction
        && Recipient(lease, interaction)
        && (interaction.Recipient != record.Actor.Participant
            || Permitted(lease.Identity.Access, record.Definition.Descriptor.Access));

    private static CommandUpdate Snapshot(CommandRecord record) =>
        record.Update with { Committed = new(record.Effects) };

    private static void PublishCityProduction(RuntimeState state, CommandRecord record, CityTarget city, int item)
    {
        if (!state.LocalPlay || !state.World.Entities.Cities.Live(city.Slot))
        {
            return;
        }
        ref readonly var current = ref state.World.Entities.Cities.At(city.Slot);
        if (current.Token != city.Token || current.Owner != record.Actor.Civilization)
        {
            return;
        }
        var notification = new CityProductionEvent(record.Update.Correlation, state.Revision, city, item);
        foreach (var lease in state.Leases.Values)
        {
            if (Owner(lease, record))
            {
                lease.EnqueueProduction(notification);
            }
        }
    }

    /// <summary>
    /// Builds a binding-specific update, hiding private interactions and effects from unrelated observers.
    /// </summary>
    private static CommandUpdate? Visible(Lease lease, CommandRecord record, int firstEffect = 0)
    {
        if (Owner(lease, record))
        {
            return record.Update with
            {
                Committed = new(record.Effects.Skip(firstEffect)),
                Interaction = record.Update.Interaction is { } interaction && Recipient(lease, interaction)
                    ? interaction
                    : null,
            };
        }

        if (DecisionAccess(lease, record))
        {
            return EmptyUpdate(
                record.Update.Correlation,
                CommandStatus.AwaitingInteraction) with
            {
                Interaction = record.Update.Interaction
            };
        }

        return null;
    }

    private static CommandUpdate EmptyUpdate(
        CommandCorrelation id,
        CommandStatus status = CommandStatus.Rejected,
        string code = "") =>
        new(id, status, code, OwnedList<NamedValue>.Empty, null, OwnedList<Effect>.Empty);

    /// <summary>
    /// Queues visible updates and advances each owner binding's effect cursor after delivery.
    /// </summary>
    private static void Deliver(RuntimeState state, CommandRecord record)
    {
        var command = record.Update.Correlation.Command;
        foreach (var lease in state.Leases.Values)
        {
            var first = lease.DeliveredEffects.GetValueOrDefault(command);
            if (Visible(lease, record, first) is not { } update)
            {
                continue;
            }

            if (Owner(lease, record))
            {
                // A controller receives each committed effect once, even across repeated status updates.
                lease.DeliveredEffects.TryAdd(command, first);
                lease.Updates.Enqueue(update);
                lease.DeliveredEffects[command] = record.Effects.Count;
            }
            else
            {
                lease.Updates.Enqueue(update);
            }
        }
    }

    /// <summary>
    /// Seeds a newly bound controller with visible pending state and the effects already committed.
    /// </summary>
    private static void RestorePendingUpdates(RuntimeState state, Lease lease)
    {
        foreach (var record in state.Commands.Values)
        {
            if (!record.Update.Status.Terminal() && Visible(lease, record) is { } update)
            {
                if (Owner(lease, record))
                {
                    lease.DeliveredEffects[record.Update.Correlation.Command] = record.Effects.Count;
                }

                lease.Updates.Enqueue(update);
            }
        }
    }

    /// <summary>
    /// Withdraws a decision from recipient-only bindings before clearing the command's interaction.
    /// </summary>
    private static void ClearInteraction(RuntimeState state, CommandRecord record)
    {
        if (record.Update.Interaction is null)
        {
            return;
        }

        foreach (var lease in state.Leases.Values)
        {
            if (DecisionAccess(lease, record) && !Owner(lease, record))
            {
                lease.Updates.Enqueue(EmptyUpdate(record.Update.Correlation, record.Update.Status));
            }
        }

        record.Update = record.Update with { Interaction = null };
    }

    private static CommandCorrelation Correlation(RuntimeState state) =>
        new(state.Identity, state.Session, new(RuntimeState.Assign(ref state.NextCommand)));

    /// <summary>
    /// Makes a command terminal, releases its continuation once, queues any required reply and publishes the
    /// update.
    /// </summary>
    /// <remarks>
    /// Previously committed effects survive. A cleanup exception changes successful completion to failure but
    /// does not replace an existing failure or cancellation.
    /// </remarks>
    private static void Finish(
        RuntimeState state,
        CommandRecord record,
        CommandStatus status,
        OwnedList<NamedValue> result,
        string code)
    {
        record.Update = record.Update with { Status = status, Result = result, Code = code };
        ClearInteraction(state, record);
        var continuation = record.Continuation;
        record.Continuation = null;
        try
        {
            continuation?.Dispose();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Release once and preserve an earlier failure/cancellation and required peer reply.
            if (status == CommandStatus.Completed)
            {
                record.Update = record.Update with
                {
                    Status = CommandStatus.Failed,
                    Result = OwnedList<NamedValue>.Empty,
                    Code = "continuation_cleanup_exception"
                };
            }
        }
        if (record.ReplyTo is { } reply)
        {
            state.Replies.Add(
                new(reply.Source, reply.Correlation, record.Update.Status, record.Update.Result, record.Update.Code));
            record.ReplyTo = null;
        }
        Deliver(state, record);
    }

    /// <summary>
    /// Validates descriptor values and authority, rechecks the binding after admission, then queues execution
    /// or peer routing.
    /// </summary>
    private static CommandUpdate Submit(
        RuntimeState state,
        CommandActor actor,
        CommandRequest request,
        CommandCorrelation id,
        Lease? lease,
        (ConnectionId, CommandCorrelation)? replyTo = null)
    {
        if (!state.Definitions.TryGetValue(request.Name, out var definition)
            || !Permitted(actor.Access, definition.Descriptor.Access))
        {
            return EmptyUpdate(id, code: "command_not_available");
        }

        if (CommandDescriptor.ValidateValues(definition.Descriptor.Arguments, request.Arguments) is { } error)
        {
            return EmptyUpdate(id, code: error);
        }

        if (state.LocalPlay && state.Pending)
        {
            return EmptyUpdate(id, code: "decision_or_command_pending");
        }

        Admission admission;
        try
        {
            admission = definition.Admit(actor, request, state.World);
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch
        {
            return EmptyUpdate(id, code: "admission_exception");
        }
        // Admission is a trusted callback, but it may still revoke or replace the caller's binding.
        if (!state.Active || state.Disposed || state.Session != id.Session || (lease is not null && !lease.Active))
        {
            return EmptyUpdate(id, code: "binding_changed_during_admission");
        }

        if (actor.Source is { } source
            && (!state.Participants.TryGetValue(actor.Participant.Value, out var seat) || seat.Connection != source
                || seat.Civilization != actor.Civilization))
        {
            return EmptyUpdate(id, code: "peer_binding_changed");
        }

        if (admission is null)
        {
            return EmptyUpdate(id, code: "admission_exception");
        }

        if (admission.Rejection is { } rejection)
        {
            return EmptyUpdate(id, code: rejection);
        }

        if (admission.Route is not (CommandRoute.Execute or CommandRoute.AwaitPeer))
        {
            return EmptyUpdate(id, code: "invalid_authority_route");
        }

        if (admission.Route == CommandRoute.AwaitPeer
            && (admission.Executor is null || definition.ApplyResult is null || actor.Source is not null))
        {
            return EmptyUpdate(id, code: "unsupported_authority_route");
        }

        var record = new CommandRecord(actor, request, definition, EmptyUpdate(id, CommandStatus.Pending))
        { ReplyTo = replyTo, Executor = admission.Route == CommandRoute.AwaitPeer ? admission.Executor : null };
        state.Commands.Add(id.Command.Value, record);
        if (record.Executor is { } executor)
        {
            state.Requests.Add(new(executor, id, request));
        }
        else
        {
            state.Ready.Enqueue(new(id.Command, new()));
        }

        Deliver(state, record);
        return Snapshot(record);
    }

    private static bool InteractionValid(InteractionSpec spec) =>
        !string.IsNullOrEmpty(spec.Prompt) && spec.Choices.Count != 0 &&
        spec.Choices.Distinct().Count() == spec.Choices.Count
            && (spec.DefaultChoice is null
                || (spec.Deadline is not null && spec.Choices.Contains(spec.DefaultChoice.Value)));

    /// <summary>
    /// Validates a continuation result and either finishes the command or publishes its next suspended state.
    /// </summary>
    private static void ApplyStep(RuntimeState state, CommandRecord record, OperationStep step)
    {
        if (step is null)
        {
            throw new InvalidOperationException("Missing operation step");
        }

        if (step.Status is CommandStatus.Completed or CommandStatus.Failed or CommandStatus.Cancelled)
        {
            var result = step.Result ?? OwnedList<NamedValue>.Empty;
            if (step.Status == CommandStatus.Completed
                && CommandDescriptor.ValidateValues(record.Definition.Descriptor.Result, result) is { } error)
            {
                Finish(
                    state,
                    record,
                    CommandStatus.Failed,
                    OwnedList<NamedValue>.Empty,
                    "invalid_operation_result:" + error);
                return;
            }
            Finish(state, record, step.Status, result, step.Code);
            return;
        }
        if (step.Status is not (CommandStatus.Pending or CommandStatus.AwaitingInteraction))
        {
            throw new InvalidOperationException("Invalid continuation lifecycle state");
        }

        record.Update = record.Update with { Status = step.Status, Code = step.Code };
        ClearInteraction(state, record);
        if (step.Status == CommandStatus.AwaitingInteraction)
        {
            if (step.Interaction is not { } spec || !InteractionValid(spec))
            {
                throw new InvalidOperationException("Invalid continuation interaction");
            }

            var participant = spec.Recipient ?? record.Actor.Participant;
            var observer = participant == record.Actor.Participant
                ? record.Actor.Civilization
                : state.Participants[participant.Value].Civilization;
            record.Update = record.Update with
            {
                Interaction = new(new(RuntimeState.Assign(ref state.NextInteraction)), participant, observer, spec)
            };
        }
        Deliver(state, record);
    }

    /// <summary>
    /// Registers a uniquely named, validated command before starting the client session.
    /// </summary>
    public void RegisterCommand(CommandDefinition definition)
    {
        CheckAlive();
        if (state.Active)
        {
            throw new InvalidOperationException("Register commands before starting the client session");
        }

        definition.Descriptor.Validate();
        if (definition.Admit is null || definition.Execute is null)
        {
            throw new ArgumentException("Command requires explicit authority admission and execution");
        }

        if (!state.Definitions.TryAdd(definition.Descriptor.Name, definition))
        {
            throw new ArgumentException("Duplicate command name");
        }
    }

    /// <summary>
    /// Consumes at most one ready input and resumes its continuation synchronously.
    /// </summary>
    /// <remarks>
    /// Committed effects publish immediately. Ordinary execution failures become terminal command failures;
    /// out-of-memory failures propagate. Recursive advancement is rejected.
    /// </remarks>
    /// <returns>False when the ready queue is empty; true when an entry was consumed, even if already
    /// terminal.</returns>
    public bool AdvanceOne()
    {
        state.RequireActive();
        if (state.Advancing)
        {
            throw new InvalidOperationException("Continuation execution cannot recursively advance the runtime");
        }

        if (!state.Ready.TryDequeue(out var work))
        {
            return false;
        }

        var record = state.Commands[work.Command.Value];
        void RecordAdvance() => state.Replay?.Record(new LocalReplayStep.Advance(work.Command,
            record.Update.Status, record.Update.Code, record.Update.Interaction?.Id, record.Effects.Count));
        if (record.Update.Status.Terminal())
        {
            RecordAdvance();
            return true;
        }

        state.Advancing = true;
        try
        {
            OperationStep step;
            try
            {
                record.Continuation ??= record.ResultApplication
                    ? record.Definition.ApplyResult!(record.Request, record.Resolved)
                    : record.Definition.Execute(record.Request);
                if (record.Continuation is null)
                {
                    throw new InvalidOperationException("Missing continuation");
                }

                var context = new OperationContext(
                    state.World,
                    state.Process,
                    state.Gameplay!,
                    state.Presentation!,
                    record.Actor,
                    record.Update.Correlation,
                    (name, values) =>
                    {
                        record.Effects.Add(new((ulong)record.Effects.Count + 1, name, values));
                        state.Publish();
                        Deliver(state, record);
                    },
                    state.Publish,
                    (city, item) => PublishCityProduction(state, record, city, item));
                step = record.Continuation.Resume(context, work.Input);
            }
            catch (ChoiceException error)
            {
                Finish(state, record, CommandStatus.Failed, OwnedList<NamedValue>.Empty, "random_choice_" + error.Code);
                return true;
            }
            catch (OutOfMemoryException)
            {
                throw;
            }
            catch
            {
                Finish(state, record, CommandStatus.Failed, OwnedList<NamedValue>.Empty, "operation_exception");
                return true;
            }
            try
            {
                ApplyStep(state, record, step);
            }
            catch (Exception error) when (error is InvalidOperationException or KeyNotFoundException
                or ArgumentException)
            {
                Finish(state, record, CommandStatus.Failed, OwnedList<NamedValue>.Empty, "invalid_operation_step");
            }
            return true;
        }
        finally
        {
            state.Advancing = false;
            RecordAdvance();
        }
    }

    /// <summary>
    /// Queues a proceed input for a locally suspended command with no pending interaction or queued input.
    /// </summary>
    public bool ContinueCommand(CommandId command)
    {
        state.RequireActive();
        if (!state.Commands.TryGetValue(command.Value, out var record) || record.Update.Status.Terminal()
            || record.Update.Interaction is not null
            || record.Executor is not null
            || state.Ready.Any(w => w.Command == command))
        {
            return false;
        }

        state.Ready.Enqueue(new(command, new(OperationInputKind.Proceed)));
        return true;
    }

    /// <summary>
    /// Queues owner-requested cancellation for a local command; cancellation takes effect when it resumes.
    /// </summary>
    public InputReceipt CancelCommand(CommandId command)
    {
        state.RequireActive();
        if (!state.Commands.TryGetValue(command.Value, out var record) || record.Update.Status.Terminal())
        {
            return new(false, "command_not_available");
        }

        if (record.Executor is not null)
        {
            return new(false, "peer_cancellation_requires_mode_adapter");
        }

        return QueueInput(state, record, new(OperationInputKind.Cancel));
    }

    /// <summary>
    /// Queues one input and withdraws the old interaction before publishing the pending state.
    /// </summary>
    private static InputReceipt QueueInput(RuntimeState state, CommandRecord record, OperationInput input)
    {
        state.Ready.Enqueue(new(record.Update.Correlation.Command, input));
        record.Update = record.Update with { Status = CommandStatus.Pending };
        ClearInteraction(state, record);
        Deliver(state, record);
        return new(true, "queued");
    }

    /// <summary>
    /// Queues timeout inputs for interactions whose explicit deadlines have passed.
    /// </summary>
    /// <remarks>
    /// The caller supplies the clock value. Each timeout carries the interaction's optional default choice.
    /// </remarks>
    /// <returns>The number of interactions moved back to the pending state.</returns>
    public int ExpireInteractions(ulong now)
    {
        state.RequireActive();
        int count = 0;
        foreach (var record in state.Commands.Values)
        {
            if (record.Update.Interaction is { Spec.Deadline: { } deadline } interaction && now >= deadline)
            {
                QueueInput(state, record, new(OperationInputKind.Timeout, interaction.Spec.DefaultChoice));
                ++count;
            }
        }

        return count;
    }

    /// <summary>
    /// Removes terminal bookkeeping only after deduplication, routing and client-update references permit it.
    /// </summary>
    public bool RetireCommand(CommandId command)
    {
        state.RequireActive();
        if (state.Advancing)
        {
            throw new InvalidOperationException("Retire commands only between operation steps");
        }

        if (!state.Commands.TryGetValue(command.Value, out var record) || !record.Update.Status.Terminal())
        {
            return false;
        }

        if (state.Received.Values.Contains(command)
            || state.Requests.Any(r => r.Correlation == record.Update.Correlation)
            || record.ReplyTo is not null
            ||
            state.Leases.Values.Any(l => l.Updates.Any(u => u.Correlation.Command == command)))
        {
            return false;
        }

        var retained = state.Ready.Where(w => w.Command != command).ToArray();
        state.Ready.Clear();
        foreach (var work in retained)
        {
            state.Ready.Enqueue(work);
        }

        foreach (var lease in state.Leases.Values)
        {
            lease.DeliveredEffects.Remove(command);
        }

        state.Commands.Remove(command.Value);
        state.Replay?.Record(new LocalReplayStep.Retire(command));
        return true;
    }

    /// <summary>
    /// Admits a request from a bound peer, using its connection and correlation to detect retransmission.
    /// </summary>
    /// <remarks>
    /// A matching retransmission reuses the retained result and may resend its terminal reply; it does not
    /// execute the command again.
    /// </remarks>
    public CommandUpdate ReceiveRequest(
        ConnectionId source,
        ParticipantId participant,
        CommandCorrelation origin,
        CommandRequest request)
    {
        state.RequireActive();
        var key = (source, origin);
        CommandUpdate update;
        if (origin.Runtime.Value == 0 || origin.Session.Value == 0 || origin.Command.Value == 0)
        {
            return EmptyUpdate(Correlation(state), code: "invalid_peer_correlation");
        }

        if (!state.Participants.TryGetValue(participant.Value, out var seat) || seat.Connection != source)
        {
            update = EmptyUpdate(Correlation(state), code: "peer_not_bound");
        }
        else if (state.Received.TryGetValue(key, out var old))
        {
            var record = state.Commands[old.Value];
            if (record.Actor.Participant != participant || record.Actor.Civilization != seat.Civilization
                || record.Request != request)
            {
                update = EmptyUpdate(Correlation(state), code: "peer_correlation_reused");
            }
            else
            {
                if (record.Update.Status.Terminal())
                {
                    state.Replies.Add(
                        new(source, origin, record.Update.Status, record.Update.Result, record.Update.Code));
                }

                return Snapshot(record);
            }
        }
        else
        {
            update = Submit(
                state,
                new(participant, seat.Civilization, Source: source),
                request,
                Correlation(state),
                null,
                key);
        }

        if (update.Status == CommandStatus.Rejected)
        {
            state.Replies.Add(new(source, origin, CommandStatus.Rejected, OwnedList<NamedValue>.Empty, update.Code));
        }
        else
        {
            state.Received.TryAdd(key, update.Correlation.Command);
        }

        return update;
    }

    /// <summary>
    /// Accepts a terminal result only from the expected executor and queues local result application on
    /// success.
    /// </summary>
    public InputReceipt ReceiveResult(ConnectionId source, RoutedReply reply)
    {
        state.RequireActive();
        if (!state.Commands.TryGetValue(reply.Correlation.Command.Value, out var record)
            || record.Update.Correlation != reply.Correlation
            || record.Executor != source)
        {
            return new(false, "unexpected_result");
        }

        if (!reply.Status.Terminal())
        {
            return new(false, "result_not_terminal");
        }

        if (reply.Status == CommandStatus.Completed)
        {
            if (CommandDescriptor.ValidateValues(record.Definition.Descriptor.Result, reply.Result) is { } error)
            {
                return new(false, error);
            }

            record.Resolved = reply.Result;
            record.ResultApplication = true;
            record.Executor = null;
            state.Ready.Enqueue(new(reply.Correlation.Command, new()));
        }
        else
        {
            record.Executor = null;
            Finish(
                state,
                record,
                reply.Status == CommandStatus.Cancelled ? CommandStatus.Cancelled : CommandStatus.Failed,
                OwnedList<NamedValue>.Empty,
                reply.Code);
        }
        return new(true, "accepted");
    }

    /// <summary>
    /// Transfers all queued peer requests to the caller as an owned snapshot and clears the outbox.
    /// </summary>
    public OwnedList<RoutedRequest> TakeRequests()
    {
        CheckAlive();
        var result = new OwnedList<RoutedRequest>(state.Requests);
        state.Requests.Clear();
        return result;
    }

    /// <summary>
    /// Transfers all queued peer replies to the caller as an owned snapshot and clears the outbox.
    /// </summary>
    public OwnedList<RoutedReply> TakeReplies()
    {
        CheckAlive();
        var result = new OwnedList<RoutedReply>(state.Replies);
        state.Replies.Clear();
        return result;
    }

    private sealed partial class PlayerClient
    {
        public OwnedList<CommandDescriptor> Commands()
        {
            var (current, grant) = Checked();
            return new(
                current.Definitions.Values.Where(d => Permitted(grant.Identity.Access, d.Descriptor.Access))
                    .Select(
                        d => d.Descriptor));
        }

        private static CommandActor Actor(Lease lease) =>
            new(lease.Identity.Participant, lease.Identity.Observer, lease.Identity.Controller, lease.Identity.Access);

        public CommandUpdate Submit(CommandRequest request)
        {
            var (current, grant) = Checked();
            var result = SessionRuntime.Submit(current, Actor(grant), request, Correlation(current), grant);
            current.Replay?.Record(new LocalReplayStep.Submit(grant.Identity.Kind, request, result.Correlation.Command,
                result.Status, result.Code));
            return result;
        }

        public OwnedList<CommandUpdate> Updates()
        {
            var (_, grant) = Checked();
            var updates = new OwnedList<CommandUpdate>(grant.Updates);
            grant.Updates.Clear();
            return updates;
        }

        public CommandUpdate? Result(CommandId command)
        {
            var (current, grant) = Checked();
            return current.Commands.TryGetValue(command.Value, out var record) ? Visible(grant, record) : null;
        }

        public InputReceipt Reply(InteractionId interaction, int choice)
        {
            var (current, grant) = Checked();
            var result = ReplyCore(current, grant, interaction, choice);
            current.Replay?.Record(new LocalReplayStep.Reply(grant.Identity.Kind, interaction, choice, result));
            return result;
        }

        private static InputReceipt ReplyCore(RuntimeState current, Lease grant, InteractionId interaction, int choice)
        {
            foreach (var record in current.Commands.Values)
            {
                if (record.Update.Interaction is { } pending && pending.Id == interaction
                    && DecisionAccess(grant, record))
                {
                    if (!pending.Spec.Choices.Contains(choice))
                    {
                        return new(false, "choice_not_allowed");
                    }

                    return QueueInput(current, record, new(OperationInputKind.Choice, choice, Actor(grant)));
                }
            }

            return new(false, "interaction_not_available");
        }

        public InputReceipt Cancel(CommandId command)
        {
            var (current, grant) = Checked();
            var result = CancelCore(current, grant, command);
            current.Replay?.Record(new LocalReplayStep.Cancel(grant.Identity.Kind, command, result));
            return result;
        }

        private static InputReceipt CancelCore(RuntimeState current, Lease grant, CommandId command)
        {
            if (!current.Commands.TryGetValue(command.Value, out var record) || record.Update.Status.Terminal()
                || (!Owner(grant, record) && !DecisionAccess(grant, record)))
            {
                return new(false, "command_not_available");
            }

            if (record.Executor is not null)
            {
                return new(false, "peer_cancellation_requires_mode_adapter");
            }

            if (record.Update.Interaction is { Spec.Cancellable: false })
            {
                return new(false, "not_cancellable");
            }

            if (current.LocalPlay && record.Update.Interaction is null)
            {
                if (record.Continuation is not null)
                {
                    return new(false, "input_already_queued");
                }

                // Local admission owns the only ready entry. Replace the unexecuted Begin input.
                if (current.Ready.TryPeek(out var work) && work.Command == command)
                {
                    current.Ready.Dequeue();
                }
            }

            return QueueInput(current, record, new(OperationInputKind.Cancel, Source: Actor(grant)));
        }
    }
}
