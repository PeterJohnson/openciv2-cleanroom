using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class TestContinuation(Func<OperationContext, OperationInput, OperationStep> handler, Action? dispose = null) : IContinuation
    {
        public OperationStep Resume(OperationContext context, OperationInput input) => handler(context, input);
        public void Dispose() => dispose?.Invoke();
    }
    private static OwnedList<NamedValue> CommandValues(int value) => new([new("value", new CommandValue.Integer(value))]);
    private static OperationStep CommandDone(int value) => new(Result: CommandValues(value));
    private static CommandRequest CommandRequestFor(int amount = 3, string name = "test.action") => new(name, new([new("amount", new CommandValue.Integer(amount))]));
    private static CommandDefinition CommandDefinitionFor(Func<CommandRequest, IContinuation> factory, string name = "test.action") => new(
        new(1, name, "Authored test operation", AccessClass.Player, new([new("amount"), new("target", ValueKind.Unit, false)]), new([new("value")])),
        (_, _, _) => new(), factory);
    private static IContinuation DecisionContinuation(ParticipantId? recipient = null, Action<OperationContext>? callback = null, bool cancellable = true)
    {
        int phase = 0;
        return new TestContinuation((context, input) =>
        {
            if (input.Kind == OperationInputKind.Cancel) return new(CommandStatus.Cancelled, Code: "cancelled");
            if (phase++ == 0)
            {
                context.World.Civilizations[1].Gold -= 5; context.Effect("test.debit", CommandValues(5)); callback?.Invoke(context);
                return new(CommandStatus.AwaitingInteraction, Interaction: new("Choose a result", recipient, null, new([2, 4]), 50, 2, cancellable));
            }
            if (input.Choice is null) return new(CommandStatus.Failed, Code: "missing_choice");
            Equal(input.Kind == OperationInputKind.Choice, input.Source is not null);
            context.World.Civilizations[1].Gold += input.Choice.Value; context.Effect("test.choice", CommandValues(context.World.Civilizations[1].Gold));
            return CommandDone(context.World.Civilizations[1].Gold);
        });
    }
    private static CommandUpdate RequiredResult(IPlayerClient client, CommandId command) => client.Result(command) ?? throw new Exception("Missing result");
    private static InteractionId RequiredInteraction(IPlayerClient client, CommandId command) => RequiredResult(client, command).Interaction?.Id ?? throw new Exception("Missing interaction");
    private static void CommandDescriptorAdmission()
    {
        using var runtime = new SessionRuntime(new(), new(1)); int executions = 0;
        var definition = CommandDefinitionFor(_ => new TestContinuation((_, _) => { ++executions; return CommandDone(3); }));
        Throws<ArgumentException>(() => runtime.RegisterCommand(definition with { Descriptor = definition.Descriptor with { Version = 2 } }));
        Throws<ArgumentException>(() => runtime.RegisterCommand(definition with { Admit = null! }));
        runtime.RegisterCommand(definition); Throws<ArgumentException>(() => runtime.RegisterCommand(definition));
        runtime.RegisterCommand(definition with { Descriptor = definition.Descriptor with { Name = "test.editor", Access = AccessClass.Developer }, Execute = _ => DecisionContinuation() });
        runtime.BeginClientSession(new()); var player = SessionClient(runtime);
        Equal(new OwnedList<string>(["test.action"]), new(player.Commands().Select(c => c.Name)));
        Equal("command_not_available", player.Submit(CommandRequestFor(name: "test.editor")).Code);
        Equal("missing_value:amount", player.Submit(new("test.action", new([]))).Code);
        Equal("wrong_type:amount", player.Submit(new("test.action", new([new("amount", new CommandValue.Boolean(true))]))).Code);
        Equal("duplicate_value:amount", player.Submit(new("test.action", new([new("amount", new CommandValue.Integer(1)), new("amount", new CommandValue.Integer(2))]))).Code);
        Equal("unknown_value:unknown", player.Submit(new("test.action", new([new("amount", new CommandValue.Integer(1)), new("unknown", new CommandValue.Integer(2))]))).Code);
        Equal("invalid_unit_slot:target", player.Submit(new("test.action", new([new("amount", new CommandValue.Integer(1)), new("target", new CommandValue.Unit(new(new(2048), new(1))))]))).Code);
        Equal(0, executions); Equal(false, runtime.AdvanceOne());
        var developer = runtime.Bind(player.Identity.Participant, ControllerKind.Scripted, AccessClass.Developer); Equal(2, developer.Commands().Count);
        var command = developer.Submit(CommandRequestFor(name: "test.editor")).Correlation.Command; runtime.AdvanceOne(); var prompt = RequiredInteraction(developer, command);
        Equal(true, player.Result(command) is null); Equal(0, player.Updates().Count); Equal(false, player.Reply(prompt, 2).Accepted); Equal(false, player.Cancel(command).Accepted);
        Equal(true, developer.Cancel(command).Accepted); runtime.AdvanceOne(); Throws<InvalidOperationException>(() => runtime.RegisterCommand(definition));
    }
    private static void CommandFifoAndOwnedInputs()
    {
        using var runtime = new SessionRuntime(new(), new(1)); var order = new List<int>(); int factories = 0;
        runtime.RegisterCommand(CommandDefinitionFor(request =>
        {
            ++factories; return new TestContinuation((context, _) =>
        { var amount = ((CommandValue.Integer)CommandDescriptor.Value(request.Arguments, "amount")).Value; order.Add(amount); context.World.Civilizations[1].Gold += amount; context.Publish(); return CommandDone(context.World.Civilizations[1].Gold); });
        }));
        runtime.BeginClientSession(new()); var first = SessionClient(runtime); var second = runtime.Bind(first.Identity.Participant, ControllerKind.Automation);
        NamedValue[] values = [new("amount", new CommandValue.Integer(3))]; var a = first.Submit(new("test.action", new(values))); values[0] = new("amount", new CommandValue.Integer(999)); var b = second.Submit(CommandRequestFor(7));
        Equal(0, factories); Equal(0, order.Count); Equal((int)CommandStatus.Pending, (int)a.Status); Equal(true, a.Correlation.Command != b.Correlation.Command);
        Equal(false, runtime.ContinueCommand(a.Correlation.Command)); Equal(true, runtime.AdvanceOne()); Equal(new OwnedList<int>([3]), new(order)); Equal(1, factories);
        Equal(CommandValues(3), RequiredResult(first, a.Correlation.Command).Result); Equal((int)CommandStatus.Pending, (int)RequiredResult(second, b.Correlation.Command).Status);
        Equal(true, runtime.AdvanceOne()); Equal(new OwnedList<int>([3, 7]), new(order)); Equal(CommandValues(10), RequiredResult(second, b.Correlation.Command).Result); Equal(false, runtime.AdvanceOne()); Equal(false, runtime.HasPendingCommands);
        Equal(2UL, first.OwnUnits().Observation.Revision);
    }
    private static void CommandDecisionsAndTimeouts()
    {
        foreach (int branch in new[] { 0, 1, 2 })
        {
            using var runtime = new SessionRuntime(new(), new(1)); runtime.RegisterCommand(CommandDefinitionFor(_ => DecisionContinuation(callback: context => context.World.Civilizations[1].Gold = 20)));
            runtime.BeginClientSession(new()); var client = SessionClient(runtime); var command = client.Submit(CommandRequestFor()).Correlation.Command; runtime.AdvanceOne();
            Equal(20, runtime.World.Civilizations[1].Gold); var prompt = RequiredInteraction(client, command); Equal(false, runtime.ContinueCommand(command));
            Equal(false, client.Reply(prompt, 99).Accepted); Equal(0, runtime.ExpireInteractions(49));
            if (branch == 0) { Equal(true, client.Reply(prompt, 4).Accepted); Equal(false, client.Reply(prompt, 4).Accepted); }
            if (branch == 1) Equal(true, client.Cancel(command).Accepted);
            if (branch == 2) { Equal(1, runtime.ExpireInteractions(50)); Equal(0, runtime.ExpireInteractions(51)); }
            runtime.AdvanceOne(); var result = RequiredResult(client, command);
            Equal(branch == 1 ? (int)CommandStatus.Cancelled : (int)CommandStatus.Completed, (int)result.Status);
            Equal(branch == 1 ? 20 : branch == 0 ? 24 : 22, runtime.World.Civilizations[1].Gold); Equal(branch == 1 ? 1 : 2, result.Committed.Count);
            Equal(false, client.Reply(prompt, 2).Accepted);
        }
    }
    private static void CommandRevocationAndPrivacy()
    {
        using var runtime = new SessionRuntime(new(), new(1)); ParticipantId recipient = new(0);
        runtime.RegisterCommand(CommandDefinitionFor(_ => DecisionContinuation(recipient))); runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var decider = SessionClient(runtime, 2); recipient = decider.Identity.Participant; var outsider = SessionClient(runtime, 3);
        var command = actor.Submit(CommandRequestFor()).Correlation.Command; runtime.AdvanceOne();
        var actorView = RequiredResult(actor, command); var promptView = RequiredResult(decider, command); var prompt = promptView.Interaction!.Id;
        Equal(true, actorView.Interaction is null); Equal(1, actorView.Committed.Count); Equal(0, promptView.Committed.Count); Equal(0, promptView.Result.Count); Equal("", promptView.Code);
        Equal(true, outsider.Result(command) is null); Equal(0, outsider.Updates().Count); Equal(false, actor.Reply(prompt, 2).Accepted);
        runtime.Revoke(decider.Identity.Controller); CheckRevoked(decider, AccessFailure.Revoked);
        var replacement = runtime.Bind(recipient, ControllerKind.Automation); Equal(prompt, RequiredInteraction(replacement, command)); Equal(1, replacement.Updates().Count);
        Equal(true, replacement.Reply(prompt, 4).Accepted); var withdrawn = replacement.Updates(); Equal(1, withdrawn.Count); Equal(true, withdrawn[0].Interaction is null); Equal(0, withdrawn[0].Committed.Count);
        runtime.AdvanceOne(); Equal(true, replacement.Result(command) is null); Equal(0, replacement.Updates().Count); Equal(2, RequiredResult(actor, command).Committed.Count);
    }
    private static void CommandPendingReplacementAndPins()
    {
        using var runtime = new SessionRuntime(new(), new(1)); runtime.RegisterCommand(CommandDefinitionFor(_ => DecisionContinuation(cancellable: false))); runtime.BeginClientSession(new()); var client = SessionClient(runtime);
        using (var activity = runtime.BeginActivity())
        {
            var command = client.Submit(CommandRequestFor()).Correlation.Command; runtime.AdvanceOne(); Equal(false, client.Cancel(command).Accepted);
            Throws<InvalidOperationException>(() => runtime.BeginClientSession(new())); Throws<InvalidOperationException>(() => runtime.EndClientSession()); Throws<InvalidOperationException>(() => runtime.BeginActivity());
            runtime.RemoveParticipant(client.Identity.Participant); Equal(true, runtime.CancelCommand(command).Accepted); Equal(true, runtime.AdvanceOne()); Equal(false, runtime.HasPendingCommands);
            Throws<InvalidOperationException>(() => runtime.EndClientSession());
        }
        runtime.EndClientSession(); Equal(false, runtime.Active);
    }
    private static void CommandFailuresAndCommittedEffects()
    {
        using var runtime = new SessionRuntime(new(), new(1)); int disposed = 0;
        runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, _) =>
        { context.World.Civilizations[1].Gold = 17; context.Effect("test.before_failure", CommandValues(17)); throw new IOException("child"); }, () => ++disposed)));
        runtime.BeginClientSession(new()); var client = SessionClient(runtime); var command = client.Submit(CommandRequestFor()).Correlation.Command; runtime.AdvanceOne();
        var result = RequiredResult(client, command); Equal("operation_exception", result.Code); Equal((int)CommandStatus.Failed, (int)result.Status); Equal(1, result.Committed.Count); Equal(17, runtime.World.Civilizations[1].Gold); Equal(1, disposed); Equal(1UL, client.OwnUnits().Observation.Revision);
        Equal(false, runtime.ContinueCommand(command)); runtime.Dispose(); Equal(1, disposed);
    }
    private static void CommandInvalidStepsAndReentry()
    {
        foreach (var step in new[] { new OperationStep(CommandStatus.Rejected), new OperationStep(CommandStatus.Completed), new OperationStep(CommandStatus.AwaitingInteraction),
            new OperationStep(CommandStatus.AwaitingInteraction, Interaction: new("prompt", null, null, new([2, 2]))),
            new OperationStep(CommandStatus.AwaitingInteraction, Interaction: new("prompt", null, null, new([2]), DefaultChoice: 2)),
            new OperationStep(CommandStatus.AwaitingInteraction, Interaction: new("prompt", new(999), null, new([2]))) })
        {
            using var runtime = new SessionRuntime(new(), new(1)); runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((_, _) => step))); runtime.BeginClientSession(new()); var client = SessionClient(runtime); var command = client.Submit(CommandRequestFor()).Correlation.Command;
            runtime.AdvanceOne(); var result = RequiredResult(client, command); Equal((int)CommandStatus.Failed, (int)result.Status); Equal(step.Status == CommandStatus.Completed ? "invalid_operation_result:missing_value:value" : "invalid_operation_step", result.Code);
        }
        using var guarded = new SessionRuntime(new(), new(1)); guarded.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((_, _) =>
        { Throws<InvalidOperationException>(() => guarded.AdvanceOne()); Throws<InvalidOperationException>(() => guarded.RetireCommand(new(1))); Throws<InvalidOperationException>(() => guarded.EndClientSession()); return CommandDone(1); })));
        guarded.BeginClientSession(new()); var owner = SessionClient(guarded); var accepted = owner.Submit(CommandRequestFor()); guarded.AdvanceOne(); Equal((int)CommandStatus.Completed, (int)RequiredResult(owner, accepted.Correlation.Command).Status);
    }
    private static void CommandAdmissionChanges()
    {
        foreach (bool throwing in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(1)); IPlayerClient? client = null; int executes = 0;
            runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((_, _) => { ++executes; return CommandDone(1); })) with
            { Admit = (_, _, _) => { if (throwing) throw new IOException("admit"); runtime.Revoke(client!.Identity.Controller); return new(); } });
            runtime.BeginClientSession(new()); client = SessionClient(runtime); var result = client.Submit(CommandRequestFor()); Equal(throwing ? "admission_exception" : "binding_changed_during_admission", result.Code); Equal(0, executes); Equal(false, runtime.AdvanceOne());
        }
        foreach (var admission in new[] { new Admission("busy"), new Admission(Route: (CommandRoute)99), new Admission(Route: CommandRoute.AwaitPeer, Executor: new(9)) })
        {
            using var runtime = new SessionRuntime(new(), new(1)); runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((_, _) => CommandDone(1))) with { Admit = (_, _, _) => admission });
            runtime.BeginClientSession(new()); var result = SessionClient(runtime).Submit(CommandRequestFor()); Equal(admission.Rejection ?? (admission.Route == (CommandRoute)99 ? "invalid_authority_route" : "unsupported_authority_route"), result.Code);
        }
    }
}
