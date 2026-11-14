#include "classic_rules.hpp"
#include "classic_text.hpp"
#include "rules/normalization.hpp"
#include <algorithm>

namespace openciv::formats::classic {
using namespace state;
namespace {
// Fixed classic vocabulary from F03 section 3, independent of input display names.
constexpr std::array<std::string_view, 100> advance_codes{
    "AFl", "Alp", "Amp", "Ast", "Ato", "Aut", "Ban", "Bri", "Bro", "Cer", "Che", "Chi", "CoL",
    "CA",  "Cmb", "Cmn", "Cmp", "Csc", "Cst", "Cor", "Cur", "Dem", "Eco", "E1",  "E2",  "Eng",
    "Env", "Esp", "Exp", "Feu", "Fli", "Fun", "FP",  "Gen", "Gue", "Gun", "Hor", "Ind", "Inv",
    "Iro", "Lab", "Las", "Ldr", "Lit", "Too", "Mag", "Map", "Mas", "MP",  "Mat", "Med", "Met",
    "Min", "Mob", "Mon", "MT",  "Mys", "Nav", "NF",  "NP",  "Phi", "Phy", "Pla", "Plu", "PT",
    "Pot", "Rad", "RR",  "Rec", "Ref", "Rfg", "Rep", "Rob", "Roc", "San", "Sea", "SFl", "Sth",
    "SE",  "Stl", "Sup", "Tac", "The", "ToG", "Tra", "Uni", "War", "Whe", "Wri", "FT",  "U1",
    "U2",  "U3",  "X1",  "X2",  "X3",  "X4",  "X5",  "X6",  "X7"};
// Capture before a service call: preservation is field-local, not a row snapshot.
template <class T, class Read> void leader_field(T& field, bool replace, Read read) {
    const auto previous = field;
    field = read();
    if (!replace)
        field = previous;
}
} // namespace
RuleLoadError::RuleLoadError(RuleLoadFault fault, std::string section, std::string line,
                             std::size_t row, int position, int code)
    : std::runtime_error("Classic rule load stopped in " + section), fault(fault),
      section(std::move(section)), line(std::move(line)), row(row), position(position), code(code) {
}

AdvanceReference advance_reference(std::string_view token) noexcept {
    if (token == "nil")
        return AdvanceReference::none;
    if (token == "no")
        return AdvanceReference::never;
    const auto found = std::find(advance_codes.begin(), advance_codes.end(), token);
    return found == advance_codes.end()
               ? AdvanceReference::invalid
               : static_cast<AdvanceReference>(found - advance_codes.begin());
}
TerrainResult terrain_result(std::string_view token, const TextCase& comparison) {
    if (!comparison.equal)
        throw std::invalid_argument("Terrain references require a comparison service");
    if (comparison.equal(token, "no"))
        return TerrainResult::no_result;
    if (comparison.equal(token, "yes"))
        return TerrainResult::improve_in_place;
    constexpr std::array<std::string_view, 11> codes{"Drt", "Pln", "Grs", "For", "Hil", "Mou",
                                                     "Tun", "Gla", "Swa", "Jun", "Oce"};
    const auto found = std::find(codes.begin(), codes.end(), token);
    return found == codes.end() ? TerrainResult::invalid
                                : static_cast<TerrainResult>(found - codes.begin());
}
RuleLoader::RuleLoader(TextReader& reader, RuleDefinitions& definitions, RuleLoadState& retained,
                       TextCase comparison, RuleLoadServices services)
    : reader_(reader), rules_(definitions), retained_(retained), comparison_(std::move(comparison)),
      services_(std::move(services)) {
    if (!comparison_.equal)
        throw std::invalid_argument("Rule loading requires a comparison service");
}
RuleText RuleLoader::store(std::string text, std::size_t minimum) {
    if (minimum && text.empty())
        text = " ";
    RuleText value{content::decode_windows1252(text, "classic rule text"), text};
    if (!minimum && text.empty())
        return value;
    const auto bytes = std::max(minimum, text.size() + 1);
    if (services_.allocate)
        services_.allocate(text, bytes);
    text.resize(bytes, '\0');
    retained_.text_allocations.push_back(std::move(text));
    return value;
}
RuleText RuleLoader::name(std::size_t minimum) {
    return store(reader_.next_token(), minimum);
}
void RuleLoader::name_into(RuleText& target, std::size_t minimum, bool keep) {
    auto token = reader_.next_token();
    if (!keep)
        target = store(std::move(token), minimum);
}
void RuleLoader::section(std::optional<std::string_view> filename, std::string_view name) {
    if (!reader_.open(filename, name))
        fail(RuleLoadFault::missing_section, name, 0, 0, 0);
}
void RuleLoader::fail(RuleLoadFault fault, std::string_view section, std::size_t row, int position,
                      int code) {
    const RuleLoadError error(fault, std::string(section), std::string(reader_.line()), row,
                              position, code);
    if (services_.fatal)
        services_.fatal(error);
    throw error;
}
void RuleLoader::load(std::string_view filename, bool replace_leader_data) {
    // Own the request across file/allocation callbacks and retained-state changes.
    const std::string owned_filename(filename);
    section(owned_filename, "COSMIC");
    cosmic();
    section(owned_filename, "CIVILIZE");
    advances();
    section(owned_filename, "IMPROVE");
    improvements();
    section(std::nullopt, "ENDWONDER");
    wonder_expiry();
    section(std::nullopt, "UNITS");
    units();
    section(std::nullopt, "TERRAIN");
    terrain();
    section(std::nullopt, "GOVERNMENTS");
    governments();
    section(std::nullopt, "LEADERS");
    leaders(replace_leader_data);
    section(std::nullopt, "CARAVAN");
    commodities();
    section(std::nullopt, "ORDERS");
    orders();
    section(std::nullopt, "DIFFICULTY");
    difficulties();
    section(std::nullopt, "ATTITUDES");
    attitudes();
    reader_.close();
}
void RuleLoader::cosmic() {
    for (std::size_t i = 0; i < rules_.cosmic.values.size(); ++i) {
        reader_.next_line();
        rules_.cosmic.values[i] =
            rules::normalize_cosmic_value(static_cast<CosmicParameter>(i), reader_.next_integer());
    }
}
void RuleLoader::advances(bool keep_names) {
    rules_.enabled_advance_count = 0;
    for (auto& row : rules_.advances) {
        row.prerequisites.fill(AdvanceReference::never);
        row.enabled = false;
    }
    std::size_t i = 0;
    for (; i < rules_.advances.size(); ++i) {
        reader_.next_line();
        if (reader_.line().empty() || reader_.line().front() == ';')
            break;
        auto& row = rules_.advances[i];
        name_into(row.identity.name, 20, keep_names);
        row.ai_value = rules::signed8(reader_.next_integer());
        row.civilized_ai_modifier = rules::signed8(reader_.next_integer());
        for (int p = 0; p < 2; ++p) {
            auto& prerequisite = row.prerequisites[static_cast<std::size_t>(p)];
            prerequisite = advance_reference(reader_.next_token());
            if (prerequisite == AdvanceReference::invalid)
                fail(RuleLoadFault::reference, "CIVILIZE", i, p, -4);
        }
        row.epoch = static_cast<AdvanceEpoch>(rules::code8(reader_.next_integer()));
        row.category = static_cast<AdvanceCategory>(rules::code8(reader_.next_integer()));
        row.enabled = row.prerequisites[0] != AdvanceReference::never;
    }
    for (; i < rules_.advances.size(); ++i) {
        auto& row = rules_.advances[i];
        if (!keep_names)
            row.identity.name = store("Advance " + std::to_string(i + 1), 20);
        row.ai_value = 3;
        row.civilized_ai_modifier = 0;
        row.epoch = AdvanceEpoch{};
        row.category = AdvanceCategory{};
        row.prerequisites.fill(AdvanceReference::never);
        row.enabled = false;
    }
    for (auto& row : rules_.advances)
        if (row.enabled) {
            ++rules_.enabled_advance_count;
            for (auto& prerequisite : row.prerequisites)
                prerequisite = rules::follow_disabled(prerequisite, rules_.advances);
        }
}
void RuleLoader::improvements(bool keep_names) {
    for (std::size_t i = 0; i < rules_.improvements.size(); ++i) {
        reader_.next_line();
        auto& row = rules_.improvements[i];
        name_into(row.identity.name, 25, keep_names);
        row.cost_rows = rules::code8(reader_.next_integer());
        row.upkeep = rules::code8(reader_.next_integer());
        row.prerequisite = advance_reference(reader_.next_token());
        if (row.prerequisite == AdvanceReference::invalid)
            fail(RuleLoadFault::reference, "IMPROVE", i, 0, -5);
        row.prerequisite = rules::follow_disabled(row.prerequisite, rules_.advances);
    }
}
void RuleLoader::wonder_expiry() {
    for (std::size_t i = 0; i < rules_.wonder_obsolescence.size(); ++i) {
        reader_.next_line();
        auto& value = rules_.wonder_obsolescence[i];
        value = advance_reference(reader_.next_token());
        if (value == AdvanceReference::invalid)
            fail(RuleLoadFault::reference, "ENDWONDER", i, 0, -4);
    }
}
void RuleLoader::units(bool keep_names) {
    retained_.double_movement_applied = false;
    for (auto& row : rules_.units)
        row.prerequisite = AdvanceReference::never;
    std::size_t i = 0;
    for (; i < rules_.units.size(); ++i) {
        reader_.next_line();
        if (reader_.line().empty() || reader_.line().front() == ';')
            break;
        auto& row = rules_.units[i];
        name_into(row.identity.name, 15, keep_names);
        if (!keep_names)
            retained_.interface_unit_names[i] = retained_.text_allocations.size() - 1;
        row.obsolete_by = advance_reference(reader_.next_token());
        row.domain = static_cast<RuleDomain>(rules::code8(reader_.next_integer()));
        row.movement = rules::normalize_movement(
            reader_.next_integer(), rules_.cosmic[CosmicParameter::road_movement_multiplier]);
        row.range = rules::code8(reader_.next_integer());
        row.attack = rules::code8(reader_.next_integer());
        row.defense = rules::code8(reader_.next_integer());
        row.hit_points = rules::normalize_classic_hp(reader_.next_integer());
        row.firepower = rules::code8(reader_.next_integer());
        row.cost_rows = rules::normalize_unit_cost(reader_.next_integer());
        row.capacity = rules::code8(reader_.next_integer());
        row.role = static_cast<RuleRole>(rules::code8(reader_.next_integer()));
        row.prerequisite = advance_reference(reader_.next_token());
        row.abilities = reader_.next_binary_flags();
        if (row.obsolete_by == AdvanceReference::invalid ||
            row.prerequisite == AdvanceReference::invalid)
            fail(RuleLoadFault::reference, "UNITS", i, 0, -6);
    }
    for (; i < rules_.units.size(); ++i) {
        auto& row = rules_.units[i];
        if (!keep_names)
            row.identity.name = store("Unit " + std::to_string(i + 1), 15);
        auto identity = std::move(row.identity);
        row = UnitRule{};
        row.identity = std::move(identity);
        row.attack = 1;
        row.cost_rows = 1;
        // F03 Appendix A: tail defaults do not update interface name bindings.
    }
}
void RuleLoader::terrain() {
    for (std::size_t i = 0; i < rules_.terrain.size(); ++i) {
        reader_.next_line();
        auto& row = rules_.terrain[i];
        row.identity.name = name(15);
        row.movement_cost = rules::code8(reader_.next_integer());
        row.defense_factor = rules::code8(reader_.next_integer());
        row.food = rules::code8(reader_.next_integer());
        row.shields = rules::code8(reader_.next_integer());
        row.trade = rules::code8(reader_.next_integer());
        if (i >= 11)
            continue;
        if (!row.work)
            row.work.emplace();
        for (int p = 0; p < 2; ++p) {
            auto& work = p == 0 ? row.work->irrigation : row.work->mining;
            work.result = terrain_result(reader_.next_token(), comparison_);
            work.yield_bonus = rules::code8(reader_.next_integer());
            work.work_turns = rules::code8(reader_.next_integer());
            work.ai_government_threshold = rules::code8(reader_.next_integer());
            if (work.result == TerrainResult::invalid)
                fail(RuleLoadFault::reference, "TERRAIN", i, p, -7);
        }
        row.work->transformation = terrain_result(reader_.next_token(), comparison_);
        if (row.work->transformation == TerrainResult::invalid)
            fail(RuleLoadFault::reference, "TERRAIN", i, 99, -7);
    }
}
void RuleLoader::governments() {
    for (auto& row : rules_.governments) {
        reader_.next_line();
        row.identity.name = name(15);
        row.titles.male = name(15);
        row.titles.female = name(15);
    }
}
void RuleLoader::leaders(bool replace) {
    for (std::size_t i = 0; i < rules_.tribes.size(); ++i) {
        reader_.next_line();
        auto& row = rules_.tribes[i];
        for (auto& leader : row.leaders)
            leader_field(leader, replace, [&] { return name(24); });
        leader_field(row.preferred_gender, replace,
                     [&] { return rules::code8(std::clamp(reader_.next_integer(), 0, 1)); });
        if (replace)
            retained_.current_leader_names[i] = row.leaders[row.preferred_gender];
        leader_field(row.preferred_color, replace,
                     [&] { return rules::normalize_leader_color(reader_.next_integer()); });
        leader_field(row.city_style, replace,
                     [&] { return rules::normalize_leader_style(reader_.next_integer()); });
        leader_field(row.plural, replace, [&] { return name(24); });
        leader_field(row.adjective, replace, [&] { return name(24); });
        for (auto& trait : row.personality)
            leader_field(trait, replace, [&] { return rules::signed8(reader_.next_integer()); });
        if (replace)
            for (std::size_t g = 0; g < row.titles.size(); ++g)
                row.titles[g] = rules_.governments[g].titles;
        while (true) {
            const auto government = reader_.next_integer();
            if (government <= 0)
                break;
            if (government > 6)
                fail(RuleLoadFault::invalid_title_index, "LEADERS", i, government, 0);
            auto& pair = row.titles[static_cast<std::size_t>(government)];
            leader_field(pair.male, replace, [&] { return name(0); });
            leader_field(pair.female, replace, [&] { return name(0); });
        }
    }
}
void RuleLoader::commodities() {
    for (auto& row : rules_.commodities) {
        reader_.next_line();
        row.name = name(10);
    }
}
void RuleLoader::orders() {
    for (auto& row : rules_.orders) {
        reader_.next_line();
        row.identity.name = name(0);
        const auto key = reader_.next_token();
        row.shortcut = key.empty() ? 0 : static_cast<std::uint8_t>(key.front());
    }
}
void RuleLoader::difficulties() {
    for (auto& row : rules_.difficulties) {
        reader_.next_line();
        row.name = store(std::string(reader_.line()), 0);
    }
}
void RuleLoader::attitudes() {
    for (auto& row : rules_.diplomatic_attitudes) {
        reader_.next_line();
        row.name = store(std::string(reader_.line()), 0);
    }
}
} // namespace openciv::formats::classic
