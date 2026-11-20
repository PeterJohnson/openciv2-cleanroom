using System.Runtime.CompilerServices;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Formats;

/// <summary>Imported opaque F06 fields, owned by the adapter rather than live game state.</summary>
public sealed class RecordCompatibility(WorldLifetime lifetime)
{
    private static readonly ConditionalWeakTable<WorldLifetime, RecordCompatibility> attached = new();
    public void Attach(WorldLifetime current)
    {
        if (For(current) is null)
        {
            throw new ArgumentException("Compatibility belongs to another world", nameof(current));
        }

        attached.Remove(current);
        attached.Add(current, this);
    }
    public static RecordCompatibility? Find(WorldLifetime current) => attached.TryGetValue(current, out var value) ? value : null;
    private readonly WeakReference<WorldLifetime> lifetime = new(
        lifetime ?? throw new ArgumentNullException(nameof(lifetime)));
    public MapCompatibility? Map { get; set; }
    public StoredGlobals Globals { get; set; } = new() { Rules = 0x2f };
    public StoredCivilization[] Civilizations { get; } = Enumerable.Range(0, EntityLimits.Civilizations).Select(_ => new StoredCivilization())
        .ToArray();
    public ScenarioParameters? Scenario { get; set; }
    public MultiplayerRecord? Multiplayer { get; set; }
    public Dictionary<UnitToken, StoredUnit> Units { get; } = [];
    public Dictionary<CityToken, StoredCity> Cities { get; } = [];

    /// <summary>The save owner must select metadata for its current world before supplying it to item
    /// writers.</summary>
    public RecordCompatibility? For(WorldLifetime current) =>
        lifetime.TryGetTarget(out var original) && ReferenceEquals(current, original) ? this : null;
}
public sealed class ImportedEventBytes
{
    private readonly WeakReference<Event> owner;
    public OwnedList<byte> Retained { get; }
    public EventRecord? Record { get; }

    private ImportedEventBytes(Event owner, EventRecord record) : this(owner, record.Retained) => Record = record;

    public ImportedEventBytes(Event owner, OwnedList<byte> retained)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(retained);
        if (retained.Count != 40)
        {
            throw new ArgumentException("Event opaque data requires 40 bytes", nameof(retained));
        }

        this.owner = new(owner);
        Retained = retained;
    }

    public static ImportedEventBytes FromRecord(Event owner, EventRecord record) => new(owner, record);

    public bool BelongsTo(Event current) => owner.TryGetTarget(out var original) && ReferenceEquals(original, current);
}
