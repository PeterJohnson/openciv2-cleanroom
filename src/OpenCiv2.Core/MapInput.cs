using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Core;

/// <summary>Owned W01/F06 semantic map operands; no reference to a live allocation.</summary>
public sealed record MapInput
{
    public int Width { get; init; }
    public int Height { get; init; }
    public ushort Topology { get; init; }
    public ushort Seed { get; init; }
    public OwnedList<Tile> Tiles { get; init; } = OwnedList<Tile>.Empty;
    public OwnedList<OwnedList<MapFeatures>> Remembered { get; init; } =
        new(Enumerable.Repeat(OwnedList<MapFeatures>.Empty, EntityLimits.PlayerCivilizations));
    public OwnedList<OwnedList<byte>> Coarse { get; init; } = new(Enumerable.Repeat(OwnedList<byte>.Empty, 2));
    public OwnedList<RegionSummary> Regions { get; init; } = new(
        Enumerable.Range(0, MapLimits.Regions).Select(_ => new RegionSummary(0, 0, 0, default)));

    public void Validate(bool embedded)
    {
        var shape = new Geometry(Width, Height);
        if (Tiles.Count != shape.Area)
        {
            throw new ArgumentException("Map tile count does not match dimensions");
        }

        if (!embedded)
        {
            return;
        }

        if (Remembered.Count != EntityLimits.PlayerCivilizations || Remembered.Any(layer => layer.Count != shape.Area))
        {
            throw new ArgumentException("Map memory does not match dimensions");
        }

        int count = shape.CoarseWidth * shape.CoarseHeight;
        if (Coarse.Count != 2 || Coarse.Any(grid => grid.Count != count))
        {
            throw new ArgumentException("Coarse map does not match dimensions");
        }

        if (Regions.Count != MapLimits.Regions)
        {
            throw new ArgumentException("Map requires 64 regional summaries");
        }
    }

    /// <summary>Validate and allocate fully before swapping; preserve destination fallback and barbarian
    /// memory.</summary>
    public void Install(MapStorage destination, bool embedded)
    {
        ArgumentNullException.ThrowIfNull(destination);
        Validate(embedded);
        var staged = new MapStorage(
            new TileCell())
        {
            StoredTopology = Topology,
            PatternSeed = Seed,
            RememberedBarbarian = destination.RememberedBarbarian.ToArray()
        };
        if (!embedded)
        {
            staged.RetainRegions(destination);
        }

        staged.Allocate(new(Width, Height));
        for (int i = 0; i < Tiles.Count; ++i)
        {
            staged.Tiles[i] = Tiles[i];
        }

        if (embedded)
        {
            for (int i = 0; i < EntityLimits.PlayerCivilizations; ++i)
            {
                for (int j = 0; j < Remembered[i].Count; ++j)
                {
                    staged.RememberedLayer(i + 1)[j] = Remembered[i][j];
                }
            }

            for (int i = 0; i < 2; ++i)
            {
                for (int j = 0; j < Coarse[i].Count; ++j)
                {
                    staged.CoarseGrid(i)[j] = Coarse[i][j];
                }
            }

            for (int i = 0; i < MapLimits.Regions; ++i)
            {
                staged.Regions[i] = Regions[i];
            }
        }
        destination.SwapStorage(staged);
    }
}
