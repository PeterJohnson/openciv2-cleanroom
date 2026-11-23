using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // M1 command contract: explicit cooperative readiness, bounded advancement and FIFO inputs.
    private static void CommandCooperativeFairness()
    {
        using var runtime = new SessionRuntime(new(), new(261));
        List<int> order = []; int factories = 0, disposed = 0;
        runtime.RegisterCommand(CommandDefinitionFor(request =>
        {
            ++factories; int phase = 0;
            int amount = ((CommandValue.Integer)CommandDescriptor.Value(request.Arguments, "amount")).Value;
            return new TestContinuation((context, input) =>
            {
                Equal(phase == 0 ? OperationInputKind.Begin : OperationInputKind.Proceed, input.Kind);
                Equal(null, input.Source); order.Add(amount); context.World.Civilizations[1].Gold += amount;
                context.Effect("test.slice", CommandValues(amount));
                return ++phase < 3 ? new(CommandStatus.Pending) { ReadyToContinue = true } : CommandDone(amount);
            }, () => ++disposed);
        }));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime);
        var first = actor.Submit(CommandRequestFor(1)).Correlation.Command;
        var second = actor.Submit(CommandRequestFor(2)).Correlation.Command;
        for (int i = 0; i < 6; ++i)
        {
            Equal(true, runtime.AdvanceOne()); Equal(i + 1, order.Count);
            Equal(false, runtime.ContinueCommand(first)); Equal(false, runtime.ContinueCommand(second));
        }
        Sequence(new[] { 1, 2, 1, 2, 1, 2 }, order); Equal(9, runtime.World.Civilizations[1].Gold);
        Equal(2, factories); Equal(2, disposed); Equal(false, runtime.AdvanceOne()); Equal(false, runtime.HasPendingCommands);
        Equal(3, RequiredResult(actor, first).Committed.Count); Equal(3, RequiredResult(actor, second).Committed.Count);
        Equal(CommandValues(1), RequiredResult(actor, first).Result); Equal(CommandValues(2), RequiredResult(actor, second).Result);
    }
    private static void CommandCooperativeInteractionsAndCancellation()
    {
        using (var runtime = new SessionRuntime(new(), new(262)))
        {
            ParticipantId recipient = default; int phase = 0;
            runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, input) =>
            {
                switch (phase++)
                {
                    case 0: return new(CommandStatus.Pending) { ReadyToContinue = true };
                    case 1: return new(CommandStatus.AwaitingInteraction, Interaction: new("Private decision", recipient, null, new([7])));
                    case 2:
                        Equal(OperationInputKind.Choice, input.Kind); Equal(7, input.Choice); Equal(recipient, input.Source!.Participant);
                        context.Effect("test.accepted", CommandValues(7)); return new(CommandStatus.Pending) { ReadyToContinue = true };
                    default: Equal(OperationInputKind.Proceed, input.Kind); return CommandDone(7);
                }
            })));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var other = SessionClient(runtime, 2); recipient = other.Identity.Participant;
            var command = actor.Submit(CommandRequestFor()).Correlation.Command;
            Equal(true, runtime.AdvanceOne()); Equal(true, runtime.AdvanceOne()); Equal(false, runtime.AdvanceOne()); Equal(2, phase);
            var prompt = RequiredInteraction(other, command); Equal(null, RequiredResult(actor, command).Interaction);
            Equal(false, actor.Reply(prompt, 7).Accepted); Equal(true, other.Reply(prompt, 7).Accepted);
            Equal(true, runtime.AdvanceOne()); Equal(CommandStatus.Pending, RequiredResult(actor, command).Status);
            Equal(true, runtime.AdvanceOne()); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(false, runtime.AdvanceOne());
        }
        foreach (bool queuedInsideStep in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(263)); List<OperationInputKind> inputs = []; int disposed = 0;
            runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, input) =>
            {
                inputs.Add(input.Kind);
                if (input.Kind == OperationInputKind.Cancel) return new(CommandStatus.Cancelled);
                context.Effect("test.before_cancel", CommandValues(inputs.Count));
                if (queuedInsideStep) Equal(true, runtime.CancelCommand(context.Correlation.Command).Accepted);
                return new(CommandStatus.Pending) { ReadyToContinue = true };
            }, () => ++disposed)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var command = actor.Submit(CommandRequestFor()).Correlation.Command;
            Equal(true, runtime.AdvanceOne());
            if (!queuedInsideStep)
            {
                // An already queued Proceed retains FIFO order; cancellation suppresses a further automatic step.
                Equal(true, actor.Cancel(command).Accepted); Equal(true, runtime.AdvanceOne());
            }
            Equal(true, runtime.AdvanceOne()); Equal(false, runtime.AdvanceOne()); Equal(1, disposed);
            Equal(CommandStatus.Cancelled, RequiredResult(actor, command).Status); Equal(queuedInsideStep ? 1 : 2, RequiredResult(actor, command).Committed.Count);
            Sequence(queuedInsideStep ? new[] { OperationInputKind.Begin, OperationInputKind.Cancel }
                : new[] { OperationInputKind.Begin, OperationInputKind.Proceed, OperationInputKind.Cancel }, inputs);
        }
    }
    private static void CommandCooperativeFailuresAndExternalWait()
    {
        foreach (var invalid in new[]
        {
            CommandDone(1) with { ReadyToContinue = true },
            new OperationStep(CommandStatus.AwaitingInteraction, Interaction: new("Choice", null, null, new([1]))) { ReadyToContinue = true },
            new OperationStep(CommandStatus.Pending, Interaction: new("Choice", null, null, new([1]))) { ReadyToContinue = true }
        })
        {
            using var runtime = new SessionRuntime(new(), new(264)); int disposed = 0;
            runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, _) =>
            { context.World.Civilizations[1].Gold = 12; context.Effect("test.prefix", CommandValues(12)); return invalid; }, () => ++disposed)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var command = actor.Submit(CommandRequestFor()).Correlation.Command;
            Equal(true, runtime.AdvanceOne()); Equal("invalid_operation_step", RequiredResult(actor, command).Code);
            Equal(1, RequiredResult(actor, command).Committed.Count); Equal(12, runtime.World.Civilizations[1].Gold); Equal(1, disposed); Equal(false, runtime.AdvanceOne());
        }
        using (var runtime = new SessionRuntime(new(), new(265)))
        {
            int phase = 0, disposed = 0;
            runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((_, _) =>
            {
                if (phase++ == 0) return new(CommandStatus.Pending) { ReadyToContinue = true };
                if (phase == 2) return new(CommandStatus.Pending); // Explicitly waiting for an external readiness event.
                throw new IOException("Continuation failed");
            }, () => ++disposed)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var command = actor.Submit(CommandRequestFor()).Correlation.Command;
            Equal(true, runtime.AdvanceOne()); Equal(true, runtime.AdvanceOne()); Equal(false, runtime.AdvanceOne()); Equal(2, phase);
            Equal(true, runtime.HasPendingCommands); Equal(true, runtime.ContinueCommand(command)); Equal(false, runtime.ContinueCommand(command));
            Equal(true, runtime.AdvanceOne()); Equal("operation_exception", RequiredResult(actor, command).Code); Equal(false, runtime.AdvanceOne()); Equal(1, disposed);
        }
        int released = 0;
        using (var runtime = new SessionRuntime(new(), new(266)))
        {
            runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((_, _) => new(CommandStatus.Pending) { ReadyToContinue = true }, () => ++released)));
            runtime.BeginClientSession(new()); SessionClient(runtime).Submit(CommandRequestFor()); Equal(true, runtime.AdvanceOne());
        }
        Equal(1, released);
    }
}
