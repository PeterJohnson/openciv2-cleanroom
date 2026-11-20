using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Formats;

public enum ClassicTileField { Features, Terrain, Revelation, Fertility, Owner, Region, Claimant }
public readonly record struct ClassicTileMutation(ClassicTileField Field, MapPoint Position,
    int Operand, int Set = 1, bool Synchronize = false);
public readonly record struct ClassicTileSyncEffect(TileSyncRoute Route, ClassicTileMutation Mutation);

/// <summary>W01 Appendix A: packed operands, reductions and original synchronization operands stay at the boundary.</summary>
public static class ClassicTileMutations
{
    public static uint Revelation(MapStorage map, MapPoint position, int civilization) => civilization < 0 ? 1U :
        StateConversion.EncodeCivilizations(map.TileAt(position).RevealedTo) & (1U << (civilization & 31));

    public static void ApplyClassic(this MapStorage map, ClassicTileMutation mutation,
        TileSyncControls controls = default, Action<ClassicTileSyncEffect>? publish = null,
        MapCompatibility? compatibility = null)
    {
        int? index = map.Geometry.TileIndex(mutation.Position);
        var before = StateConversion.ToStored(map.TileAt(mutation.Position));
        if (compatibility is not null)
        {
            before = StateConversion.PreserveOpaque(before, compatibility.Imported(map, index));
        }
        byte value = unchecked((byte)mutation.Operand);
        var position = mutation.Position;
        bool set = mutation.Set != 0;
        TileChange change = mutation.Field switch
        {
            ClassicTileField.Features => new TileChange.Features(position, StateConversion.DecodeMapFeatures(value), set),
            ClassicTileField.Terrain => new TileChange.Terrain(position, (Terrain)(value & 15),
                (value & 0x80) != 0, (value & 0x40) != 0),
            ClassicTileField.Revelation => new TileChange.Revelation(position, StateConversion.DecodeCivilizations(value), set),
            ClassicTileField.Fertility => new TileChange.Fertility(position, (byte)(value & 15)),
            ClassicTileField.Owner => new TileChange.Owner(position, mutation.Operand is >= 0 and <= EntityLimits.Civilizations ? value : null),
            ClassicTileField.Region => new TileChange.Region(position, value),
            ClassicTileField.Claimant => new TileChange.Claimant(position, (byte)(value & 7)),
            _ => throw new ArgumentException("Unknown classic tile field", nameof(mutation))
        };
        if (mutation.Field == ClassicTileField.Terrain && (value & 0x30) != 0)
        {
            if (compatibility is null)
            {
                throw new ArgumentException("Opaque terrain mutations require map compatibility storage", nameof(compatibility));
            }
            compatibility.Imported(map, index).TerrainFlags |= (byte)(value & 0x30);
        }
        map.Mutate(change);
        if (change is TileChange.Owner owner)
        {
            mutation = mutation with { Operand = owner.Civilization ?? 15 };
        }
        var after = StateConversion.ToStored(map.TileAt(position));
        if (compatibility is not null)
        {
            after = StateConversion.PreserveOpaque(after, compatibility.Imported(map, index));
        }
        if (!mutation.Synchronize || before == after || controls.MultiplayerMode <= 2)
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
        publish(new(route.Value, mutation));
    }
}
