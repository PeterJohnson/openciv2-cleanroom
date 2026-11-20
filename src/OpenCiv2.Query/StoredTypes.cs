using System.Globalization;
using OpenCiv2.Base;

namespace OpenCiv2.Query;

public enum FileKind { Save, RawMap }
public enum FileAccess { OfflineOmniscient }
public enum Section
{
    Header, Globals, CivilizationNames, Civilizations, MapHeader, Starts, Remembered, Tiles,
    Coarse, Regions, Units, Cities, Tribes, Cursor, Leaders, PowerHistory, Layout, Views,
    Scenario, Eliminations, Multiplayer, EventHeader, Events, EventStrings, Trailing
}
public static class Sections
{
    private static readonly string[] Names = [
        "header",
        "globals",
        "civilization_names",
        "civilizations",
        "map_header",
        "starts",
        "remembered",
        "tiles",
        "coarse",
        "regions",
        "units",
        "cities",
        "tribes",
        "cursor",
        "leaders",
        "power_history",
        "layout",
        "views",
        "scenario",
        "eliminations",
        "multiplayer",
        "event_header",
        "events",
        "event_strings",
        "trailing"
    ];

    public static string Name(this Section section) => Names[(int)section];

    public static Section? Parse(string value)
    {
        int index = Array.IndexOf(Names, value);
        return index < 0 ? null : (Section)index;
    }
}
public readonly record struct RecordKey(Section Section, int Slot);
public abstract record FieldValue
{
    private protected FieldValue() { }

    public sealed record Signed(long Value) : FieldValue;

    public sealed record Unsigned(ulong Value) : FieldValue;

    public sealed record Bytes(OwnedList<byte> Value) : FieldValue;

    public string Text() => this switch
    {
        Signed value => value.Value.ToString(CultureInfo.InvariantCulture),
        Unsigned value => value.Value.ToString(CultureInfo.InvariantCulture),
        Bytes value => ContentIdentity.HexBytes(value.Value.ToArray()),
        _ => throw new InvalidOperationException("Unknown inspection value.")
    };
}
public sealed record Field(string Name, string Type, FieldValue? Value)
{
    public string Text => Value?.Text() ?? "null";
}
public sealed record Record(RecordKey Key, int Offset, OwnedList<byte> Bytes, OwnedList<Field> Fields);
public enum Category { Structural, Compatibility, Semantic }
public enum Severity { Information, Warning, Error }
public sealed record Diagnostic(
    Category Category,
    Severity Severity,
    string Code,
    string Field,
    string Message,
    int? Offset);
public sealed class StoredSnapshot
{
    public FileKind Kind { get; }
    public ushort? Version { get; }
    public string SourceHash { get; }
    public OwnedList<byte> SourceBytes { get; }
    public OwnedList<Record> Records { get; }
    public OwnedList<Diagnostic> Diagnostics { get; }
    public OwnedList<string> Checks { get; }
    public bool Decoded { get; }
    public bool HasErrors => Diagnostics.Any(d => d.Severity == Severity.Error);
    public bool Unsupported =>
        Diagnostics.Any(d => d.Category == Category.Compatibility && d.Severity == Severity.Error);

    internal StoredSnapshot(FileKind kind, ushort? version, byte[] bytes, IEnumerable<Record> records,
        IEnumerable<Diagnostic> diagnostics, IEnumerable<string> checks, bool decoded)
    {
        Kind = kind;
        Version = version;
        SourceHash = ContentIdentity.Sha256(bytes);
        SourceBytes = new(bytes);
        Records = new(records);
        Diagnostics = new(diagnostics);
        Checks = new(checks);
        Decoded = decoded;
    }
}
public sealed record ByteChange(int Offset, OwnedList<byte> Before, OwnedList<byte> After);
public sealed record FieldChange(RecordKey Record, string Field, Field? Before, Field? After);
public sealed record StoredDiff(string BeforeHash, string AfterHash, bool FieldsCompared,
    OwnedList<ByteChange> Bytes, OwnedList<FieldChange> Fields)
{
    public bool Different => Bytes.Count != 0;
}
