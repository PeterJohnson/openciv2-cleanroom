export module openciv.query.stored;

import std;
export import openciv.formats.classic_save;

export namespace openciv::query {
/// Select classic save or standalone map inspection.
enum class FileKind { save, raw_map };
/// Offline file access is deliberately separate from future live-session grants.
/// No API here accepts a live world, player binding or gameplay random context.
enum class FileAccess { offline_omniscient };
/// Logical sections used to identify decoded file records.
enum class Section {
    header,
    globals,
    civilization_names,
    civilizations,
    map_header,
    starts,
    remembered,
    tiles,
    coarse,
    regions,
    units,
    cities,
    tribes,
    cursor,
    leaders,
    power_history,
    layout,
    views,
    scenario,
    eliminations,
    multiplayer,
    event_header,
    events,
    event_strings,
    trailing
};
/// Return the stable output name of a stored section.
std::string_view name(Section section);
/// Identify a record by logical section and retained slot number.
struct RecordKey {
    Section section;
    std::size_t slot = 0;
    auto operator<=>(const RecordKey&) const = default;
};
// All integers retain an explicit type and exact value. Bytes are never assumed
// UTF-8; classic-text fields include their NUL and retained tails. Null is a
// present optional field with no target, distinct from an absent diff field.
using Value = std::variant<std::monostate, std::int64_t, std::uint64_t, formats::Bytes>;
/// A named inspection field with an explicit type and lossless value.
struct Field {
    std::string name, type;
    Value value;
    bool operator==(const Field&) const = default;
};
/// A decoded record with its source offset, exact bytes, and typed fields.
struct Record {
    RecordKey key;
    std::size_t offset = 0;
    formats::Bytes bytes;
    std::vector<Field> fields;
    bool operator==(const Record&) const = default;
};
/// Distinguish structural, compatibility, and semantic inspection findings.
enum class Category { structural, compatibility, semantic };
/// The severity of a stored-file inspection diagnostic.
enum class Severity { information, warning, error };
/// A categorized finding with a stable code and optional source offset.
struct Diagnostic {
    Category category;
    Severity severity;
    std::string code, field, message;
    std::optional<std::size_t> offset;
    bool operator==(const Diagnostic&) const = default;
};
/// Immutable, owned observation of a file. Hash identifies the exact observation;
/// neither path names nor a new runtime token sequence serve as its identity.
class StoredSnapshot {
public:
    /// Return the kind of file inspected.
    FileKind kind() const noexcept { return kind_; }
    /// Return the stored version when available.
    std::optional<std::uint16_t> version() const noexcept { return version_; }
    /// Return the hash identifying the exact observed bytes.
    const std::string& source_hash() const noexcept { return source_hash_; }
    /// Borrow the complete original input bytes owned by this snapshot.
    std::span<const formats::Byte> source_bytes() const noexcept { return bytes_; }
    /// Inspect decoded records without changing the snapshot.
    const std::vector<Record>& records() const noexcept { return records_; }
    /// Inspect findings recorded during decoding and validation.
    const std::vector<Diagnostic>& diagnostics() const noexcept { return diagnostics_; }
    /// Inspect the identifiers of checks performed on this snapshot.
    const std::vector<std::string>& checks() const noexcept { return checks_; }
    /// Report whether structured decoding completed.
    bool decoded() const noexcept { return decoded_; }
    /// Report whether any diagnostic has error severity.
    bool has_errors() const noexcept;
    /// Report whether inspection encountered an unsupported format.
    bool unsupported() const noexcept;

private:
    friend StoredSnapshot inspect_stored(std::span<const formats::Byte>, FileKind, FileAccess);
    FileKind kind_ = FileKind::save;
    std::optional<std::uint16_t> version_;
    std::string source_hash_;
    formats::Bytes bytes_;
    std::vector<Record> records_;
    std::vector<Diagnostic> diagnostics_;
    std::vector<std::string> checks_;
    bool decoded_ = false;
};
/// F06 stored decoding plus explicit F02/F06 nominal checks; no load/repair.
StoredSnapshot inspect_stored(std::span<const formats::Byte> bytes, FileKind kind,
                              FileAccess access);

/// A contiguous changed byte range with its before and after values.
struct ByteChange {
    std::size_t offset = 0;
    formats::Bytes before, after;
    bool operator==(const ByteChange&) const = default;
};
/// A typed field change; absent fields remain distinct from present null values.
struct FieldChange {
    RecordKey record;
    std::string field;
    std::optional<Field> before, after;
    bool operator==(const FieldChange&) const = default;
};
/// Owned byte and field differences between two identified file snapshots.
struct StoredDiff {
    std::string before_hash, after_hash;
    bool fields_compared = false;
    std::vector<ByteChange> bytes;
    std::vector<FieldChange> fields;
    /// Report whether the original file bytes differ.
    bool different() const noexcept { return !bytes.empty(); }
    bool operator==(const StoredDiff&) const = default;
};
/// Compare exact input bytes and, when available, their decoded typed fields.
StoredDiff diff_stored(const StoredSnapshot& before, const StoredSnapshot& after);
} // namespace openciv::query
