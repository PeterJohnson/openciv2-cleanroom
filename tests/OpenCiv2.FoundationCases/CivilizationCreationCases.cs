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
    private static RevelationFixture CreationFixture(RevelationFixture? f = null)
    {
        f ??= new();
        f.World.Map.Regions[1] = new(480, 64, 0, default);
        foreach (ref var tile in f.World.Map.Tiles)
        {
            tile.Region = 1;
            tile.Fertility = 15;
            tile.Owner = 0; // No random hut affects the controlled candidate.
        }
        f.Rules.Units[0] = f.Rules.Units[0] with { Role = RuleRole.Settle, Prerequisite = AdvanceReference.None };
        return f;
    }

    private static List<Choice> CreationAttitudes(int code = 0) => Enumerable.Range(1, 7)
        .Select(c => Choice.Create(new("V01.creation.attitude", "create", c), new SamplingLaw.PrimaryModulo(80), ChoiceCode.Primary(code))).ToList();

    private static void CreationChoice(List<Choice> choices, string site, int bound, int value, int attempt = 0) =>
        choices.Add(Choice.Create(new("V01.creation." + site, "create", attempt), new SamplingLaw.PrimaryModulo(bound), ChoiceCode.Primary(value)));

    private static void CreationSiteChoices(List<Choice> choices, int count = 1)
    {
        for (int i = 0; i < count; ++i)
        {
            choices.Add(Choice.Create(new("F05.start-site.x", "create/site", i), new SamplingLaw.PrimaryModulo(40), ChoiceCode.Primary(10)));
            choices.Add(Choice.Create(new("F05.start-site.y", "create/site", i), new SamplingLaw.PrimaryModulo(24), ChoiceCode.Primary(8)));
        }
    }

    private static CivilizationCreation Creation(RevelationFixture f, int c, GameplayRandom random,
        StartingPositions? hints = null, ResearchGrants? grants = null) =>
        new(f.World, f.Process, c, hints ?? new(), random, "create",
            new(new(f.World, f.Process, f.Stacks, f), new(f.World, f.Rules, new(5_000_000, 64), f),
                grants ?? new GrantFixture(f, random).Grants,
                (owner, type) => UnitAvailability.CanBuild(f.World, f.Rules, owner, null, type)));

    private static GameplayRandom CreationRandom(List<Choice> choices) => new(new ControlledChoices(new(choices)), "civilization-create");

    private static void CivilizationCreationReset()
    {
        var f = CreationFixture();
        ref var civ = ref f.World.Civilizations[1];
        civ.Gold = 99;
        civ.NumAdvances = civ.ResearchCostAdvances = 15;
        civ.ResearchProgress = 400;
        civ.Researching = 19;
        civ.FutureTechs = 14;
        civ.Government = Government.Democracy;
        civ.Flags.AtWar = true;
        civ.SpaceshipPopulation = 42;
        civ.ContinentCities[1] = 255;
        civ.ContinentMilitary[1] = 20;
        civ.CityCount = civ.MilitaryCount = civ.TotalCitySize = 10;
        civ.UnitsActive[0] = civ.UnitsLost[0] = civ.UnitsInProduction[0] = 9;
        civ.Advances = default(AdvanceSet).With(20);
        civ.Goals[0] = new(new(8, 8), 1, -4);
        civ.StandingGoals[0] = new(new(14, 10), 2, 3);
        for (int other = 0; other < 8; ++other)
        {
            civ.ReputationCredit[other] = f.World.Civilizations[other].ReputationCredit[1] = 7;
            civ.AggressionCounter[other] = f.World.Civilizations[other].AggressionCounter[1] = 8;
            civ.LastContactTurn[other] = f.World.Civilizations[other].LastContactTurn[1] = 17;
            civ.Treaties[other].Peace = f.World.Civilizations[other].Treaties[1].Peace = true;
        }
        f.AddUnit(0, 1, 0, new(10, 10));
        f.World.Globals.Difficulty = Difficulty.Deity;
        f.World.Globals.AdvanceKnownBy[20] = CivilizationSet.Only(1).With(2);
        var random = CreationRandom(CreationAttitudes(79));
        var operation = Creation(f, 1, random);
        Equal(1, operation.Advance(1)!.Value); // Inactive human still receives the reset, no unit creation.
        random.RequireChoicesConsumed();
        Equal(0, civ.Gold);
        Equal((byte)0, civ.NumAdvances);
        Equal((short)0, civ.ResearchProgress);
        Equal((short)19, civ.Researching);
        Equal((byte)14, civ.FutureTechs);
        Equal((short)42, civ.SpaceshipPopulation);
        Equal(true, civ.Flags.AtWar);
        Equal(Government.Despotism, civ.Government);
        Equal((short)10, civ.LastCityTurn);
        Equal((byte)4, civ.ScienceRate);
        Equal((byte)255, civ.ContinentCities[1]);
        Equal((ushort)20, civ.ContinentMilitary[1]);
        Equal((byte)5, civ.ContinentPolicy[1]);
        Equal((byte)0, civ.UnitsActive[0]);
        Equal(false, civ.Advances.Contains(20));
        Equal(byte.MaxValue, civ.AdvanceSource[20]);
        Equal(CivilizationSet.Only(2), f.World.Globals.AdvanceKnownBy[20]);
        Equal((byte)75, civ.Attitude[1]);
        Equal((byte)89, civ.Attitude[2]);
        Equal((byte)100, civ.Attitude[0]);
        Equal((short)-1, f.World.Civilizations[2].LastContactTurn[1]);
        Equal((short)17, civ.LastContactTurn[2]);
        Equal((short)-1, civ.LastContactTurn[1]);
        Equal(false, f.World.Civilizations[2].Treaties[1].Peace);
        Equal((sbyte)0, f.World.Civilizations[2].ReputationCredit[1]);
        Equal((byte)0, civ.AggressionCounter[2]);
        Equal(new MapPoint(14, 10), civ.Goals[0].Position);
        Equal((sbyte)3, civ.Goals[0].Priority);
        Equal((sbyte)-1, civ.StandingGoals[0].Role);
        Equal(true, f.Unit(0).Token.Occupied);
        Equal(1, f.World.Entities.Units.End);
        Equal(1, operation.Advance(1)!.Value);
    }

    private static void CivilizationCreationEarlyReturnsAndLeaders()
    {
        var human = CreationFixture();
        human.World.Globals.Turn = 1;
        human.World.Globals.Multiplayer = GameMode.Tcp; // Human refusal also precedes the authority path.
        human.World.Civilizations[1].Gold = 42;
        var noChoices = CreationRandom([]);
        Equal(0, Creation(human, 1, noChoices).Advance(1)!.Value);
        Equal(42, human.World.Civilizations[1].Gold);
        noChoices.RequireChoicesConsumed();
        var barb = CreationFixture();
        Equal(1, Creation(barb, 0, noChoices).Advance(1)!.Value);
        Equal((byte)100, barb.World.Civilizations[0].Attitude[7]);
        Equal((byte)0, barb.World.Civilizations[7].Attitude[0]);
        noChoices.RequireChoicesConsumed();

        var absent = CreationFixture();
        absent.World.Globals.CivsAlive = CivilizationSet.Only(2);
        var random = CreationRandom(CreationAttitudes());
        Equal(1, Creation(absent, 2, random).Advance(1)!.Value);
        Equal(false, absent.World.Globals.CivsAlive.Contains(2));
        Equal(false, absent.World.Globals.Status.Won);
        random.RequireChoicesConsumed();

        var f = CreationFixture();
        f.Process.Rules.Leaders[2].Color = f.Process.Rules.Leaders[5].Color = 2;
        var chosen = f.Process.Rules.Leaders[20];
        chosen.Color = 7;
        chosen.TimesUsed = 255;
        chosen.CityNameIndex = 17;
        chosen.Names[1] = new("Selected Queen");
        f.World.PendingLeaderOverrides[2] = 20;
        var choices = CreationAttitudes();
        CreationChoice(choices, "leader", 2, 0);
        CreationChoice(choices, "reuse", 256, 255);
        CreationChoice(choices, "gender", 3, 0);
        random = CreationRandom(choices);
        Equal(1, Creation(f, 2, random).Advance(1)!.Value); // Inactive AI selects a leader before returning.
        random.RequireChoicesConsumed();
        Equal((short)20, f.World.Civilizations[2].Tribe);
        Equal((sbyte)-1, f.World.PendingLeaderOverrides[2]);
        Equal((byte)0, chosen.TimesUsed);
        Equal((byte)0, chosen.CityNameIndex);
        Equal(true, chosen.Female);
        Equal("Selected Queen", chosen.LeaderName.Text);
        Equal((byte)41, f.World.Civilizations[2].LeaderPortrait);
        Equal(true, f.World.Civilizations[2].Flags.FemaleLeader);
        Equal(0, f.World.Entities.Units.End);
    }

    private static void CivilizationCreationCatchup()
    {
        foreach (var difficulty in new[] { Difficulty.Chieftain, Difficulty.Prince })
        {
            var f = CreationFixture();
            f.World.Globals.Turn = 25;
            f.World.Globals.Difficulty = difficulty;
            f.World.Civilizations[2].UnitsActive[4] = 5;
            f.World.Globals.FirstDiscoverer[10] = 1;
            f.World.Globals.AdvanceKnownBy[10] = CivilizationSet.Only(1).With(3); // First human, too few know it.
            f.World.Globals.FirstDiscoverer[11] = 7;
            f.World.Globals.AdvanceKnownBy[11] = CivilizationSet.Only(1);
            f.World.Globals.FirstDiscoverer[12] = 1;
            f.World.Globals.AdvanceKnownBy[12] = CivilizationSet.Only(1).With(2).With(3); // Reset removes self first.
            f.World.Globals.FirstDiscoverer[13] = 1;
            f.World.Globals.AdvanceKnownBy[13] = CivilizationSet.Only(1).With(0).With(3);
            f.World.Globals.AdvanceKnownBy[99] = CivilizationSet.Only(1); // The last table entry is excluded.
            var choices = CreationAttitudes();
            if (difficulty != Difficulty.Chieftain)
            {
                CreationChoice(choices, "catch-up", 3, 1, 11);
                CreationChoice(choices, "catch-up", 3, 2, 13);
            }
            var random = CreationRandom(choices);
            var creation = Creation(f, 2, random);
            Equal(1, creation.Advance(1)!.Value); // No matching AI leader after the reset.
            random.RequireChoicesConsumed();
            Equal((byte)(difficulty == Difficulty.Chieftain ? 0 : 2), f.World.Civilizations[2].NumAdvances);
            if (difficulty != Difficulty.Chieftain)
            {
                Equal(true, f.World.Civilizations[2].Advances.Contains(11));
                Equal(true, f.World.Civilizations[2].Advances.Contains(13));
                Equal((byte)2, f.World.Civilizations[2].AdvanceSource[11]);
                Equal((byte)2, f.World.Civilizations[2].AdvanceSource[13]);
                Equal(true, f.World.Globals.AdvanceKnownBy[11].Contains(2));
                Equal((byte)7, f.World.Globals.FirstDiscoverer[11]);
            }
            Equal((byte)0, f.World.Civilizations[2].UnitsActive[4]);
            Equal((byte)0, f.World.Civilizations[2].UnitsLost[4]);
            Equal((short)25, f.World.Civilizations[2].LastCityTurn);
        }
    }

    private static void CivilizationCreationHintsAndRejection()
    {
        var f = CreationFixture();
        f.World.Globals.CivsAlive = CivilizationSet.Only(1);
        var hints = new StartingPositions();
        hints.Positions[0] = new(1, 0); // Rectangular, polar, wrong parity: still admitted for the human.
        var choices = CreationAttitudes();
        CreationSiteChoices(choices);
        var random = CreationRandom(choices);
        var human = Creation(f, 1, random, hints);
        Equal(1, human.Advance(1)!.Value);
        Equal(new StartingSite(new(1, 0), 0), human.Site!.Value);
        Equal(new MapPoint(1, 0), f.Unit(0).Position);
        Equal(new MapPoint(1, 0), f.World.Views[0].Center);
        Equal((short)1, f.World.Civilizations[1].CapitalX);
        random.RequireChoicesConsumed();

        foreach (bool enough in new[] { true, false })
        {
            f = CreationFixture();
            f.World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3, enough);
            f.World.Civilizations[2].Tribe = 7;
            f.Process.Rules.Leaders[2].Color = 2;
            f.Process.Rules.Leaders[2].Civilize = -5;
            hints = new();
            hints.Positions[2] = new(12, 0);
            choices = CreationAttitudes();
            CreationChoice(choices, "gender", 3, 1);
            CreationSiteChoices(choices);
            random = CreationRandom(choices);
            Equal(enough ? 0 : -1, Creation(f, 2, random, hints).Advance(1)!.Value);
            Equal((short)7, f.World.Civilizations[2].Tribe);
            Equal((byte)1, f.Process.Rules.Leaders[2].TimesUsed);
            Equal((byte)254, f.World.Civilizations[2].ScienceRate);
            Equal((byte)11, f.World.Civilizations[2].TaxRate);
            Equal(!enough, f.World.Globals.Status.Won);
            Equal(enough ? EndReason.None : EndReason.Conquest, f.World.PendingEndReason);
            random.RequireChoicesConsumed();
        }
    }

    private static void CivilizationCreationSearchRescue()
    {
        var f = CreationFixture();
        f.World.Globals.CivsAlive = CivilizationSet.Only(2);
        f.Process.Rules.Leaders[2].Color = 2;
        foreach (ref var tile in f.World.Map.Tiles)
        {
            tile.Terrain = Terrain.Ocean;
        }
        var hints = new StartingPositions();
        hints.Positions[2] = new(12, 10);
        var choices = CreationAttitudes();
        CreationChoice(choices, "gender", 3, 1);
        CreationSiteChoices(choices, 2001);
        var random = CreationRandom(choices);
        var operation = Creation(f, 2, random, hints);
        Equal((int?)null, operation.Advance(2000));
        Equal(0, f.World.Entities.Units.End);
        Equal(1, operation.Advance(1)!.Value);
        Equal(new StartingSite(new(12, 10), 0), operation.Site!.Value);
        Equal(true, f.World.Map.TileAt(f.Unit(0).Position).Ocean);
        Equal((byte)1, f.World.Civilizations[2].NumAdvances);
        random.RequireChoicesConsumed();
    }

    private static void CivilizationCreationRevivalPlacement()
    {
        foreach (bool seenByHuman in new[] { false, true })
        {
            var f = CreationFixture();
            f.World.Globals.Turn = 41;
            f.World.Globals.CivsAlive = CivilizationSet.Only(2);
            f.Process.Rules.Leaders[2].Color = 2;
            f.Rules.Units[3] = f.Rules.Units[0] with { Role = RuleRole.Defend, Movement = 3, Prerequisite = AdvanceReference.None };
            f.AddUnit(0, 0, 1, new(12, 8));
            f.AddUnit(1, 0, 1, new(28, 8)); // Distance exactly 9 survives.
            f.World.Map.TileAt(new(10, 8)).RevealedTo = seenByHuman ? CivilizationSet.Only(1) : default;
            f.World.Map.TileAt(new(12, 6)).Features = MapFeatures.Road;
            var choices = CreationAttitudes();
            CreationChoice(choices, "gender", 3, 1);
            CreationSiteChoices(choices);
            var random = CreationRandom(choices);
            Equal(1, Creation(f, 2, random).Advance(1)!.Value);
            Equal(new MapPoint(12, 8), f.Removed.Single());
            Equal((byte)0, f.Unit(1).Owner.Value);
            Equal(seenByHuman ? 1 : 3, f.World.UnitTotals[2]);
            Equal((byte)(seenByHuman ? 1 : 2), f.World.Civilizations[2].UnitsActive[0]);
            Equal((byte)(seenByHuman ? 0 : 1), f.World.Civilizations[2].UnitsActive[3]);
            Equal(true, f.World.Map.TileAt(new(12, 6)).RevealedTo.Contains(2));
            Equal(MapFeatures.None, f.World.Map.Remembered(2, new(12, 6))); // Final radius explores without memory.
            random.RequireChoicesConsumed();
        }
    }

    private static void CivilizationCreationAdmissionAndFailure()
    {
        foreach (var (turn, noRestart, reused, accepted) in new[]
        {
            (175, false, false, true), (176, false, false, false),
            (0, true, false, true), (1, true, false, false), (0, false, true, false)
        })
        {
            var f = CreationFixture();
            f.World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3);
            f.World.Globals.Turn = (short)turn;
            f.World.Globals.Difficulty = Difficulty.Deity;
            f.World.Globals.Rules.NoRestart = noRestart;
            f.Process.Rules.Leaders[2].Color = 2;
            f.Process.Rules.Leaders[2].TimesUsed = reused ? (byte)1 : (byte)0;
            var hints = new StartingPositions();
            hints.Positions[2] = new(12, 10);
            var choices = CreationAttitudes();
            if (reused) CreationChoice(choices, "reuse", 2, 1);
            CreationChoice(choices, "gender", 3, 1);
            CreationSiteChoices(choices);
            var random = CreationRandom(choices);
            Equal(accepted ? 1 : 0, Creation(f, 2, random, hints).Advance(1)!.Value);
            Equal(accepted, f.World.Globals.CivsAlive.Contains(2));
            random.RequireChoicesConsumed();
        }
        var won = CreationFixture();
        won.World.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
        won.World.Globals.Status.Won = true;
        won.World.PendingEndReason = EndReason.Defeat;
        won.Process.Rules.Leaders[2].Color = 2;
        var polar = new StartingPositions();
        polar.Positions[2] = new(12, 0);
        var selected = CreationAttitudes();
        CreationChoice(selected, "gender", 3, 1);
        CreationSiteChoices(selected);
        Equal(-1, Creation(won, 2, CreationRandom(selected), polar).Advance(1)!.Value);
        Equal(EndReason.Defeat, won.World.PendingEndReason);

        var failure = CreationFixture();
        failure.World.Globals.CivsAlive = CivilizationSet.Only(2);
        failure.World.Globals.Turn = 25;
        failure.World.Globals.Difficulty = Difficulty.Prince;
        failure.World.Civilizations[2].Gold = 42;
        failure.World.Civilizations[2].UnitsActive[4] = 7;
        failure.World.Globals.AdvanceKnownBy[11] = CivilizationSet.Only(1);
        selected = CreationAttitudes();
        CreationChoice(selected, "catch-up", 3, 1, 11);
        var choicesForFailure = CreationRandom(selected);
        failure.Rules.Advances[AdvanceIds.MobileWarfare] = new()
        {
            Enabled = false,
            Prerequisites = new([(AdvanceReference)AdvanceIds.MobileWarfare, AdvanceReference.None])
        };
        var operation = Creation(failure, 2, choicesForFailure);
        Throws<ArgumentException>(() => operation.Advance(1));
        Equal(0, failure.World.Civilizations[2].Gold);
        Equal((byte)7, failure.World.Civilizations[2].UnitsActive[4]);
        Equal(false, failure.World.Globals.CivsAlive.Contains(2));
        Equal(true, failure.World.Civilizations[2].Advances.Contains(11));
        Equal((byte)1, failure.World.Civilizations[2].NumAdvances);
        choicesForFailure.RequireChoicesConsumed();
        Throws<InvalidOperationException>(() => operation.Advance(1));
    }
}
