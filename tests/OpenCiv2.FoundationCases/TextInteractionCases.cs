using System.Text;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Content;
using OpenCiv2.Session;
using OpenCiv2.ViewModels;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static CommandDefinition TextCommand(Func<InteractionSpec> prompt, Action<OperationInput>? received = null) => new(
        new(1, "test.text", "Text reply", AccessClass.Player, new([]), new([new("text", ValueKind.Text)])), (_, _, _) => new(),
        _ => new TestContinuation((context, input) =>
        {
            if (input.Kind == OperationInputKind.Begin)
            { context.World.Civilizations[1].Gold = 15; context.Publish(); return new(CommandStatus.AwaitingInteraction, Interaction: prompt()); }
            received?.Invoke(input);
            return input.Kind == OperationInputKind.Cancel ? new(CommandStatus.Cancelled)
                : new(Result: new([new("text", new CommandValue.Text(input.Text ?? ""))]));
        }));
    private static InteractionSpec TextPrompt(ParticipantId? recipient = null, int maximum = 3) =>
        new("Enter text", recipient, null, new([0, 1])) { TextInput = new("Name", "Old", maximum, 0) };
    private static CommandRequest TextRequest() => new("test.text", new([]));

    private static void TextReplyPrivateValidation()
    {
        using var runtime = new SessionRuntime(new(), new(720)); ParticipantId decider = default; OperationInput? received = null;
        runtime.RegisterCommand(TextCommand(() => TextPrompt(decider), input => received = input)); runtime.BeginClientSession(new());
        var actor = SessionClient(runtime); var other = SessionClient(runtime, 2); decider = other.Identity.Participant;
        var command = actor.Submit(TextRequest()).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(other, command); Equal(null, actor.Result(command)!.Interaction);
        Equal("interaction_not_available", actor.Reply(prompt, 0, "x").Code);
        Equal("text_required", other.Reply(prompt, 0).Code); Equal("choice_not_allowed", other.Reply(prompt, 99, "x").Code);
        Equal("text_too_long", other.Reply(prompt, 0, "four").Code); Equal("text_not_allowed", other.Reply(prompt, 1, "x").Code);
        Equal(prompt, RequiredInteraction(other, command)); Equal(null, received);
        var replacement = runtime.Bind(decider, ControllerKind.Frontend); runtime.Revoke(other.Identity.Controller);
        Throws<AccessException>(() => other.Reply(prompt, 0, "x"));
        Equal(true, replacement.Reply(prompt, 0, "🌲:a").Accepted); Equal(false, replacement.Reply(prompt, 0, "again").Accepted);
        Equal(null, received); runtime.AdvanceOne(); Equal("🌲:a", received!.Text); Equal(decider, received.Source!.Participant);
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal((ushort)15, runtime.World.Civilizations[1].Gold);
    }

    private static void TextDialogIdentityAndCancellation()
    {
        using var runtime = new SessionRuntime(new(), new(721)); runtime.RegisterCommand(TextCommand(() => TextPrompt()));
        runtime.BeginClientSession(new()); var client = SessionClient(runtime); var panel = new SessionPanel(); panel.Bind(client);
        var model = new InteractionDialogModel();
        DialogIdentity Open()
        { panel.Submit(TextRequest()); runtime.AdvanceOne(); panel.Refresh(); model.Refresh(panel.View); return model.View!.Identity; }
        var first = Open(); Equal(false, model.Respond(panel, first, 0)); Equal(false, model.View!.Submitted);
        Equal(false, model.Respond(panel, first, 0, "long")); Equal(false, model.View!.Submitted);
        Equal(true, model.Respond(panel, first, 0, "")); Equal(true, model.View!.Submitted);
        Equal(false, model.Respond(panel, first, 0, "x")); runtime.AdvanceOne(); panel.Refresh(); model.Refresh(panel.View);
        Equal(null, model.View); Equal(new CommandValue.Text(""), panel.View.Command!.Result.Single().Value);
        var next = Open(); Equal(false, model.Respond(panel, first, 0, "x")); Equal(true, model.Respond(panel, next, null));
        runtime.AdvanceOne(); panel.Refresh(); Equal(CommandStatus.Cancelled, panel.View.Command!.Status);
        Equal((ushort)15, runtime.World.Civilizations[1].Gold);
    }

    private static void TextInteractionMetadataAndDeadline()
    {
        foreach (int variant in Enumerable.Range(0, 4))
        {
            using var runtime = new SessionRuntime(new(), new(722));
            var field = variant switch
            {
                0 => new InteractionTextInput("", "", 3, 0),
                1 => new("Name", "", -1, 0),
                2 => new("Name", "long", 3, 0),
                _ => new("Name", "", 3, 2)
            };
            runtime.RegisterCommand(TextCommand(() => TextPrompt() with { TextInput = field })); runtime.BeginClientSession(new());
            var client = SessionClient(runtime); var id = client.Submit(TextRequest()).Correlation.Command; runtime.AdvanceOne();
            Equal(CommandStatus.Failed, RequiredResult(client, id).Status);
        }
        using var timed = new SessionRuntime(new(), new(723)); OperationInput? received = null;
        timed.RegisterCommand(TextCommand(() => TextPrompt() with { Deadline = 10, DefaultChoice = 0, Cancellable = false }, input => received = input));
        timed.BeginClientSession(new()); var owner = SessionClient(timed); var command = owner.Submit(TextRequest()).Correlation.Command;
        timed.AdvanceOne(); Equal(0, timed.ExpireInteractions(9)); Equal(1, timed.ExpireInteractions(10)); timed.AdvanceOne();
        Equal(OperationInputKind.Timeout, received!.Kind); Equal("Old", received.Text); Equal(null, received.Source);
        Equal(CommandStatus.Completed, RequiredResult(owner, command).Status);
    }

    private static void TextReplayEncodingAndLimits()
    {
        var recording = new LocalReplayRecorder(ContentIdentity.Sha256("text-replies"u8));
        using var game = new LocalGame(recording); Equal(false, game.Player.Reply(new(999), 0, "🌲: a").Accepted);
        var tape = recording.Capture(); Equal(1, tape.Steps.Count); Equal(true, tape.Steps[0] is LocalReplayStep.TextReply);
        byte[] bytes = LocalReplayCodec.Encode(tape); var decoded = LocalReplayCodec.Decode(bytes); Equal(tape, decoded);
        var envelope = JsonNode.Parse(bytes)!.AsObject(); Equal(LocalReplayCodec.TextFormat, envelope["format"]!.GetValue<string>());
        Equal(true, game.SaveClassic().AsSpan().SequenceEqual(LocalReplay.Run(decoded)));
        envelope["format"] = LocalReplayCodec.Format;
        Throws<ArgumentException>(() => LocalReplayCodec.Decode(Encoding.UTF8.GetBytes(envelope.ToJsonString())));
        var legacy = tape with { Steps = new([new LocalReplayStep.Reply(ControllerKind.Frontend, new(99), 0, new(false, "interaction_not_available"))]) };
        var old = LocalReplayCodec.Encode(legacy); Equal(LocalReplayCodec.Format, JsonNode.Parse(old)!["format"]!.GetValue<string>());
        Equal(legacy, LocalReplayCodec.Decode(old));
        game.Player.Reply(new(999), 0, new string('x', LocalCheckpointCodec.MaximumText + 1));
        Throws<InvalidOperationException>(() => recording.Capture()); Equal(false, game.Player.Reply(new(999), 0).Accepted);
    }

    private static void FoundingPrivateTextSession()
    {
        foreach (bool cancel in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(724));
            var f = new FoundingFixture(data: CreationFixture(new(runtime.Process, runtime.World))); ParticipantId actorSeat = default;
            runtime.RegisterCommand(new(new(1, "test.found", "Found city", AccessClass.Player, new([]), new([new("outcome", ValueKind.Text)])),
                (_, _, _) => new(), _ => new CityFoundingOperation(f.Player(), _ => actorSeat,
                    visit => new("Visit " + visit.Name, actorSeat, new CommandValue.City(visit.City), new([0])))));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); actorSeat = actor.Identity.Participant;
            var other = SessionClient(runtime, 2); var command = actor.Submit(new("test.found", new([]))).Correlation.Command; runtime.AdvanceOne();
            var prompt = RequiredResult(actor, command).Interaction!; Equal("First", prompt.Spec.TextInput!.InitialText);
            Equal(null, other.Result(command)); Equal(false, other.Reply(prompt.Id, 0, "Stolen").Accepted);
            Equal(false, actor.Reply(prompt.Id, 0).Accepted); Equal(false, actor.Reply(prompt.Id, 0, new string('x', 16)).Accepted);
            if (cancel) actor.Cancel(command); else actor.Reply(prompt.Id, 0, "🌲 Haven");
            runtime.AdvanceOne();
            if (!cancel)
            {
                Equal(false, f.Data.Unit(0).Token.Occupied); Equal("🌲 Haven", f.World.Entities.Cities.At(new(0)).Name);
                Equal(true, f.World.Calculation.Processing); Equal(null, RequiredResult(actor, command).Interaction!.Spec.TextInput);
                Equal(true, actor.Cancel(command).Accepted); runtime.AdvanceOne(); Equal(false, f.World.Calculation.Processing);
            }
            var result = RequiredResult(actor, command); Equal(CommandStatus.Completed, result.Status);
            Equal(new CommandValue.Text(cancel ? "Cancelled" : "Founded"), result.Result.Single().Value);
            Equal(cancel, f.Data.Unit(0).Token.Occupied); Equal(!cancel, f.World.Entities.Cities.At(new(0)).Token.Occupied);
        }
    }

    private static void FoundingPrivateLifecycleSession()
    {
        using var runtime = new SessionRuntime(new(), new(725));
        var f = new FoundingFixture(choices: [EscapeChoice("found/delete/unit/0/check/spy/1", 2, 0)],
            data: CreationFixture(new(runtime.Process, runtime.World)));
        EliminationSpySetup(f.Life); f.Data.Unit(0).Type = new(0); f.World.Civilizations[2].UnitsActive[0] = 1;
        f.Data.AddCity(0, 3, new(10, 10)); ParticipantId victimSeat = default;
        runtime.RegisterCommand(new(new(1, "test.found", "Found city", AccessClass.Player, new([]), new([new("outcome", ValueKind.Text)])),
            (_, _, _) => new(), _ => new CityFoundingOperation(f.Founding.Begin(new(0), "found", new(0)), _ => victimSeat,
                _ => throw new InvalidOperationException("Direct founding has no city visit"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var victim = SessionClient(runtime, 2); victimSeat = victim.Identity.Participant;
        var command = actor.Submit(new("test.found", new([]))).Correlation.Command; runtime.AdvanceOne();
        Equal(null, RequiredResult(actor, command).Interaction); var prompt = RequiredInteraction(victim, command);
        Equal(false, actor.Reply(prompt, 0).Accepted); Equal(false, victim.Reply(prompt, 0, "not a name").Accepted);
        Equal(true, victim.Reply(prompt, 0).Accepted); runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
        Equal(EndReason.Defeat, f.World.PendingEndReason); f.Life.Random.RequireChoicesConsumed();
    }
}
