using OpenCiv2.Base;

namespace OpenCiv2.Contracts;

public sealed record PlayOption(int Id, string Name);

public sealed record PlayUnit(UnitTarget Target, string Name, int MovesLeft, bool CanFound);

public sealed record WorkerTile(int Index, MapPoint Position, bool Worked, int Food, int Shields, int Trade);

public sealed record PlayCity(
    CityTarget Target,
    int Food,
    int FoodRequired,
    int FoodCapacity,
    int Shields,
    int ShieldSupport,
    int ShieldCost,
    int Science,
    int Tax,
    int Specialists,
    OwnedList<WorkerTile> Tiles,
    OwnedList<PlayOption> Production,
    OwnedList<PlayOption> Improvements,
    bool ImprovementSold);

/// <summary>Owned player-phase and city-window values for the bounded local ruleset.</summary>
public sealed record LocalPlayObservation(
    LiveObservation Observation,
    int Turn,
    int Year,
    bool PlayerPhase,
    int Gold,
    int Researching,
    int ResearchProgress,
    int ResearchCost,
    OwnedList<PlayOption> Research,
    OwnedList<PlayOption> Discovered,
    OwnedList<PlayUnit> Units,
    PlayCity? City);
