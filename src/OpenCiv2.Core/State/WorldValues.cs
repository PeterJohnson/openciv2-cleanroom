using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Core.State;

public readonly record struct Rectangle(int Left, int Top, int Right, int Bottom);
public record struct Layout
{
    public bool Valid;
    public Fixed13<Rectangle> Rectangles;
}
public record struct MapView(bool Open, bool TrackMoves, MapPoint Center, short Zoom);
public record struct WorldSetup
{
    public short Width, Height;
    public Fixed5<int> Parameters;
}
public record struct StartingPositions
{
    public StartingPositions()
    {
        for (int i = 0; i < RuleLimits.Tribes; ++i)
        {
            Positions[i] = new(-1, -1);
        }
    }

    public TribeArray<MapPoint> Positions;
}
public record struct CivilizationNames
{
    public CivilizationNames() { }

    public short CityStyle;
    public string Leader = string.Empty,
        Tribe = string.Empty,
        Adjective = string.Empty;
    public OwnedList<string> Titles = new(Enumerable.Repeat(string.Empty, RuleLimits.Governments));
}
public record struct EliminationHistory
{
    public const int Capacity = 12;
    public EliminationHistory() { }

    public short Count;
    public EliminationArray<short> Turns;
    public EliminationArray<byte> Killers, Portraits;
    public OwnedList<string> Names = new(Enumerable.Repeat(string.Empty, Capacity));
}
public record struct ScenarioParameters
{
    public ScenarioParameters() { }

    public ScenarioOptions Flags;
    public string Name = string.Empty;
    public short TechnologyParadigm, YearIncrement, StartYear, MaximumTurns, ObjectiveCiv;
    public Fixed4<short> VictoryThresholds;

    // S01 §2 / F06: parameters only, not world/event flags.
    public void Reset() => this = new() { TechnologyParadigm = 10 };
}
public sealed class TextParameters
{
    public const int StringCount = 11;
    public Fixed6<int> Numbers;
    public string[] Strings { get; } = Enumerable.Repeat(string.Empty, StringCount).ToArray();
    public string Line = string.Empty;

    public void Clear()
    {
        Numbers = default;
        Array.Fill(Strings, string.Empty);
        Line = string.Empty;
    }
}
