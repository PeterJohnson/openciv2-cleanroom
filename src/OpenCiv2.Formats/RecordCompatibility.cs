using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Formats;

/// <summary>Imported opaque F06 fields, owned by the adapter rather than live game state.</summary>
public sealed class RecordCompatibility(WorldLifetime lifetime)
{
    private readonly WeakReference<WorldLifetime> lifetime = new(lifetime ?? throw new ArgumentNullException(nameof(lifetime)));
    public StoredGlobals Globals { get; set; } = new();
    public StoredCivilization[] Civilizations { get; } = Enumerable.Range(0, 8).Select(_ => new StoredCivilization()).ToArray();
    public MultiplayerRecord? Multiplayer { get; set; }
    public Dictionary<UnitToken, StoredUnit> Units { get; } = [];
    public Dictionary<CityToken, StoredCity> Cities { get; } = [];
    /// <summary>The save owner must select metadata for its current world before supplying it to item writers.</summary>
    public RecordCompatibility? For(WorldLifetime current) => lifetime.TryGetTarget(out var original) && ReferenceEquals(current, original) ? this : null;
}
public sealed class ImportedEventBytes
{
    private readonly WeakReference<Event> owner;
    public OwnedList<byte> Retained { get; }
    public ImportedEventBytes(Event owner, OwnedList<byte> retained)
    {
        ArgumentNullException.ThrowIfNull(owner); ArgumentNullException.ThrowIfNull(retained);
        if (retained.Count != 40) throw new ArgumentException("Event opaque data requires 40 bytes", nameof(retained));
        this.owner = new(owner); Retained = retained;
    }
    public bool BelongsTo(Event current) => owner.TryGetTarget(out var original) && ReferenceEquals(original, current);
}
