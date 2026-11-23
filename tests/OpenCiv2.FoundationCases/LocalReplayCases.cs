using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void LocalReplayCommandsAndDecisions()
    {
        var (initial, _) = DesktopProductionPolicyReference();
        var recording = new LocalReplayRecorder(ContentIdentity.Sha256("example"u8));
        using var game = new LocalGame(recording);
        Equal(LocalLoadResult.InvalidFile, game.LoadClassic(ReadOnlyMemory<byte>.Empty));
        Equal(LocalLoadResult.Loaded, game.LoadClassic(initial));
        var automation = game.Bind(ControllerKind.Automation);

        var ending = automation.Submit(new(LocalPlayCommands.EndTurn, OwnedList<NamedValue>.Empty));
        var decision = AwaitLocal(game, ending.Correlation.Command);
        Equal(false, game.Player.Reply(decision.Interaction!.Id, -1).Accepted);
        Equal(true, game.Player.Cancel(ending.Correlation.Command).Accepted);
        Equal(true, game.AdvanceOne());
        Equal(CommandStatus.Cancelled, automation.Result(ending.Correlation.Command)!.Status);
        game.Player.Updates();
        automation.Updates();
        Equal(true, game.RetireCommand(ending.Correlation.Command));
        EndLocalTurn(game, false, 2);

        var city = game.Player.OwnCities().Records.Single();
        var target = new NamedValue("city", new CommandValue.City(new(city.Slot, city.Token)));
        var choice = new NamedValue("type", new CommandValue.Integer(4));
        var duplicate = automation.Submit(new(LocalPlayCommands.Production, new([target, choice, choice])));
        Equal(CommandStatus.Rejected, duplicate.Status);
        var admitted = automation.Submit(new(LocalPlayCommands.Production, new([target, choice])));
        Equal(CommandStatus.Pending, admitted.Status);
        Equal(true, game.AdvanceOne());
        Equal(CommandStatus.Completed, automation.Result(admitted.Correlation.Command)!.Status);
        byte[] first = game.SaveClassic();
        Equal(LocalLoadResult.Loaded, game.LoadClassic(first));
        byte[] expected = game.SaveClassic();

        var tape = recording.Capture();
        byte[] bytes = LocalReplayCodec.Encode(tape);
        Equal(tape, LocalReplayCodec.Decode(bytes));
        BytesEqual(expected, LocalReplay.Run(LocalReplayCodec.Decode(bytes)));

        var changes = tape.Steps.ToArray();
        int index = Array.FindIndex(changes, step => step is LocalReplayStep.Advance);
        var original = (LocalReplayStep.Advance)changes[index];
        changes[index] = original with { Effects = original.Effects + 1 };
        Throws<InvalidDataException>(() => LocalReplay.Run(tape with { Steps = new(changes) }));
        var interrupted = tape.Steps.Take(index + 1);
        Throws<InvalidDataException>(() => LocalReplay.Run(tape with { Steps = new(interrupted) }));
        string text = System.Text.Encoding.UTF8.GetString(bytes);
        var envelope = System.Text.Json.Nodes.JsonNode.Parse(text)!.AsObject();
        envelope["sha256"] = new string('0', 64);
        Throws<ArgumentException>(() => LocalReplayCodec.Decode(System.Text.Encoding.UTF8.GetBytes(envelope.ToJsonString())));
    }

    private static void LocalReplayRecordingLimit()
    {
        var recording = new LocalReplayRecorder(ContentIdentity.Sha256("example"u8));
        using var game = new LocalGame(recording);
        for (int i = 0; i <= LocalCheckpointCodec.MaximumItems; ++i)
        {
            Equal(false, game.Player.Cancel(new(0)).Accepted);
        }
        Throws<InvalidOperationException>(() => recording.Capture());
        Equal(2, game.Player.OwnUnits().Records.Count);
    }
}
