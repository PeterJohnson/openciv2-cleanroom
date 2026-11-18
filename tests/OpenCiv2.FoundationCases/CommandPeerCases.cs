using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void CommandPeerExecutionAndResults()
    {
        using var requester = new SessionRuntime(new(), new(1)); using var executor = new SessionRuntime(new(), new(2)); int executions = 0, applications = 0;
        requester.RegisterCommand(CommandDefinitionFor(_ => throw new Exception("Requester must not execute")) with
        {
            Admit = (_, _, _) => new(Route: CommandRoute.AwaitPeer, Executor: new(7)),
            ApplyResult = (_, result) => new TestContinuation((context, _) => { ++applications; context.World.Civilizations[1].Gold = ((CommandValue.Integer)result[0].Value).Value; return new(Result: result); }),
        });
        executor.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, _) => { ++executions; return CommandDone(context.Gameplay.PrimaryModulo("test.peer", 50)); })));
        requester.BeginClientSession(new(1, 2)); executor.BeginClientSession(new(3, 4)); var client = SessionClient(requester); var remoteSeat = executor.AddParticipant(new(1), new(9));
        var before = requester.GameplayRandom.Capture(); var submitted = client.Submit(CommandRequestFor()); var command = submitted.Correlation.Command;
        Equal(false, requester.AdvanceOne()); Equal(false, requester.ContinueCommand(command)); Equal("peer_cancellation_requires_mode_adapter", client.Cancel(command).Code); Equal(false, requester.CancelCommand(command).Accepted);
        Throws<InvalidOperationException>(() => requester.EndClientSession()); var request = requester.TakeRequests().Single(); Equal(new ConnectionId(7), request.Destination);
        var accepted = executor.ReceiveRequest(new(9), remoteSeat, request.Correlation, request.Request); Equal(accepted, executor.ReceiveRequest(new(9), remoteSeat, request.Correlation, request.Request)); executor.AdvanceOne(); Equal(1, executions);
        var reply = executor.TakeReplies().Single(); Equal(new ConnectionId(9), reply.Destination); Equal(false, requester.ReceiveResult(new(8), reply).Accepted);
        Equal(false, requester.ReceiveResult(new(7), reply with { Correlation = reply.Correlation with { Session = new(999) } }).Accepted);
        Equal("result_not_terminal", requester.ReceiveResult(new(7), reply with { Status = CommandStatus.Pending }).Code);
        Equal("missing_value:value", requester.ReceiveResult(new(7), reply with { Result = new([]) }).Code);
        Equal(true, requester.ReceiveResult(new(7), reply).Accepted); Equal(false, requester.ReceiveResult(new(7), reply).Accepted); Equal(0, applications);
        requester.AdvanceOne(); Equal(1, applications); Equal(reply.Result, RequiredResult(client, command).Result); Equal(before, requester.GameplayRandom.Capture());
        executor.ReceiveRequest(new(9), remoteSeat, request.Correlation, request.Request); var duplicate = executor.TakeReplies().Single(); Equal(reply, duplicate); Equal(1, executions); Equal(false, executor.AdvanceOne());
        Equal(false, executor.RetireCommand(accepted.Correlation.Command));
    }
    private static void CommandPeerAdmissionAndObligations()
    {
        using var runtime = new SessionRuntime(new(), new(1)); runtime.RegisterCommand(CommandDefinitionFor(_ => DecisionContinuation())); runtime.BeginClientSession(new());
        var seat = runtime.AddParticipant(new(1), new(7)); var origin = new CommandCorrelation(new(2), new(1), new(1));
        Equal("invalid_peer_correlation", runtime.ReceiveRequest(new(7), seat, origin with { Runtime = new(0) }, CommandRequestFor()).Code); Equal(0, runtime.TakeReplies().Count);
        Equal("peer_not_bound", runtime.ReceiveRequest(new(8), seat, origin, CommandRequestFor()).Code); Equal(1, runtime.TakeReplies().Count);
        var accepted = runtime.ReceiveRequest(new(7), seat, origin, CommandRequestFor()); runtime.AdvanceOne();
        Equal("peer_correlation_reused", runtime.ReceiveRequest(new(7), seat, origin, CommandRequestFor(99)).Code); Equal(1, runtime.TakeReplies().Count);
        var client = runtime.Bind(seat, ControllerKind.Scripted); var prompt = RequiredInteraction(client, accepted.Correlation.Command); runtime.Disconnect(new(7)); CheckRevoked(client, AccessFailure.Revoked);
        Throws<InvalidOperationException>(() => runtime.EndClientSession()); runtime.RemoveParticipant(seat); Equal(true, runtime.CancelCommand(accepted.Correlation.Command).Accepted); runtime.AdvanceOne();
        Equal(false, runtime.HasPendingCommands); Throws<InvalidOperationException>(() => runtime.EndClientSession()); var reply = runtime.TakeReplies().Single(); Equal(new ConnectionId(7), reply.Destination); Equal(origin, reply.Correlation); Equal((int)CommandStatus.Cancelled, (int)reply.Status);
        runtime.EndClientSession(); Equal(false, runtime.Active);
    }
    private static void CommandPeerFailureAndChangedBinding()
    {
        foreach (var status in new[] { CommandStatus.Rejected, CommandStatus.Failed, CommandStatus.Cancelled })
        {
            using var runtime = new SessionRuntime(new(), new(1)); runtime.RegisterCommand(CommandDefinitionFor(_ => throw new Exception()) with
            { Admit = (_, _, _) => new(Route: CommandRoute.AwaitPeer, Executor: new(7)), ApplyResult = (_, _) => throw new Exception() });
            runtime.BeginClientSession(new()); var client = SessionClient(runtime); var submitted = client.Submit(CommandRequestFor()); runtime.TakeRequests();
            Equal(true, runtime.ReceiveResult(new(7), new(new(9), submitted.Correlation, status, new([]), "remote_failure")).Accepted);
            var result = RequiredResult(client, submitted.Correlation.Command); Equal(status == CommandStatus.Cancelled ? (int)CommandStatus.Cancelled : (int)CommandStatus.Failed, (int)result.Status); Equal("remote_failure", result.Code); Equal(false, runtime.AdvanceOne());
        }
        using var changed = new SessionRuntime(new(), new(1)); ParticipantId seat = new(0);
        changed.RegisterCommand(CommandDefinitionFor(_ => throw new Exception()) with { Admit = (_, _, _) => { changed.ReassignParticipant(seat, new(2), new(7)); return new(); } });
        changed.BeginClientSession(new()); seat = changed.AddParticipant(new(1), new(7));
        Equal("peer_binding_changed", changed.ReceiveRequest(new(7), seat, new(new(2), new(1), new(1)), CommandRequestFor()).Code); Equal(1, changed.TakeReplies().Count); Equal(false, changed.AdvanceOne());
    }
    private static void CommandLinearEffectsAndRetirement()
    {
        using var runtime = new SessionRuntime(new(), new(1)); runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, _) =>
        { for (int i = 0; i < 1000; ++i) context.Effect("test.burst", CommandValues(i)); return CommandDone(1000); })));
        runtime.BeginClientSession(new()); var first = SessionClient(runtime); var second = runtime.Bind(first.Identity.Participant, ControllerKind.Automation); var outsider = SessionClient(runtime, 2);
        var command = first.Submit(CommandRequestFor()).Correlation.Command; Equal(false, runtime.RetireCommand(command)); first.Cancel(command); runtime.AdvanceOne();
        Equal(false, runtime.RetireCommand(command)); var retained = RequiredResult(first, command); Equal(1000, retained.Committed.Count);
        foreach (var client in new[] { first, second })
        {
            ulong count = 0; foreach (var update in client.Updates()) foreach (var effect in update.Committed) Equal(++count, effect.Index);
            Equal(1000UL, count); Equal(0, client.Updates().Count);
        }
        Equal(0, outsider.Updates().Count); Equal(true, runtime.RetireCommand(command)); Equal(true, first.Result(command) is null); Equal(false, runtime.AdvanceOne()); Equal(1000, retained.Committed.Count); Equal(false, runtime.RetireCommand(command));
    }
    private static void CommandPendingEffectRebind()
    {
        using var runtime = new SessionRuntime(new(), new(1)); runtime.RegisterCommand(CommandDefinitionFor(_ => DecisionContinuation())); runtime.BeginClientSession(new()); var first = SessionClient(runtime);
        var command = first.Submit(CommandRequestFor()).Correlation.Command; runtime.AdvanceOne(); var participant = first.Identity.Participant; runtime.Revoke(first.Identity.Controller);
        var next = runtime.Bind(participant, ControllerKind.Automation); var updates = next.Updates(); Equal(1, updates.Count); Equal(1, updates[0].Committed.Count); Equal(1UL, updates[0].Committed[0].Index);
        next.Reply(RequiredInteraction(next, command), 2); runtime.AdvanceOne(); var suffix = next.Updates().SelectMany(u => u.Committed).ToArray(); Equal(1, suffix.Length); Equal(2UL, suffix[0].Index); Equal(2, RequiredResult(next, command).Committed.Count);
    }
    private static void CommandCleanupAndAllocationFailures()
    {
        foreach (bool failure in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(1)); int closes = 0;
            runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, _) => { context.Effect("before", CommandValues(1)); if (failure) throw new IOException("first"); return CommandDone(1); }, () => { ++closes; throw new IOException("cleanup"); })));
            runtime.BeginClientSession(new()); var client = SessionClient(runtime); var command = client.Submit(CommandRequestFor()).Correlation.Command; runtime.AdvanceOne();
            Equal(failure ? "operation_exception" : "continuation_cleanup_exception", RequiredResult(client, command).Code); Equal(1, closes); runtime.Dispose(); Equal(1, closes);
        }
        var owner = new SessionRuntime(new(), new(1)); int released = 0;
        owner.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((_, _) => new(CommandStatus.Pending), () => { ++released; throw new IOException("close"); })));
        owner.BeginClientSession(new()); var bound = SessionClient(owner); bound.Submit(CommandRequestFor()); bound.Submit(CommandRequestFor()); owner.AdvanceOne(); owner.AdvanceOne();
        Throws<IOException>(owner.Dispose); Equal(2, released); owner.Dispose(); Equal(2, released); CheckRevoked(bound, AccessFailure.SessionEnded);
        using var exhausted = new SessionRuntime(new(), new(2)); exhausted.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((_, _) => throw new OutOfMemoryException())));
        exhausted.BeginClientSession(new()); var pending = SessionClient(exhausted).Submit(CommandRequestFor()); Throws<OutOfMemoryException>(() => exhausted.AdvanceOne()); Equal(true, exhausted.HasPendingCommands); Equal(true, exhausted.ContinueCommand(pending.Correlation.Command));
    }
    private static void CommandControlledChoiceFailure()
    {
        using var runtime = new SessionRuntime(new(), new(1)); runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, _) =>
        { context.World.Civilizations[1].Gold = 13; context.Effect("before.choice", CommandValues(13)); return CommandDone(context.Gameplay.PrimaryModulo(new ChoiceKey("missing", "operation", 0), 3)); })));
        runtime.BeginClientSession(new()); runtime.GameplayRandom.Restore(new GameplayRandom(new ControlledChoices(new([])), "session").Capture(), 0);
        var client = SessionClient(runtime); var command = client.Submit(CommandRequestFor()).Correlation.Command; runtime.AdvanceOne();
        Equal("random_choice_missing", RequiredResult(client, command).Code); Equal(1, RequiredResult(client, command).Committed.Count); Equal(13, runtime.World.Civilizations[1].Gold); Equal(0UL, runtime.GameplayRandom.Draws);
    }
    private static void CommandRetainedChoicesAcrossInteraction()
    {
        var tape = new ControlledChoices(new([
            Choice.Create(new("candidate", "operation", 0), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(6)),
            Choice.Create(new("candidate", "operation", 1), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(8)),
            Choice.Create(new("reward", "operation", 0), new SamplingLaw.SecondaryRange(1, 14), ChoiceCode.Secondary(uint.MaxValue)),
        ]));
        var checkpoint = new GameplayRandom(tape, "session").Capture();
        IContinuation Factory(CommandRequest request)
        {
            int phase = 0, selected = -1;
            return new TestContinuation((context, input) =>
            {
                if (phase++ == 0)
                {
                    for (int attempt = 0; attempt < 3; ++attempt) { selected = context.Gameplay.PrimaryModulo(new ChoiceKey("candidate", "operation", attempt), 3); if (selected != 0) break; }
                    Equal(2, selected); context.World.Civilizations[1].Gold -= selected; context.Effect("selected", CommandValues(selected));
                    return new(CommandStatus.AwaitingInteraction, Interaction: new("Resolve", null, null, new([1, 2])));
                }
                context.World.Civilizations[1].Gold += selected;
                return CommandDone(context.Gameplay.SecondaryRange(new ChoiceKey("reward", "operation", 0), 1, context.World.Civilizations[1].Gold));
            });
        }
        using var first = new SessionRuntime(new(), new(1)); using var second = new SessionRuntime(new(), new(2));
        foreach (var runtime in new[] { first, second }) { runtime.RegisterCommand(CommandDefinitionFor(Factory)); runtime.BeginClientSession(new()); runtime.GameplayRandom.Restore(checkpoint, 0); runtime.World.Civilizations[1].Gold = 10; }
        var a = SessionClient(first); var b = SessionClient(second); var ca = a.Submit(CommandRequestFor()).Correlation.Command; var cb = b.Submit(CommandRequestFor()).Correlation.Command;
        first.AdvanceOne(); second.AdvanceOne(); Equal(8, first.World.Civilizations[1].Gold); Equal(2, first.GameplayRandom.Capture().Trace!.Events.Count);
        var suspended = second.GameplayRandom.Capture(); second.GameplayRandom.Restore(suspended, 0);
        for (int i = 0; i < 50; ++i) { second.PresentationRandom.PrimaryModulo("report", 7); b.OwnUnits(); }
        Equal(suspended, second.GameplayRandom.Capture()); first.World.Civilizations[1].Gold = second.World.Civilizations[1].Gold = 12;
        a.Reply(RequiredInteraction(a, ca), 1); b.Reply(RequiredInteraction(b, cb), 1); first.AdvanceOne(); second.AdvanceOne();
        Equal(CommandValues(14), RequiredResult(a, ca).Result); Equal(RequiredResult(a, ca).Result, RequiredResult(b, cb).Result); Equal(first.GameplayRandom.Capture(), second.GameplayRandom.Capture());
        first.GameplayRandom.RequireChoicesConsumed(); second.GameplayRandom.RequireChoicesConsumed();
    }
    private static void CommandExplicitContinuation()
    {
        using var runtime = new SessionRuntime(new(), new(1)); int steps = 0;
        runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((_, input) =>
        {
            ++steps;
            if (steps == 1) { Equal((int)OperationInputKind.Begin, (int)input.Kind); return new(CommandStatus.Pending); }
            Equal((int)OperationInputKind.Proceed, (int)input.Kind); Equal(true, input.Source is null); return CommandDone(steps);
        })));
        runtime.BeginClientSession(new()); var client = SessionClient(runtime); var command = client.Submit(CommandRequestFor()).Correlation.Command;
        runtime.AdvanceOne(); Equal(false, runtime.AdvanceOne()); Equal(true, runtime.HasPendingCommands); Equal(true, runtime.ContinueCommand(command)); Equal(false, runtime.ContinueCommand(command));
        runtime.AdvanceOne(); Equal(2, steps); Equal(CommandValues(2), RequiredResult(client, command).Result); Equal(false, runtime.ContinueCommand(command));
    }
    private static void CommandExecutionRevalidation()
    {
        using var runtime = new SessionRuntime(new(), new(1));
        runtime.RegisterCommand(CommandDefinitionFor(request => new TestContinuation((context, _) =>
        {
            var target = ((CommandValue.Unit)CommandDescriptor.Value(request.Arguments, "target")).Value;
            if (!context.World.Entities.Units.Live(target.Slot) || context.World.Entities.Units.At(target.Slot).Token != target.Token) return new(CommandStatus.Failed, Code: "stale_target");
            if (context.World.Entities.Units.At(target.Slot).Owner != context.Actor.Civilization) return new(CommandStatus.Failed, Code: "owner_changed");
            return CommandDone(1);
        })));
        runtime.BeginClientSession(new()); var client = SessionClient(runtime); runtime.World.Entities.Units.SetEnd(1);
        runtime.World.Entities.Units.Storage[0] = new() { Token = new(1), Owner = new(1) };
        var request = new CommandRequest("test.action", new([new("amount", new CommandValue.Integer(1)), new("target", new CommandValue.Unit(new(new(0), new(1))))]));
        var first = client.Submit(request).Correlation.Command; runtime.World.Entities.Units.Storage[0].Token = new(2); runtime.AdvanceOne(); Equal("stale_target", RequiredResult(client, first).Code);
        runtime.World.Entities.Units.Storage[0].Token = new(1); var second = client.Submit(request).Correlation.Command; runtime.World.Entities.Units.Storage[0].Owner = new(2); runtime.AdvanceOne(); Equal("owner_changed", RequiredResult(client, second).Code);
    }
}
