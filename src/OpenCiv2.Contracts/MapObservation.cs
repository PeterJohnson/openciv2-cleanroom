using OpenCiv2.Base;

namespace OpenCiv2.Contracts;

/// <summary>Spatial features, including occupancy, structures and environmental changes. Unit presence in remembered
/// state never authorizes disclosing a current unit.</summary>
[Flags]
public enum MapFeatures : byte
{
    None = 0, Unit = 1, CitySite = 2, Irrigation = 4, Mine = 8, Road = 16, Railroad = 32, Fortification = 64, Pollution = 128
}

public readonly record struct MapPoint(int X, int Y);

/// <summary>W03 §1: base terrain is known only after exploration. Improvements are remembered components.</summary>
public readonly record struct ObservedTile(byte? Terrain, MapFeatures Improvements, bool River,
    byte Resource, bool GrasslandShield,
    MapFact<MapPresence> Hut = default, MapFact<MapTileOwnership> Ownership = default,
    MapFact<MapTileOwnership> AirbaseOccupant = default);

/// <summary>Foreign units carry display information only. An own target can be submitted through the normal command
/// lane.</summary>
public readonly record struct ObservedMapUnit(
    MapPoint Position,
    UnitType Type,
    CivilizationSlot Owner,
    UnitTarget? Target,
    MapUnitDetails Details = default);
public sealed record ObservedMapCity(
    MapPoint Position,
    CitySlot Slot,
    CityToken Token,
    string Name,
    sbyte Size,
    MapCityDetails? Details = null);

/// <summary>Owned P01/P02 map inputs in canonical W01 row order. Identity binds the snapshot to one
/// controller.</summary>
public sealed record MapObservation(
    ClientIdentity Identity,
    ulong Revision,
    int Width,
    int Height,
    bool Flat,
    OwnedList<ObservedTile> Tiles,
    OwnedList<ObservedMapUnit> Units,
    OwnedList<ObservedMapCity> Cities,
    MapAnnotations? Annotations = null)
{
    public MapObservationDelta? Delta { get; init; }
}

public enum MapFactState { Unavailable, Withheld, Known }

/// <summary>Unavailable facts lack a provider. Withheld facts lack disclosure.
/// A known nullable value may be absent, and a known numeric value may be zero.</summary>
public readonly record struct MapFact<T> where T : struct
{
    public MapFactState State { get; }
    private readonly T value;

    private MapFact(MapFactState state, T value) => (State, this.value) = (state, value);

    public static MapFact<T> Known(T value) => new(MapFactState.Known, value);

    public static MapFact<T> Withheld => new(MapFactState.Withheld, default);
    public T? Value => State == MapFactState.Known ? value : null;
}

public enum MapPresence { Absent, Present }

public readonly record struct MapHealth(int RuleHitPoints, int Damage, bool UndamagedDisplay = false)
{
    public int Maximum => RuleHitPoints == 0 ? 10 : RuleHitPoints;
    public long Remaining => UndamagedDisplay ? Maximum : (long)Maximum - Damage;
}

public readonly record struct MapUnitDetails(MapFact<MapHealth> Health, MapFact<UnitOrder> Order,
    MapFact<MapPresence> Waiting, MapFact<MapPresence> Veteran, MapFact<MapPresence> Automated,
    MapFact<int> Work, MapFact<MapPresence> StationaryAircraft = default)
{
    public static MapUnitDetails Withheld { get; } = new(MapFact<MapHealth>.Withheld, MapFact<UnitOrder>.Withheld,
        MapFact<MapPresence>.Withheld, MapFact<MapPresence>.Withheld, MapFact<MapPresence>.Withheld,
        MapFact<int>.Withheld, MapFact<MapPresence>.Withheld);
}

/// <summary>P02 city appearance policy over already admitted inputs.</summary>
public readonly record struct MapCityAppearance(int Style, int SizeClass, bool Walled)
{
    public static MapCityAppearance Resolve(int configuredStyle, int disclosedSize, bool industrial,
        bool automobile, bool electronics, bool palaceBonus, bool walled)
    {
        if (configuredStyle is < 0 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(configuredStyle));
        }

        int style = automobile && electronics ? 5 : industrial ? 4 : configuredStyle;
        var (small, medium, large) = style switch { 5 => (5, 11, 19), 4 => (5, 8, 11), _ => (4, 6, 8) };
        int sizeClass = disclosedSize >= large ? 3 : disclosedSize >= medium ? 2 : disclosedSize >= small ? 1 : 0;
        return new(style, palaceBonus && sizeClass < 3 ? sizeClass + 1 : sizeClass, walled);
    }

    public int StockIndex => Style is >= 0 and < 6 && SizeClass is >= 0 and < 4
        ? 8 * Style + 2 * SizeClass + (Walled ? 1 : 0)
        : throw new ArgumentOutOfRangeException(nameof(Style), "Unbound city appearance");
}

public enum CityKnowledge { Own, Remembered, AuthorizedReveal }

public sealed record MapCityDetails(CivilizationSlot Owner, CityKnowledge Knowledge,
    MapFact<MapCityAppearance> Appearance, MapFact<int> Population, MapFact<MapPresence> Garrison,
    MapFact<MapPresence> Disorder);

/// <summary>Known absence and a classified occupant are distinct. The nullable role belongs to a named
/// observer-safe occupant.</summary>
public readonly record struct MapTileOwnership(CivilizationSlot? Owner);

public enum MapPathKind { ActiveRoute, TradePartner, TradeBonusSatisfied, TradeBonusUnsatisfied }
/// <summary>An active route starts at the bound active unit's observed tile. Trade lines may be unbound.</summary>
public sealed record MapPath(MapPathKind Kind, OwnedList<MapPoint> Points, UnitTarget? Target = null);
public sealed record MapRegion(OwnedList<MapPoint> Tiles);

/// <summary>Optional W03/P02 data admitted by the observation owner, including authored-only paths.</summary>
public sealed record MapAnnotations(OwnedList<byte> FactionRoles, UnitTarget? ActiveUnit = null,
    OwnedList<MapPath>? Paths = null, MapRegion? Region = null)
{
    public byte ColorRole(CivilizationSlot slot)
    {
        int index = slot.Value;
        byte role = index < FactionRoles.Count ? FactionRoles[index] : slot.Value;
        return role < ColorRoleCount ? role : throw new InvalidDataException($"Unsupported map owner color role {role}");
    }

    public const int ColorRoleCount = 8;

    public static MapAnnotations Stock { get; } = new(new OwnedList<byte>([0, 1, 2, 3, 4, 5, 6, 7]));
}
