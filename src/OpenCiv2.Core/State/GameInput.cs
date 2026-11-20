using OpenCiv2.Base;

namespace OpenCiv2.Core.State;

public readonly record struct TribeInput(byte TimesUsed, byte CityNameIndex, bool Female);
public sealed record LeaderCustomization(bool Plural, bool Adjective, OwnedList<OwnedList<bool>> Titles);

/// <summary>F06 format-independent restoration operands. Source tokens indicate occupancy only; installation renews
/// them.</summary>
public sealed record GameInput
{
    public GlobalValues Globals { get; init; } = new();
    public OwnedList<CivilizationNames> Names { get; init; } = new(
        Enumerable.Range(0, 8).Select(_ => new CivilizationNames()));
    public OwnedList<Civilization> Civilizations { get; init; } = OwnedList<Civilization>.Empty;
    public MapInput Map { get; init; } = new();
    public OwnedList<Unit> Units { get; init; } = OwnedList<Unit>.Empty;
    public OwnedList<City> Cities { get; init; } = OwnedList<City>.Empty;
    public OwnedList<TribeInput> Tribes { get; init; } = new(new TribeInput[21]);
    public OwnedList<LeaderCustomization?> Leaders { get; init; } = new(new LeaderCustomization?[21]);
    public short CursorX { get; init; }
    public short CursorY { get; init; }
    public Fixed150<Fixed8<byte>> PowerHistory { get; init; }
    public Layout Layout { get; init; }
    public OwnedList<MapView> Views { get; init; } = new(new MapView[8]);
    public ScenarioParameters? Scenario { get; init; }
    public EliminationHistory Eliminations { get; init; } = new();
    public MultiplayerValues? Multiplayer { get; init; }
    public OwnedList<EventInput>? Events { get; init; }
}
