using OpenCiv2.Base;

namespace OpenCiv2.Core.State;

/// <summary>F02/S02 retained semantic operands; serialized presence and opaque bytes belong to Formats.</summary>
public record struct EventValues
{
    public uint Triggers, Actions;
    public int UnitType, WhoCiv, TalkerType, WhomCiv, ListenerType, Turn, Denominator, Technology;
    public int MoveOwnerCiv, MoveUnitType, MoveCount;
    public Fixed8<int> MoveRectangle;
    public int MoveX, MoveY, AggressionWhomCiv, AggressionWhoCiv, CreateOwnerCiv, CreateUnitType;
    public Fixed20<int> CreateLocations;
    public int CreateLocationCount;
    public bool CreateVeteran;
    public int AuxiliaryScalar, MoneyReceiverCiv, MoneyAmount;
    public bool NegotiationFlag;
    public int CdTrack, TerrainType;
    public Fixed8<int> TerrainRectangle;
    public int DestroyCiv, GiveTech, GiveTechCiv;
}

/// <summary>S02 §2 shared logical string identity. Absent differs from present empty.</summary>
public readonly struct EventString : IEquatable<EventString>
{
    private sealed class Backing(OwnedList<byte> text) { public OwnedList<byte> Text = text; }

    private readonly Backing? backing;

    public EventString(IEnumerable<byte> text) => backing = new(new(text.TakeWhile(b => b != 0)));

    public bool Present => backing is not null;
    public OwnedList<byte> Value
    {
        get => (backing ?? throw new ArgumentOutOfRangeException(nameof(Value), "Event string is absent")).Text;
        set =>
            (backing
                ?? throw new ArgumentOutOfRangeException(nameof(Value), "Event string is absent")).Text = value
                ?? throw new ArgumentNullException(nameof(value));
    }

    public bool Equals(EventString other) => ReferenceEquals(backing, other.backing);

    public override bool Equals(object? obj) => obj is EventString other && Equals(other);

    public override int GetHashCode() => backing?.GetHashCode() ?? 0;

    public static bool operator ==(EventString left, EventString right) => left.Equals(right);

    public static bool operator !=(EventString left, EventString right) => !left.Equals(right);
}
public sealed record EventInput(EventValues Values, OwnedList<OwnedList<byte>?> Strings);
public sealed class Event
{
    public const int StringCount = 35, TextBegin = 4, OtherBegin = 24;

    internal Event(ulong identity) => Identity = identity;

    public ulong Identity { get; }
    public EventValues Values;
    private readonly EventString[] strings = new EventString[StringCount];
    public Span<EventString> Strings => strings;

    public void SetString(int field, EventString text) => strings[field] = text;
}
public sealed class EventCollection
{
    private List<Event> entries = [];
    private ulong nextIdentity = 1;
    public int Count => entries.Count;
    public bool Pristine => entries.Count == 0 && nextIdentity == 1;

    public Event At(int index) => entries[index];

    public bool Contains(Event? value) => value is not null && entries.Contains(value);

    public Event? Find(ulong identity) => entries.Find(e => e.Identity == identity);

    public Event Append()
    {
        if (nextIdentity == ulong.MaxValue)
        {
            throw new OverflowException("Event logical identities exhausted");
        }

        var result = new Event(nextIdentity);
        entries.Add(result);
        ++nextIdentity;
        return result;
    }

    public void Reset() => entries.Clear();

    /// <summary>S02 §2: append payloads before copying strings from both heads; text holes retain aliases.</summary>
    public void CopyFrom(EventCollection source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (ReferenceEquals(this, source))
        {
            throw new ArgumentException("Event self-copy has no finite admitted traversal", nameof(source));
        }

        foreach (var from in source.entries)
        {
            var to = Append();
            to.Values = from.Values;
            from.Strings.CopyTo(to.Strings);
        }
        for (int i = 0; i < source.Count; ++i)
        {
            var from = source.At(i);
            var to = At(i);
            for (int field = 0; field < Event.StringCount; ++field)
            {
                if (field >= Event.TextBegin && field < Event.OtherBegin && !from.Strings[field].Present)
                {
                    field = Event.OtherBegin - 1;
                    continue;
                }
                if (from.Strings[field].Present)
                {
                    to.SetString(field, new(from.Strings[field].Value));
                }
            }
        }
    }

    public void Compact()
    {
        var replacement = new EventCollection();
        replacement.CopyFrom(this);
        for (int i = 0; i < Count; ++i)
        {
            replacement.At(i).Strings.CopyTo(At(i).Strings);
        }
    }

    /// <summary>Prepare complete replacement before publication; IDs remain monotonic within the collection.</summary>
    public void Install(OwnedList<EventInput> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var replacement = new EventCollection { nextIdentity = nextIdentity };
        foreach (var source in input)
        {
            if (source.Strings.Count != Event.StringCount)
            {
                throw new ArgumentException("Event input requires 35 optional string fields", nameof(input));
            }

            var entry = replacement.Append();
            entry.Values = source.Values;
            for (int field = 0; field < Event.StringCount; ++field)
            {
                if (source.Strings[field] is { } text)
                {
                    entry.SetString(field, new(text));
                }
            }
        }
        entries = replacement.entries;
        nextIdentity = replacement.nextIdentity;
    }
}
public sealed class EventState
{
    public EventCollection Collection { get; } = new();
    public bool Loaded;
}
