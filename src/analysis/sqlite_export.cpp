module;
#include <sqlite3.h>

module openciv.analysis.sqlite_export;

import std;
import openciv.base.cancellation;

namespace openciv::analysis {
namespace {
using namespace query;
using Database = std::unique_ptr<sqlite3, decltype(&sqlite3_close)>;
using StatementHandle = std::unique_ptr<sqlite3_stmt, decltype(&sqlite3_finalize)>;
void check(sqlite3* db, int result) {
    if (result != SQLITE_OK)
        throw std::runtime_error(std::string{"SQLite export: "} + sqlite3_errmsg(db));
}
void execute(sqlite3* db, const char* sql) {
    check(db, sqlite3_exec(db, sql, nullptr, nullptr, nullptr));
}
class Statement {
public:
    Statement(sqlite3* db, const char* sql) : db_(db), handle_(nullptr, sqlite3_finalize) {
        sqlite3_stmt* statement = nullptr;
        const auto result = sqlite3_prepare_v2(db, sql, -1, &statement, nullptr);
        handle_.reset(statement);
        check(db, result);
    }
    void bind(int index, std::string_view text) {
        check(db_, sqlite3_bind_text64(handle_.get(), index, text.empty() ? "" : text.data(),
                                       text.size(), SQLITE_TRANSIENT, SQLITE_UTF8));
    }
    void bind(int index, std::size_t value) {
        if (static_cast<std::uint64_t>(value) >
            static_cast<std::uint64_t>(std::numeric_limits<sqlite3_int64>::max()))
            throw std::length_error("SQLite export index exceeds i64");
        check(db_, sqlite3_bind_int64(handle_.get(), index, static_cast<sqlite3_int64>(value)));
    }
    void bind(int index, std::span<const formats::Byte> bytes) {
        // A null pointer means SQL NULL, even at length zero. Keep empty BLOB distinct.
        constexpr formats::Byte empty = 0;
        check(db_, sqlite3_bind_blob64(handle_.get(), index, bytes.empty() ? &empty : bytes.data(),
                                       bytes.size(), SQLITE_TRANSIENT));
    }
    void value(int kind, int integer, int blob, const Value& value) {
        std::visit(
            [&](const auto& item) {
                using T = std::remove_cvref_t<decltype(item)>;
                if constexpr (std::same_as<T, std::monostate>) {
                    bind(kind, "null");
                } else if constexpr (std::same_as<T, formats::Bytes>) {
                    bind(kind, "bytes");
                    bind(blob, std::span<const formats::Byte>{item});
                } else {
                    bind(kind, std::is_signed_v<T> ? "signed" : "unsigned");
                    bind(integer, std::to_string(item));
                }
            },
            value);
    }
    void run() {
        const auto result = sqlite3_step(handle_.get());
        if (result != SQLITE_DONE)
            check(db_, result);
        check(db_, sqlite3_reset(handle_.get()));
        check(db_, sqlite3_clear_bindings(handle_.get()));
    }

private:
    sqlite3* db_;
    StatementHandle handle_;
};
constexpr auto schema = R"sql(
PRAGMA application_id=1329803859;
PRAGMA user_version=1;
PRAGMA foreign_keys=ON;
PRAGMA temp_store=MEMORY;
BEGIN;
CREATE TABLE snapshots(
 id TEXT PRIMARY KEY, source_format TEXT NOT NULL, source_version INTEGER,
 source_bytes INTEGER NOT NULL, view TEXT NOT NULL CHECK(view='stored'),
 access TEXT NOT NULL CHECK(access='offline-omniscient'),
 validation_status TEXT NOT NULL, validation_complete INTEGER NOT NULL CHECK(validation_complete=0)
);
CREATE TABLE metadata(key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE records(
 snapshot_id TEXT NOT NULL REFERENCES snapshots(id), section TEXT NOT NULL,
 slot INTEGER NOT NULL, ordinal INTEGER NOT NULL, source_offset INTEGER NOT NULL,
 raw BLOB NOT NULL, provenance TEXT NOT NULL CHECK(provenance='stored'),
 PRIMARY KEY(snapshot_id,section,slot), UNIQUE(snapshot_id,ordinal)
);
CREATE TABLE fields(
 snapshot_id TEXT NOT NULL, section TEXT NOT NULL, slot INTEGER NOT NULL,
 ordinal INTEGER NOT NULL, name TEXT NOT NULL, type TEXT NOT NULL,
 value_kind TEXT NOT NULL CHECK(value_kind IN ('null','signed','unsigned','bytes')),
 integer_decimal TEXT, value_blob BLOB,
 PRIMARY KEY(snapshot_id,section,slot,ordinal), UNIQUE(snapshot_id,section,slot,name),
 FOREIGN KEY(snapshot_id,section,slot) REFERENCES records(snapshot_id,section,slot),
 CHECK((value_kind='null' AND integer_decimal IS NULL AND value_blob IS NULL) OR
       (value_kind='bytes' AND integer_decimal IS NULL AND value_blob IS NOT NULL) OR
       (value_kind IN ('signed','unsigned') AND integer_decimal IS NOT NULL AND value_blob IS NULL))
);
CREATE TABLE coverage(
 snapshot_id TEXT NOT NULL REFERENCES snapshots(id), section TEXT NOT NULL,
 record_count INTEGER NOT NULL, exported INTEGER NOT NULL CHECK(exported=1),
 PRIMARY KEY(snapshot_id,section)
);
CREATE TABLE validation_checks(
 snapshot_id TEXT NOT NULL REFERENCES snapshots(id), ordinal INTEGER NOT NULL,
 name TEXT NOT NULL, PRIMARY KEY(snapshot_id,ordinal)
);
CREATE TABLE limitations(ordinal INTEGER PRIMARY KEY, name TEXT NOT NULL);
CREATE TABLE diagnostics(
 snapshot_id TEXT NOT NULL REFERENCES snapshots(id), ordinal INTEGER NOT NULL,
 category TEXT NOT NULL, severity TEXT NOT NULL, code TEXT NOT NULL,
 field TEXT NOT NULL, byte_offset INTEGER, message TEXT NOT NULL,
 PRIMARY KEY(snapshot_id,ordinal)
);
CREATE VIEW unit_slots AS SELECT r.*, f.integer_decimal AS token FROM records r JOIN fields f
 USING(snapshot_id,section,slot) WHERE r.section='units' AND f.name='token';
CREATE VIEW city_slots AS SELECT r.*, f.integer_decimal AS token FROM records r JOIN fields f
 USING(snapshot_id,section,slot) WHERE r.section='cities' AND f.name='token';
CREATE VIEW tiles AS SELECT * FROM records WHERE section='tiles';
CREATE VIEW civilizations AS SELECT * FROM records WHERE section='civilizations';
INSERT INTO metadata VALUES('schema','openciv2-analysis/1'),('complete','true'),
 ('integer_encoding','exact-decimal-text'),('classic_text_encoding','raw-blob'),
 ('rules','unavailable'),('computed_fields','none'),
 ('snapshot_identity','sha256-of-source-bytes');
INSERT INTO limitations VALUES(0,'rule-dependent-validation'),(1,'loaded-state'),
 (2,'gameplay-compatibility'),(3,'classic-text-code-page'),(4,'retained-payload-and-unknown-bits');
)sql";
} // namespace

formats::Bytes sqlite_snapshot(const query::StoredSnapshot& snapshot, base::CancellationToken stop,
                               const base::CallbackRef<void(std::size_t)>& progress) {
    if (!snapshot.decoded())
        throw std::invalid_argument("SQLite export requires a decoded stored snapshot");
    const auto checkpoint = [&](std::size_t completed) {
        base::check_stop(stop);
        if (progress)
            progress(completed);
        base::check_stop(stop);
    };
    checkpoint(0);
    sqlite3* pointer = nullptr;
    const auto opened =
        sqlite3_open_v2(":memory:", &pointer,
                        SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE | SQLITE_OPEN_MEMORY, nullptr);
    Database db{pointer, sqlite3_close};
    if (!db)
        throw std::bad_alloc{};
    check(db.get(), opened);
    execute(db.get(), schema);
    const auto& identity = snapshot.source_hash();
    Statement header{db.get(),
                     "INSERT INTO snapshots VALUES(?,?,?,?, 'stored', 'offline-omniscient',?,0)"};
    header.bind(1, identity);
    header.bind(2, snapshot.kind() == FileKind::save ? "classic-save" : "classic-raw-map");
    if (snapshot.version())
        header.bind(3, static_cast<std::size_t>(*snapshot.version()));
    header.bind(4, snapshot.source_bytes().size());
    header.bind(5, snapshot.has_errors() ? "errors" : "passed-available-checks");
    header.run();
    Statement metadata{db.get(), "INSERT INTO metadata VALUES('sqlite_version',?)"};
    metadata.bind(1, sqlite3_libversion());
    metadata.run();
    Statement record_insert{db.get(), "INSERT INTO records VALUES(?,?,?,?,?,?,'stored')"};
    Statement field_insert{db.get(), "INSERT INTO fields VALUES(?,?,?,?,?,?,?,?,?)"};
    std::map<Section, std::size_t> counts;
    for (std::size_t i = 0; i < snapshot.records().size(); ++i) {
        const auto& record = snapshot.records()[i];
        ++counts[record.key.section];
        record_insert.bind(1, identity);
        record_insert.bind(2, name(record.key.section));
        record_insert.bind(3, record.key.slot);
        record_insert.bind(4, i);
        record_insert.bind(5, record.offset);
        record_insert.bind(6, std::span<const formats::Byte>{record.bytes});
        record_insert.run();
        for (std::size_t j = 0; j < record.fields.size(); ++j) {
            const auto& field = record.fields[j];
            field_insert.bind(1, identity);
            field_insert.bind(2, name(record.key.section));
            field_insert.bind(3, record.key.slot);
            field_insert.bind(4, j);
            field_insert.bind(5, field.name);
            field_insert.bind(6, field.type);
            field_insert.value(7, 8, 9, field.value);
            field_insert.run();
        }
        checkpoint(i + 1);
    }
    Statement coverage{db.get(), "INSERT INTO coverage VALUES(?,?,?,1)"};
    for (const auto& [section, count] : counts) {
        coverage.bind(1, identity);
        coverage.bind(2, name(section));
        coverage.bind(3, count);
        coverage.run();
    }
    Statement checks{db.get(), "INSERT INTO validation_checks VALUES(?,?,?)"};
    for (std::size_t i = 0; i < snapshot.checks().size(); ++i) {
        checks.bind(1, identity);
        checks.bind(2, i);
        checks.bind(3, snapshot.checks()[i]);
        checks.run();
    }
    Statement diagnostics{db.get(), "INSERT INTO diagnostics VALUES(?,?,?,?,?,?,?,?)"};
    constexpr std::array categories{"structural", "compatibility", "semantic"};
    constexpr std::array severities{"information", "warning", "error"};
    for (std::size_t i = 0; i < snapshot.diagnostics().size(); ++i) {
        const auto& d = snapshot.diagnostics()[i];
        diagnostics.bind(1, identity);
        diagnostics.bind(2, i);
        diagnostics.bind(3, categories.at(static_cast<std::size_t>(d.category)));
        diagnostics.bind(4, severities.at(static_cast<std::size_t>(d.severity)));
        diagnostics.bind(5, d.code);
        diagnostics.bind(6, d.field);
        if (d.offset)
            diagnostics.bind(7, *d.offset);
        diagnostics.bind(8, d.message);
        diagnostics.run();
    }
    checkpoint(snapshot.records().size());
    execute(db.get(), "COMMIT");
    sqlite3_int64 size = 0;
    std::unique_ptr<unsigned char, decltype(&sqlite3_free)> bytes{
        sqlite3_serialize(db.get(), "main", &size, 0), sqlite3_free};
    if (!bytes || size <= 0)
        throw std::runtime_error("SQLite export serialization failed");
    if (static_cast<std::uint64_t>(size) > std::numeric_limits<std::size_t>::max())
        throw std::length_error("SQLite export exceeds host memory domain");
    checkpoint(snapshot.records().size());
    return {bytes.get(), bytes.get() + static_cast<std::size_t>(size)};
}
} // namespace openciv::analysis
