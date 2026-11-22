using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public sealed class ProductionRecommendation { public int Value { get; set; } }

/// <summary>A02 local city production policy, using actual city calculation, eligibility, terrain,
/// spaceship and airlift children. A01 owns the retained census/policies consumed here.</summary>
public sealed partial class CityProductionChoice(GameState world, RuleState installed, CityPreferences preferences,
    CityEconomy economy, StackOperations stacks, SpaceshipPerformance spaceship, AiProductionAirlift airlift,
    WonderBuildRestrictions? scenario = null)
{
    private readonly GameState world = world;
    private readonly RuleState installed = installed;
    private readonly CityPreferences preferences = preferences;
    private readonly CityEconomy economy = economy;
    private readonly StackOperations stacks = stacks;
    private readonly SpaceshipPerformance spaceship = spaceship;
    private readonly AiProductionAirlift airlift = airlift;
    private readonly WonderBuildRestrictions? scenario = scenario;

    public int Choose(CitySlot city, string call, ProductionRecommendation? unit = null, ProductionRecommendation? building = null)
    {
        MapKnowledge.RequireLocal(world);
        if (unit is not null)
        {
            unit.Value = 999;
        }

        if (building is not null)
        {
            building.Value = 999;
        }

        economy.Calculate(city, false);
        return new Evaluation(this, city, call).Run(unit, building);
    }

    private sealed partial class Evaluation(CityProductionChoice parent, CitySlot slot, string call)
    {
        private GameState World => parent.world;
        private RuleTables Rules => parent.installed.Definitions;
        private ref State.City City => ref World.Entities.Cities.At(slot);
        private ref Civilization Civ => ref World.Civilizations[C];
        private CityCalculationWorkspace Work => World.Calculation;
        private int A => parent.installed.Leaders[Civ.Tribe].Attack;
        private int Z => parent.installed.Leaders[Civ.Tribe].Civilize;
        private int X => parent.installed.Leaders[Civ.Tribe].Expand;
        private bool Human => World.Globals.HumanPlayers.Contains(C);
        private int Government => (byte)Civ.Government;
        private int Difficulty => (byte)World.Globals.Difficulty;
        private int H => World.Globals.BestHumanRank;
        private bool Stronger => Civ.ContinentFlags[R].StrongerEnemy;
        private bool RegionalFlags => Civ.ContinentFlags[R] != default;
        private int RegionalCities => Civ.ContinentCities[R];
        private bool Flat => World.Globals.Rules.FlatWorld;
        private readonly int[] queued = new int[RuleLimits.UnitTypes], present = new int[RuleLimits.UnitTypes];
        private readonly CivilizationSet[] rivalry = new CivilizationSet[RuleLimits.Wonders];
        private int C, R, P, T, F, S, Q, E, O, Fm, U, Wh, Wo, B, Ap, Pf, Same, Ahead, ForeignSame, Wb, Sb, Hsb, Ut, Expand, G, Offshore, Need, Urgent, Naval, D;
        private int cursor, palaces, largestPalace;
        private bool Wip;
        private int Race, choice = 999, choiceScore = 999, bestBuild = 999, bestBuildScore = 999, bestUnit = 999, bestUnitScore = 999, bestRaw;
        private bool Has(int building) => Has(slot, building);
        private bool Has(CitySlot city, int building) => BuildingQueries.HasBuilding(World, city, building);
        private bool Wonder(int wonder) => BuildingQueries.HasWonder(World, Rules, C, wonder);
        private bool Knows(int advance) => Knows(C, advance);
        private bool Knows(int owner, int advance) => CivilizationKnowledge.KnowsAdvance(World.Civilizations, owner, advance);
        private bool Build(int item) => BuildingAvailability.Improvement(World, Rules, C, slot, item, parent.scenario);
        private bool Unit(int type) => UnitAvailability.CanBuild(World, Rules, C, slot, new((byte)type));
        private static bool IsWonder(int production) => production < -BuildingIds.Capitalization;
        private static int Div(int value, int denominator) => Arithmetic.Divide32(value, denominator);
        private static int Clamp(int value, int low, int high) => Arithmetic.ClampAB(value, low, high);
        private int Active(int type) => Civ.UnitsActive[type];
        private int Queued(int type) => Civ.UnitsInProduction[type];
        private int ForeignActive(int type) => World.Civilizations[H].UnitsActive[type];
        private int OldSize => World.Entities.Cities.Storage[cursor].Size;
        private int StackRole(int first, RuleRole role) => parent.stacks.CountRole(first, (int)role, Rules);
        private int Weight(int score, int defensive, bool cutoff = true) => cutoff && score >= 400 ? 16383 : Div(unchecked((T == defensive ? 10 : 20) * score * 3), Z + 3);
        private void CompareBuild(int item, int score)
        {
            if (score < choiceScore)
            { choice = item; choiceScore = score; }
            if (score < bestBuildScore)
            { bestBuild = item; bestBuildScore = score; }
        }
        public int Run(ProductionRecommendation? unit, ProductionRecommendation? building)
        {
            Survey();
            if (Ut == 0 && !Human && Government < (int)Contracts.Government.Monarchy && unit is null && building is null)
            {
                for (int t = RuleLimits.UnitTypes - 1; t >= 0; --t)
                {
                    if (Unit(t) && Rules.Units[t].Role == RuleRole.Defend && !Rules.Units[t].Abilities.Paradrop)
                    {
                        return t;
                    }
                }
            }

            if (C == 0)
            {
                return Barbarian();
            }

            DefenseAndAirlift();
            Improvements();
            bool preference = false;
            if (Human)
            {
                for (int i = 0; i < parent.preferences.Count; ++i)
                {
                    int item = parent.preferences.BuildOrder[i];
                    if (!Has(item) && Build(item))
                    { choice = bestBuild = -item; preference = true; break; }
                }
            }

            if (!preference)
            {
                if (Wip || !Stronger && City.Size > 2 && !(City.Flags.MoodOnEdge && City.Flags.SpecialistHeld)
                    && (RegionalCities >= 4 - Z || !RegionalFlags && Civ.CityCount >= 4 - Z))
                {
                    Wonders();
                }

                if (Civ.MilitaryCount < 512)
                {
                    SpecialUnits();
                }
            }
            if (Civ.MilitaryCount < 512)
            {
                OrdinaryUnits();
            }
            else if (choice == 999)
            {
                choice = -BuildingIds.Capitalization;
            }

            if (building is not null)
            {
                building.Value = bestBuild;
            }

            if (unit is not null)
            {
                unit.Value = bestUnit;
            }

            return Finish();
        }
        private int Finish()
        {
            if (!Human || !City.Flags.Autobuild)
            {
                if (!Human && City.Production == -BuildingIds.Palace)
                {
                    return -BuildingIds.Palace;
                }

                if (IsWonder(choice) && !Has(BuildingIds.Temple))
                {
                    return -BuildingIds.Temple;
                }

                if (IsWonder(City.Production) && Same == 0 && !IsWonder(choice) && !City.Flags.SecondDisorderTurn)
                {
                    int wonder = -City.Production - 39;
                    if (Need < 2 && !BuildingQueries.Expired(World, Rules, wonder) && World.Globals.WonderCity[wonder] == BuildingQueries.Unbuilt
                        && (Urgent == 0 || !City.Flags.DefenderLost) && StackRole(parent.stacks.FirstAt(City.Position), RuleRole.Defend) > 1)
                    {
                        return City.Production;
                    }
                }
                return choice;
            }
            if (City.Flags.UnitsOnlyAutomation)
            {
                return bestUnit > 98 || bestUnitScore > 500 ? 99 : bestUnit;
            }

            if (City.Flags.ImprovementsOnlyAutomation)
            {
                return bestBuild > 98 || bestBuildScore > 500 ? 99 : bestBuild;
            }

            return choiceScore > 500 ? 99 : choice;
        }
    }
}
