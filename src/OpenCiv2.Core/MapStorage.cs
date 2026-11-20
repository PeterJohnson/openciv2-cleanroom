using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Core;

/// <summary>Process-owned fallback shared by its maps, independent of map allocation/release.</summary>
public sealed class TileCell { public Tile Value = new(); }
public readonly record struct TileSyncControls(int MultiplayerMode, bool Client, bool Batching, bool Enabled);
public enum TileSyncRoute { ClientBatch, RequestHost, Broadcast }
public readonly record struct TileSyncEffect(TileSyncRoute Route, TileChange Change);

/// <summary>W01 owned spatial layers. Borrowed spans/references are for synchronous engine operations, never public
/// client observations.</summary>
public sealed class MapStorage
{
    private readonly TileCell fallback;
    private Geometry? shape;
    private Tile[]? tiles;
    private MapFeatures[]?[] remembered = new MapFeatures[EntityLimits.Civilizations][];
    private byte?[]?[] coarse = new byte?[4][];
    private RegionSummary[] regions = Enumerable.Range(0, MapLimits.Regions).Select(_ => new RegionSummary(0, 0, 0, default))
        .ToArray();

    public MapStorage(TileCell fallback) =>
        this.fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));

    public bool Allocated { get; private set; }
    /// <summary>Identity of the current spatial allocation. Replacement/release invalidate it; swaps transfer it.</summary>
    public object StorageIdentity { get; private set; } = new();
    public Geometry Geometry => shape ?? throw new InvalidOperationException("Map dimensions have not been supplied");
    public ushort PatternSeed { get; set; }
    public ushort StoredTopology { get; set; }
    public Span<RegionSummary> Regions => regions;
    /// <summary>Regional summaries survive spatial allocation/release and follow storage swaps.</summary>
    public object RegionStorageIdentity { get; private set; } = new();
    internal void RetainRegions(MapStorage other)
    {
        regions = other.regions;
        RegionStorageIdentity = other.RegionStorageIdentity;
    }
    public MapFeatures[] RememberedBarbarian { get; set; } = [];
    public Span<Tile> Tiles => tiles ?? throw new InvalidOperationException("Tile storage is unavailable");

    public void Allocate(Geometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        shape = geometry;
        StorageIdentity = new();
        tiles = new Tile[geometry.Area];
        Array.Fill(tiles, new Tile());
        for (int civ = 1; civ < EntityLimits.Civilizations; ++civ)
        {
            remembered[civ] = new MapFeatures[geometry.Area];
        }

        for (int grid = 0; grid < 4; ++grid)
        {
            coarse[grid] = new byte?[geometry.CoarseWidth * geometry.CoarseHeight];
        }
        // Managed spans keep replaced arrays alive while an admitted old view exists.
        // No retained-buffer list or pointer-based lifetime emulation is necessary.
        Allocated = true;
    }

    public void Release()
    {
        if (!Allocated)
        {
            return;
        }

        StorageIdentity = new();
        Array.Clear(coarse);
        tiles = null;
        for (int civ = 1; civ < EntityLimits.Civilizations; ++civ)
        {
            remembered[civ] = null;
        }

        Allocated = false;
    }

    public void SwapStorage(MapStorage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        (shape, other.shape) = (other.shape, shape);
        (StorageIdentity, other.StorageIdentity) = (other.StorageIdentity, StorageIdentity);
        (Allocated, other.Allocated) = (other.Allocated, Allocated);
        (tiles, other.tiles) = (other.tiles, tiles);
        (remembered, other.remembered) = (other.remembered, remembered);
        (coarse, other.coarse) = (other.coarse, coarse);
        (PatternSeed, other.PatternSeed) = (other.PatternSeed, PatternSeed);
        (StoredTopology, other.StoredTopology) = (other.StoredTopology, StoredTopology);
        (regions, other.regions) = (other.regions, regions);
        (RegionStorageIdentity, other.RegionStorageIdentity) = (other.RegionStorageIdentity, RegionStorageIdentity);
        (RememberedBarbarian, other.RememberedBarbarian) = (other.RememberedBarbarian, RememberedBarbarian);
        // Fallback binding stays with the supplying process.
    }

    public ref Tile TileAt(MapPoint position)
    {
        int? index = Geometry.TileIndex(position);
        if (index is null)
        {
            return ref fallback.Value;
        }

        return ref Tiles[index.Value];
    }

    private static int CheckedIndex(int index, int length) =>
        index >= 0 && index < length
            ? index
            : throw new ArgumentOutOfRangeException(nameof(index), "Direct grid index has no supplied backing");

    public Span<MapFeatures> RememberedLayer(int civilization)
    {
        if (civilization < 0 || civilization >= EntityLimits.Civilizations)
        {
            throw new ArgumentOutOfRangeException(nameof(civilization));
        }

        if (civilization == 0)
        {
            return RememberedBarbarian;
        }

        return remembered[civilization] ?? throw new InvalidOperationException("Remembered layer is unavailable");
    }

    public ref MapFeatures Remembered(int civilization, MapPoint position)
    {
        var layer = RememberedLayer(civilization);
        int index = unchecked(Arithmetic.ShiftSigned(position.X, -1) + (Geometry.Width / 2) * position.Y);
        return ref layer[CheckedIndex(index, layer.Length)];
    }

    public Span<byte?> CoarseGrid(int grid)
    {
        if (grid < 0 || grid >= 4 || coarse[grid] is null)
        {
            throw new ArgumentOutOfRangeException(nameof(grid), "Coarse grid is unavailable");
        }

        return coarse[grid];
    }

    private ref byte? CoarseCell(int grid, MapPoint position)
    {
        var values = CoarseGrid(grid);
        int index = unchecked(Geometry.CoarseWidth * position.Y + position.X);
        return ref values[CheckedIndex(index, values.Length)];
    }

    public byte Coarse(int grid, MapPoint position) =>
        CoarseCell(grid, position)
            ?? throw new InvalidOperationException("Coarse cell has not been initialized by a producer");

    public void SetCoarse(int grid, MapPoint position, byte value) => CoarseCell(grid, position) = value;

    public byte? Owner(MapPoint position) => Geometry.InBounds(position) ? TileAt(position).Owner : null;

    public byte? CityOwner(MapPoint position) => TileAt(position).City ? Owner(position) : null;

    public byte? UnitOwner(MapPoint position) => TileAt(position).Unit ? Owner(position) : null;

    public byte? AirbaseOwner(MapPoint position) => TileAt(position).Airbase ? Owner(position) : null;

    public byte? CityOrUnitOwner(MapPoint position) => CityOwner(position) ?? UnitOwner(position);

    public byte? LandRegion(MapPoint position) => TileAt(position).Ocean ? null : TileAt(position).Region;

    public byte ClaimantOrFertility(MapPoint position)
    {
        ref var selected = ref TileAt(position);
        return selected.Claimant != 0
            ? selected.Claimant
            : selected.Fertility is >= 1 and <= 8 ? (byte)8 : selected.Fertility;
    }

    public bool IsRevealedTo(MapPoint position, int civilization) =>
        civilization < 0 || TileAt(position).RevealedTo.Contains(civilization);

    public bool SelectTerrain(MapPoint position, bool ocean, bool flat, ref MapPoint selected)
    {
        for (int i = 0; i < 2; ++i)
        {
            var candidate = new MapPoint(Geometry.WrapX(unchecked(position.X + i), flat), unchecked(position.Y + i));
            if (Geometry.InBounds(candidate) && TileAt(candidate).Ocean == ocean)
            {
                selected = candidate;
                return true;
            }
        }
        return false;
    }

    private (int U, int V, uint Cell, int Pattern) PatternAt(MapPoint position)
    {
        // Rectangular admission bounds sums; negative V still needs floor division.
        int u = (position.X + position.Y) / 2, v = position.X - u;
        uint cell = (unchecked((uint)u) & 3) + 4 * (unchecked((uint)v) & 3);
        return (u, v, cell, 11 * ((position.X + position.Y) / 8) + 13 * Arithmetic.ShiftSigned(v, -2) + PatternSeed);
    }

    public byte SpecialResource(MapPoint position)
    {
        if (!Geometry.InBounds(position))
        {
            return 0;
        }

        ref var selected = ref TileAt(position);
        if (selected.ResourceSuppressed || selected.Terrain == Terrain.Grassland)
        {
            return 0;
        }

        var pattern = PatternAt(position);
        if (pattern.Cell != (unchecked((uint)pattern.Pattern) & 15))
        {
            return 0;
        }

        int shift = (PatternSeed / 16) & 3;
        return (Arithmetic.ShiftSigned(pattern.U, -shift) & 1) == (Arithmetic.ShiftSigned(pattern.V, -shift) & 1)
            ? (byte)2
            : (byte)1;
    }

    public bool Hut(MapPoint position) => HutCandidate(position) && TileAt(position).Owner is null;

    /// <summary>W01 coordinate and terrain test independent of the live occupant.</summary>
    public bool HutCandidate(MapPoint position)
    {
        if (!Geometry.InBounds(position) || TileAt(position).Ocean)
        {
            return false;
        }

        var pattern = PatternAt(position);
        return pattern.Cell == (unchecked((uint)(pattern.Pattern + 8)) & 31);
    }

    public void Mutate(
        TileChange change,
        bool synchronize = false,
        TileSyncControls controls = default,
        Action<TileSyncEffect>? publish = null)
    {
        ArgumentNullException.ThrowIfNull(change);
        ref var selected = ref TileAt(change.Position);
        var before = selected;
        switch (change)
        {
            case TileChange.Features features:
                selected.Features = features.Set
                    ? selected.Features | features.Components
                    : selected.Features & ~features.Components;
                break;
            case TileChange.Terrain terrain:
                selected.Terrain = terrain.Kind;
                selected.River |= terrain.AddRiver;
                selected.ResourceSuppressed |= terrain.SuppressResource;
                break;
            case TileChange.Revelation revelation:
                selected.RevealedTo = revelation.Set
                    ? selected.RevealedTo.Union(revelation.Civilizations)
                    : selected.RevealedTo.Except(revelation.Civilizations);
                break;
            case TileChange.Fertility fertility:
                selected.Fertility = fertility.Value;
                break;
            case TileChange.Owner owner:
                selected.Owner = owner.Civilization;
                break;
            case TileChange.Region region:
                selected.Region = region.Value;
                break;
            case TileChange.Claimant claimant:
                selected.Claimant = claimant.Civilization;
                break;
            default:
                throw new ArgumentException("Unknown tile change", nameof(change));
        }
        if (!synchronize || selected == before || controls.MultiplayerMode <= 2)
        {
            return;
        }

        TileSyncRoute? route = controls.Client && controls.Batching
            ? TileSyncRoute.ClientBatch
            : controls.Enabled ? controls.Client ? TileSyncRoute.RequestHost : TileSyncRoute.Broadcast : null;
        if (route is null)
        {
            return;
        }

        if (publish is null)
        {
            throw new InvalidOperationException("Tile synchronization requires a publisher after local mutation");
        }

        publish(new(route.Value, change));
    }
}
