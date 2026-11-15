export module openciv.query.output;

import std;
export import openciv.query.stored;

export namespace openciv::query {
struct OutputSelection {
    std::vector<Section> sections; // Empty means all sections.
    bool includes(Section section) const;
};
// Versioned lossless JSON. Integer values and offsets are decimal strings;
// opaque/classic-text values are hexadecimal bytes. Diagnostics are separate.
void write_snapshot_json(std::ostream& out, const StoredSnapshot& snapshot,
                         const OutputSelection& selection = {}, bool include_records = true);
void write_snapshot_text(std::ostream& out, const StoredSnapshot& snapshot,
                         const OutputSelection& selection = {}, bool include_records = false);
void write_diff_json(std::ostream& out, const StoredSnapshot& before, const StoredSnapshot& after,
                     const StoredDiff& diff);
void write_diff_text(std::ostream& out, const StoredSnapshot& before, const StoredSnapshot& after,
                     const StoredDiff& diff);
} // namespace openciv::query
