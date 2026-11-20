using OpenCiv2.Contracts;
namespace OpenCiv2.Core;

public enum Terrain : byte
{
    Desert,
    Plains,
    Grassland,
    Forest,
    Hills,
    Mountains,
    Tundra,
    Glacier,
    Swamp,
    Jungle,
    Ocean
}
/// <summary>W01 logical spatial state; opaque terrain/radius bytes remain in stored records.</summary>
public record struct Tile
{
    public Tile()
    {
        Terrain = Terrain.Ocean;
        Owner = 0;
    }

    public Terrain Terrain { get; set; }
    public bool River { get; set; }
    public bool ResourceSuppressed { get; set; }
    public MapFeatures Features { get; set; }
    public byte Claimant { get; set; }
    public byte Region { get; set; }
    public CivilizationSet RevealedTo { get; set; }
    public byte Fertility { get; set; }
    public byte? Owner { get; set; }
    public bool Ocean => Terrain == Terrain.Ocean;
    public bool City => (Features & (MapFeatures.CitySite | MapFeatures.Fortification)) == MapFeatures.CitySite;
    public bool Airbase => (Features & (MapFeatures.CitySite | MapFeatures.Fortification)) ==
        (MapFeatures.CitySite | MapFeatures.Fortification);
    public bool Unit => Features.HasFlag(MapFeatures.Unit);
}
public sealed record RegionSummary(
    short Land,
    short Fertile,
    short Water,
    RegionSet NeighboringWater);
public readonly record struct CivilizationGoal(MapPoint Position, sbyte Role, sbyte Priority);
