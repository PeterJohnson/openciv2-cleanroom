using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Core;

/// <summary>W01 §6 semantic tile operations. Classic packed operands are translated by Formats.</summary>
public abstract record TileChange(MapPoint Position)
{
    public sealed record Features(MapPoint Position, MapFeatures Components, bool Set = true) : TileChange(Position);
    public sealed record Terrain(MapPoint Position, Core.Terrain Kind, bool AddRiver = false,
        bool SuppressResource = false) : TileChange(Position);
    public sealed record Revelation(MapPoint Position, CivilizationSet Civilizations, bool Set = true) : TileChange(Position);
    public sealed record Fertility(MapPoint Position, byte Value) : TileChange(Position);
    public sealed record Owner(MapPoint Position, byte? Civilization) : TileChange(Position);
    public sealed record Region(MapPoint Position, byte Value) : TileChange(Position);
    public sealed record Claimant(MapPoint Position, byte Civilization) : TileChange(Position);
}
