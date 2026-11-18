using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using OpenCiv2.Base;

namespace OpenCiv2.Query;

public static class Output
{
    private static string Number<T>(T value) where T : IFormattable => value.ToString(null, CultureInfo.InvariantCulture);
    private static string Hex(IEnumerable<byte> value) => ContentIdentity.HexBytes(value.ToArray());
    private static string Label<T>(T value) where T : Enum => value.ToString().ToLowerInvariant();
    private static bool Includes(IReadOnlyCollection<Section> selection, Section section) => selection.Count == 0 || selection.Contains(section);
    private static void Strings(Utf8JsonWriter w, string name, IEnumerable<string> values)
    {
        w.WriteStartArray(name); foreach (string value in values) w.WriteStringValue(value); w.WriteEndArray();
    }
    private static void Field(Utf8JsonWriter w, Field? field)
    {
        if (field is null) { w.WriteNullValue(); return; }
        w.WriteStartObject(); w.WriteString("name", field.Name); w.WriteString("type", field.Type);
        if (field.Value is null) w.WriteNull("value"); else w.WriteString("value", field.Text);
        w.WriteEndObject();
    }
    private static void Snapshot(Utf8JsonWriter w, StoredSnapshot s, IReadOnlyCollection<Section> selection, bool includeRecords)
    {
        w.WriteStartObject(); w.WriteString("schema", "openciv2-inspection/1"); w.WriteStartObject("source");
        w.WriteString("format", s.Kind == FileKind.Save ? "classic-save" : "classic-raw-map");
        if (s.Version is { } version) w.WriteString("version", Number(version)); else w.WriteNull("version");
        w.WriteString("sha256", s.SourceHash); w.WriteString("byteCount", Number(s.SourceBytes.Count)); w.WriteEndObject();
        w.WriteString("view", "stored"); w.WriteString("access", "offline-omniscient"); w.WriteString("observation", s.SourceHash);
        w.WriteBoolean("decoded", s.Decoded); w.WriteStartObject("validation");
        w.WriteString("status", s.Unsupported ? "unsupported" : s.HasErrors ? "errors" : "passed-available-checks");
        w.WriteBoolean("complete", false); Strings(w, "checks", s.Checks);
        Strings(w, "unavailable", ["rule-dependent-validation", "loaded-state", "gameplay-compatibility"]); w.WriteEndObject();
        var sections = s.Records.Select(r => r.Key.Section).Distinct().Order().ToArray();
        w.WriteStartObject("coverage"); Strings(w, "supportedSections", sections.Select(Sections.Name));
        Strings(w, "omittedSections", sections.Where(section => !includeRecords || !Includes(selection, section)).Select(Sections.Name));
        Strings(w, "uninterpreted", ["retained-payload-and-unknown-bits", "classic-text-code-page", "trailing-bytes"]); w.WriteEndObject();
        w.WriteStartArray("records");
        if (includeRecords)
            foreach (var record in s.Records)
            {
                if (!Includes(selection, record.Key.Section)) continue;
                w.WriteStartObject(); w.WriteString("section", record.Key.Section.Name()); w.WriteString("slot", Number(record.Key.Slot));
                w.WriteString("offset", Number(record.Offset)); w.WriteString("byteCount", Number(record.Bytes.Count));
                w.WriteString("bytes", Hex(record.Bytes)); w.WriteString("provenance", "stored"); w.WriteStartArray("fields");
                foreach (var field in record.Fields) Field(w, field);
                w.WriteEndArray(); w.WriteEndObject();
            }
        w.WriteEndArray(); w.WriteStartArray("diagnostics");
        foreach (var d in s.Diagnostics)
        {
            w.WriteStartObject(); w.WriteString("category", Label(d.Category)); w.WriteString("severity", Label(d.Severity));
            w.WriteString("code", d.Code); w.WriteString("field", d.Field);
            if (d.Offset is { } offset) w.WriteString("offset", Number(offset)); else w.WriteNull("offset");
            w.WriteString("message", d.Message); w.WriteEndObject();
        }
        w.WriteEndArray(); w.WriteEndObject();
    }
    private static byte[] Json(Action<Utf8JsonWriter> emit)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) emit(writer);
        return stream.ToArray();
    }
    public static byte[] SnapshotJson(StoredSnapshot snapshot, IReadOnlyCollection<Section>? selection = null, bool includeRecords = true) =>
        Json(w => Snapshot(w, snapshot, selection ?? [], includeRecords));
    public static byte[] DiffJson(StoredSnapshot before, StoredSnapshot after, StoredDiff diff) => Json(w =>
    {
        w.WriteStartObject(); w.WriteString("schema", "openciv2-diff/1");
        w.WritePropertyName("before"); Snapshot(w, before, [], false); w.WritePropertyName("after"); Snapshot(w, after, [], false);
        w.WriteBoolean("different", diff.Different); w.WriteBoolean("fieldsCompared", diff.FieldsCompared); w.WriteStartArray("byteChanges");
        foreach (var change in diff.Bytes)
        {
            w.WriteStartObject(); w.WriteString("offset", Number(change.Offset)); w.WriteString("before", Hex(change.Before));
            w.WriteString("after", Hex(change.After)); w.WriteEndObject();
        }
        w.WriteEndArray(); w.WriteStartArray("fieldChanges");
        foreach (var change in diff.Fields)
        {
            w.WriteStartObject(); w.WriteString("section", change.Record.Section.Name()); w.WriteString("slot", Number(change.Record.Slot));
            w.WriteString("field", change.Field); w.WritePropertyName("before"); Field(w, change.Before);
            w.WritePropertyName("after"); Field(w, change.After); w.WriteEndObject();
        }
        w.WriteEndArray(); w.WriteEndObject();
    });
    private static void Diagnostics(StringBuilder text, StoredSnapshot s)
    {
        foreach (var d in s.Diagnostics)
        {
            text.Append(Label(d.Severity)).Append(" [").Append(Label(d.Category)).Append('/').Append(d.Code).Append(']');
            if (d.Field.Length != 0) text.Append(' ').Append(d.Field);
            if (d.Offset is { } offset) text.Append(" at byte ").Append(Number(offset));
            text.Append(": ").Append(d.Message).Append('\n');
        }
    }
    public static string SnapshotText(StoredSnapshot s, IReadOnlyCollection<Section>? selection = null, bool includeRecords = false)
    {
        var text = new StringBuilder(s.Kind == FileKind.Save ? "Classic save" : "Classic raw map");
        if (s.Version is { } version) text.Append("; version ").Append(Number(version));
        text.Append("; ").Append(Number(s.SourceBytes.Count)).Append(" bytes\nSHA-256 ").Append(s.SourceHash)
            .Append("\nView: stored; access: offline omniscient\n").Append(s.Decoded ? "Decoded" : "Not decoded").Append("; ")
            .Append(s.HasErrors ? "diagnostic errors present" : "available checks passed").Append("; validation coverage is incomplete\n");
        foreach (var group in s.Records.GroupBy(r => r.Key.Section).OrderBy(g => g.Key))
            text.Append(group.Key.Name()).Append(": ").Append(Number(group.Count())).Append(" records\n");
        if (includeRecords)
            foreach (var r in s.Records)
            {
                if (!Includes(selection ?? [], r.Key.Section)) continue;
                text.Append(r.Key.Section.Name()).Append('[').Append(Number(r.Key.Slot)).Append("] @").Append(Number(r.Offset))
                    .Append(" (").Append(Number(r.Bytes.Count)).Append(" bytes)\n");
                foreach (var f in r.Fields) text.Append("  ").Append(f.Name).Append(" (").Append(f.Type).Append(") = ").Append(f.Text).Append('\n');
            }
        Diagnostics(text, s); return text.ToString();
    }
    public static string DiffText(StoredSnapshot before, StoredSnapshot after, StoredDiff diff)
    {
        var text = new StringBuilder("Stored diff ").Append(diff.BeforeHash).Append(" -> ").Append(diff.AfterHash).Append('\n')
            .Append(Number(diff.Bytes.Count)).Append(" changed byte ranges; ").Append(Number(diff.Fields.Count)).Append(" changed fields\n");
        if (!diff.FieldsCompared) text.Append("Decoded field comparison unavailable\n");
        foreach (var c in diff.Bytes) text.Append("bytes @").Append(Number(c.Offset)).Append(": ").Append(Hex(c.Before)).Append(" -> ").Append(Hex(c.After)).Append('\n');
        foreach (var c in diff.Fields) text.Append(c.Record.Section.Name()).Append('[').Append(Number(c.Record.Slot)).Append("].").Append(c.Field)
            .Append(": ").Append(c.Before?.Text ?? "<field absent>").Append(" -> ").Append(c.After?.Text ?? "<field absent>").Append('\n');
        text.Append("Before diagnostics:\n"); Diagnostics(text, before); text.Append("After diagnostics:\n"); Diagnostics(text, after);
        return text.ToString();
    }
}
