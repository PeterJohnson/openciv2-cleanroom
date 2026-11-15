module openciv.query.output;

import std;
import openciv.base.hash;

namespace openciv::query {
namespace {
void quoted(std::ostream& out, std::string_view text) {
    constexpr char hex[] = "0123456789abcdef";
    out << '"';
    for (const auto ch : text) {
        const auto byte = static_cast<unsigned char>(ch);
        if (ch == '"' || ch == '\\')
            out << '\\' << ch;
        else if (byte < 0x20)
            out << "\\u00" << hex[byte >> 4] << hex[byte & 15];
        else
            out << ch;
    }
    out << '"';
}
void decimal(std::ostream& out, auto value) {
    quoted(out, std::to_string(value));
}
std::string_view category(Category value) {
    switch (value) {
    case Category::structural:
        return "structural";
    case Category::compatibility:
        return "compatibility";
    case Category::semantic:
        return "semantic";
    }
    throw std::invalid_argument("Unknown diagnostic category");
}
std::string_view severity(Severity value) {
    switch (value) {
    case Severity::information:
        return "information";
    case Severity::warning:
        return "warning";
    case Severity::error:
        return "error";
    }
    throw std::invalid_argument("Unknown diagnostic severity");
}
std::string value_text(const Value& value) {
    return std::visit(
        [](const auto& item) -> std::string {
            using T = std::remove_cvref_t<decltype(item)>;
            if constexpr (std::same_as<T, std::monostate>)
                return "null";
            else if constexpr (std::same_as<T, formats::Bytes>)
                return base::hex_bytes(item);
            else
                return std::to_string(item);
        },
        value);
}
void field_json(std::ostream& out, const Field& field) {
    out << "{\"name\":";
    quoted(out, field.name);
    out << ",\"type\":";
    quoted(out, field.type);
    out << ",\"value\":";
    if (std::holds_alternative<std::monostate>(field.value))
        out << "null";
    else
        quoted(out, value_text(field.value));
    out << '}';
}
void optional_field(std::ostream& out, const std::optional<Field>& field) {
    if (field)
        field_json(out, *field);
    else
        out << "null";
}
void record_json(std::ostream& out, const Record& record) {
    out << "{\"section\":";
    quoted(out, name(record.key.section));
    out << ",\"slot\":";
    decimal(out, record.key.slot);
    out << ",\"offset\":";
    decimal(out, record.offset);
    out << ",\"byteCount\":";
    decimal(out, record.bytes.size());
    out << ",\"bytes\":";
    quoted(out, base::hex_bytes(record.bytes));
    out << ",\"provenance\":\"stored\",\"fields\":[";
    bool separator = false;
    for (const auto& field : record.fields) {
        if (separator)
            out << ',';
        separator = true;
        field_json(out, field);
    }
    out << "]}";
}
std::map<Section, std::size_t> sections(const StoredSnapshot& snapshot) {
    std::map<Section, std::size_t> result;
    for (const auto& record : snapshot.records())
        ++result[record.key.section];
    return result;
}
void diagnostics_text(std::ostream& out, const StoredSnapshot& snapshot) {
    for (const auto& d : snapshot.diagnostics()) {
        out << severity(d.severity) << " [" << category(d.category) << '/' << d.code << ']';
        if (!d.field.empty())
            out << ' ' << d.field;
        if (d.offset)
            out << " at byte " << *d.offset;
        out << ": " << d.message << '\n';
    }
}
} // namespace
bool OutputSelection::includes(Section section) const {
    return sections.empty() ||
           std::find(sections.begin(), sections.end(), section) != sections.end();
}
void write_snapshot_json(std::ostream& out, const StoredSnapshot& snapshot,
                         const OutputSelection& selection, bool include_records) {
    out << "{\"schema\":\"openciv2-inspection/1\",\"source\":{\"format\":";
    quoted(out, snapshot.kind() == FileKind::save ? "classic-save" : "classic-raw-map");
    out << ",\"version\":";
    if (snapshot.version())
        decimal(out, *snapshot.version());
    else
        out << "null";
    out << ",\"sha256\":";
    quoted(out, snapshot.source_hash());
    out << ",\"byteCount\":";
    decimal(out, snapshot.source_bytes().size());
    out << "},\"view\":\"stored\",\"access\":\"offline-omniscient\",\"observation\":";
    quoted(out, snapshot.source_hash());
    out << ",\"decoded\":" << (snapshot.decoded() ? "true" : "false");
    out << ",\"validation\":{\"status\":";
    quoted(out, snapshot.unsupported()  ? "unsupported"
                : snapshot.has_errors() ? "errors"
                                        : "passed-available-checks");
    out << ",\"complete\":false,\"checks\":[";
    bool separator = false;
    for (const auto& check : snapshot.checks()) {
        if (separator)
            out << ',';
        separator = true;
        quoted(out, check);
    }
    out << "],\"unavailable\":[\"rule-dependent-validation\",\"loaded-state\",\"gameplay-"
           "compatibility\"]},\"coverage\":{\"supportedSections\":[";
    separator = false;
    const auto counts = sections(snapshot);
    for (const auto& [section, count] : counts) {
        (void)count;
        if (separator)
            out << ',';
        separator = true;
        quoted(out, name(section));
    }
    out << "],\"omittedSections\":[";
    separator = false;
    for (const auto& [section, count] : counts) {
        (void)count;
        if (include_records && selection.includes(section))
            continue;
        if (separator)
            out << ',';
        separator = true;
        quoted(out, name(section));
    }
    out << "],\"uninterpreted\":[\"retained-payload-and-unknown-bits\",\"classic-text-code-page\","
           "\"trailing-bytes\"]},\"records\":[";
    separator = false;
    if (include_records)
        for (const auto& record : snapshot.records()) {
            if (!selection.includes(record.key.section))
                continue;
            if (separator)
                out << ',';
            separator = true;
            record_json(out, record);
        }
    out << "],\"diagnostics\":[";
    separator = false;
    for (const auto& d : snapshot.diagnostics()) {
        if (separator)
            out << ',';
        separator = true;
        out << "{\"category\":";
        quoted(out, category(d.category));
        out << ",\"severity\":";
        quoted(out, severity(d.severity));
        out << ",\"code\":";
        quoted(out, d.code);
        out << ",\"field\":";
        quoted(out, d.field);
        out << ",\"offset\":";
        if (d.offset)
            decimal(out, *d.offset);
        else
            out << "null";
        out << ",\"message\":";
        quoted(out, d.message);
        out << '}';
    }
    out << "]}";
}
void write_snapshot_text(std::ostream& out, const StoredSnapshot& snapshot,
                         const OutputSelection& selection, bool include_records) {
    out << (snapshot.kind() == FileKind::save ? "Classic save" : "Classic raw map");
    if (snapshot.version())
        out << "; version " << *snapshot.version();
    out << "; " << snapshot.source_bytes().size() << " bytes\nSHA-256 " << snapshot.source_hash()
        << "\nView: stored; access: offline omniscient\n";
    out << (snapshot.decoded() ? "Decoded" : "Not decoded") << "; "
        << (snapshot.has_errors() ? "diagnostic errors present" : "available checks passed")
        << "; validation coverage is incomplete\n";
    for (const auto& [section, count] : sections(snapshot))
        out << name(section) << ": " << count << " records\n";
    if (include_records)
        for (const auto& record : snapshot.records()) {
            if (!selection.includes(record.key.section))
                continue;
            out << name(record.key.section) << '[' << record.key.slot << "] @" << record.offset
                << " (" << record.bytes.size() << " bytes)\n";
            for (const auto& field : record.fields)
                out << "  " << field.name << " (" << field.type << ") = " << value_text(field.value)
                    << '\n';
        }
    diagnostics_text(out, snapshot);
}
void write_diff_json(std::ostream& out, const StoredSnapshot& before, const StoredSnapshot& after,
                     const StoredDiff& diff) {
    out << "{\"schema\":\"openciv2-diff/1\",\"before\":";
    write_snapshot_json(out, before, {}, false);
    out << ",\"after\":";
    write_snapshot_json(out, after, {}, false);
    out << ",\"different\":" << (diff.different() ? "true" : "false")
        << ",\"fieldsCompared\":" << (diff.fields_compared ? "true" : "false")
        << ",\"byteChanges\":[";
    bool separator = false;
    for (const auto& change : diff.bytes) {
        if (separator)
            out << ',';
        separator = true;
        out << "{\"offset\":";
        decimal(out, change.offset);
        out << ",\"before\":";
        quoted(out, base::hex_bytes(change.before));
        out << ",\"after\":";
        quoted(out, base::hex_bytes(change.after));
        out << '}';
    }
    out << "],\"fieldChanges\":[";
    separator = false;
    for (const auto& change : diff.fields) {
        if (separator)
            out << ',';
        separator = true;
        out << "{\"section\":";
        quoted(out, name(change.record.section));
        out << ",\"slot\":";
        decimal(out, change.record.slot);
        out << ",\"field\":";
        quoted(out, change.field);
        out << ",\"before\":";
        optional_field(out, change.before);
        out << ",\"after\":";
        optional_field(out, change.after);
        out << '}';
    }
    out << "]}";
}
void write_diff_text(std::ostream& out, const StoredSnapshot& before, const StoredSnapshot& after,
                     const StoredDiff& diff) {
    out << "Stored diff " << diff.before_hash << " -> " << diff.after_hash << '\n'
        << diff.bytes.size() << " changed byte ranges; " << diff.fields.size()
        << " changed fields\n";
    if (!diff.fields_compared)
        out << "Decoded field comparison unavailable\n";
    for (const auto& change : diff.bytes)
        out << "bytes @" << change.offset << ": " << base::hex_bytes(change.before) << " -> "
            << base::hex_bytes(change.after) << '\n';
    for (const auto& change : diff.fields)
        out << name(change.record.section) << '[' << change.record.slot << "]." << change.field
            << ": " << (change.before ? value_text(change.before->value) : "<field absent>")
            << " -> " << (change.after ? value_text(change.after->value) : "<field absent>")
            << '\n';
    out << "Before diagnostics:\n";
    diagnostics_text(out, before);
    out << "After diagnostics:\n";
    diagnostics_text(out, after);
}
} // namespace openciv::query
