using OpenCiv2.Contracts;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void LocalRateEditorDecisions()
    {
        foreach (bool console in new[] { false, true })
        {
            var preferences = new BufferedPreferenceWriter();
            var recorder = new LocalReplayRecorder(new string('a', 64));
            using var game = new LocalGame(recorder, preferences);
            using var direct = new LocalGame();
            foreach (var current in new[] { game, direct })
            {
                var unit = current.Player.OwnUnits().Records.Single(u => u.Type.Value == 0);
                RunLocal(current, false, "found", LocalPlayCommands.Found,
                    new("unit", new CommandValue.Unit(new(unit.Slot, unit.Token))), new("name", new CommandValue.Text("Rates")));
            }
            CommandUpdate Open() => AwaitLocal(game,
                SubmitLocal(game, console, LocalPlayCommands.EditRates, LocalPlayCommands.EditRates));
            CommandUpdate Reply(CommandUpdate update, int choice)
            {
                Decide(game.Player, console, update, choice);
                Equal(true, game.AdvanceOne());
                return game.Player.Result(update.Correlation.Command)!;
            }
            var opening = Open();
            Equal("Apply rates", opening.Interaction!.Spec.Options.Single(o => o.Value == 1).Label);
            Equal(false, game.Player.Reply(opening.Interaction.Id, 99).Accepted);
            var hold = Reply(Reply(opening, 5), 3);
            Equal(true, hold.Interaction!.Spec.Prompt.Contains("Hold: Luxury", StringComparison.Ordinal));
            Equal(false, game.Player.Reply(opening.Interaction.Id, 1).Accepted);
            var preview = Reply(Reply(hold, 2), 6);
            Equal(true, preview.Interaction!.Spec.Prompt.StartsWith("Tax 60% · Science 40% · Luxury 0%", StringComparison.Ordinal));
            Equal(new PlayRates(4, 6, 0, 6), game.Player.ObserveLocalPlay()!.Rates);
            Equal(0, preferences.Capture().Length);
            Decide(game.Player, console, preview, null);
            Equal(true, game.AdvanceOne());
            var cancelled = game.Player.Result(preview.Correlation.Command)!;
            Equal(CommandStatus.Cancelled, cancelled.Status);
            Equal(2, cancelled.Committed.Count(effect => effect.Name == "civilization.rates.previewed"));
            Equal(new PlayRates(4, 6, 0, 6), game.Player.ObserveLocalPlay()!.Rates);

            Equal(LocalLoadResult.Loaded, game.LoadClassic(game.SaveClassic()));
            Equal(LocalLoadResult.Loaded, direct.LoadClassic(direct.SaveClassic()));
            opening = Open();
            Equal(true, opening.Interaction!.Spec.Prompt.Contains("Hold: Luxury", StringComparison.Ordinal));
            var applied = Reply(Reply(Reply(opening, 2), 6), 1);
            Equal(CommandStatus.Completed, applied.Status);
            Equal("civilization.rates", applied.Committed[^1].Name);
            Equal((short)6, ClassicPreferences.DecodeOrdinary(preferences.Capture().AsSpan(0, ClassicPreferences.OrdinarySize)).Tax);
            RunLocal(direct, false, "rates", LocalPlayCommands.Rates,
                new("tax", new CommandValue.Integer(6)), new("science", new CommandValue.Integer(4)));
            BytesEqual(direct.SaveClassic(), game.SaveClassic());
            EndLocalTurn(game, console, 2); EndLocalTurn(direct, false, 2);
            BytesEqual(direct.SaveClassic(), game.SaveClassic());

            opening = Open();
            var overlapping = game.Player.Submit(new(LocalPlayCommands.Rates,
                new([new("tax", new CommandValue.Integer(5)), new("science", new CommandValue.Integer(5))])));
            Equal(CommandStatus.Rejected, overlapping.Status);
            Decide(game.Player, console, opening, null);
            Equal(true, game.AdvanceOne());
            RunLocal(game, false, "rates", LocalPlayCommands.Rates,
                new("tax", new CommandValue.Integer(5)), new("science", new CommandValue.Integer(5)));
            Equal(false, game.Player.Reply(opening.Interaction!.Id, 1).Accepted);
            Equal(new PlayRates(5, 5, 0, 6), game.Player.ObserveLocalPlay()!.Rates);
            byte[] saved = game.SaveClassic();
            BytesEqual(saved, LocalReplay.Run(recorder.Capture()));
        }

        using var failed = new LocalGame(null, new FailedRatePreferences());
        var id = SubmitLocal(failed, false, LocalPlayCommands.EditRates, LocalPlayCommands.EditRates);
        var decision = AwaitLocal(failed, id);
        Decide(failed.Player, false, decision, 1);
        Equal(true, failed.AdvanceOne());
        Equal("preferences_output_failed", failed.Player.Result(id)!.Code);
        Equal("civilization.rates", failed.Player.Result(id)!.Committed[^1].Name);
    }
}
