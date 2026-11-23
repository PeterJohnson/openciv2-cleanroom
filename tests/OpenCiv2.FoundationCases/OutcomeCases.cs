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
    private static OperationContext OutcomeContext(RevelationFixture f, List<string>? effects = null)
    {
        f.World.Multiplayer.LocalHuman = 1;
        f.World.Civilizations[1].Tribe = 0;
        f.Process.Rules.Leaders[0].LeaderName = new("Cedar"); f.Process.Rules.Leaders[0].Plural = new("Alders");
        return new(f.World, f.Process, new GameplayRandom(1, "outcome"), new PresentationRandom(2, "outcome"),
            new(default, new(1)), default, (name, _) => effects?.Add(name), () => { });
    }
    private static readonly OperationInput AcknowledgeOutcome = new(OperationInputKind.Choice, 0);

    private static void SpaceshipArrivalBoundary()
    {
        var f = WorkingFixture(); var w = f.World; w.Multiplayer.LocalHuman = 3; w.Globals.Year = 1999;
        w.Globals.CivsAlive = CivilizationSet.Only(3); w.PendingEndReason = EndReason.Retirement;
        foreach (int c in new[] { 2, 3, 4 }) { w.Civilizations[c].SpaceshipFlags.Launched = true; w.Civilizations[c].SpaceshipArrivalYear = 2000; }
        Equal(null, SpaceshipArrival.Begin(w)); w.Globals.Year = 2000;
        var first = SpaceshipArrival.Begin(w)!; Equal(2, first.Civilization); Equal(true, first.ShowLanding);
        Equal(true, w.Civilizations[2].SpaceshipFlags.Arrived); Equal(true, w.Civilizations[2].SpaceshipFlags.FirstArrivalVictory);
        Equal(false, w.Globals.Status.Won); Equal(2, w.SpaceshipWinner); Equal(EndReason.OtherSpace, w.PendingEndReason);
        SpaceshipArrival.Complete(w); Equal(true, w.Globals.Status.Won);
        var second = SpaceshipArrival.Begin(w)!; Equal(3, second.Civilization); Equal(false, second.ShowLanding);
        Equal(false, w.Civilizations[3].SpaceshipFlags.FirstArrivalVictory); Equal(2, w.SpaceshipWinner); Equal(EndReason.OtherSpace, w.PendingEndReason);
        SpaceshipArrival.Complete(w); w.PendingEndReason = EndReason.None; w.Multiplayer.Saved.AutoplayActive = true;
        var third = SpaceshipArrival.Begin(w)!; Equal(4, third.Civilization); Equal(false, third.ShowLanding); SpaceshipArrival.Complete(w);
        Equal(null, SpaceshipArrival.Begin(w)); Equal(2, w.SpaceshipWinner); Equal(EndReason.None, w.PendingEndReason);
        w.Civilizations[3].SpaceshipFlags.Arrived = false; w.Multiplayer.Saved.AutoplayActive = false;
        Equal(true, SpaceshipArrival.Begin(w)!.ShowLanding); // Won local ship with no pending reason still gets its notice.
        var fresh = new GameState(f.Process); Equal(0, fresh.SpaceshipWinner);
        f = WorkingFixture(); var context = OutcomeContext(f); w = f.World; w.Globals.Year = 2020;
        w.Civilizations[2].SpaceshipFlags.Launched = true; w.Civilizations[2].SpaceshipArrivalYear = 2020;
        var operation = new SpaceshipArrivalOperation();
        Equal(CommandStatus.AwaitingInteraction, operation.Resume(context, new()).Status);
        Equal(false, w.Globals.Status.Won); Equal(EndReason.OtherSpace, w.PendingEndReason);
        Equal(CommandStatus.Completed, operation.Resume(context, AcknowledgeOutcome).Status); Equal(true, w.Globals.Status.Won);
        f = WorkingFixture(); context = OutcomeContext(f); w = f.World; w.Globals.Year = 2020;
        w.Civilizations[1].SpaceshipFlags.Launched = true; w.Civilizations[1].SpaceshipArrivalYear = 2020;
        Equal(CommandStatus.Completed, new SpaceshipArrivalOperation().Resume(context, new()).Status);
        Equal(EndReason.LocalSpace, w.PendingEndReason); Equal(1, w.SpaceshipWinner); Equal(true, w.Globals.Status.Won);
    }

    private static void ScheduledRetirementEqualities()
    {
        var f = WorkingFixture(); var w = f.World; var effects = new List<string>(); var context = OutcomeContext(f, effects);
        foreach (short year in new short[] { 1999, 2001, 2019, 2021 })
        { w.Globals.Year = year; Equal(CommandStatus.Completed, new ScheduledRetirementOperation().Resume(context, new()).Status); }
        w.Globals.Year = 2000; w.Multiplayer.Saved.AutoplayActive = true;
        for (int i = 0; i < 2; ++i)
        {
            var warning = new ScheduledRetirementOperation(); Equal(true, warning.Resume(context, new()).Interaction!.Prompt.StartsWith("Retirement warning", StringComparison.Ordinal));
            Equal(CommandStatus.Completed, warning.Resume(context, AcknowledgeOutcome).Status);
            Equal(false, w.Globals.Status.Won); Equal(EndReason.None, w.PendingEndReason);
        }
        Equal(2, effects.Count);
        w.Globals.Status.Scenario = true; w.Scenario.MaximumTurns = 10; w.Globals.Year = 123; w.Globals.Turn = 5;
        var scenarioWarning = new ScheduledRetirementOperation(); Equal(true, scenarioWarning.Resume(context, new()).Interaction!.Prompt.StartsWith("Scenario warning", StringComparison.Ordinal));
        scenarioWarning.Resume(context, AcknowledgeOutcome);
        w.Globals.Status.Scenario = false; w.Globals.Status.ScenarioExport = true;
        Equal(CommandStatus.Completed, new ScheduledRetirementOperation().Resume(context, new()).Status);
        w.Globals.Status.Scenario = true; w.Globals.Turn = 10;
        var end = new ScheduledRetirementOperation(); Equal(true, end.Resume(context, new()).Interaction!.Prompt.StartsWith("Scenario ended", StringComparison.Ordinal));
        Equal(false, w.Globals.Status.Won); Equal(EndReason.None, w.PendingEndReason);
        end.Resume(context, AcknowledgeOutcome); Equal(true, w.Globals.Status.Won); Equal(EndReason.Retirement, w.PendingEndReason);
        w.Civilizations[1].Tribe = short.MaxValue; // Early guards must not look up ruler text.
        Equal(CommandStatus.Completed, new ScheduledRetirementOperation().Resume(context, new()).Status);
        w.Globals.Status.Won = false; w.PendingEndReason = EndReason.Conquest;
        Equal(CommandStatus.Completed, new ScheduledRetirementOperation().Resume(context, new()).Status);
        w.Civilizations[1].Tribe = 0; w.PendingEndReason = EndReason.None; w.Scenario.MaximumTurns = 0; w.Globals.Year = 2020;
        end = new(); Equal(true, end.Resume(context, new()).Interaction!.Prompt.StartsWith("Scheduled retirement", StringComparison.Ordinal));
        end.Resume(context, AcknowledgeOutcome); Equal(true, w.Globals.Status.Won);
    }

    private static OperationStep ReadOutcomeReports(PendingOutcomeOperation operation, OperationContext context, OperationStep step)
    {
        foreach (string title in new[] { "Power history", "Civilization score", "Civilization rating", "Hall of Fame" })
        {
            Equal(CommandStatus.AwaitingInteraction, step.Status);
            Equal(true, step.Interaction!.Prompt.StartsWith(title, StringComparison.Ordinal));
            step = operation.Resume(context, AcknowledgeOutcome);
        }
        return step;
    }

    private static void PendingOutcomeContinuations()
    {
        foreach (int year in new[] { 123, 2020 })
        {
            var f = WorkingFixture(); var context = OutcomeContext(f); var w = f.World;
            w.PendingEndReason = EndReason.Retirement; w.Globals.Status.Won = true; w.Globals.Year = (short)year;
            using var operation = new PendingOutcomeOperation(new(new BufferedHallStorage()));
            var step = ReadOutcomeReports(operation, context, operation.Resume(context, new()));
            if (year == 2020)
            {
                Equal("Keep playing?", step.Interaction!.Prompt); Equal(false, w.Globals.Status.Scored); Equal(EndReason.Retirement, w.PendingEndReason);
                step = operation.Resume(context, new(OperationInputKind.Choice, -3)); // Any nonzero trusted result continues.
                Equal(true, w.Globals.Status.Scored);
            }
            else { Equal(false, w.Globals.Status.Scored); }
            Equal(CommandStatus.Completed, step.Status); Equal(false, operation.Quit); Equal(EndReason.None, w.PendingEndReason); Equal(true, w.Globals.Status.Won);
        }
        var live = WorkingFixture(); var ctx = OutcomeContext(live); var world = live.World;
        world.PendingEndReason = EndReason.Retirement; world.Globals.Year = 2020;
        using var changed = new PendingOutcomeOperation(new(new BufferedHallStorage()));
        var next = changed.Resume(ctx, new());
        for (int i = 0; i < 3; ++i) { next = changed.Resume(ctx, AcknowledgeOutcome); }
        Equal(true, next.Interaction!.Prompt.StartsWith("Hall of Fame", StringComparison.Ordinal));
        world.Globals.Year = 2021;
        Equal(CommandStatus.Completed, changed.Resume(ctx, AcknowledgeOutcome).Status); Equal(false, world.Globals.Status.Scored);
        world.PendingEndReason = EndReason.None;
        using var none = new PendingOutcomeOperation(new(new BufferedHallStorage()));
        next = ReadOutcomeReports(none, ctx, none.Resume(ctx, new())); Equal(CommandStatus.Completed, next.Status); Equal(false, world.Globals.Status.Scored);
        world.PendingEndReason = EndReason.Retirement; world.Globals.Year = 2020;
        using var failure = new PendingOutcomeOperation(new(new CallbackStorage()));
        next = ReadOutcomeReports(failure, ctx, failure.Resume(ctx, new())); Equal("Keep playing?", next.Interaction!.Prompt);
        next = failure.Resume(ctx, AcknowledgeOutcome); Equal(CommandStatus.Failed, next.Status); Equal("hall_output_failed", next.Code);
        Equal(true, failure.Quit); Equal(true, world.Globals.Status.Scored); Equal(EndReason.None, world.PendingEndReason);
        live = WorkingFixture(); ctx = OutcomeContext(live); world = live.World;
        world.PendingEndReason = EndReason.Retirement; world.Globals.Year = 2020;
        using var changedMode = new PendingOutcomeOperation(new(new BufferedHallStorage()));
        next = changedMode.Resume(ctx, new());
        for (int i = 0; i < 3; ++i) { next = changedMode.Resume(ctx, AcknowledgeOutcome); }
        world.Globals.Multiplayer = (GameMode)1; // The final decision observes mode after the real report children.
        next = changedMode.Resume(ctx, AcknowledgeOutcome);
        Equal(CommandStatus.Failed, next.Status); Equal("single_player_required", next.Code); // The single-player Hall reports the mode change.
        Equal(true, changedMode.Quit); Equal(false, world.Globals.Status.Scored); Equal(EndReason.None, world.PendingEndReason);
    }

    private static void PendingOutcomePresentations()
    {
        var f = WorkingFixture(); var context = OutcomeContext(f); var w = f.World;
        w.PendingEndReason = EndReason.Conquest; w.Globals.Status.Scored = true;
        w.Eliminations.Count = 3; w.Eliminations.Killers[0] = 1; w.Eliminations.Killers[1] = 2; w.Eliminations.Killers[2] = 1;
        var names = w.Eliminations.Names.ToArray(); names[0] = "Birches"; names[1] = "Willows"; names[2] = "Cedars"; w.Eliminations.Names = new(names);
        using var conquest = new PendingOutcomeOperation(new(new BufferedHallStorage()));
        var step = conquest.Resume(context, new()); Equal(true, step.Interaction!.Prompt.Contains("Birches", StringComparison.Ordinal)); Equal(false, step.Interaction.Prompt.Contains("Willows", StringComparison.Ordinal));
        step = conquest.Resume(context, AcknowledgeOutcome); Equal(true, step.Interaction!.Prompt.StartsWith("Power history", StringComparison.Ordinal));
        step = conquest.Resume(context, AcknowledgeOutcome); Equal("Keep playing?", step.Interaction!.Prompt);
        conquest.Resume(context, new(OperationInputKind.Choice, 1)); Equal(false, conquest.Quit); Equal(false, w.Globals.Status.Won);
        w.PendingEndReason = EndReason.OtherSpace; w.SpaceshipWinner = 2; w.Civilizations[2].Tribe = 1;
        f.Process.Rules.Leaders[1].LeaderName = new("Rowan"); f.Process.Rules.Leaders[1].Adjective = new("Cedarian");
        w.Civilizations[2].SpaceshipLaunchYear = 1970; w.Civilizations[2].SpaceshipArrivalYear = 2001; w.Civilizations[2].SpaceshipPopulation = 4;
        using var space = new PendingOutcomeOperation(new(new BufferedHallStorage()));
        step = space.Resume(context, new()); Equal(true, step.Interaction!.Prompt.StartsWith("Another civilization", StringComparison.Ordinal));
        Equal(true, step.Interaction.Prompt.Contains("Cedar\n", StringComparison.Ordinal)); Equal(false, step.Interaction.Prompt.Contains("Rowan", StringComparison.Ordinal));
        Equal(true, step.Interaction.Prompt.Contains("40,000", StringComparison.Ordinal));
        space.Resume(context, AcknowledgeOutcome); space.Resume(context, AcknowledgeOutcome); space.Resume(context, AcknowledgeOutcome); Equal(true, space.Quit);
        w.PendingEndReason = EndReason.Defeat; w.Globals.Status.Scored = false; w.Civilizations[1].Advances = default;
        using var defeat = new PendingOutcomeOperation(new(new BufferedHallStorage()));
        step = defeat.Resume(context, new()); Equal(true, step.Interaction!.Prompt.StartsWith("Defeat", StringComparison.Ordinal)); Equal(false, step.Interaction.Prompt.Contains("Known advance", StringComparison.Ordinal));
        step = ReadOutcomeReports(defeat, context, defeat.Resume(context, AcknowledgeOutcome));
        Equal(CommandStatus.Completed, step.Status); Equal(true, defeat.Quit); Equal(true, w.Globals.Status.Scored); Equal(EndReason.None, w.PendingEndReason);
    }

    private static byte[] ScheduledInitial(int year)
    {
        var source = ClassicFiles.DecodeStoredSave(LocalScoreReference().Continued);
        int turn = Enumerable.Range(1, 1000).First(t => Calendar.Year(t, new() { FirstLaunchTurn = -1 }, new(), ClassicCalendar) == year) - 1;
        var civs = source.Civilizations.ToArray(); civs[1] = civs[1] with { Gold = 70000 };
        return ClassicFiles.EncodeStoredSave(source with
        {
            Civilizations = new(civs),
            Globals = source.Globals with
            { Turn = (short)turn, Year = (short)Calendar.Year(turn, new() { FirstLaunchTurn = -1 }, new(), ClassicCalendar) }
        });
    }

    public static (byte[] Warning, byte[] Warned, byte[] End, byte[] Continued, byte[] Hall) LocalScheduledReference()
    {
        byte[] warning = ScheduledInitial(2000), end = ScheduledInitial(2020);
        var storage = new BufferedHallStorage(); using var game = new LocalGame(null, hallStorage: storage);
        Equal(LocalLoadResult.Loaded, game.LoadClassic(warning));
        var step = AwaitLocal(game, SubmitLocal(game, false, LocalPlayCommands.EndTurn, LocalPlayCommands.EndTurn));
        step = ReplyLocal(game, false, step, 1); Equal(true, step.Interaction!.Spec.Prompt.StartsWith("Retirement warning", StringComparison.Ordinal));
        Equal(70000, game.Player.ObserveLocalPlay()!.Gold);
        step = ReplyLocal(game, false, step, 0); Equal(CommandStatus.Completed, step.Status); byte[] warned = game.SaveClassic();
        Equal(LocalLoadResult.Loaded, game.LoadClassic(end));
        step = AwaitLocal(game, SubmitLocal(game, false, LocalPlayCommands.EndTurn, LocalPlayCommands.EndTurn));
        step = ReplyLocal(game, false, step, 1); Equal(true, step.Interaction!.Spec.Prompt.StartsWith("Scheduled retirement", StringComparison.Ordinal));
        foreach (string title in new[] { "Power history", "Civilization score", "Civilization rating", "Hall of Fame", "Keep playing?" })
        {
            step = ReplyLocal(game, false, step, 0); Equal(true, step.Interaction!.Spec.Prompt.StartsWith(title, StringComparison.Ordinal));
        }
        Equal(70000, game.Player.ObserveLocalPlay()!.Gold);
        step = ReplyLocal(game, false, step, 1); Equal(CommandStatus.Completed, step.Status);
        return (warning, warned, end, game.SaveClassic(), storage.Capture()!);
    }

    private static void LocalScheduledRetirement()
    {
        foreach (bool console in new[] { false, true })
            foreach (bool keep in new[] { false, true })
            {
                var storage = new BufferedHallStorage(); using var game = new LocalGame(null, hallStorage: storage);
                Equal(LocalLoadResult.Loaded, game.LoadClassic(ScheduledInitial(2020)));
                var before = game.Player.ObserveLocalPlay()!;
                var step = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.EndTurn, LocalPlayCommands.EndTurn));
                step = ReplyLocal(game, console, step, 1); Equal(true, step.Interaction!.Spec.Prompt.StartsWith("Scheduled retirement", StringComparison.Ordinal));
                foreach (string title in new[] { "Power history", "Civilization score", "Civilization rating", "Hall of Fame", "Keep playing?" })
                {
                    step = ReplyLocal(game, console, step, 0); Equal(true, step.Interaction!.Spec.Prompt.StartsWith(title, StringComparison.Ordinal));
                }
                var pending = game.Player.ObserveLocalPlay()!; Equal(2020, pending.Year); Equal(70000, pending.Gold);
                Equal(before.City!.Food, pending.City!.Food); Equal(before.City.Shields, pending.City.Shields);
                step = ReplyLocal(game, console, step, keep ? 1 : 0); Equal(CommandStatus.Completed, step.Status);
                var play = game.Player.ObserveLocalPlay()!; Equal(keep, play.GameActive); Equal(keep, play.PlayerPhase);
                Equal(keep, step.Committed.Any(e => e.Name == "turn.advanced"));
                var saved = game.SaveClassic(); var state = StateConversion.DecodeGameStatus(ClassicFiles.DecodeStoredSave(saved).Globals.GameFlags);
                Equal(true, state.Won); Equal(true, state.Scored); Equal(432, storage.Capture()!.Length);
                if (keep)
                {
                    Equal(LocalLoadResult.Loaded, game.LoadClassic(saved));
                    var next = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.EndTurn, LocalPlayCommands.EndTurn));
                    next = ReplyLocal(game, console, next, 1); Equal(CommandStatus.Completed, next.Status); // No repeated end sequence after Won.
                }
            }
    }

    private static void LocalFamineDefeat()
    {
        using var game = new LocalGame(); FoundCouncil(game);
        var source = ClassicFiles.DecodeStoredSave(game.SaveClassic());
        var city = source.Cities.Single();
        var specialists = new SpecialistArray<SpecialistKind>(); specialists[0] = SpecialistKind.Entertainer;
        city = city with
        {
            FoodBox = -1,
            WorkedTiles = StateConversion.EncodeCityWork(default(CityWorkSet).With(20), 1),
            SpecialistKinds = StateConversion.EncodeSpecialists(specialists)
        };
        var tiles = source.Map.Tiles.ToArray();
        var geometry = new Geometry(source.Map.Header.Width, source.Map.Header.Height);
        int center = geometry.TileIndex(new(city.X, city.Y))!.Value;
        tiles[center] = tiles[center] with { Features = (byte)(tiles[center].Features | 0x80) };
        var initial = ClassicFiles.EncodeStoredSave(source with { Cities = new([city]), Map = source.Map with { Tiles = new(tiles) } });
        Equal(LocalLoadResult.Loaded, game.LoadClassic(initial));
        var step = AwaitLocal(game, SubmitLocal(game, false, LocalPlayCommands.EndTurn, LocalPlayCommands.EndTurn));
        step = ReplyLocal(game, false, step, 1);
        foreach (string title in new[] { "Defeat", "Power history", "Civilization score", "Civilization rating", "Hall of Fame" })
        {
            Equal(true, step.Interaction!.Spec.Prompt.StartsWith(title, StringComparison.Ordinal));
            step = ReplyLocal(game, false, step, 0);
        }
        Equal(CommandStatus.Completed, step.Status); Equal(false, game.Player.ObserveLocalPlay()!.GameActive);
        Equal(0, game.Player.OwnCities().Records.Count); Equal(0, game.Player.OwnUnits().Records.Count);
        Equal(true, step.Committed.Any(e => e.Name == "game.outcome.handled"));
        Equal(false, step.Committed.Any(e => e.Name == "turn.advanced"));
    }
}
