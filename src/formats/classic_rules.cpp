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
RuleLoader::RuleLoader(TextReader& reader, RuleInput& output, TextCase comparison,
                       RuleLoadServices services)
    : reader_(reader), rules_(output.definitions), output_(output),
      comparison_(std::move(comparison)), services_(std::move(services)) {
    if (!comparison_.equal)
        throw std::invalid_argument("Rule loading requires a comparison service");
}
RuleText RuleLoader::text(std::string bytes, bool blank_as_space) {
    if (blank_as_space && bytes.empty())
        bytes = " ";
    return {decode_windows1252(bytes, "classic rule text"), std::move(bytes)};
}
void RuleLoader::name_into(RuleText& target, bool blank_as_space) {
    target = text(reader_.next_token(), blank_as_space);
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
void RuleLoader::load(std::string_view filename) {
    // Own the request across file callbacks and retained-state changes.
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
    leaders();
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
void RuleLoader::advances() {
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
        name_into(row.identity.name, true);
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
        row.identity.name = text("Advance " + std::to_string(i + 1), true);
        row.ai_value = 3;
        row.civilized_ai_modifier = 0;
        row.epoch = AdvanceEpoch{};
        row.category = AdvanceCategory{};
        row.prerequisites.fill(AdvanceReference::never);
        row.enabled = false;
    }
}
void RuleLoader::improvements() {
    for (std::size_t i = 0; i < rules_.improvements.size(); ++i) {
        reader_.next_line();
        auto& row = rules_.improvements[i];
        name_into(row.identity.name, true);
        row.cost_rows = rules::code8(reader_.next_integer());
        row.upkeep = rules::code8(reader_.next_integer());
        row.prerequisite = advance_reference(reader_.next_token());
        if (row.prerequisite == AdvanceReference::invalid)
            fail(RuleLoadFault::reference, "IMPROVE", i, 0, -5);
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
void RuleLoader::units() {
    output_.movement_points.fill(0);
    for (auto& row : rules_.units)
        row.prerequisite = AdvanceReference::never;
    std::size_t i = 0;
    for (; i < rules_.units.size(); ++i) {
        reader_.next_line();
        if (reader_.line().empty() || reader_.line().front() == ';')
            break;
        auto& row = rules_.units[i];
        name_into(row.identity.name, true);
        row.obsolete_by = advance_reference(reader_.next_token());
        row.domain = static_cast<RuleDomain>(rules::code8(reader_.next_integer()));
        const auto movement_points = reader_.next_integer();
        output_.movement_points[i] = movement_points;
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
        row.identity.name = text("Unit " + std::to_string(i + 1), true);
        auto identity = std::move(row.identity);
        row = UnitRule{};
        row.identity = std::move(identity);
        row.attack = 1;
        row.cost_rows = 1;
    }
}
void RuleLoader::terrain() {
    for (std::size_t i = 0; i < rules_.terrain.size(); ++i) {
        reader_.next_line();
        auto& row = rules_.terrain[i];
        name_into(row.identity.name, true);
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
    for (std::size_t i = 0; i < rules_.governments.size(); ++i) {
        reader_.next_line();
        auto& row = rules_.governments[i];
        name_into(row.identity.name, true);
        name_into(row.titles.male, true);
        name_into(row.titles.female, true);
    }
}
void RuleLoader::leaders() {
    for (std::size_t i = 0; i < rules_.tribes.size(); ++i) {
        output_.title_overrides[i].clear();
        reader_.next_line();
        auto& row = rules_.tribes[i];
        for (auto& leader : row.leaders)
            name_into(leader, true);
        row.preferred_gender = static_cast<std::uint8_t>(std::clamp(reader_.next_integer(), 0, 1));
        row.preferred_color = rules::normalize_leader_color(reader_.next_integer());
        row.city_style = rules::normalize_leader_style(reader_.next_integer());
        name_into(row.plural, true);
        name_into(row.adjective, true);
        for (auto& value : row.personality)
            value = rules::signed8(reader_.next_integer());
        while (true) {
            const auto government = reader_.next_integer();
            if (government <= 0)
                break;
            if (government > 6)
                fail(RuleLoadFault::invalid_title_index, "LEADERS", i, government, 0);
            TitlePair titles;
            name_into(titles.male, false);
            name_into(titles.female, false);
            output_.title_overrides[i].push_back(
                {static_cast<std::uint8_t>(government), std::move(titles)});
        }
    }
}
void RuleLoader::commodities() {
    for (std::size_t i = 0; i < rules_.commodities.size(); ++i) {
        reader_.next_line();
        name_into(rules_.commodities[i].name, true);
    }
}
void RuleLoader::orders() {
    for (std::size_t i = 0; i < rules_.orders.size(); ++i) {
        reader_.next_line();
        auto& row = rules_.orders[i];
        name_into(row.identity.name, false);
        const auto key = reader_.next_token();
        row.shortcut = key.empty() ? 0 : static_cast<std::uint8_t>(key.front());
    }
}
void RuleLoader::difficulties() {
    for (std::size_t i = 0; i < rules_.difficulties.size(); ++i) {
        reader_.next_line();
        rules_.difficulties[i].name = text(std::string(reader_.line()), false);
    }
}
void RuleLoader::attitudes() {
    for (std::size_t i = 0; i < rules_.diplomatic_attitudes.size(); ++i) {
        reader_.next_line();
        rules_.diplomatic_attitudes[i].name = text(std::string(reader_.line()), false);
    }
}
} // namespace openciv::formats::classic
