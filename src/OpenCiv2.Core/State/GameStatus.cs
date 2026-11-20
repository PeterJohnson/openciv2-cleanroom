namespace OpenCiv2.Core.State;

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
