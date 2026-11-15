export module openciv.query.stored;

import std;
export import openciv.formats.classic_save;

export extern "C++" {
    namespace openciv::query {
    enum class FileKind { save, raw_map };
    // Offline file access is deliberately separate from future live-session grants.
    // No API here accepts a live world, player binding or gameplay random context.
    enum class FileAccess { offline_omniscient };
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
    std::string_view name(Section section);
    struct RecordKey {
        Section section;
        std::size_t slot = 0;
        auto operator<=>(const RecordKey&) const = default;
    };
    // All integers retain an explicit type and exact value. Bytes are never assumed
    // UTF-8; classic-text fields include their NUL and retained tails. Null is a
    // present optional field with no target, distinct from an absent diff field.
    using Value = std::variant<std::monostate, std::int64_t, std::uint64_t, formats::Bytes>;
    struct Field {
        std::string name, type;
        Value value;
        bool operator==(const Field&) const = default;
    };
    struct Record {
        RecordKey key;
        std::size_t offset = 0;
        formats::Bytes bytes;
        std::vector<Field> fields;
        bool operator==(const Record&) const = default;
    };
    enum class Category { structural, compatibility, semantic };
    enum class Severity { information, warning, error };
    struct Diagnostic {
        Category category;
        Severity severity;
        std::string code, field, message;
        std::optional<std::size_t> offset;
        bool operator==(const Diagnostic&) const = default;
    };
    // Immutable, owned observation of a file. Hash identifies the exact observation;
    // neither path names nor a new runtime token sequence serve as its identity.
    class StoredSnapshot {
    public:
        FileKind kind() const noexcept { return kind_; }
        std::optional<std::uint16_t> version() const noexcept { return version_; }
        const std::string& source_hash() const noexcept { return source_hash_; }
        std::span<const formats::Byte> source_bytes() const noexcept { return bytes_; }
        const std::vector<Record>& records() const noexcept { return records_; }
        const std::vector<Diagnostic>& diagnostics() const noexcept { return diagnostics_; }
        const std::vector<std::string>& checks() const noexcept { return checks_; }
        bool decoded() const noexcept { return decoded_; }
        bool has_errors() const noexcept;
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
    // F06 stored decoding plus explicit F02/F06 nominal checks; no load/repair.
    StoredSnapshot inspect_stored(std::span<const formats::Byte> bytes, FileKind kind,
                                  FileAccess access);

    struct ByteChange {
        std::size_t offset = 0;
        formats::Bytes before, after;
        bool operator==(const ByteChange&) const = default;
    };
    struct FieldChange {
        RecordKey record;
        std::string field;
        std::optional<Field> before, after;
        bool operator==(const FieldChange&) const = default;
    };
    struct StoredDiff {
        std::string before_hash, after_hash;
        bool fields_compared = false;
        std::vector<ByteChange> bytes;
        std::vector<FieldChange> fields;
        bool different() const noexcept { return !bytes.empty(); }
        bool operator==(const StoredDiff&) const = default;
    };
    StoredDiff diff_stored(const StoredSnapshot& before, const StoredSnapshot& after);
    } // namespace openciv::query
}
