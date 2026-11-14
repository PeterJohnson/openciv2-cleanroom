#include "query/stored_validation.hpp"
#include <map>

namespace openciv::query::detail {
namespace {
// F06 §1 nominal format and F02 §§3–7 invariants. These checks deliberately do
// not replace retained censuses, clamp cyclic stocks or invoke stack repair.
class Validator {
public:
    Validator(const std::vector<Record>& records, std::vector<Diagnostic>& diagnostics)
        : diagnostics_(diagnostics) {
        for (const auto& record : records)
            offsets_.emplace(record.key, record.offset);
    }
    void error(Section section, std::size_t slot, std::string field, std::size_t local_offset,
               std::string code, std::string message) {
        diagnostics_.push_back(
            {Category::semantic, Severity::error, std::move(code),
             std::string{name(section)} + "[" + std::to_string(slot) + "]." + field,
             std::move(message), offsets_.at({section, slot}) + local_offset});
    }
    void text(Section section, std::size_t slot, std::string field, std::size_t offset,
              const auto& value) {
        if (!value.terminated())
            error(section, slot, std::move(field), offset, "unterminated-text",
                  "Fixed classic text has no NUL within its capacity (F06 section 1)");
    }
    void map(const formats::MapHeader& header, const std::vector<world::Tile>& tiles) {
        const world::Geometry geometry{header.width, header.height};
        if (header.area != static_cast<std::int32_t>(geometry.area()))
            error(
                Section::map_header, 0, "area", 4, "map-size-mismatch",
                "Stored area disagrees with dimensions; decoding used the dimension-derived size");
        if (header.coarse_width != geometry.coarse_width())
            error(Section::map_header, 0, "coarse_width", 10, "map-size-mismatch",
                  "Stored coarse width disagrees with dimensions");
        if (header.coarse_height != geometry.coarse_height())
            error(Section::map_header, 0, "coarse_height", 12, "map-size-mismatch",
                  "Stored coarse height disagrees with dimensions");
        for (std::size_t i = 0; i < tiles.size(); ++i) {
            if (static_cast<std::uint8_t>(tiles[i].terrain) > 10)
                error(Section::tiles, i, "terrain", 0, "terrain-range",
                      "Base terrain is outside the eleven classic slots");
            if (tiles[i].owner && *tiles[i].owner > 7)
                error(Section::tiles, i, "owner", 5, "owner-range",
                      "Occupant owner is neither absent nor a civilization slot 0..7");
        }
    }
    void trailing(const formats::Bytes& bytes) {
        if (!bytes.empty())
            diagnostics_.push_back({Category::compatibility, Severity::warning,
                                    "uninterpreted-trailing-bytes", "trailing[0].payload",
                                    "Trailing bytes are preserved without interpretation; F06 does "
                                    "not require exact EOF",
                                    offsets_.at({Section::trailing, 0})});
    }

private:
    std::map<RecordKey, std::size_t> offsets_;
    std::vector<Diagnostic>& diagnostics_;
};
} // namespace
void validate(const formats::RawMapFile& map, const std::vector<Record>& records,
              std::vector<Diagnostic>& diagnostics, std::vector<std::string>& checks) {
    Validator v{records, diagnostics};
    checks = {"F06.current-raw-map-structure", "F06.map-size-consistency",
              "W01.tile-scalar-domains"};
    v.map(map.header, map.tiles);
    v.trailing(map.trailing);
}
void validate(const formats::StoredSave& save, const std::vector<Record>& records,
              std::vector<Diagnostic>& diagnostics, std::vector<std::string>& checks) {
    Validator v{records, diagnostics};
    checks = {"F06.current-save-structure",
              "F06.map-size-consistency",
              "W01.tile-scalar-domains",
              "F06.fixed-text-termination",
              "F06.selector-view-history-domains",
              "F02.live-entity-references",
              "F02.stack-reciprocity-position-cycles"};
    v.map(save.map.header, save.map.tiles);
    v.trailing(save.trailing);
    if (save.globals.multiplayer > 6)
        v.error(Section::globals, 0, "multiplayer", 0x1a, "mode-range",
                "Saved mode is outside the documented 0..6 encodings");
    const std::array selectors{save.globals.player_civ, save.globals.saved_current_civ,
                               save.globals.turn_civ};
    constexpr std::array selector_names{"player_civ", "saved_current_civ", "turn_civ"};
    for (std::size_t i = 0; i < selectors.size(); ++i)
        if (selectors[i] < 0 || selectors[i] > 7)
            v.error(Section::globals, 0, selector_names[i], 0x1b + i, "civilization-range",
                    "Saved civilization selector is outside 0..7");
    // F06 catalogs view_civ as i8 but does not establish its sentinel domain.
    // Original files contain -1. Preserve it without inventing a validity rule.
    diagnostics.push_back({Category::compatibility, Severity::information,
                           "view-civ-domain-unresolved", "globals[0].view_civ",
                           "Saved view-civilization sentinel semantics are unspecified; this "
                           "field is preserved without semantic validation",
                           12 + 0x1e});
    for (std::size_t i = 0; i < save.names.size(); ++i) {
        const auto& names = save.names[i];
        v.text(Section::civilization_names, i, "leader", 2, names.leader);
        v.text(Section::civilization_names, i, "tribe", 0x1a, names.tribe);
        v.text(Section::civilization_names, i, "adjective", 0x32, names.adjective);
        for (std::size_t j = 0; j < names.titles.size(); ++j)
            v.text(Section::civilization_names, i, "titles[" + std::to_string(j) + "]",
                   0x4a + 24 * j, names.titles[j]);
        const auto tribe = save.civilizations[i].tribe;
        if ((save.globals.civs_alive & (1U << i)) != 0 && (tribe < 0 || tribe > 20))
            v.error(Section::civilizations, i, "tribe", 6, "tribe-range",
                    "Living civilization's tribe is outside 0..20");
    }
    for (std::size_t i = 0; i < save.tribes.size(); ++i)
        if (save.tribes[i].female > 1)
            v.error(Section::tribes, i, "female", 2, "gender-range",
                    "Leader gender is outside 0/1");
    for (std::size_t i = 0; i < save.leaders.size(); ++i)
        if (save.leaders[i].female > 1)
            v.error(Section::leaders, i, "female", 4, "gender-range",
                    "Leader gender is outside 0/1");
    for (std::size_t i = 0; i < save.views.size(); ++i)
        if (save.views[i].zoom < -7 || save.views[i].zoom > 8)
            v.error(Section::views, i, "zoom", 8, "zoom-range", "Saved zoom is outside -7..8");
    if (save.scenario)
        v.text(Section::scenario, 0, "name", 2, save.scenario->name);
    if (save.eliminations.count < 0 || save.eliminations.count > 12)
        v.error(Section::eliminations, 0, "count", 0, "elimination-count-range",
                "Elimination count is outside 0..12");
    for (std::size_t i = 0;
         i < static_cast<std::size_t>(std::clamp<int>(save.eliminations.count, 0, 12)); ++i)
        v.text(Section::eliminations, 0, "names[" + std::to_string(i) + "]", 0x32 + 24 * i,
               save.eliminations.names[i]);
    const auto city_exists = [&](std::size_t slot) {
        return slot < save.cities.size() && state::occupied(save.cities[slot].token);
    };
    const auto unit_exists = [&](std::size_t slot) {
        return slot < save.units.size() && state::occupied(save.units[slot].token);
    };
    for (std::size_t i = 0; i < save.cities.size(); ++i) {
        const auto& city = save.cities[i];
        if (!state::occupied(city.token))
            continue;
        v.text(Section::cities, i, "name", 0x20, city.name);
        if (static_cast<std::uint8_t>(city.owner) > 7)
            v.error(Section::cities, i, "owner", 8, "owner-range",
                    "Live city's owner is outside 0..7");
        if (static_cast<std::uint8_t>(city.founder) > 7)
            v.error(Section::cities, i, "founder", 10, "civilization-range",
                    "Live city's founder is outside 0..7");
        if (city.route_count > 3)
            v.error(Section::cities, i, "route_count", 0x3a, "route-count-range",
                    "City has more than three active route slots");
        for (std::size_t j = 0; j < std::min<std::size_t>(city.route_count, 3); ++j) {
            const auto partner = city.route_partners[j].value;
            if (partner < 0 || !city_exists(static_cast<std::size_t>(partner)))
                v.error(Section::cities, i, "route_partners[" + std::to_string(j) + "]",
                        0x44 + 2 * j, "missing-city-reference",
                        "Active route partner does not identify a live scanned city");
        }
    }
    for (std::size_t i = 0; i < save.units.size(); ++i) {
        const auto& unit = save.units[i];
        if (!state::occupied(unit.token))
            continue;
        if (static_cast<std::uint8_t>(unit.owner) > 7)
            v.error(Section::units, i, "owner", 7, "owner-range",
                    "Live unit's owner is outside 0..7");
        if (unit.home && !city_exists(static_cast<std::size_t>(*unit.home)))
            v.error(Section::units, i, "home", 0x10, "missing-city-reference",
                    "Home does not identify a live scanned city");
        const std::array links{unit.previous.value, unit.next.value};
        for (std::size_t j = 0; j < links.size(); ++j) {
            if (links[j] < 0)
                continue; // All negative links are terminal.
            const auto target = static_cast<std::size_t>(links[j]);
            const auto field = j == 0 ? "previous" : "next";
            if (!unit_exists(target)) {
                v.error(Section::units, i, field, 0x16 + 2 * j, "missing-unit-reference",
                        "Stack link does not identify a live scanned unit");
                continue;
            }
            const auto& other = save.units[target];
            const auto reciprocal = j == 0 ? other.next.value : other.previous.value;
            if (reciprocal != static_cast<std::int32_t>(i))
                v.error(Section::units, i, field, 0x16 + 2 * j, "stack-not-reciprocal",
                        "Stack link has no reciprocal predecessor/successor");
            if (unit.x != other.x || unit.y != other.y)
                v.error(Section::units, i, field, 0x16 + 2 * j, "stack-position-mismatch",
                        "Linked units have different exact coordinates");
        }
    }
    // A functional next-link graph needs only one walk per unvisited node.
    std::vector<std::uint8_t> color(save.units.size());
    for (std::size_t start = 0; start < save.units.size(); ++start) {
        if (color[start] != 0 || !unit_exists(start))
            continue;
        std::vector<std::size_t> path;
        auto current = start;
        while (unit_exists(current) && color[current] == 0) {
            color[current] = 1;
            path.push_back(current);
            const auto next = save.units[current].next.value;
            if (next < 0) {
                current = save.units.size();
                break;
            }
            current = static_cast<std::size_t>(next);
        }
        if (unit_exists(current) && color[current] == 1)
            v.error(Section::units, current, "next", 0x18, "stack-cycle",
                    "Stack successor links form a cycle");
        for (auto slot : path)
            color[slot] = 2;
    }
}
} // namespace openciv::query::detail
