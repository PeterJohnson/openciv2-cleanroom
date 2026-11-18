using OpenCiv2.Base;

namespace OpenCiv2.Core;

public static class GameplayNames
{
    public static OwnedList<string> Components { get; } = new(["calendar", "spaceship", "governments", "difficulty", "combat", "scoring"]);
    public static OwnedList<string> CalendarProfiles { get; } = new(["chieftain-warlord", "prince", "king", "emperor-deity"]);
    public static OwnedList<string> Governments { get; } = new(["anarchy", "despotism", "monarchy", "communism", "fundamentalism", "republic", "democracy"]);
    public static OwnedList<string> Difficulties { get; } = new(["chieftain", "warlord", "prince", "king", "emperor", "deity"]);
    public static OwnedList<string> SpaceshipParts { get; } = new(["structural", "propulsion", "fuel", "habitation", "life-support", "solar-energy"]);
    public static OwnedList<string> ScenarioOutcomes { get; } = new(["decisive-victory", "marginal-victory", "draw", "marginal-defeat", "decisive-defeat"]);
}
public readonly record struct Rational(int Numerator, int Denominator);
public readonly record struct CalendarPeriod(int StartYear, int TurnCount, int YearStep);
public sealed record CalendarValues(OwnedList<OwnedList<CalendarPeriod>> Profiles, int WarningYear, int EndYear, int ScenarioWarningLeadTurns);
public enum SpaceshipGroup { Structural, Component, Module }
public readonly record struct SpaceshipPartValues(SpaceshipGroup Group, int MaximumCount, int MassHundredsOfTons);
public sealed record SpaceshipValues(OwnedList<SpaceshipPartValues> Parts, int PeoplePerHabitation);
public sealed record GovernmentValues(int MaximumRateTenths, int CorruptionDivisor, string? RequiredAdvance);
public readonly record struct DifficultyValues(int AiBoxFactor, int HumanResearchMultiplier, int AiResearchMultiplier);
public sealed record CombatValues(Rational VeteranBonus, Rational ParadropBonus, int FortificationFactor, int FortressFactor,
    int CityWallsFactor, int OrdinaryAircraftMultiplier, int DestroyedAircraftMultiplier, int PartisansAgainstZeroAttackMultiplier);
public readonly record struct PeaceScoring(int FirstEligibleTurn, int PointsPerTurn, int MaximumPoints);
public readonly record struct BarbarianScoring(int PointsPerLevel, int OffsetPoints);
public sealed record ConquestScoring(int PointsPerEnemy, int BasePoints, int TurnReference, int PointsPerRemainingTurn,
    int NoRestartMinimumTurn, Rational LargeMapMultiplier, Rational SmallMapMultiplier);
public sealed record ScoringValues(int PerCitizen, int PerHappyCitizen, int PerUnhappyCitizen, int WonderPoints,
    int SpaceshipPointsPerPopulationSuccess, int PollutionPointsPerTile, int FutureTechnologyPoints, PeaceScoring Peace,
    BarbarianScoring Barbarians, ConquestScoring Conquest, int ObjectiveCity, int ObjectiveWonder, OwnedList<int> OutcomePoints,
    int PointsPerObjective, int ScenarioConquestMultiplier, OwnedList<int> RatingDifficultyFactors);
public sealed record GameplayValues(CalendarValues Calendar, SpaceshipValues Spaceship, OwnedList<GovernmentValues> Governments,
    OwnedList<DifficultyValues> Difficulties, CombatValues Combat, ScoringValues Scoring);
