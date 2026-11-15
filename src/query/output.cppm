export module openciv.query.output;

import std;
export import openciv.query.stored;

export namespace openciv::query {
/// Restrict inspection output to selected stored sections.
struct OutputSelection {
    std::vector<Section> sections; // Empty means all sections.
    /// Report whether a section is selected for output.
    bool includes(Section section) const;
};
/// Versioned lossless JSON. Integer values and offsets are decimal strings;
/// opaque/classic-text values are hexadecimal bytes. Diagnostics are separate.
void write_snapshot_json(std::ostream& out, const StoredSnapshot& snapshot,
                         const OutputSelection& selection = {}, bool include_records = true);
/// Write a human-readable inspection summary, optionally including individual records.
void write_snapshot_text(std::ostream& out, const StoredSnapshot& snapshot,
                         const OutputSelection& selection = {}, bool include_records = false);
/// Write exact byte and typed-field differences as JSON.
void write_diff_json(std::ostream& out, const StoredSnapshot& before, const StoredSnapshot& after,
                     const StoredDiff& diff);
/// Write a human-readable comparison of two stored snapshots.
void write_diff_text(std::ostream& out, const StoredSnapshot& before, const StoredSnapshot& after,
                     const StoredDiff& diff);
} // namespace openciv::query
