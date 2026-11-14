#include "query/stored.hpp"
#include "query/fields.hpp"
#include "query/stored_validation.hpp"
#include "base/hash.hpp"
#include <map>

namespace openciv::query {
namespace {
using namespace formats;
class Builder {
public:
    Builder(std::span<const Byte> bytes, std::vector<Record>& records)
        : input_(bytes), records_(records) {}
    void record(Section section, std::size_t slot, std::size_t size, auto fill) {
        Record record{{section, slot}, input_.position(), {}, {}};
        const auto bytes = input_.take(size);
        record.bytes.assign(bytes.begin(), bytes.end());
        detail::Fields fields{record.fields};
        fill(fields);
        records_.push_back(std::move(record));
    }
    void typed(Section section, std::size_t slot, std::size_t size, const auto& value) {
        record(section, slot, size, [&](auto& fields) { visit_fields(fields, value); });
    }
    void bytes(Section section, std::size_t slot, const Bytes& value) {
        record(section, slot, value.size(), [&](auto& fields) { fields.bytes("payload", value); });
    }
    void finish() const {
        if (!input_.remaining().empty())
            throw std::logic_error("Stored projection did not cover all file bytes");
    }

private:
    ByteReader input_;
    std::vector<Record>& records_;
};
void project_map(Builder& b, const StoredMap& map) {
    b.typed(Section::map_header, 0, map_header_size, map.header);
    for (std::size_t i = 0; i < map.remembered.size(); ++i)
        b.bytes(Section::remembered, i + 1, map.remembered[i]);
    for (std::size_t i = 0; i < map.tiles.size(); ++i)
        b.typed(Section::tiles, i, tile_record_size, map.tiles[i]);
    for (std::size_t i = 0; i < map.coarse.size(); ++i)
        b.bytes(Section::coarse, i, map.coarse[i]);
    for (std::size_t i = 0; i < map.regions.size(); ++i)
        b.typed(Section::regions, i, region_record_size, map.regions[i]);
}
void project(Builder& b, const RawMapFile& map) {
    b.typed(Section::map_header, 0, map_header_size, map.header);
    b.record(Section::starts, 0, 42, [&](auto& fields) { fields("x", map.starts.x); });
    b.record(Section::starts, 1, 42, [&](auto& fields) { fields("y", map.starts.y); });
    for (std::size_t i = 0; i < map.tiles.size(); ++i)
        b.typed(Section::tiles, i, tile_record_size, map.tiles[i]);
    b.bytes(Section::trailing, 0, map.trailing);
}
void project(Builder& b, const StoredSave& save) {
    b.record(Section::header, 0, 12, [&](auto& fields) {
        constexpr std::array<Byte, 9> magic{'C', 'I', 'V', 'I', 'L', 'I', 'Z', 'E', 0};
        fields.bytes("magic", magic);
        fields("separator", save.header.separator);
        fields("version", save.header.version);
    });
    b.typed(Section::globals, 0, global_record_size, save.globals);
    for (std::size_t i = 0; i < save.names.size(); ++i)
        b.typed(Section::civilization_names, i, civilization_names_size, save.names[i]);
    for (std::size_t i = 0; i < save.civilizations.size(); ++i)
        b.typed(Section::civilizations, i, civilization_record_size, save.civilizations[i]);
    project_map(b, save.map);
    for (std::size_t i = 0; i < save.units.size(); ++i)
        b.typed(Section::units, i, unit_record_size, save.units[i]);
    for (std::size_t i = 0; i < save.cities.size(); ++i)
        b.typed(Section::cities, i, city_record_size, save.cities[i]);
    for (std::size_t i = 0; i < save.tribes.size(); ++i)
        b.record(Section::tribes, i, 3, [&](auto& fields) {
            fields("times_used", save.tribes[i].times_used);
            fields("city_name_index", save.tribes[i].city_name_index);
            fields("female", save.tribes[i].female);
        });
    b.record(Section::cursor, 0, 4, [&](auto& fields) {
        fields("x", save.cursor_x);
        fields("y", save.cursor_y);
    });
    for (std::size_t i = 0; i < save.leaders.size(); ++i)
        b.typed(Section::leaders, i, leader_record_size, save.leaders[i]);
    for (std::size_t i = 0; i < save.power_history.size(); ++i)
        b.record(Section::power_history, i, 8,
                 [&](auto& fields) { fields("civilizations", save.power_history[i]); });
    b.typed(Section::layout, 0, layout_record_size, save.layout);
    for (std::size_t i = 0; i < save.views.size(); ++i)
        b.typed(Section::views, i, 10, save.views[i]);
    if (save.scenario)
        b.typed(Section::scenario, 0, scenario_parameters_size, *save.scenario);
    b.typed(Section::eliminations, 0, elimination_record_size, save.eliminations);
    if (save.multiplayer)
        b.typed(Section::multiplayer, 0, multiplayer_record_size, *save.multiplayer);
    if (save.events) {
        b.record(Section::event_header, 0, 8, [&](auto& fields) {
            constexpr std::array<Byte, 4> tag{'E', 'V', 'N', 'T'};
            fields.bytes("tag", tag);
            fields("count", static_cast<std::int32_t>(save.events->entries.size()));
        });
        for (std::size_t i = 0; i < save.events->entries.size(); ++i)
            b.typed(Section::events, i, event_record_size, save.events->entries[i].record);
        for (std::size_t i = 0; i < save.events->entries.size(); ++i) {
            const auto& strings = save.events->entries[i].strings;
            std::size_t size = 0;
            const auto count_strings = [&](const auto& values) {
                for (const auto& value : values)
                    if (value)
                        size += value->size() + 1;
            };
            count_strings(strings.names);
            count_strings(strings.text);
            count_strings(strings.other);
            b.record(Section::event_strings, i, size, [&](auto& fields) {
                constexpr std::array names{"unit_name", "city_name", "who_name", "whom_name"};
                constexpr std::array other{
                    "move_owner_name",     "move_unit_name",     "aggression_whom_name",
                    "aggression_who_name", "create_owner_name",  "create_unit_name",
                    "create_homecity",     "auxiliary_civ_name", "auxiliary_unit_name",
                    "money_receiver_name", "wave_file"};
                for (std::size_t j = 0; j < names.size(); ++j)
                    fields.text(names[j], strings.names[j]);
                for (std::size_t j = 0; j < strings.text.size(); ++j)
                    fields.text("text[" + std::to_string(j) + "]", strings.text[j]);
                for (std::size_t j = 0; j < other.size(); ++j)
                    fields.text(other[j], strings.other[j]);
            });
        }
    }
    b.bytes(Section::trailing, 0, save.trailing);
}
} // namespace
std::string_view name(Section section) {
    constexpr std::array names{"header",        "globals",      "civilization_names",
                               "civilizations", "map_header",   "starts",
                               "remembered",    "tiles",        "coarse",
                               "regions",       "units",        "cities",
                               "tribes",        "cursor",       "leaders",
                               "power_history", "layout",       "views",
                               "scenario",      "eliminations", "multiplayer",
                               "event_header",  "events",       "event_strings",
                               "trailing"};
    return names.at(static_cast<std::size_t>(section));
}
bool StoredSnapshot::has_errors() const noexcept {
    return std::any_of(diagnostics_.begin(), diagnostics_.end(),
                       [](const auto& d) { return d.severity == Severity::error; });
}
bool StoredSnapshot::unsupported() const noexcept {
    return std::any_of(diagnostics_.begin(), diagnostics_.end(), [](const auto& d) {
        return d.category == Category::compatibility && d.severity == Severity::error;
    });
}
StoredSnapshot inspect_stored(std::span<const formats::Byte> bytes, FileKind kind,
                              FileAccess access) {
    if (access != FileAccess::offline_omniscient ||
        (kind != FileKind::save && kind != FileKind::raw_map))
        throw std::invalid_argument("Invalid offline inspection context or file kind");
    StoredSnapshot snapshot;
    snapshot.kind_ = kind;
    snapshot.bytes_.assign(bytes.begin(), bytes.end());
    snapshot.source_hash_ = base::sha256(bytes);
    try {
        Builder builder{bytes, snapshot.records_};
        if (kind == FileKind::save) {
            snapshot.version_ = decode_game_header(bytes).version;
            if (*snapshot.version_ != 0x2c) {
                snapshot.diagnostics_.push_back(
                    {Category::compatibility, Severity::error, "unsupported-version",
                     "header.version",
                     "Stored inspection supports version 0x2c; input version is " +
                         std::to_string(*snapshot.version_),
                     10});
                return snapshot;
            }
            const auto save = decode_stored_save(bytes);
            project(builder, save);
            detail::validate(save, snapshot.records_, snapshot.diagnostics_, snapshot.checks_);
        } else {
            const auto map = decode_raw_map(bytes);
            project(builder, map);
            detail::validate(map, snapshot.records_, snapshot.diagnostics_, snapshot.checks_);
        }
        builder.finish();
        snapshot.decoded_ = true;
        snapshot.diagnostics_.push_back(
            {Category::compatibility, Severity::information, "rules-unavailable", "",
             "External rules and language mapping were not loaded; rule-dependent validation and "
             "text interpretation are unavailable",
             std::nullopt});
    } catch (const DecodeError& error) {
        snapshot.records_.clear();
        snapshot.checks_.clear();
        snapshot.diagnostics_.push_back({Category::structural, Severity::error, "decode-failed", "",
                                         error.what(), error.offset()});
    }
    return snapshot;
}

StoredDiff diff_stored(const StoredSnapshot& before, const StoredSnapshot& after) {
    StoredDiff diff;
    diff.before_hash = before.source_hash();
    diff.after_hash = after.source_hash();
    const auto left = before.source_bytes(), right = after.source_bytes();
    for (std::size_t offset = 0; offset < std::max(left.size(), right.size());) {
        if (offset < left.size() && offset < right.size() && left[offset] == right[offset]) {
            ++offset;
            continue;
        }
        ByteChange change{offset, {}, {}};
        do {
            if (offset < left.size())
                change.before.push_back(left[offset]);
            if (offset < right.size())
                change.after.push_back(right[offset]);
            ++offset;
        } while (offset < std::max(left.size(), right.size()) &&
                 !(offset < left.size() && offset < right.size() && left[offset] == right[offset]));
        diff.bytes.push_back(std::move(change));
    }
    if (!before.decoded() || !after.decoded() || before.kind() != after.kind())
        return diff;
    diff.fields_compared = true;
    std::map<RecordKey, std::pair<const Record*, const Record*>> records;
    for (const auto& record : before.records())
        records[record.key].first = &record;
    for (const auto& record : after.records())
        records[record.key].second = &record;
    for (const auto& [key, pair] : records) {
        std::map<std::string_view, const Field*> right_fields;
        if (pair.second)
            for (const auto& field : pair.second->fields)
                right_fields.emplace(field.name, &field);
        if (pair.first)
            for (const auto& field : pair.first->fields) {
                const auto found = right_fields.find(field.name);
                if (found == right_fields.end())
                    diff.fields.push_back({key, field.name, field, std::nullopt});
                else {
                    if (field != *found->second)
                        diff.fields.push_back({key, field.name, field, *found->second});
                    right_fields.erase(found);
                }
            }
        if (pair.second)
            for (const auto& field : pair.second->fields)
                if (right_fields.contains(field.name))
                    diff.fields.push_back({key, field.name, std::nullopt, field});
    }
    return diff;
}
} // namespace openciv::query
