using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Content;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static CivilizationScore ScoreFor(RevelationFixture f, ScoringValues? values = null) => new(f.World,
        values ?? GameplayContent.Builtin.Values.Scoring, new(f.World, f.Rules, ClassicShip, ClassicCalendar), new(f.World, f.Process.Rules, f.Stacks));

    private static void ScoreComponentsAndRawWonders()
    {
        var f = WorkingFixture(); var w = f.World; var score = ScoreFor(f);
        w.Entities.Cities.Storage[0].Size = 4; w.Entities.Cities.Storage[0].Happy = 255; w.Entities.Cities.Storage[0].Unhappy = 128;
        w.Entities.Cities.Storage[8] = new() { Owner = new(1) }; w.Globals.WonderCity[0] = 8; // Dead and beyond End, but backed.
        w.Globals.PollutedTiles = 1; w.Globals.WarmingPollution = 3;
        w.Globals.Turn = 200; w.Globals.PeaceTurns = 40; w.Globals.BarbarianActivity = 3;
        w.Civilizations[1].FutureTechs = 255;
        Equal(1571, score.Calculate(1)); // Citizens131 + wonder20 + pollution20 + peace100 + barbarians25 + future1275.
        Equal(131, w.Score.Citizens); Equal(20, w.Score.Wonders); Equal(0, w.Score.ConquestBonus);
        Equal(false, w.Globals.Status.Scored);
        w.Globals.Turn = 199; score.Calculate(1); Equal(0, w.Score.Peace);
        w.Globals.Turn = 200; w.Globals.PeaceTurns = -1; score.Calculate(1); Equal(0, w.Score.Peace);
        w.Civilizations[1].FutureTechs = 0; w.Globals.WonderCity[0] = -2; w.Globals.PollutedTiles = 100;
        Equal(0, score.Calculate(1)); // Ordinary zero floor only.
        w.Score.CivObjectives = 77; w.Score.Rating = 99;
        score.Calculate(1); Equal(77, w.Score.CivObjectives); Equal(99, w.Score.Rating);
        w.Globals.WonderCity[1] = short.MaxValue;
        Throws<IndexOutOfRangeException>(() => score.Calculate(1)); Equal(131, w.Score.Citizens); Equal(0, w.Score.Wonders);
    }

    private static void ScoreSpaceshipAndConquest()
    {
        var f = new ShipFixture(); f.Parts(39, 8, 8, 4, 4, 4); var w = f.World; var score = ScoreFor(f.Data);
        w.Globals.BarbarianActivity = 2;
        Equal(0, score.Calculate(1)); Equal(400, w.Score.SpaceshipInFlight); Equal((short)4, w.Civilizations[1].SpaceshipPopulation);
        w.Civilizations[1].SpaceshipFlags.FirstArrivalVictory = true;
        Equal(400, score.Calculate(1)); Equal(0, w.Score.SpaceshipInFlight); Equal(400, w.Score.SpaceshipArrived);
        Equal(false, w.Civilizations[1].SpaceshipFlags.Exists); Equal(false, w.Civilizations[1].SpaceshipFlags.Launched);
        w.Globals.CivsAlive = CivilizationSet.Only(0).With(1); w.Globals.NumEnemies = 6;
        w.Globals.Turn = 1; w.Globals.Rules.NoRestart = true;
        Equal(1640, score.Calculate(1)); Equal(400, w.Score.Total); Equal(1640, w.Score.ConquestBonus);
        w.Globals.Status.LargeMap = true; Equal(2050, score.Calculate(1));
        w.Globals.Status.SmallMap = true; Equal(1640, score.Calculate(1));
        w.Globals.Status.LargeMap = false; Equal(1312, score.Calculate(1));
        w.Globals.CivsAlive = w.Globals.CivsAlive.With(2); Equal(400, score.Calculate(1));
        w.Globals.CivsAlive = CivilizationSet.Only(1); w.Globals.Turn = 2000;
        Equal(400, score.Calculate(1)); Equal(-1488, w.Score.ConquestBonus);
    }

    private static void ObjectiveScoringAndPerspective()
    {
        var f = WorkingFixture(); var w = f.World; var score = ScoreFor(f);
        f.AddCity(1, 2, new(18, 10), 4);
        w.Entities.Cities.Storage[0].Flags.Objective = true; w.Entities.Cities.Storage[1].Flags.Objective = true;
        w.Globals.WonderCity[0] = 0; w.Globals.WonderCity[1] = 0; w.Scenario.Flags.WonderObjectives = true;
        Equal(3, score.ObjectiveValue(new(0))); // Independent of Scenario/objective mode.
        w.Entities.Cities.Storage[0].Token = default; Equal(3, score.ObjectiveValue(new(0)));
        w.Entities.Cities.Storage[0].Token = new(1);
        w.Globals.Status.Scenario = true; w.Scenario.Flags.ObjectiveVictory = true; w.Scenario.ObjectiveCiv = 2;
        w.Scenario.VictoryThresholds[0] = 4; w.Scenario.VictoryThresholds[1] = 3; w.Scenario.VictoryThresholds[2] = 1; w.Scenario.VictoryThresholds[3] = 0;
        Equal(780, score.Calculate(1)); Equal(ObjectiveOutcome.MarginalDefeat, w.Score.ObjectiveOutcome);
        Equal(4, w.Score.TotalObjectives); Equal(1, w.Score.ObjectiveCivValue); Equal(3, w.Score.CivObjectives); Equal(-1, w.Score.ConquestBonus);
        w.Scenario.ObjectiveCiv = 0; Equal(780, score.Calculate(1)); Equal(ObjectiveOutcome.MarginalVictory, w.Score.ObjectiveOutcome);
        w.Globals.CivsAlive = CivilizationSet.Only(1); Equal(1560, score.Calculate(1));
        w.Scenario.VictoryThresholds[0] = 0; w.Scenario.VictoryThresholds[1] = -2; w.Scenario.VictoryThresholds[2] = 20; w.Scenario.VictoryThresholds[3] = 30;
        Equal(2060, score.Calculate(1)); Equal(ObjectiveOutcome.DecisiveVictory, w.Score.ObjectiveOutcome); // No sorting.
        var negative = ScoreFor(f, GameplayContent.Builtin.Values.Scoring with { PointsPerObjective = -1000 });
        Equal(-4000, negative.Calculate(1)); Equal(-4000, negative.Prepare(1)!.Score); // No ordinary floor.
        w.Globals.Status.Scenario = false; w.Globals.Status.ScenarioExport = true;
        negative.Calculate(1); Equal(0, w.Score.ScenarioPoints); Equal(3, w.Score.CivObjectives);
    }

    private static void ScoreRatingAndWrappedValues()
    {
        var f = WorkingFixture(); var w = f.World; var score = ScoreFor(f);
        w.Score.Total = 1000; w.Score.ConquestBonus = 0;
        foreach (var (difficulty, expected) in new[] { (Difficulty.Chieftain, 40), (Difficulty.Warlord, 50), (Difficulty.Prince, 60), (Difficulty.King, 80), (Difficulty.Emperor, 100), (Difficulty.Deity, 130) })
        {
            w.Globals.Difficulty = difficulty; score.PrepareRating(); Equal(expected, w.Score.Rating);
        }
        w.Globals.Difficulty = Difficulty.Chieftain; w.Score.Total = 4800; score.PrepareRating(); Equal(192, w.Score.Rating); Equal(23, w.Score.TitleIndex);
        w.Score.Total = int.MaxValue; score.PrepareRating(); Equal(0, w.Score.Rating); Equal(0, w.Score.TitleIndex); // Wrapped -4 / 100.
        w.Score.Total = -1000; w.Score.ConquestBonus = -2000; score.PrepareRating(); Equal(-40, w.Score.Rating); Equal(0, w.Score.TitleIndex);
        var values = GameplayContent.Builtin.Values.Scoring with { PerCitizen = int.MaxValue, PerHappyCitizen = 0, PerUnhappyCitizen = 0 };
        w.Entities.Cities.Storage[0].Size = 2; w.Globals.BarbarianActivity = 2;
        Equal(0, ScoreFor(f, values).Calculate(1)); Equal(-2, w.Score.Citizens);
    }

    private static void ScoreReportPreparation()
    {
        var f = WorkingFixture(); var w = f.World; var score = ScoreFor(f);
        f.AddCity(1, 1, new(20, 10), 4); w.Entities.Cities.Storage[0].Size = 4;
        for (int i = 0; i < 2; ++i) { w.Entities.Cities.Storage[i].Happy = 127; w.Entities.Cities.Storage[i].Unhappy = 128; }
        w.Globals.WonderCity[0] = 8; w.Entities.Cities.Storage[8] = new() { Owner = new(1) };
        score.Calculate(1); Equal(518, w.Score.Citizens);
        var report = score.Prepare(1)!;
        Equal(false, report.Citizens == 518); Equal(20, report.Wonders); Equal(0, report.VisibleWonders.Count);
        Equal(false, w.Entities.Cities.Storage[1].Happy == 127); // Recalculates every city, independent of layout.
        int preparedCitizens = report.Citizens;
        w.Entities.Cities.Storage[0].Happy = 127; Equal(preparedCitizens, report.Citizens); // Owned, stable presentation.
        w.Globals.Status.Scored = true; w.Score.Total = 4321;
        Equal(null, score.Prepare(1)); Equal(4321, w.Score.Total); Equal((byte)127, w.Entities.Cities.Storage[0].Happy);
        w.Globals.Status.Scored = false; w.Globals.Status.Scenario = true; w.Scenario.Flags.ObjectiveVictory = true;
        score.Prepare(1); Equal((byte)127, w.Entities.Cities.Storage[0].Happy); // Objective report bypasses C03.
    }

    public static (byte[] Saved, byte[] Continued) LocalScoreReference()
    {
        byte[] initial = LocalPopulationReference().Initial;
        using var game = new LocalGame(); FoundCouncil(game);
        Equal(LocalLoadResult.Loaded, game.LoadClassic(initial)); EndLocalTurn(game, false, 2);
        Equal(LocalLoadResult.Loaded, game.LoadClassic(game.SaveClassic())); EndLocalTurn(game, false, 3);
        var report = AwaitLocal(game, SubmitLocal(game, false, LocalPlayCommands.Score, LocalPlayCommands.Score));
        Decide(game.Player, false, report, 0); Equal(true, game.AdvanceOne());
        Equal(CommandStatus.Completed, game.Player.Result(report.Correlation.Command)!.Status);
        byte[] saved = game.SaveClassic();
        Equal(LocalLoadResult.Loaded, game.LoadClassic(saved)); EndLocalTurn(game, false, 4);
        return (saved, game.SaveClassic());
    }

    private static void LocalScoreDecisions()
    {
        var recorder = new LocalReplayRecorder(new string('a', 64));
        foreach (bool console in new[] { false, true })
        {
            using var game = new LocalGame(console ? null : recorder); FoundCouncil(game);
            var opening = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.Score, LocalPlayCommands.Score));
            Equal(true, opening.Interaction!.Spec.Prompt.StartsWith("Civilization score", StringComparison.Ordinal));
            Equal(1, opening.Committed.Count(e => e.Name == "civilization.score.prepared"));
            var effect = opening.Committed.Single(); Equal(true, effect.Values.Any(v => v.Name == "rating"));
            Decide(game.Player, console, opening, 0); Equal(true, game.AdvanceOne());
            var closed = game.Player.Result(opening.Correlation.Command)!; Equal(CommandStatus.Completed, closed.Status);
            Equal(1, closed.Committed.Count); Equal(3, closed.Result.Count);
            if (!console) BytesEqual(game.SaveClassic(), LocalReplay.Run(recorder.Capture()));
            EndLocalTurn(game, console, 2);
            var stored = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            var flags = StateConversion.DecodeGameStatus(stored.Globals.GameFlags); flags.Scored = true;
            var scored = stored with { Globals = stored.Globals with { GameFlags = StateConversion.EncodeGameStatus(flags) } };
            Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(scored)));
            var again = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.Score, LocalPlayCommands.Score));
            Equal("This game has already been scored.", again.Interaction!.Spec.Prompt); Equal(0, again.Committed.Count);
            Decide(game.Player, console, again, 0); Equal(true, game.AdvanceOne()); Equal(CommandStatus.Completed, game.Player.Result(again.Correlation.Command)!.Status);
        }
    }
}
