using System.Runtime.CompilerServices;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Formats;

/// <summary>Opaque tile fields follow a particular spatial allocation, including storage swaps.</summary>
public sealed class MapCompatibility
{
    private static readonly ConditionalWeakTable<object, StoredRegionSummary[]> retainedRegions = new();
    private readonly WeakReference<object> storage;
    private readonly WeakReference<object> regionStorage;
    private readonly StoredTile[] tiles;
    private StoredTile fallback;
    private readonly StoredRegionSummary[]? regions;

    public MapCompatibility(MapStorage map, IEnumerable<StoredTile>? imported = null, IEnumerable<StoredRegionSummary>? importedRegions = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        storage = new(map.StorageIdentity);
        regionStorage = new(map.RegionStorageIdentity);
        regions = importedRegions?.ToArray();
        if (regions is not null && regions.Length != MapLimits.Regions)
        {
            throw new ArgumentException($"Expected {MapLimits.Regions} regions", nameof(importedRegions));
        }

        tiles = imported?.ToArray() ?? new StoredTile[map.Tiles.Length];
        if (tiles.Length != map.Tiles.Length)
        {
            throw new ArgumentException("Imported tiles do not match allocated map", nameof(imported));
        }
        if (regions is not null)
        {
            retainedRegions.Remove(map.RegionStorageIdentity);
            retainedRegions.Add(map.RegionStorageIdentity, regions);
        }
        else if (retainedRegions.TryGetValue(map.RegionStorageIdentity, out var retained))
        {
            regions = retained;
        }
    }

    public bool BelongsTo(MapStorage map) => storage.TryGetTarget(out var original) &&
        ReferenceEquals(original, map.StorageIdentity);

    public StoredTile Export(MapStorage map, int index) => StateConversion.PreserveOpaque(
        StateConversion.ToStored(map.Tiles[index]), Imported(map, index));

    public bool HasRegionsFor(MapStorage map) => regionStorage.TryGetTarget(out var original) &&
        ReferenceEquals(original, map.RegionStorageIdentity);

    public StoredRegionSummary ExportRegion(MapStorage map, int index)
    {
        if (!HasRegionsFor(map))
        {
            throw new ArgumentException("Opaque regions belong to a different regional storage", nameof(map));
        }

        var result = StateConversion.ToStored(map.Regions[index]);
        return regions is null ? result : result with { Retained = regions[index].Retained };
    }

    internal ref StoredTile Imported(MapStorage map, int? index)
    {
        if (!BelongsTo(map))
        {
            throw new ArgumentException("Opaque tiles belong to a different map allocation", nameof(map));
        }
        if (index is null)
        {
            return ref fallback;
        }
        return ref tiles[index.Value];
    }
}
