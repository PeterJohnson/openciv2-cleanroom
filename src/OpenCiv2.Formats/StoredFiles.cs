using OpenCiv2.Base;
using OpenCiv2.Core;

namespace OpenCiv2.Formats;

public readonly record struct GameHeader(byte Separator = 0x1a, ushort Version = 0x2c);
public enum VersionStatus { Supported, UnsupportedOld, Newer }
public sealed record RawMapFile(MapHeader Header, OwnedList<short> StartX, OwnedList<short> StartY,
    OwnedList<StoredTile> Tiles, OwnedList<byte> Trailing);
public sealed record StoredMap(MapHeader Header, OwnedList<OwnedList<byte>> Remembered,
    OwnedList<StoredTile> Tiles, OwnedList<OwnedList<byte>> Coarse, OwnedList<StoredRegionSummary> Regions);
public readonly record struct TribeUsage(byte TimesUsed, byte CityNameIndex, byte Female);
/// <summary>F06 string markers keep null and present-empty distinct; bytes exclude the terminator.</summary>
public sealed record StoredEvent(EventRecord Record, OwnedList<OwnedList<byte>?> Strings);
public sealed record StoredSave(GameHeader Header, StoredGlobals Globals,
    OwnedList<StoredCivilizationNames> Names, OwnedList<StoredCivilization> Civilizations,
    StoredMap Map, OwnedList<StoredUnit> Units, OwnedList<StoredCity> Cities,
    OwnedList<TribeUsage> Tribes, short CursorX, short CursorY, OwnedList<StoredLeader> Leaders,
    OwnedList<OwnedList<byte>> PowerHistory, SavedLayout Layout, OwnedList<SavedMapView> Views,
    ScenarioParameters? Scenario, EliminationHistory Eliminations, MultiplayerRecord? Multiplayer,
    OwnedList<StoredEvent>? Events, OwnedList<byte> Trailing);
