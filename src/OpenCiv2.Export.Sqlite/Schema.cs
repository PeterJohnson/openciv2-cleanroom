namespace OpenCiv2.Export.Sqlite;

// M1 analysis schema 1: retained SQL definitions from the cleanroom exporter.
public static partial class SqliteSnapshot
{
    private const string Schema = """
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
""";
}
