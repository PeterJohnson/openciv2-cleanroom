using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void RetainedPowerHistory()
    {
        var f = WorkingFixture(); var w = f.World;
        w.Globals.Turn = 9; w.Globals.CivsAlive = CivilizationSet.Only(1); w.Globals.CivsAtStart = CivilizationSet.Only(2);
        w.Civilizations[1].Tribe = 0; w.Civilizations[2].Tribe = 1;
        f.Process.Rules.Leaders[0].Plural = new("Alders"); f.Process.Rules.Leaders[1].Plural = new("unused", true); w.Names[2].Tribe = "Cedars";
        w.PowerHistory[0][1] = 12; w.PowerHistory[1][1] = 25; w.PowerHistory[0][2] = 7; w.PowerHistory[1][2] = 0;
        w.PowerHistory[1][7] = 201; w.PowerHistory[2][7] = 255; // Unshown civilization still sets the admitted maximum.
        var report = PowerHistory.Prepare(w, f.Process, ClassicCalendar);
        Equal(4, report.Interval); Equal(2, report.Dates.Count); Equal(201, report.Maximum); Equal(2, report.Series.Count);
        Equal("Cedars", report.Series[1].Name); Equal(1, report.Dates[0].Turn); Equal(5, report.Dates[1].Turn);
        Equal(Calendar.Year(5, w.Globals, w.Scenario, ClassicCalendar), report.Dates[1].Year);
        w.PowerHistory[0][1] = 55; Equal((byte)12, report.Series[0].Values[0]);
        w.Globals.Status.Scenario = true; w.Scenario.Flags.ObjectiveVictory = true;
        Equal(4, PowerHistory.Prepare(w, f.Process, ClassicCalendar).Dates.Count);
        w.Globals.Status.Scenario = false; w.Globals.Status.ScenarioExport = true;
        Equal(2, PowerHistory.Prepare(w, f.Process, ClassicCalendar).Dates.Count);
        w.Globals.Turn = -1; Equal(0, PowerHistory.Prepare(w, f.Process, ClassicCalendar).Dates.Count);
        w.Globals.Turn = 1000; Equal(150, PowerHistory.Prepare(w, f.Process, ClassicCalendar).Dates.Count);
    }

    private static void EndGameReportOrder()
    {
        var f = WorkingFixture(); var w = f.World; w.Multiplayer.LocalHuman = 1;
        f.Process.Rules.Leaders[0].LeaderName = new("Cedar"); f.Process.Rules.Leaders[0].Plural = new("Alders");
        var storage = new BufferedHallStorage(); var hall = new HallOfFame(storage);
        var effects = new List<string>();
        var context = new OperationContext(w, f.Process, new GameplayRandom(1, "end"), new PresentationRandom(2, "end"),
            new(default, new(1)), default, (name, _) => effects.Add(name), () => { });
        w.Score.Total = 999; w.Score.Rating = 1234;
        using var reports = new EndGameReports(hall);
        Equal("Power history", reports.Resume(context, new()).Interaction!.Prompt.Split('\n')[0]);
        Equal(999, w.Score.Total); Equal(1234, w.Score.Rating);
        var score = reports.Resume(context, new(OperationInputKind.Choice, 0));
        Equal(true, score.Interaction!.Prompt.StartsWith("Civilization score", StringComparison.Ordinal));
        Equal(1234, w.Score.Rating); Equal(false, score.Interaction.Prompt.Contains("rating", StringComparison.Ordinal));
        w.Score.Total = 1000; w.Score.ConquestBonus = 0;
        var rating = reports.Resume(context, new(OperationInputKind.Choice, 0));
        Equal(true, rating.Interaction!.Prompt.StartsWith("Civilization rating", StringComparison.Ordinal));
        Equal(40, w.Score.Rating);
        w.Multiplayer.LocalHuman = 2; w.Civilizations[2].Tribe = 1;
        f.Process.Rules.Leaders[1].LeaderName = new("Rowan"); f.Process.Rules.Leaders[1].Plural = new("Cedars");
        var ranked = reports.Resume(context, new(OperationInputKind.Choice, 0));
        Equal(true, ranked.Interaction!.Prompt.Contains("Rowan", StringComparison.Ordinal)); Equal((short)1000, hall.Record(0).Score);
        Equal(CommandStatus.Completed, reports.Resume(context, new(OperationInputKind.Cancel)).Status);
        Equal("civilization.score.prepared,civilization.rating.prepared,hall.saved", string.Join(',', effects));
        Equal(false, w.Globals.Status.Scored); Equal(false, w.Globals.Status.Won);
        using var skip = new EndGameReports(hall); skip.Resume(context, new());
        w.Globals.Status.Scored = true;
        Equal(CommandStatus.Completed, skip.Resume(context, new(OperationInputKind.Choice, 0)).Status); // Live gate after graph.
        Equal(3, effects.Count);
    }

    private static void ExplicitRetirementFailureAndFlags()
    {
        var f = WorkingFixture(); var w = f.World; w.Multiplayer.LocalHuman = 1;
        w.PlayerPhaseActive = w.TurnLoopActive = true; w.PendingEndReason = EndReason.Conquest;
        f.Process.Rules.Leaders[0].LeaderName = new("Cedar"); f.Process.Rules.Leaders[0].Plural = new("Alders");
        var effects = new List<string>();
        var context = new OperationContext(w, f.Process, new GameplayRandom(1, "retire"), new PresentationRandom(2, "retire"),
            new(default, new(1)), default, (name, _) => effects.Add(name), () => { });
        using var operation = new RetireGameOperation(true, new(new CallbackStorage())); // Failed Hall output must not prevent confirmed retirement.
        operation.Resume(context, new());
        var step = operation.Resume(context, new(OperationInputKind.Choice, -2));
        for (int i = 0; i < 4; ++i) { Equal(CommandStatus.AwaitingInteraction, step.Status); step = operation.Resume(context, new(OperationInputKind.Choice, 0)); }
        Equal(CommandStatus.Failed, step.Status); Equal("hall_output_failed", step.Code);
        Equal(false, w.TurnLoopActive); Equal(false, w.PlayerPhaseActive);
        Equal(false, w.Globals.Status.Scored); Equal(false, w.Globals.Status.Won); Equal(EndReason.Conquest, w.PendingEndReason);
        Equal("game.closed", effects[^1]);
        w.TurnLoopActive = w.PlayerPhaseActive = true; w.Globals.Status.Scored = true; w.Score.Total = 555;
        using var quit = new RetireGameOperation(false, new(new CallbackStorage()));
        quit.Resume(context, new()); Equal(CommandStatus.Completed, quit.Resume(context, new(OperationInputKind.Choice, 1)).Status);
        Equal(555, w.Score.Total); Equal(true, w.Globals.Status.Scored); Equal(false, w.Globals.Status.Won);
    }

    private static CommandUpdate ReplyLocal(LocalGame game, bool console, CommandUpdate current, int value)
    {
        Decide(game.Player, console, current, value); Equal(true, game.AdvanceOne());
        return game.Player.Result(current.Correlation.Command)!;
    }

    public static (byte[] Initial, byte[] Hall) LocalRetirementReference()
    {
        var source = ClassicFiles.DecodeStoredSave(LocalScoreReference().Continued);
        source = source with
        {
            Globals = source.Globals with { Turn = 80, Year = (short)Calendar.Year(80, new() { FirstLaunchTurn = -1 }, new(), ClassicCalendar) },
            PowerHistory = new(Enumerable.Range(0, 150).Select(i => new OwnedList<byte>(Enumerable.Range(0, 8)
                .Select(c => c == 1 && i < 20 ? (byte)(i < 10 ? i * 12 : (20 - i) * 9) : (byte)0))))
        };
        byte[] initial = ClassicFiles.EncodeStoredSave(source);
        var storage = new BufferedHallStorage(); storage.Install(HallReference());
        using var game = new LocalGame(null, hallStorage: storage);
        // Match the desktop's complete-table read before its later short-file read. Sparse resets
        // retain already-read metadata, including rows beyond that short prefix.
        var viewed = AwaitLocal(game, SubmitLocal(game, false, LocalPlayCommands.Hall, LocalPlayCommands.Hall));
        ReplyLocal(game, false, viewed, 0);
        storage.Install(HallReference()[..30]);
        viewed = AwaitLocal(game, SubmitLocal(game, false, LocalPlayCommands.Hall, LocalPlayCommands.Hall));
        ReplyLocal(game, false, viewed, 0);
        Equal(LocalLoadResult.Loaded, game.LoadClassic(initial));
        var step = AwaitLocal(game, SubmitLocal(game, false, LocalPlayCommands.Retire, LocalPlayCommands.Retire));
        step = ReplyLocal(game, false, step, 1);
        foreach (string title in new[] { "Power history", "Civilization score", "Civilization rating", "Hall of Fame" })
        {
            Equal(true, step.Interaction!.Spec.Prompt.StartsWith(title, StringComparison.Ordinal));
            step = ReplyLocal(game, false, step, 0);
        }
        Equal(CommandStatus.Completed, step.Status); Equal(false, game.Player.ObserveLocalPlay()!.GameActive);
        return (initial, storage.Capture()!);
    }

    private static void LocalRetirementAndQuit()
    {
        foreach (bool console in new[] { false, true })
        {
            var storage = new BufferedHallStorage(); using var game = new LocalGame(null, hallStorage: storage); FoundCouncil(game);
            var before = game.SaveClassic();
            var step = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.Retire, LocalPlayCommands.Retire));
            Equal(CommandStatus.Cancelled, ReplyLocal(game, console, step, 0).Status); BytesEqual(before, game.SaveClassic()); Equal(null, storage.Capture());
            step = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.Retire, LocalPlayCommands.Retire));
            step = ReplyLocal(game, console, step, 1);
            foreach (string title in new[] { "Power history", "Civilization score", "Civilization rating", "Hall of Fame" })
            {
                Equal(true, step.Interaction!.Spec.Prompt.StartsWith(title, StringComparison.Ordinal));
                step = ReplyLocal(game, console, step, 0);
            }
            Equal(CommandStatus.Completed, step.Status); Equal(false, game.Player.ObserveLocalPlay()!.PlayerPhase);
            Equal(false, game.Player.ObserveLocalPlay()!.GameActive); Equal(432, storage.Capture()!.Length);
            var saved = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            var status = StateConversion.DecodeGameStatus(saved.Globals.GameFlags); Equal(false, status.Scored); Equal(false, status.Won);
            var rejected = game.Player.Submit(new(LocalPlayCommands.EndTurn, new([])));
            Equal(CommandStatus.Rejected, rejected.Status);
            Equal(LocalLoadResult.Loaded, game.LoadClassic(before)); Equal(true, game.Player.ObserveLocalPlay()!.GameActive);
            byte[] hall = storage.Capture()!;
            step = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.Quit, LocalPlayCommands.Quit));
            Equal(CommandStatus.Completed, ReplyLocal(game, console, step, 1).Status); BytesEqual(hall, storage.Capture()!);
        }
    }
}
