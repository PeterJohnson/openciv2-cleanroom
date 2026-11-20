using OpenCiv2.Core;

namespace OpenCiv2.Formats;

/// <summary>W01 Appendix A / F06 six-byte tile, including unassigned terrain and radius bits.</summary>
public record struct StoredTile
{
    public StoredTile()
    {
        Terrain = Terrain.Ocean;
        Owner = 0;
    }

    public Terrain Terrain { get; set; }
    public byte TerrainFlags { get; set; }
    public byte Features { get; set; }
    public byte Claimant { get; set; }
    public byte RadiusOther { get; set; }
    public byte Region { get; set; }
    public byte Revelation { get; set; }
    public byte Fertility { get; set; }
    public byte? Owner { get; set; }
}
