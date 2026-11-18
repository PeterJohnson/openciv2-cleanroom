using OpenCiv2.Base;

namespace OpenCiv2.Core;

public enum Terrain : byte { Desert, Plains, Grassland, Forest, Hills, Mountains, Tundra, Glacier, Swamp, Jungle, Ocean }
public record struct Tile
{
    public Tile() { Terrain = Terrain.Ocean; Owner = 0; }
    public Terrain Terrain { get; set; }
    public byte TerrainFlags { get; set; }
    public byte Features { get; set; }
    public byte Claimant { get; set; }
    public byte RadiusOther { get; set; }
    public byte Region { get; set; }
    public byte Revelation { get; set; }
    public byte Fertility { get; set; }
    public byte? Owner { get; set; }
    public bool Ocean => Terrain == Terrain.Ocean;
    public bool City => (Features & 0x42) == 0x02;
    public bool Airbase => (Features & 0x42) == 0x42;
    public bool Unit => (Features & 1) != 0;
}
public sealed record RegionSummary(short Land, short Fertile, short Water, ulong NeighboringWater, OwnedList<byte> Retained);
public readonly record struct CivilizationGoal(short X, short Y, sbyte Role, sbyte Priority);
