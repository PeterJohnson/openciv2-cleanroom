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
    private static void TreasuryAndPopulationQueries()
    {
        var f = WorkingFixture();
        foreach (var (input, expected) in new[] { (int.MinValue, 30000), (-16385, 30000), (-16384, 0), (-1, 0), (0, 0), (30000, 30000), (30001, 30000), (int.MaxValue, 30000) })
        {
            f.World.Civilizations[1].Gold = input;
            Equal(expected, CivilizationFinances.NormalizeTreasury(f.World, 1));
            Equal(expected, f.World.Civilizations[1].Gold);
        }
        foreach (var (size, expected) in new[] { (-128, 1), (-1, 1), (0, 1), (1, 1), (2, 3), (4, 10), (13, 91), (14, 105), (127, 8128) })
        {
            var city = new City { Size = (sbyte)size }; // Token deliberately empty.
            Equal(expected, CivilizationFinances.CityPopulation(city));
        }
        f.World.Entities.Cities.SetEnd(5);
        for (int i = 0; i < 5; ++i)
            f.World.Entities.Cities.Storage[i] = new() { Token = new((uint)(i + 1)), Owner = new(1), Size = 127 };
        Equal(32000, CivilizationFinances.Population(f.World, 1));
        f.World.Entities.Cities.Storage[4].Owner = new(255);
        Equal(8128, CivilizationFinances.Population(f.World, -1));
        Equal(1, CivilizationFinances.Population(f.World, 255));
        f.World.Entities.Cities.Storage[4].Token = default;
        Equal(1, CivilizationFinances.Population(f.World, -1));
        Equal(1, CivilizationFinances.Population(f.World, 2));
    }

    private static void PopulationMilestonesAndFunds()
    {
        var f = WorkingFixture(); var world = f.World; ref var civ = ref world.Civilizations[1];
        world.TurnLoopActive = true; civ.CityCount = 2; civ.Gold = 90;
        foreach (var (size, old, milestone, rounded) in new[] { (4, 0, 1, 10), (13, 0, 9, 90), (14, 9, 10, 100), (1, -1, 0, 0) })
        {
            world.Entities.Cities.Storage[0].Size = (sbyte)size; civ.PopulationMilestone = (short)old;
            var operation = new CivilizationFinances.Operation(world, 1, 100);
            Equal(new CivilizationNotice(CivilizationNoticeKind.Population, rounded), operation.Next());
            Equal((short)milestone, civ.PopulationMilestone);
            Equal(operation.Next(), operation.Next()); // Pending notices do not recalculate.
            operation.Reply(0);
            Equal(CivilizationNoticeKind.Funds, operation.Next()!.Kind);
            operation.Reply(-7); // Every nonzero response opens the actual rates child.
            Equal(CivilizationNoticeKind.Rates, operation.Next()!.Kind);
            Equal(false, operation.Completed);
            operation.Reply(0); Equal(null, operation.Next()); Equal(true, operation.Completed);
        }
        civ.PopulationMilestone = 10; world.Entities.Cities.Storage[0].Size = 4;
        var falling = new CivilizationFinances.Operation(world, 1, 90);
        Equal(null, falling.Next()); Equal((short)10, civ.PopulationMilestone);
        foreach (int gate in Enumerable.Range(0, 5))
        {
            world.Globals.HumanPlayers = default(CivilizationSet).With(1); world.Multiplayer.Saved.AutoplayActive = false;
            world.TurnLoopActive = true; civ.CityCount = 2; civ.Gold = 90; civ.PopulationMilestone = 0;
            var operation = new CivilizationFinances.Operation(world, 1, 100);
            Equal(CivilizationNoticeKind.Population, operation.Next()!.Kind);
            switch (gate)
            {
                case 0: world.Globals.HumanPlayers = default; break;
                case 1: world.Multiplayer.Saved.AutoplayActive = true; break;
                case 2: world.TurnLoopActive = false; break;
                case 3: civ.CityCount = -1; break;
                case 4: civ.Gold = 100; break;
            }
            operation.Reply(0); Equal(null, operation.Next());
        }
        world.Globals.HumanPlayers = default; civ.PopulationMilestone = 0;
        Equal(null, new CivilizationFinances.Operation(world, 1, 100).Next());
        Equal((short)1, civ.PopulationMilestone); // AI updates silently.
        world.Globals.HumanPlayers = default(CivilizationSet).With(1); world.Multiplayer.Saved.AutoplayActive = false;
        world.TurnLoopActive = true; civ.CityCount = 2; civ.Gold = 0;
        // Wrapped arithmetic differs from a widened debt forecast.
        Equal(null, new CivilizationFinances.Operation(world, 1, int.MaxValue).Next());
        var zero = new CivilizationFinances.Operation(world, 1, 1);
        Equal(CivilizationNoticeKind.Funds, zero.Next()!.Kind); zero.Reply(0); Equal(true, zero.Completed);
    }

    private static void FinanceRateContinuation()
    {
        foreach (bool failOutput in new[] { false, true })
        {
            var f = WorkingFixture(); var world = f.World; ref var civ = ref world.Civilizations[1];
            world.Globals.TurnCiv = 1; world.TurnLoopActive = true; world.PlayerPhaseActive = false;
            world.Globals.Turn = 2; civ.Government = Government.Despotism;
            civ.CityCount = 2; civ.Gold = 90; civ.PopulationMilestone = 100; civ.TaxRate = 4; civ.ScienceRate = 6;
            var effects = new List<string>();
            var context = new OperationContext(world, f.Process, new GameplayRandom(1, "finance"), new PresentationRandom(2, "finance"),
                new(default, new(1)), default, (name, _) => effects.Add(name), () => { });
            var buffered = new BufferedPreferenceWriter();
            var operation = new CivilizationFinanceOperation(failOutput ? new FailedRatePreferences() : buffered, new(), 100);
            var notice = operation.Resume(context, new());
            Equal(false, notice.Interaction!.Cancellable);
            Equal("Edit rates", notice.Interaction.Options[1].Label);
            OperationStep Reply(int value) => operation.Resume(context, new(OperationInputKind.Choice, value));
            Equal(true, Reply(1).Interaction!.Prompt.StartsWith("Tax ", StringComparison.Ordinal));
            Reply(2); Reply(6); var completed = Reply(1);
            Equal(failOutput ? CommandStatus.Failed : CommandStatus.Completed, completed.Status);
            Equal(failOutput ? "preferences_output_failed" : "", completed.Code);
            Equal((byte)6, civ.TaxRate); Equal((byte)4, civ.ScienceRate);
            Equal(1, effects.Count(e => e == "civilization.funds.notice"));
            Equal(1, effects.Count(e => e == "civilization.rates"));
            if (!failOutput) Equal((short)6, ClassicPreferences.DecodeOrdinary(buffered.Capture().AsSpan(0, ClassicPreferences.OrdinarySize)).Tax);
            var cancelled = new CivilizationFinanceOperation(buffered, new(), 100);
            cancelled.Resume(context, new()); cancelled.Resume(context, new(OperationInputKind.Choice, 1));
            Equal(CommandStatus.Completed, cancelled.Resume(context, new(OperationInputKind.Cancel)).Status);
            Equal((byte)6, civ.TaxRate);
        }
    }

    public static (byte[] Initial, byte[] Saved, byte[] Continued) LocalPopulationReference()
    {
        using var game = new LocalGame();
        FoundCouncil(game);
        var saved = ClassicFiles.DecodeStoredSave(game.SaveClassic());
        var civs = saved.Civilizations.ToArray(); civs[1] = civs[1] with { Gold = int.MaxValue, PopulationMilestone = 0, Researching = 0, ResearchProgress = -100 };
        var initial = ClassicFiles.EncodeStoredSave(saved with { Civilizations = new(civs), Cities = new([saved.Cities[0] with { Size = 4 }]) });
        Equal(LocalLoadResult.Loaded, game.LoadClassic(initial));
        var turn = EndLocalTurn(game, false, 2);
        Equal(1, turn.Committed.Count(e => e.Name == "civilization.population.notice"));
        var result = game.SaveClassic(); Equal(LocalLoadResult.Loaded, game.LoadClassic(result));
        Equal(false, EndLocalTurn(game, false, 3).Committed.Any(e => e.Name == "civilization.population.notice"));
        return (initial, result, game.SaveClassic());
    }

    private static void LocalPopulationTurn()
    {
        var reference = LocalPopulationReference();
        foreach (bool console in new[] { false, true })
        {
            using var game = new LocalGame(); FoundCouncil(game);
            Equal(LocalLoadResult.Loaded, game.LoadClassic(reference.Initial));
            var id = SubmitLocal(game, console, "turn.end", LocalPlayCommands.EndTurn);
            Decide(game.Player, console, AwaitLocal(game, id), 1); Equal(true, game.AdvanceOne());
            var notice = game.Player.Result(id)!;
            Equal(CommandStatus.AwaitingInteraction, notice.Status);
            Equal(true, notice.Interaction!.Spec.Prompt.Contains("100,000", StringComparison.Ordinal));
            Equal(false, game.Player.ObserveLocalPlay()!.PlayerPhase);
            Equal(true, game.Player.ObserveLocalPlay()!.Gold >= 30000);
            Equal(false, notice.Committed.Any(e => e.Name == "turn.advanced"));
            Equal(false, game.Player.Cancel(id).Accepted);
            Decide(game.Player, console, notice, 0); Equal(true, game.AdvanceOne());
            Equal(CommandStatus.Completed, game.Player.Result(id)!.Status);
            BytesEqual(reference.Saved, game.SaveClassic());
            Equal(LocalLoadResult.Loaded, game.LoadClassic(game.SaveClassic())); EndLocalTurn(game, console, 3);
            BytesEqual(reference.Continued, game.SaveClassic());
        }
    }
}
