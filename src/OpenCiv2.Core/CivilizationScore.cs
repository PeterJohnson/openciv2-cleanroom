using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum ObjectiveOutcome { DecisiveVictory, MarginalVictory, Draw, MarginalDefeat, DecisiveDefeat }

/// <summary>V08 shared score scratch. Ordinary queries intentionally retain prior objective and rating fields.</summary>
public sealed class ScoreWorkspace
{
    public int Citizens, Wonders, SpaceshipInFlight, SpaceshipArrived, Pollution, Peace, FutureTechs, BarbarianBonus;
    public int Total, ConquestBonus, ScenarioPoints, TotalObjectives, ObjectiveCivValue, CivObjectives;
    public ObjectiveOutcome ObjectiveOutcome;
    public int TitleIndex, Rating;
}

public sealed record ScoreReport(int Civilization, bool Objective, int Citizens, int Wonders,
    int SpaceshipInFlight, int SpaceshipArrived, int Pollution, int Peace, int FutureTechs, int BarbarianBonus,
    int Total, int ConquestBonus, int TotalObjectives, int ObjectiveCivValue, int CivObjectives,
    ObjectiveOutcome ObjectiveOutcome, OwnedList<short> Thresholds, OwnedList<int> VisibleWonders,
    int Rating, int TitleIndex, bool PeaceEligible, bool Scenario, bool Cheated)
{
    public bool UsesConquest => !Objective && ConquestBonus > Total;
    public int Score => Objective ? Total : Math.Max(Total, ConquestBonus);
}

/// <summary>V08 §§2–3 score queries and semantic report preparation. Uses actual V07 and C03 children;
/// report painting consumes an owned snapshot and performs no gameplay calculations.</summary>
public sealed class CivilizationScore(GameState world, ScoringValues values, SpaceshipPerformance spaceship, CityEconomy economy)
{
    private ScoreWorkspace Work => world.Score;
    private bool Objective => world.Globals.Status.Scenario && world.Scenario.Flags.ObjectiveVictory;

    public int ObjectiveValue(CitySlot slot)
    {
        int result = world.Entities.Cities.At(slot).Flags.Objective ? values.ObjectiveCity : 0;
        if (world.Scenario.Flags.WonderObjectives)
        {
            for (int w = 0; w < RuleLimits.Wonders; ++w)
            {
                if (world.Globals.WonderCity[w] == slot.Value)
                {
                    result = unchecked(result + values.ObjectiveWonder);
                }
            }
        }

        return result;
    }

    public int Calculate(int owner)
    {
        MapKnowledge.RequireLocal(world);
        unchecked
        {
            Work.Citizens = 0;
            foreach (ref var city in world.Entities.Cities.Scanned)
            {
                if (city.Token.Occupied && (sbyte)city.Owner.Value == owner)
                {
                    Work.Citizens += city.Size * values.PerCitizen + (sbyte)city.Happy * values.PerHappyCitizen
                        + (sbyte)city.Unhappy * values.PerUnhappyCitizen;
                }
            }

            Work.Wonders = 0;
            for (int w = 0; w < RuleLimits.Wonders; ++w)
            {
                int location = world.Globals.WonderCity[w];
                if (location >= 0 && (sbyte)world.Entities.Cities.Storage[location].Owner.Value == owner)
                {
                    Work.Wonders += values.WonderPoints;
                }
            }
            Work.SpaceshipInFlight = Work.SpaceshipArrived = 0;
            spaceship.Calculate(owner, true);
            ref var civ = ref world.Civilizations[owner];
            if (world.Spaceship.Success != 0 && civ.SpaceshipPopulation != 0)
            {
                Work.SpaceshipInFlight = civ.SpaceshipPopulation * world.Spaceship.Success * values.SpaceshipPointsPerPopulationSuccess;
                if (civ.SpaceshipFlags.FirstArrivalVictory)
                {
                    Work.SpaceshipArrived = Work.SpaceshipInFlight;
                    Work.SpaceshipInFlight = 0;
                }
            }
            ref var globals = ref world.Globals;
            Work.Pollution = (globals.PollutedTiles - globals.WarmingPollution) * values.PollutionPointsPerTile;
            Work.Peace = globals.Turn >= values.Peace.FirstEligibleTurn
                ? Arithmetic.ClampAB(globals.PeaceTurns * values.Peace.PointsPerTurn, 0, values.Peace.MaximumPoints) : 0;
            Work.FutureTechs = civ.FutureTechs * values.FutureTechnologyPoints;
            Work.BarbarianBonus = globals.BarbarianActivity * values.Barbarians.PointsPerLevel + values.Barbarians.OffsetPoints;
            Work.Total = Work.Peace + Work.Pollution + Work.SpaceshipArrived + Work.Wonders + Work.Citizens + Work.BarbarianBonus + Work.FutureTechs;
            if (Work.Total < 0)
            {
                Work.Total = 0;
            }

            Work.ConquestBonus = 0;
            if (globals.CivsAlive.Except(CivilizationSet.Only(0)) == CivilizationSet.Only(owner))
            {
                int endTurn = globals.Turn;
                var conquest = values.Conquest;
                if (globals.Rules.NoRestart && endTurn <= conquest.NoRestartMinimumTurn)
                {
                    endTurn = conquest.NoRestartMinimumTurn;
                }

                Work.ConquestBonus = conquest.PointsPerEnemy * globals.NumEnemies
                    + conquest.PointsPerRemainingTurn * (conquest.TurnReference - endTurn) + conquest.BasePoints;
                if (globals.Status.LargeMap)
                {
                    Work.ConquestBonus = Arithmetic.Divide32(Work.ConquestBonus * conquest.LargeMapMultiplier.Numerator, conquest.LargeMapMultiplier.Denominator);
                }

                if (globals.Status.SmallMap)
                {
                    Work.ConquestBonus = Arithmetic.Divide32(Work.ConquestBonus * conquest.SmallMapMultiplier.Numerator, conquest.SmallMapMultiplier.Denominator);
                }
            }
            Work.ScenarioPoints = 0;
            if (!Objective)
            {
                return Math.Max(Work.Total, Work.ConquestBonus);
            }

            return CalculateObjectives(owner);
        }
    }

    private int CalculateObjectives(int owner)
    {
        unchecked
        {
            Work.TotalObjectives = Work.ObjectiveCivValue = Work.CivObjectives = 0;
            for (int i = 0; i < world.Entities.Cities.End; ++i)
            {
                ref var city = ref world.Entities.Cities.Storage[i];
                if (!city.Token.Occupied)
                {
                    continue;
                }

                int amount = ObjectiveValue(new((ushort)i));
                Work.TotalObjectives += amount;
                if ((sbyte)city.Owner.Value == world.Scenario.ObjectiveCiv)
                {
                    Work.ObjectiveCivValue += amount;
                }

                if ((sbyte)city.Owner.Value == owner)
                {
                    Work.CivObjectives += amount;
                }
            }
            if (world.Scenario.ObjectiveCiv == 0)
            {
                Work.ObjectiveCivValue = Work.CivObjectives;
            }

            int score = Work.ObjectiveCivValue;
            ref var thresholds = ref world.Scenario.VictoryThresholds;
            Work.ObjectiveOutcome = score >= thresholds[0] ? ObjectiveOutcome.DecisiveVictory
                : score >= thresholds[1] ? ObjectiveOutcome.MarginalVictory
                : score <= thresholds[3] ? ObjectiveOutcome.DecisiveDefeat
                : score <= thresholds[2] ? ObjectiveOutcome.MarginalDefeat : ObjectiveOutcome.Draw;
            int outcome = (int)Work.ObjectiveOutcome;
            if (world.Scenario.ObjectiveCiv != 0 && world.Scenario.ObjectiveCiv != owner)
            {
                outcome = 4 - outcome;
            }

            Work.ScenarioPoints = values.OutcomePoints[outcome] + values.PointsPerObjective * Work.CivObjectives;
            if (Work.ConquestBonus > 0)
            {
                Work.ScenarioPoints *= values.ScenarioConquestMultiplier;
            }

            Work.ConquestBonus = -1;
            Work.Total = Work.ScenarioPoints;
            return Work.ScenarioPoints;
        }
    }

    public void PrepareRating()
    {
        int factor = values.RatingDifficultyFactors[(int)world.Globals.Difficulty];
        int rating = unchecked(factor * Math.Max(Work.Total, Work.ConquestBonus)) / 100;
        int title = 0;
        for (int i = 1; i <= 24; ++i)
        {
            if (i * i / 3 <= rating)
            {
                title = i - 1;
            }
        }

        Work.TitleIndex = Math.Min(title, 23);
        Work.Rating = rating;
    }

    public ScoreReport? Prepare(int owner)
    {
        MapKnowledge.RequireLocal(world);
        if (world.Globals.Status.Scored)
        {
            return null;
        }

        if (!Objective)
        {
            for (int i = 0; i < world.Entities.Cities.End; ++i)
            {
                ref var city = ref world.Entities.Cities.Storage[i];
                if (city.Token.Occupied && unchecked((sbyte)city.Owner.Value) == owner)
                {
                    economy.Calculate(new((ushort)i), true);
                }
            }
        }

        Calculate(owner);
        PrepareRating();
        var visible = new List<int>();
        for (int w = 0; w < RuleLimits.Wonders; ++w)
        {
            int location = world.Globals.WonderCity[w];
            if (location >= 0 && location < world.Entities.Cities.End
                && world.Entities.Cities.Storage[location].Token.Occupied
                && unchecked((sbyte)world.Entities.Cities.Storage[location].Owner.Value) == owner)
            {
                visible.Add(w);
            }
        }
        return new(owner, Objective, Work.Citizens, Work.Wonders, Work.SpaceshipInFlight, Work.SpaceshipArrived,
            Work.Pollution, Work.Peace, Work.FutureTechs, Work.BarbarianBonus, Work.Total, Work.ConquestBonus,
            Work.TotalObjectives, Work.ObjectiveCivValue, Work.CivObjectives, Work.ObjectiveOutcome,
            new(Enumerable.Range(0, 4).Select(i => world.Scenario.VictoryThresholds[i])), new(visible),
            Work.Rating, Work.TitleIndex, world.Globals.Turn >= values.Peace.FirstEligibleTurn, world.Globals.Status.Scenario, world.Globals.Status.Cheated);
    }
}
