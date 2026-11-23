using OpenCiv2.Contracts;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void FoundCouncil(LocalGame game)
    {
        var unit = game.Player.OwnUnits().Records.Single(u => u.Type.Value == 0);
        RunLocal(game, false, "found", LocalPlayCommands.Found,
            new("unit", new CommandValue.Unit(new(unit.Slot, unit.Token))), new("name", new CommandValue.Text("Council")));
    }

    private static CommandUpdate GovernmentReply(LocalGame game, bool console, CommandUpdate update, int? value)
    {
        Decide(game.Player, console, update, value);
        Equal(true, game.AdvanceOne());
        return game.Player.Result(update.Correlation.Command)!;
    }

    private static CommandUpdate StartRevolution(LocalGame game, bool console, int? reply)
    {
        var opening = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.Revolution, LocalPlayCommands.Revolution));
        Equal("Start revolution", opening.Interaction!.Spec.Options.Single(o => o.Value == 0).Label);
        return GovernmentReply(game, console, opening, reply);
    }

    private static CommandUpdate GovernmentTurnDecision(LocalGame game, bool console)
    {
        var turn = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.EndTurn, LocalPlayCommands.EndTurn));
        var government = GovernmentReply(game, console, turn, 1);
        Equal(CommandStatus.AwaitingInteraction, government.Status);
        Equal("Choose a new government.", government.Interaction!.Spec.Prompt);
        Equal(true, government.Interaction.Spec.Choices.SequenceEqual([1]));
        Equal(false, game.Player.ObserveLocalPlay()!.PlayerPhase);
        return government;
    }

    public static (byte[] Initial, byte[] Selected, byte[] Continued) LocalGovernmentReference()
    {
        using var game = new LocalGame();
        FoundCouncil(game);
        Equal(CommandStatus.Completed, StartRevolution(game, false, 0).Status);
        byte[] initial = game.SaveClassic();
        Equal(LocalLoadResult.Loaded, game.LoadClassic(initial));
        EndLocalTurn(game, false, 2); EndLocalTurn(game, false, 3);
        var rates = GovernmentReply(game, false, GovernmentTurnDecision(game, false), 1);
        Equal(CommandStatus.AwaitingInteraction, rates.Status);
        Equal(CommandStatus.Completed, GovernmentReply(game, false, rates, 1).Status);
        byte[] selected = game.SaveClassic();
        Equal(LocalLoadResult.Loaded, game.LoadClassic(selected));
        EndLocalTurn(game, false, 5);
        return (initial, selected, game.SaveClassic());
    }

    private static void LocalGovernmentRevolutionAndTurns()
    {
        byte[]? direct = null;
        foreach (bool console in new[] { false, true })
        {
            var recorder = new LocalReplayRecorder(new string('b', 64));
            var preferences = new BufferedPreferenceWriter();
            using var game = new LocalGame(recorder, preferences);
            FoundCouncil(game);
            byte[] before = game.SaveClassic();
            foreach (string name in new[] { LocalPlayCommands.Revolution, LocalPlayCommands.EditRates })
            {
                var queued = SubmitLocal(game, console, name, name);
                Equal(true, game.Player.Cancel(queued).Accepted);
                Equal(true, game.AdvanceOne());
                var cancelled = game.Player.Result(queued)!;
                Equal(CommandStatus.Cancelled, cancelled.Status); Equal(0, cancelled.Committed.Count);
                BytesEqual(before, game.SaveClassic());
            }
            Equal(CommandStatus.Cancelled, StartRevolution(game, console, null).Status);
            BytesEqual(before, game.SaveClassic());
            Equal(CommandStatus.Completed, StartRevolution(game, console, 1).Status);
            BytesEqual(before, game.SaveClassic());
            var revolution = StartRevolution(game, console, 0);
            Equal(CommandStatus.Completed, revolution.Status);
            Equal(0, game.Player.ObserveLocalPlay()!.Government!.Id);
            Equal(false, game.Player.ObserveLocalPlay()!.CanRevolt);
            Equal(0, ClassicFiles.DecodeStoredSave(game.SaveClassic()).Civilizations[1].Flags & 1);
            // Direct command in Anarchy is a completed no-op, even though the menu disables it.
            RunLocal(game, console, LocalPlayCommands.Revolution, LocalPlayCommands.Revolution);
            Equal(LocalLoadResult.Loaded, game.LoadClassic(game.SaveClassic()));
            EndLocalTurn(game, console, 2); EndLocalTurn(game, console, 3);
            Equal(0, game.Player.ObserveLocalPlay()!.Government!.Id);
            var government = GovernmentTurnDecision(game, console);
            var rates = GovernmentReply(game, console, government, 1);
            Equal(CommandStatus.AwaitingInteraction, rates.Status);
            Equal(1, game.Player.ObserveLocalPlay()!.Government!.Id);
            var finished = GovernmentReply(game, console, rates, null); // Cancel only the rate draft, after the government store.
            Equal(CommandStatus.Completed, finished.Status);
            Equal(true, finished.Committed.Any(e => e.Name == "civilization.government.established"));
            Equal(true, game.Player.ObserveLocalPlay()!.PlayerPhase);
            Equal(0, preferences.Capture().Length);
            // The retained GovernmentChanged flag permits another selection without a new revolution.
            var again = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.Revolution, LocalPlayCommands.Revolution));
            Equal("Choose a new government.", again.Interaction!.Spec.Prompt);
            Equal(CommandStatus.Cancelled, GovernmentReply(game, console, again, null).Status);
            Equal(1, game.Player.ObserveLocalPlay()!.Government!.Id);
            Equal(LocalLoadResult.Loaded, game.LoadClassic(game.SaveClassic()));
            EndLocalTurn(game, console, 5);
            byte[] saved = game.SaveClassic();
            if (direct is null) direct = saved; else BytesEqual(direct, saved);
            BytesEqual(saved, LocalReplay.Run(recorder.Capture()));
        }
    }

    private static void LocalGovernmentCancellationAndOutputFailure()
    {
        using var game = new LocalGame(null, new FailedRatePreferences());
        StartRevolution(game, false, 0);
        EndLocalTurn(game, false, 2); EndLocalTurn(game, false, 3);
        var decision = GovernmentTurnDecision(game, false);
        Equal(CommandStatus.Completed, GovernmentReply(game, false, decision, null).Status);
        Equal(0, game.Player.ObserveLocalPlay()!.Government!.Id);
        var picker = AwaitLocal(game, SubmitLocal(game, false, LocalPlayCommands.Revolution, LocalPlayCommands.Revolution));
        var rates = GovernmentReply(game, false, picker, 1);
        var failed = GovernmentReply(game, false, rates, 1);
        Equal(CommandStatus.Failed, failed.Status); Equal("preferences_output_failed", failed.Code);
        Equal(1, game.Player.ObserveLocalPlay()!.Government!.Id);
        Equal(true, game.Player.ObserveLocalPlay()!.PlayerPhase);
        EndLocalTurn(game, false, 5);

        using var turnFailure = new LocalGame(null, new FailedRatePreferences());
        StartRevolution(turnFailure, false, 0);
        EndLocalTurn(turnFailure, false, 2); EndLocalTurn(turnFailure, false, 3);
        picker = GovernmentTurnDecision(turnFailure, false);
        rates = GovernmentReply(turnFailure, false, picker, 1);
        failed = GovernmentReply(turnFailure, false, rates, 1);
        Equal(CommandStatus.Failed, failed.Status); Equal("preferences_output_failed", failed.Code);
        Equal(true, failed.Committed.Any(e => e.Name == "turn.advanced"));
        Equal(true, turnFailure.Player.ObserveLocalPlay()!.PlayerPhase);
        EndLocalTurn(turnFailure, false, 5);
    }
}
