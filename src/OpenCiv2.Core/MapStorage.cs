using OpenCiv2.Base;

namespace OpenCiv2.Core;

/// <summary>Process-owned fallback shared by its maps, independent of map allocation/release.</summary>
public sealed class TileCell { public Tile Value = new(); }
public enum TileField { Features, Terrain, Revelation, Fertility, Owner, Region, Claimant }
public readonly record struct TileMutation(TileField Field, Position Position, int Operand, int Set = 1, bool Synchronize = false);
public readonly record struct TileSyncControls(int MultiplayerMode, bool Client, bool Batching, bool Enabled);
public enum TileSyncRoute { ClientBatch, RequestHost, Broadcast }
public readonly record struct TileSyncEffect(TileSyncRoute Route, TileMutation Mutation);

/// <summary>W01 owned spatial layers. Borrowed spans/references are for synchronous engine operations, never public client observations.</summary>
public sealed class MapStorage
{
    private readonly TileCell fallback;
    private Geometry? shape;
    private Tile[]? tiles;
    private byte[]?[] remembered = new byte[8][];
    private byte?[]?[] coarse = new byte?[4][];
    private RegionSummary[] regions = Enumerable.Range(0, 64).Select(_ => new RegionSummary(0, 0, 0, 0, new([0, 0]))).ToArray();
    public MapStorage(TileCell fallback) => this.fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
    public bool Allocated { get; private set; }
    public Geometry Geometry => shape ?? throw new InvalidOperationException("Map dimensions have not been supplied");
    public ushort PatternSeed { get; set; }
    public ushort StoredTopology { get; set; }
    public Span<RegionSummary> Regions => regions;
    public byte[] RememberedBarbarian { get; set; } = [];
    public Span<Tile> Tiles => tiles ?? throw new InvalidOperationException("Tile storage is unavailable");
    public void Allocate(Geometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry); shape = geometry;
        tiles = new Tile[geometry.Area]; Array.Fill(tiles, new Tile());
        for (int civ = 1; civ < 8; ++civ) remembered[civ] = new byte[geometry.Area];
        for (int grid = 0; grid < 4; ++grid) coarse[grid] = new byte?[geometry.CoarseWidth * geometry.CoarseHeight];
        // Managed spans keep replaced arrays alive while an admitted old view exists.
        // No retained-buffer list or pointer-based lifetime emulation is necessary.
        Allocated = true;
    }
    public void Release()
    {
        if (!Allocated) return;
        Array.Clear(coarse); tiles = null;
        for (int civ = 1; civ < 8; ++civ) remembered[civ] = null;
        Allocated = false;
    }
    public void SwapStorage(MapStorage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        (shape, other.shape) = (other.shape, shape); (Allocated, other.Allocated) = (other.Allocated, Allocated);
        (tiles, other.tiles) = (other.tiles, tiles); (remembered, other.remembered) = (other.remembered, remembered); (coarse, other.coarse) = (other.coarse, coarse);
        (PatternSeed, other.PatternSeed) = (other.PatternSeed, PatternSeed); (StoredTopology, other.StoredTopology) = (other.StoredTopology, StoredTopology);
        (regions, other.regions) = (other.regions, regions); (RememberedBarbarian, other.RememberedBarbarian) = (other.RememberedBarbarian, RememberedBarbarian);
        // Fallback binding stays with the supplying process.
    }
    public ref Tile TileAt(Position position)
    {
        int? index = Geometry.TileIndex(position);
        if (index is null) return ref fallback.Value;
        return ref Tiles[index.Value];
    }
    private static int CheckedIndex(int index, int length) => index >= 0 && index < length ? index : throw new ArgumentOutOfRangeException(nameof(index), "Direct grid index has no supplied backing");
    public Span<byte> RememberedLayer(int civilization)
    {
        if (civilization < 0 || civilization >= 8) throw new ArgumentOutOfRangeException(nameof(civilization));
        if (civilization == 0) return RememberedBarbarian;
        return remembered[civilization] ?? throw new InvalidOperationException("Remembered layer is unavailable");
    }
    public ref byte Remembered(int civilization, Position position)
    {
        var layer = RememberedLayer(civilization);
        int index = unchecked(Arithmetic.ShiftSigned(position.X, -1) + (Geometry.Width / 2) * position.Y);
        return ref layer[CheckedIndex(index, layer.Length)];
    }
    public Span<byte?> CoarseGrid(int grid)
    {
        if (grid < 0 || grid >= 4 || coarse[grid] is null) throw new ArgumentOutOfRangeException(nameof(grid), "Coarse grid is unavailable");
        return coarse[grid];
    }
    private ref byte? CoarseCell(int grid, Position position)
    {
        var values = CoarseGrid(grid); int index = unchecked(Geometry.CoarseWidth * position.Y + position.X);
        return ref values[CheckedIndex(index, values.Length)];
    }
    public byte Coarse(int grid, Position position) => CoarseCell(grid, position) ?? throw new InvalidOperationException("Coarse cell has not been initialized by a producer");
    public void SetCoarse(int grid, Position position, byte value) => CoarseCell(grid, position) = value;
    public byte? Owner(Position position) => Geometry.InBounds(position) ? TileAt(position).Owner : null;
    public byte? CityOwner(Position position) => TileAt(position).City ? Owner(position) : null;
    public byte? UnitOwner(Position position) => TileAt(position).Unit ? Owner(position) : null;
    public byte? AirbaseOwner(Position position) => TileAt(position).Airbase ? Owner(position) : null;
    public byte? CityOrUnitOwner(Position position) => CityOwner(position) ?? UnitOwner(position);
    public byte? LandRegion(Position position) => TileAt(position).Ocean ? null : TileAt(position).Region;
    public byte ClaimantOrFertility(Position position)
    {
        ref var selected = ref TileAt(position);
        return selected.Claimant != 0 ? selected.Claimant : selected.Fertility is >= 1 and <= 8 ? (byte)8 : selected.Fertility;
    }
    public uint Revelation(Position position, int civilization) => civilization < 0 ? 1U : TileAt(position).Revelation & (1U << (civilization & 31));
    public bool SelectTerrain(Position position, bool ocean, bool flat, ref Position selected)
    {
        for (int i = 0; i < 2; ++i)
        {
            var candidate = new Position(Geometry.WrapX(unchecked(position.X + i), flat), unchecked(position.Y + i));
            if (Geometry.InBounds(candidate) && TileAt(candidate).Ocean == ocean) { selected = candidate; return true; }
        }
        return false;
    }
    private (int U, int V, uint Cell, int Pattern) PatternAt(Position position)
    {
        // Rectangular admission bounds sums; negative V still needs floor division.
        int u = (position.X + position.Y) / 2, v = position.X - u;
        uint cell = (unchecked((uint)u) & 3) + 4 * (unchecked((uint)v) & 3);
        return (u, v, cell, 11 * ((position.X + position.Y) / 8) + 13 * Arithmetic.ShiftSigned(v, -2) + PatternSeed);
    }
    public byte SpecialResource(Position position)
    {
        if (!Geometry.InBounds(position)) return 0;
        ref var selected = ref TileAt(position);
        if ((selected.TerrainFlags & 0x40) != 0 || selected.Terrain == Terrain.Grassland) return 0;
        var pattern = PatternAt(position);
        if (pattern.Cell != (unchecked((uint)pattern.Pattern) & 15)) return 0;
        int shift = (PatternSeed / 16) & 3;
        return (Arithmetic.ShiftSigned(pattern.U, -shift) & 1) == (Arithmetic.ShiftSigned(pattern.V, -shift) & 1) ? (byte)2 : (byte)1;
    }
    public bool Hut(Position position)
    {
        if (!Geometry.InBounds(position) || TileAt(position).Ocean || TileAt(position).Owner is not null) return false;
        var pattern = PatternAt(position); return pattern.Cell == (unchecked((uint)(pattern.Pattern + 8)) & 31);
    }
    public void Mutate(TileMutation mutation, TileSyncControls controls = default, Action<TileSyncEffect>? publish = null)
    {
        ref var selected = ref TileAt(mutation.Position); var before = selected; byte value = unchecked((byte)mutation.Operand);
        switch (mutation.Field)
        {
            case TileField.Features: selected.Features = mutation.Set != 0 ? (byte)(selected.Features | value) : (byte)(selected.Features & ~value); break;
            case TileField.Terrain: selected.Terrain = (Terrain)(value & 15); selected.TerrainFlags |= (byte)(value & 0xf0); break;
            case TileField.Revelation: selected.Revelation = mutation.Set != 0 ? (byte)(selected.Revelation | value) : (byte)(selected.Revelation & ~value); break;
            case TileField.Fertility: selected.Fertility = (byte)(value & 15); break;
            case TileField.Owner: selected.Owner = mutation.Operand is >= 0 and <= 8 ? value : null; mutation = mutation with { Operand = selected.Owner ?? 15 }; break;
            case TileField.Region: selected.Region = value; break;
            case TileField.Claimant: selected.Claimant = (byte)(value & 7); break;
            default: throw new ArgumentException("Unknown tile field", nameof(mutation));
        }
        if (!mutation.Synchronize || selected == before || controls.MultiplayerMode <= 2) return;
        TileSyncRoute? route = controls.Client && controls.Batching ? TileSyncRoute.ClientBatch : controls.Enabled ? controls.Client ? TileSyncRoute.RequestHost : TileSyncRoute.Broadcast : null;
        if (route is null) return;
        if (publish is null) throw new InvalidOperationException("Tile synchronization requires a publisher after local mutation");
        publish(new(route.Value, mutation));
    }
}
