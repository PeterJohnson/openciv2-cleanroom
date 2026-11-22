using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void UnitBuildEligibility()
    {
        var f = CreationFixture();
        bool Build(int type = 4, int civ = 1, CitySlot? city = null) => UnitAvailability.CanBuild(f.World, f.Rules, civ, city, new((byte)type));
        f.Rules.Units[4] = new() { Role = RuleRole.Defend, Prerequisite = AdvanceReference.None, Movement = 3, Attack = 1, Defense = 2 };
        f.Rules.Units[7] = new() { Defense = 3 };
        Equal(true, Build());
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(35);
        Equal(false, Build());
        f.Rules.Units[4] = f.Rules.Units[4] with { Defense = 3 };
        Equal(true, Build());
        f.Rules.Units[4] = f.Rules.Units[4] with { Prerequisite = AdvanceReference.Never };
        Equal(false, Build());
        f.Rules.Units[4] = f.Rules.Units[4] with { Prerequisite = (AdvanceReference)35, ObsoleteBy = (AdvanceReference)35 };
        Equal(false, Build());
        f.Rules.Units[4] = f.Rules.Units[4] with
        {
            Prerequisite = AdvanceReference.None,
            ObsoleteBy = AdvanceReference.None,
            Role = RuleRole.Attack,
            Movement = 6,
            Attack = 4,
            Defense = 2
        };
        f.Rules.Units[19] = new() { Attack = 4, Defense = 3, Prerequisite = (AdvanceReference)35 };
        Equal(false, Build());
        f.Rules.Units[4] = f.Rules.Units[4] with { Attack = 5 };
        Equal(true, Build());
        f.Rules.Units[4] = f.Rules.Units[4] with { Attack = 99 };
        f.World.Civilizations[1].Advances = f.World.Civilizations[1].Advances.With(58);
        Equal(false, Build());
        f.World.Globals.WonderCity[23] = -2;
        Equal(true, Build());
        f.Rules.Units[4] = f.Rules.Units[4] with { Attack = 128, Domain = RuleDomain.Sea, Role = RuleRole.NavalSuperiority };
        f.World.Globals.WonderCity[23] = -1;
        Equal(true, Build()); // Signed negative attack skips nuclear gate.
        Equal(false, Build(city: new(0)));
        f.World.Entities.Cities.Storage[0].Flags.Coastal = true;
        Equal(false, Build(city: new(0)));
        f.World.Entities.Cities.Storage[0].Flags.OceanAccess = true;
        Equal(true, Build(city: new(0))); // No lifetime gate.

        f.Rules.Units[4] = new() { Role = RuleRole.Defend, Prerequisite = AdvanceReference.None, Movement = 6, Attack = 3, Defense = 3 };
        f.Rules.Units[8] = f.Rules.Units[4] with { Attack = 4, ObsoleteBy = (AdvanceReference)5 };
        f.World.Civilizations[2].Advances = default(AdvanceSet).With(5);
        Equal(true, Build());
        Equal(false, Build(civ: 2)); // Obsolete, nonfundamentalist alternative still excludes the AI's weaker unit.
        Equal(false, Build(type: 8, civ: 2));
        f.Rules.Units[8] = f.Rules.Units[8] with { Attack = 3, ObsoleteBy = AdvanceReference.None };
        Equal(true, Build(civ: 2)); // Equal alternatives do not exclude.
        Equal(false, Build(type: 8));
        f.World.Civilizations[1].Government = Government.Fundamentalism;
        Equal(true, Build(type: 8));
    }

    private static void StartingCompensation()
    {
        foreach (var (landCount, expectedBonus, expectedAdvances) in new[] { (150, 0, 3), (100, 1, 1), (50, 1, 2) })
        {
            var f = CreationFixture();
            f.World.Map.Regions[1] = new(300, 64, 0, default);
            f.World.Map.Regions[2] = new((short)landCount, 64, 0, default);
            f.AddUnit(0, 1, 0, new(10, 8));
            f.AddUnit(1, 2, 0, new(30, 8));
            f.World.Map.TileAt(new(30, 8)).Region = 2;
            f.World.Civilizations[1].Researching = f.World.Civilizations[2].Researching = -1;
            for (int a = 0; a < 100; ++a)
            {
                f.Rules.Advances[a] = new()
                {
                    Enabled = true,
                    AiValue = 1,
                    Prerequisites = new([AdvanceReference.None, AdvanceReference.None])
                };
            }
            // Isolated regions score 11 and 8/7/6; compensation invokes the real AI research recommendation.
            var scores = StartingSites.Assess(f.World);
            Equal(11, scores.Scores[1]);
            Equal(11 - (landCount == 150 ? 3 : landCount == 100 ? 4 : 5), scores.Scores[2]);
            var random = new GameplayRandom(17, "compensation");
            StartingSites.Compensate(f.World, f.Process, new(f.World, f.Process, f.Stacks, f), random, "balance");
            Equal(expectedBonus, f.World.UnitTotals[2]); // Fixture's initial units did not populate the cached count.
            Equal(expectedAdvances, Enumerable.Range(0, 100).Count(a => f.World.Civilizations[2].Advances.Contains(a)));
            Equal(0, Enumerable.Range(0, 100).Count(a => f.World.Civilizations[1].Advances.Contains(a)));
            Equal((byte)0, f.World.Civilizations[2].NumAdvances); // Turn-zero grants do not change this count.
        }
        var deity = CreationFixture();
        deity.AddUnit(0, 1, 0, new(10, 8));
        deity.World.Globals.Difficulty = Difficulty.Deity;
        StartingSites.Compensate(deity.World, deity.Process, new(deity.World, deity.Process, deity.Stacks, deity),
            CreationRandom([]), "balance");
        Equal(2, deity.World.Entities.Units.End);
        Equal(new MapPoint(10, 8), deity.Unit(1).Position);
    }

    private static void InstallGeneratedStartingRules(SessionRuntime runtime)
    {
        var rules = runtime.Process.Rules.Definitions;
        var generation = GenerationRules();
        rules.Cosmic = generation.Cosmic;
        generation.Terrain.CopyTo(rules.Terrain, 0);
        rules.Units[0] = new() { Role = RuleRole.Settle, Movement = 3, HitPoints = 10, Prerequisite = AdvanceReference.None };
        rules.Units[2] = generation.Units[2];
        rules.Units[33] = generation.Units[33];
        for (int a = 0; a < 100; ++a)
        {
            rules.Advances[a] = new() { Enabled = true, AiValue = 10, Prerequisites = new([AdvanceReference.None, AdvanceReference.None]) };
        }
        for (int w = 0; w < rules.WonderObsolescence.Length; ++w)
        {
            rules.WonderObsolescence[w] = AdvanceReference.Never;
        }
        for (int c = 1; c < 8; ++c)
        {
            runtime.Process.Rules.Leaders[c].Color = (short)c;
            runtime.Process.Rules.Leaders[c].Civilize = 1;
            runtime.World.Civilizations[c].Tribe = (short)c;
        }
        runtime.World.Globals.HumanPlayers = CivilizationSet.Only(1);
        runtime.World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3).With(4).With(5).With(6).With(7);
        runtime.World.Globals.PlayerCiv = 1;
        runtime.World.Multiplayer.LocalHuman = 1;
        runtime.Process.OrdinaryPreferences.Science = 6;
        runtime.Process.OrdinaryPreferences.Tax = 4;
    }

    private static void GeneratedCivilizationIntegration()
    {
        foreach (var (width, height, flat, difficulty) in new[]
        {
            (80, 50, false, Difficulty.Chieftain), (80, 50, true, Difficulty.King),
            (100, 80, false, Difficulty.Deity), (150, 120, true, Difficulty.Prince),
            (40, 51, true, Difficulty.King), (500, 40, false, Difficulty.King)
        })
        {
            using var runtime = new SessionRuntime(new(), new(901));
            string? baseline = null;
            ulong baselineDraws = 0;
            for (int run = 0; run < 2; ++run)
            {
                runtime.PrepareNewGame();
                InstallGeneratedStartingRules(runtime);
                var world = runtime.World;
                world.WorldSetup.Width = (short)width;
                world.WorldSetup.Height = (short)height;
                world.Globals.Rules.FlatWorld = flat;
                world.Globals.Difficulty = difficulty;
                var hints = new StartingPositions();
                var random = new GameplayRandom(17, "generated-initialization");
                WorldGeneration.Generate(runtime, ref hints, random, "world", new(1234));
                using var initialize = new GeneratedGameInitialization(runtime, hints, random, "start");
                for (int poll = 0; poll < 50_000 && !initialize.Completed; ++poll)
                {
                    initialize.Poll(run == 0 ? 7 : 257);
                }
                Equal(true, initialize.Completed);
                Equal(false, initialize.Cancelled);
                Equal(world.Globals.CivsAlive, world.Globals.CivsAtStart);
                Equal(true, world.Globals.CivsAlive.Contains(1));
                Equal(false, world.Globals.Status.Won);
                Equal((short)0, world.Globals.Turn);
                Equal((short)-4000, world.Globals.Year);
                Equal((byte)6, world.Civilizations[1].ScienceRate);
                Equal(difficulty == Difficulty.Chieftain ? 50 : 0, world.Civilizations[1].Gold);
                Equal(0, world.Entities.Cities.End);
                foreach (ref readonly var unit in world.Entities.Units.Scanned)
                {
                    if (!unit.Token.Occupied) continue;
                    Equal((byte)0, unit.Type.Value);
                    Equal(true, world.Map.Geometry.Canonical(unit.Position));
                    Equal(false, world.Map.TileAt(unit.Position).Ocean);
                    Equal(true, world.Map.TileAt(unit.Position).RevealedTo.Contains(unit.Owner.Value));
                }
                string snapshot = string.Join("|", world.Entities.Units.Scanned.ToArray().Where(u => u.Token.Occupied)
                    .Select(u => $"{u.Owner.Value}:{u.Type.Value}:{u.Position.X},{u.Position.Y}"))
                    + ";" + string.Join("|", world.Civilizations.Select(c => $"{c.Tribe}:{c.NumAdvances}:{c.ScienceRate}:{c.TaxRate}:"
                        + string.Join(",", Enumerable.Range(0, 100).Where(c.Advances.Contains))));
                if (run == 0)
                {
                    baseline = snapshot;
                    baselineDraws = random.Draws;
                }
                else
                {
                    Equal(baseline!, snapshot);
                    Equal(baselineDraws, random.Draws);
                }
                world.Civilizations[1].Advances = default(AdvanceSet).With(99);
                world.PendingEndReason = EndReason.Defeat;
            }
        }
    }

    private static void GeneratedInitializationLifetime()
    {
        using var runtime = new SessionRuntime(new(), new(902));
        runtime.PrepareNewGame();
        InstallGeneratedStartingRules(runtime);
        runtime.World.Map.Allocate(new(40, 24));
        foreach (ref var tile in runtime.World.Map.Tiles) tile.Terrain = Terrain.Ocean;
        var random = new GameplayRandom(17, "cancel-start");
        using var pending = new GeneratedGameInitialization(runtime, new(), random, "start");
        Equal(false, pending.Poll(1)); // Barbarian reset.
        Equal(false, pending.Poll(1)); // Human search cannot accept Ocean.
        Throws<InvalidOperationException>(() => runtime.PrepareNewGame());
        ulong draws = random.Draws;
        pending.Dispose();
        Equal(true, pending.Cancelled);
        Equal(false, pending.Poll());
        Equal(draws, random.Draws);
        runtime.PrepareNewGame();
        InstallGeneratedStartingRules(runtime);
        runtime.World.Map.Allocate(new(40, 24));
        random = new(new ControlledChoices(new([HistoryChoice("start")])), "failed-start");
        using var failed = new GeneratedGameInitialization(runtime, new(), random, "start");
        Equal(false, failed.Poll(1));
        Throws<ChoiceException>(() => failed.Poll(1));
        Equal(true, failed.Cancelled);
        runtime.PrepareNewGame();
        Equal(EndReason.None, runtime.World.PendingEndReason);
    }

    private static void GeneratedWorldEntryCreation()
    {
        using var f = new EntryFixture();
        var units = f.Input.Definitions.Units.ToArray();
        units[0] = units[0] with { Role = RuleRole.Settle, Domain = RuleDomain.Land, Abilities = default };
        f.Input = f.Input with
        {
            Definitions = f.Input.Definitions with
            {
                Units = new(units),
                Advances = new(Enumerable.Range(0, 100).Select(_ => new AdvanceRule
                {
                    Enabled = true,
                    AiValue = 10,
                    Prerequisites = new([AdvanceReference.None, AdvanceReference.None])
                }))
            }
        };
        f.Services = f.Services with { GenerateWorld = null, Generation = new(91), Initialize = null };
        Equal(true, f.Run(new GameplayRandom(99, "entry")));
        var world = f.Runtime.World;
        Equal(true, world.Entities.Units.Scanned.ToArray().Any(u => u.Token.Occupied && u.Owner.Value == 1));
        Equal(false, f.Trace.Any(value => value.StartsWith("create:", StringComparison.Ordinal) || value == "balance"));
        Equal("introduce", f.Trace[^1]);
        Equal((short)0, world.Globals.Turn);
        Equal(world.Globals.CivsAlive, world.Globals.CivsAtStart);
        f.Runtime.PrepareNewGame(); // Both nested operation pins were released.
    }
}
