using OpenCiv2.Base;

namespace OpenCiv2.Core.State;

public readonly record struct Rectangle(int Left, int Top, int Right, int Bottom);
public record struct Layout
{
    public bool Valid;
    public Fixed13<Rectangle> Rectangles;
}
public record struct MapView(bool Open, bool TrackMoves, short X, short Y, short Zoom);
public record struct WorldSetup
{
    public short Width, Height;
    public Fixed5<int> Parameters;
}
public record struct StartingPositions
{
    public StartingPositions() { for (int i = 0; i < 21; ++i) { X[i] = -1; Y[i] = -1; } }
    public Fixed21<short> X, Y;
}
public record struct CivilizationNames
{
    public CivilizationNames() { }
    public short CityStyle;
    public OwnedList<byte> Leader = OwnedList<byte>.Empty, Tribe = OwnedList<byte>.Empty, Adjective = OwnedList<byte>.Empty;
    public OwnedList<OwnedList<byte>> Titles = new(Enumerable.Repeat(OwnedList<byte>.Empty, 7));
}
public record struct EliminationHistory
{
    public EliminationHistory() { }
    public short Count;
    public Fixed12<short> Turns;
    public Fixed12<byte> Killers, Portraits;
    public OwnedList<OwnedList<byte>> Names = new(Enumerable.Repeat(OwnedList<byte>.Empty, 12));
}
public record struct ScenarioParameters
{
    public ScenarioParameters() { }
    public ushort Flags;
    public OwnedList<byte> Name = OwnedList<byte>.Empty;
    public short TechnologyParadigm, YearIncrement, StartYear, MaximumTurns, ObjectiveCiv;
    public Fixed4<short> VictoryThresholds;
    // S01 §2 / F06: parameters only, not world/event flags.
    public void Reset() => this = new() { TechnologyParadigm = 10 };
}
public sealed class TextParameters
{
    public Fixed6<int> Numbers;
    public OwnedList<byte>[] Strings { get; } = Enumerable.Repeat(OwnedList<byte>.Empty, 11).ToArray();
    public OwnedList<byte> Line = OwnedList<byte>.Empty;
    public void Clear()
    {
        Numbers = default; Array.Fill(Strings, OwnedList<byte>.Empty); Line = OwnedList<byte>.Empty;
    }
}
