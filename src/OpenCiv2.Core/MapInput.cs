using OpenCiv2.Base;

namespace OpenCiv2.Core;

/// <summary>Owned W01/F06 semantic map operands; no reference to a live allocation.</summary>
public sealed record MapInput
{
    public int Width { get; init; }
    public int Height { get; init; }
    public ushort Topology { get; init; }
    public ushort Seed { get; init; }
    public OwnedList<Tile> Tiles { get; init; } = OwnedList<Tile>.Empty;
    public OwnedList<OwnedList<byte>> Remembered { get; init; } = new(Enumerable.Repeat(OwnedList<byte>.Empty, 7));
    public OwnedList<OwnedList<byte>> Coarse { get; init; } = new(Enumerable.Repeat(OwnedList<byte>.Empty, 2));
    public OwnedList<RegionSummary> Regions { get; init; } = new(Enumerable.Range(0, 64).Select(_ => new RegionSummary(0, 0, 0, 0, new([0, 0]))));
    public void Validate(bool embedded)
    {
        var shape = new Geometry(Width, Height);
        if (Tiles.Count != shape.Area) throw new ArgumentException("Map tile count does not match dimensions");
        if (!embedded) return;
        if (Remembered.Count != 7 || Remembered.Any(layer => layer.Count != shape.Area)) throw new ArgumentException("Map memory does not match dimensions");
        int count = shape.CoarseWidth * shape.CoarseHeight;
        if (Coarse.Count != 2 || Coarse.Any(grid => grid.Count != count)) throw new ArgumentException("Coarse map does not match dimensions");
        if (Regions.Count != 64) throw new ArgumentException("Map requires 64 regional summaries");
    }
    /// <summary>Validate and allocate fully before swapping; preserve destination fallback and barbarian memory.</summary>
    public void Install(MapStorage destination, bool embedded)
    {
        ArgumentNullException.ThrowIfNull(destination); Validate(embedded);
        var staged = new MapStorage(new TileCell()) { StoredTopology = Topology, PatternSeed = Seed, RememberedBarbarian = destination.RememberedBarbarian.ToArray() };
        destination.Regions.CopyTo(staged.Regions); staged.Allocate(new(Width, Height));
        for (int i = 0; i < Tiles.Count; ++i) staged.Tiles[i] = Tiles[i];
        if (embedded)
        {
            for (int i = 0; i < 7; ++i) for (int j = 0; j < Remembered[i].Count; ++j) staged.RememberedLayer(i + 1)[j] = Remembered[i][j];
            for (int i = 0; i < 2; ++i) for (int j = 0; j < Coarse[i].Count; ++j) staged.CoarseGrid(i)[j] = Coarse[i][j];
            for (int i = 0; i < 64; ++i) staged.Regions[i] = Regions[i];
        }
        destination.SwapStorage(staged);
    }
}
