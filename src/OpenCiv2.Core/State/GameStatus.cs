namespace OpenCiv2.Core.State;

/// <summary>V08 pending end-sequence reason. This is distinct from saved Won/Scored status.</summary>
public enum EndReason { None, LocalSpace, OtherSpace, Conquest, Defeat, Retirement }

/// <summary>F06 §2 game status. Unknown classic flag bits belong to the format adapter.</summary>
public record struct GameStatus
{
    public bool AiCoordination;
    public bool Won;
    // Both classifications can be supplied by an imported game; the calendar consumes each independently.
    public bool LargeMap;
    public bool SmallMap;
    public bool Cheated;
    public bool Scored;
    public bool ScenarioExport;
    public bool Scenario;

    public readonly bool HasScenario => ScenarioExport || Scenario;
}
