using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static (GameState World, RuleState Rules) ResearchWorld()
    {
        var process = new ProcessContext();
        var world = new GameState(process);
        world.Globals.HumanPlayers = CivilizationSet.Only(1);
        for (int c = 0; c < EntityLimits.Civilizations; ++c)
        {
            world.Civilizations[c].Tribe = 13;
            world.Civilizations[c].Researching = -1;
        }
        for (int a = 0; a < RuleLimits.Advances; ++a)
        {
            process.Rules.Definitions.Advances[a] = new()
            {
                AiValue = 10,
                Prerequisites = new([AdvanceReference.None, AdvanceReference.None])
            };
            world.Globals.AdvanceKnownBy[a] = CivilizationSet.Only(7);
        }
        for (int w = 0; w < RuleLimits.Wonders; ++w)
        {
            world.Globals.WonderCity[w] = -1;
            process.Rules.Definitions.WonderObsolescence[w] = AdvanceReference.Never;
        }
        return (world, process.Rules);
    }

    // C08 storage and four wonder queries: dead records, global expiry, sentinels and Eiffel's narrow gate.
    private static void BuildingAndWonderQueries()
    {
        var (world, installed) = ResearchWorld();
        var rules = installed.Definitions;
        Equal(false, BuildingQueries.HasBuilding(world, new(ushort.MaxValue), 0));
        BuildingQueries.SetBuilding(world, new(ushort.MaxValue), 35, true);
        BuildingQueries.SetBuilding(world, new(0), 34, true);
        Equal(true, BuildingQueries.HasBuilding(world, new(0), 34));
        Equal(false, world.Entities.Cities.Live(new(0)));
        BuildingQueries.SetBuilding(world, new(0), 34, false);
        Equal(false, BuildingQueries.HasBuilding(world, new(0), 34));
        Equal((int?)null, BuildingQueries.ActiveOwner(world, rules, 0));
        world.Globals.WonderCity[0] = -2;
        Equal(-2, BuildingQueries.ActiveCity(world, rules, 0));
        world.Globals.WonderCity[0] = 0;
        world.Entities.Cities.Storage[0].Owner = new(2);
        Equal((int?)2, BuildingQueries.ActiveOwner(world, rules, 0));
        Equal(true, BuildingQueries.HasWonder(world, rules, 258, 0));
        rules.WonderObsolescence[0] = (AdvanceReference)4;
        world.Civilizations[0].Advances = default(AdvanceSet).With(4);
        Equal(false, BuildingQueries.Expired(world, rules, 0));
        world.Civilizations[7].Advances = default(AdvanceSet).With(4);
        Equal(-1, BuildingQueries.ActiveCity(world, rules, 0));
        Equal((short)0, world.Globals.WonderCity[0]);
        world.Globals.WonderCity[0] = -2;
        Equal(-1, BuildingQueries.ActiveCity(world, rules, 0));
        rules.WonderObsolescence[0] = AdvanceReference.Invalid;
        Equal(-2, BuildingQueries.ActiveCity(world, rules, 0));

        const int eiffel = WonderIds.EiffelTower;
        world.Globals.WonderCity[eiffel] = 0;
        world.Globals.Status.Scenario = true;
        world.Scenario.Flags.ObjectiveVictory = true;
        Equal(false, BuildingQueries.HasWonder(world, rules, 2, eiffel));
        Equal((int?)2, BuildingQueries.ActiveOwner(world, rules, eiffel));
        world.Globals.Status.Scenario = false;
        Equal(true, BuildingQueries.HasWonder(world, rules, 2, eiffel));
    }

    // V03 §§2–3: Future Technology eligibility, ordered cycle failure and repeated-edge counting differ.
    private static void ResearchQueriesAndWalks()
    {
        var (world, installed) = ResearchWorld();
        var rules = installed.Definitions;
        rules.Advances[2] = rules.Advances[2] with
        {
            Enabled = true,
            Prerequisites = new([(AdvanceReference)(-3), AdvanceReference.None])
        };
        Equal(true, Research.Available(world, rules, 1, 2));
        world.Civilizations[1].Advances = default(AdvanceSet).With(2);
        Equal(false, Research.Available(world, rules, 1, 2));
        rules.Advances[89] = rules.Advances[2];
        world.Civilizations[1].Advances = world.Civilizations[1].Advances.With(89);
        Equal(true, Research.Available(world, rules, 1, 89));
        rules.Advances[2] = rules.Advances[2] with { Enabled = false };
        Equal(false, Research.Available(world, rules, 999, 2));
        Equal(false, Research.DescendsFrom(rules, -1, -1));
        Equal(true, Research.DescendsFrom(rules, 999, 999));
        rules.Advances[2] = rules.Advances[2] with { Prerequisites = new([(AdvanceReference)3, (AdvanceReference)4]) };
        rules.Advances[3] = rules.Advances[3] with { Prerequisites = new([(AdvanceReference)5, AdvanceReference.None]) };
        rules.Advances[4] = rules.Advances[3];
        Equal(true, Research.DescendsFrom(rules, 2, 5));
        Equal(false, Research.DescendsFrom(rules, 2, 6));
        Equal(4, Research.CountPrerequisites(rules, 2));
        rules.Advances[3] = rules.Advances[3] with { Prerequisites = new([(AdvanceReference)3, (AdvanceReference)5]) };
        Throws<InvalidOperationException>(() => Research.DescendsFrom(rules, 2, 5));
        rules.Advances[1] = rules.Advances[1] with { Prerequisites = new([(AdvanceReference)1, (AdvanceReference)1]) };
        Equal(202, Research.CountPrerequisites(rules, 1));
        Equal(101, Research.CountPrerequisites(rules, 999, 101));
        rules.Advances[1] = rules.Advances[1] with { Prerequisites = new([(AdvanceReference)0, (AdvanceReference)0]) };
        Equal(0, Research.CountPrerequisites(rules, 1));
    }

    // A08 §2: war pressure changes a working trait, signed ships and shared facility prerequisites retain meaning.
    private static void ResearchValuationState()
    {
        var (world, installed) = ResearchWorld();
        var rules = installed.Definitions;
        installed.Leaders[13].Civilize = 1;
        rules.Advances[4] = rules.Advances[4] with { CivilizedAiModifier = 2 };
        Equal(12, AdvanceValuation.Value(world, installed, 2, 4));
        ref var civ = ref world.Civilizations[2];
        civ.Treaties[1].War = true;
        civ.NumAdvances = 1;
        world.Civilizations[1].NumAdvances = 4;
        civ.ContinentCities[1] = world.Civilizations[1].ContinentCities[1] = 1;
        Equal(10, AdvanceValuation.Value(world, installed, 2, 4));
        civ.ContinentCities[2] = world.Civilizations[1].ContinentCities[2] = 1;
        Equal(8, AdvanceValuation.Value(world, installed, 2, 4));
        Equal((sbyte)1, installed.Leaders[13].Civilize);
        civ.ContinentCities[2] = 0;
        world.Civilizations[1].Reputation = 7;
        Equal(8, AdvanceValuation.Value(world, installed, 2, 4));
        civ.ContinentCities[1] = 0;
        civ.ContinentCities[63] = world.Civilizations[1].ContinentCities[63] = 1;
        Equal(12, AdvanceValuation.Value(world, installed, 2, 4));

        installed.Leaders[13].Civilize = 0;
        rules.Improvements[34] = new() { Prerequisite = (AdvanceReference)4 };
        civ.ShipCount = -7;
        Equal(9, AdvanceValuation.Value(world, installed, 2, 4));
        rules.Improvements[9] = rules.Improvements[23] = new() { Prerequisite = (AdvanceReference)4 };
        rules.Cosmic = rules.Cosmic with { AqueductSize = 8, SewerSize = 12 };
        world.Entities.Cities.SetEnd(2);
        world.Entities.Cities.Storage[0] = new() { Token = new(1), Owner = new(2), Size = 8 };
        world.Entities.Cities.Storage[1] = new() { Token = new(2), Owner = new(1), Size = 20 };
        Equal(11, AdvanceValuation.Value(world, installed, 2, 4));
        world.Entities.Cities.Storage[0].Size = -1;
        Equal(9, AdvanceValuation.Value(world, installed, 2, 4));
        world.Globals.AdvanceKnownBy[4] = default;
        Equal(10, AdvanceValuation.Value(world, installed, 2, 4));
        rules.Advances[5] = rules.Advances[5] with { Prerequisites = new([(AdvanceReference)4, (AdvanceReference)4]) };
        civ.Advances = default(AdvanceSet).With(5);
        Equal(9, AdvanceValuation.Value(world, installed, 2, 4));
    }

    // A08 §2: expansion deliberately reads own regional presence; tech-race bonuses are independent and AI-only.
    private static void ResearchValuationExpansionAndRace()
    {
        var (world, installed) = ResearchWorld();
        var rules = installed.Definitions;
        rules.Units[32] = new() { Prerequisite = (AdvanceReference)4 };
        Equal(11, AdvanceValuation.Value(world, installed, 1, 4));
        world.Civilizations[1].ContinentCities[1] = 1;
        Equal(10, AdvanceValuation.Value(world, installed, 1, 4));
        world.Civilizations[2].ContinentCities[1] = 1;
        Equal(11, AdvanceValuation.Value(world, installed, 2, 4));
        world.Globals.Status.LargeMap = true;
        Equal(11, AdvanceValuation.Value(world, installed, 1, 4));
        world.Civilizations[1].Tribe = 17;
        Equal(12, AdvanceValuation.Value(world, installed, 1, 4));
        world.Civilizations[1].Tribe = 13;
        rules.Units[32] = new();
        world.Globals.AdvanceKnownBy[76] = CivilizationSet.Only(1);
        rules.Advances[32] = rules.Advances[32] with { Prerequisites = new([(AdvanceReference)4, AdvanceReference.None]) };
        for (int i = 35; i <= 37; ++i)
        {
            rules.Improvements[i] = new() { Prerequisite = (AdvanceReference)4 };
        }
        Equal(21, AdvanceValuation.Value(world, installed, 2, 4));
        Equal(10, AdvanceValuation.Value(world, installed, 1, 4));
        rules.Units[28] = new() { Prerequisite = (AdvanceReference)4 };
        world.Globals.AdvanceKnownBy[4] = CivilizationSet.Only(1);
        Equal(26, AdvanceValuation.Value(world, installed, 2, 4));
        world.Globals.AdvanceKnownBy[4] = CivilizationSet.Only(7);
        Equal(21, AdvanceValuation.Value(world, installed, 2, 4));

        world.Entities.Cities.Storage[0].Owner = new(2);
        world.Globals.WonderCity[0] = 0;
        rules.WonderObsolescence[0] = (AdvanceReference)4;
        Equal(19, AdvanceValuation.Value(world, installed, 2, 4));
        world.Entities.Cities.Storage[0].Owner = new(1);
        Equal(23, AdvanceValuation.Value(world, installed, 2, 4));
        world.Civilizations[7].Advances = default(AdvanceSet).With(4);
        Equal(21, AdvanceValuation.Value(world, installed, 2, 4));
    }

    // A08 §2 tribe table and ordered knowledge/ancestry bonuses, with a final lower clamp.
    private static void ResearchValuationTribesAndKnowledge()
    {
        var (world, installed) = ResearchWorld();
        var rules = installed.Definitions;
        (int Tribe, int Advance, int Bonus)[] cases =
        [
            (0, 39, 2), (0, 55, -1), (1, 12, 1), (2, 6, 1), (3, 47, 2), (4, 21, 2), (4, 15, -1),
            (5, 60, 2), (6, 64, 2), (7, 34, 1), (8, 8, -1), (8, 55, 0), (9, 17, 1), (10, 35, -2),
            (11, 35, 1), (12, 55, 1), (13, 4, 0), (14, 4, 0), (15, 52, 1), (16, 46, 1),
            (17, 55, 1), (18, 9, 1), (19, 56, 1), (20, 36, 2)
        ];
        foreach (var (tribe, advance, bonus) in cases)
        {
            world.Civilizations[1].Tribe = (short)tribe;
            Equal(10 + bonus, AdvanceValuation.Value(world, installed, 1, advance));
        }
        world.Civilizations[1].Tribe = 13;
        installed.Leaders[13].Civilize = 2;
        world.Civilizations[1].Advances = default(AdvanceSet).With(85);
        Equal(15, AdvanceValuation.Value(world, installed, 1, 16));
        rules.Advances[16] = rules.Advances[16] with { Prerequisites = new([(AdvanceReference)4, AdvanceReference.None]) };
        Equal(13, AdvanceValuation.Value(world, installed, 1, 4));
        world.Civilizations[1].Advances = default(AdvanceSet).With(38);
        rules.Advances[37] = rules.Advances[37] with { Prerequisites = new([(AdvanceReference)4, AdvanceReference.None]) };
        Equal(11, AdvanceValuation.Value(world, installed, 1, 4));
        world.Civilizations[1].Advances = world.Civilizations[1].Advances.With(57);
        Equal(12, AdvanceValuation.Value(world, installed, 1, 4));
        rules.Advances[4] = rules.Advances[4] with { AiValue = -128, CivilizedAiModifier = -128 };
        Equal(1, AdvanceValuation.Value(world, installed, 1, 4));
    }

    // V03 §4: humans skip later modulo-class candidates, AI value-one candidates consume no RNG, first ties win.
    private static void ResearchRecommendationAndStartingGrant()
    {
        var (world, installed) = ResearchWorld();
        var rules = installed.Definitions;
        world.Globals.Difficulty = Difficulty.Prince;
        foreach (int a in new[] { 0, 2, 3 })
        {
            rules.Advances[a] = rules.Advances[a] with { Enabled = true };
        }
        var random = new GameplayRandom(new ControlledChoices(new(new[]
        {
            Choice.Create(new("V03.recommend.human", "research", 0), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(2)),
            Choice.Create(new("V03.recommend.human", "research", 2), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(2))
        })), "research-tests");
        Equal(0, Research.Recommend(world, installed, 1, random, "research"));
        Equal((short)-1, world.Civilizations[1].Researching);
        random.RequireChoicesConsumed();
        foreach (int a in new[] { 0, 2, 3 })
        {
            rules.Advances[a] = rules.Advances[a] with { AiValue = -10 };
        }
        random = new(new ControlledChoices(new([])), "research-tests");
        Equal(0, Research.Recommend(world, installed, 2, random, "research"));
        random.RequireChoicesConsumed();
        rules.Advances[2] = rules.Advances[2] with { AiValue = 10 };
        random = new(new ControlledChoices(new(new[]
        {
            Choice.Create(new("V03.recommend.ai", "research", 2), new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(9))
        })), "research-tests");
        Equal(2, Research.Recommend(world, installed, 2, random, "research"));
        random.RequireChoicesConsumed();

        ref var civ = ref world.Civilizations[1];
        civ.Researching = 2;
        civ.ResearchProgress = 123;
        civ.NumAdvances = 1;
        world.Globals.AdvanceKnownBy[2] = default;
        random = new(new ControlledChoices(new([])), "research-tests");
        Equal(2, Research.CompleteStarting(world, installed, 1, random, "grant"));
        Equal(true, civ.Advances.Contains(2));
        Equal(CivilizationSet.Only(1), world.Globals.AdvanceKnownBy[2]);
        Equal((byte)0, world.Globals.FirstDiscoverer[2]);
        Equal((byte)1, civ.NumAdvances);
        Equal((short)-1, civ.Researching);
        Equal((short)0, civ.ResearchProgress);
        civ.Researching = 89;
        civ.FutureTechs = 255;
        Equal(89, Research.CompleteStarting(world, installed, 1, random, "future"));
        Equal((byte)0, civ.FutureTechs);
        Equal(false, civ.Advances.Contains(89));
        random.RequireChoicesConsumed();
        world.Entities.Cities.SetEnd(1);
        world.Entities.Cities.Storage[0].Token = new(1);
        world.Entities.Cities.Storage[0].Owner = new(1);
        Throws<InvalidOperationException>(() => Research.CompleteStarting(world, installed, 1, random, "city"));
        world.Entities.Cities.SetEnd(0);
        world.Globals.Turn = 1;
        Throws<InvalidOperationException>(() => Research.CompleteStarting(world, installed, 1, random, "turn"));
    }
}
