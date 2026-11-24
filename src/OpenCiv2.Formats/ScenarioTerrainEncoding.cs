using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Formats;

/// <summary>S02 §9 replaces the entire terrain encoding, unlike W01's additive terrain setter.</summary>
public sealed class ScenarioTerrainEncoding(MapStorage map, MapCompatibility compatibility) : IScenarioTerrainEncoding
{
    public void Replace(MapPoint position, byte value)
    {
        // Validate the allocation before publishing any part of the replacement.
        ref var imported = ref compatibility.Imported(map, map.Geometry.TileIndex(position));
        ref var tile = ref map.TileAt(position);
        tile.Terrain = (Terrain)(value & 15);
        tile.River = (value & 0x80) != 0;
        tile.ResourceSuppressed = (value & 0x40) != 0;
        imported.TerrainFlags = (byte)(value & 0x30);
    }
}
