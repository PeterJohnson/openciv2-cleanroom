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
template <class T, class U, class Read>
void leader_field(T& model, U& field, bool replace, Read read) {
    const auto previous_model = model;
    const auto previous_field = field;
    const auto value = read();
    model = static_cast<T>(value);
    field = static_cast<U>(value);
    if (!replace) {
        model = previous_model;
        field = previous_field;
    }
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
RuleLoader::RuleLoader(TextReader& reader, RuleState& retained, TextCase comparison,
                       RuleLoadServices services, RuleConversionOperands* conversion)
    : reader_(reader), rules_(retained.definitions), retained_(retained),
      comparison_(std::move(comparison)), services_(std::move(services)), conversion_(conversion) {
    if (!comparison_.equal)
        throw std::invalid_argument("Rule loading requires a comparison service");
    if (!retained_.text.size())
        throw std::invalid_argument("Rule loading requires an initialized shared text pool");
}
RuleLoader::StoredText RuleLoader::store(std::string text, bool blank_as_space) {
    if (blank_as_space && text.empty())
        text = " ";
    RuleText value{content::decode_windows1252(text, "classic rule text"), text};
    const auto id = retained_.text.store(text);
    return {std::move(value), id};
}
void RuleLoader::store_into(RuleText& target, TextId& binding, std::string text,
                            bool blank_as_space) {
    auto stored = store(std::move(text), blank_as_space);
    binding = stored.id;
    target = std::move(stored.value);
}
void RuleLoader::name_into(RuleText& target, TextId& binding, bool blank_as_space, bool keep) {
    auto token = reader_.next_token();
    if (!keep)
        store_into(target, binding, std::move(token), blank_as_space);
}
void RuleLoader::leader_name(RuleText& target, NameReference& binding, bool blank_as_space,
                             bool replace) {
    auto token = reader_.next_token();
    if (!replace)
        return;
    auto stored = store(std::move(token), blank_as_space);
    binding = {stored.id, false};
    target = std::move(stored.value);
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
    if (conversion_ && !replace_leader_data)
        throw std::invalid_argument("Complete conversion operands require replacement leader data");
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
    if (conversion_)
        for (auto& pair : conversion_->advance_prerequisites)
            pair.fill(AdvanceReference::never);
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
        name_into(row.identity.name, retained_.names.advances[i], true, keep_names);
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
        if (conversion_)
            conversion_->advance_prerequisites[i] = row.prerequisites;
    }
    for (; i < rules_.advances.size(); ++i) {
        auto& row = rules_.advances[i];
        if (!keep_names)
            store_into(row.identity.name, retained_.names.advances[i],
                       "Advance " + std::to_string(i + 1), true);
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
        name_into(row.identity.name, retained_.names.improvements[i], true, keep_names);
        row.cost_rows = rules::code8(reader_.next_integer());
        row.upkeep = rules::code8(reader_.next_integer());
        row.prerequisite = advance_reference(reader_.next_token());
        if (row.prerequisite == AdvanceReference::invalid)
            fail(RuleLoadFault::reference, "IMPROVE", i, 0, -5);
        if (conversion_)
            conversion_->improvement_prerequisites[i] = row.prerequisite;
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
    if (conversion_)
        conversion_->movement_points.fill(0);
    retained_.double_movement_applied = false;
    for (auto& row : rules_.units)
        row.prerequisite = AdvanceReference::never;
    std::size_t i = 0;
    for (; i < rules_.units.size(); ++i) {
        reader_.next_line();
        if (reader_.line().empty() || reader_.line().front() == ';')
            break;
        auto& row = rules_.units[i];
        name_into(row.identity.name, retained_.names.units[i], true, keep_names);
        row.obsolete_by = advance_reference(reader_.next_token());
        row.domain = static_cast<RuleDomain>(rules::code8(reader_.next_integer()));
        const auto movement_points = reader_.next_integer();
        if (conversion_)
            conversion_->movement_points[i] = movement_points;
        row.movement = rules::normalize_movement(
            movement_points, rules_.cosmic[CosmicParameter::road_movement_multiplier]);
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
            store_into(row.identity.name, retained_.names.units[i], "Unit " + std::to_string(i + 1),
                       15);
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
        name_into(row.identity.name, retained_.names.terrain[i], true);
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
        name_into(row.identity.name, retained_.names.governments[i], true);
        name_into(row.titles.male, retained_.names.government_titles[i][0], true);
        name_into(row.titles.female, retained_.names.government_titles[i][1], true);
    }
}
void RuleLoader::leaders(bool replace) {
    if (conversion_ && !replace)
        throw std::invalid_argument("Conversion title operands require replacement leader data");
    for (std::size_t i = 0; i < rules_.tribes.size(); ++i) {
        if (conversion_)
            conversion_->title_overrides[i].clear();
        reader_.next_line();
        auto& row = rules_.tribes[i];
        auto& live = retained_.leaders[i];
        for (std::size_t sex = 0; sex < 2; ++sex)
            leader_name(row.leaders[sex], live.names[sex], true, replace);
        leader_field(row.preferred_gender, live.female, replace,
                     [&] { return std::clamp(reader_.next_integer(), 0, 1); });
        if (replace)
            live.leader_name = live.names[live.female];
        leader_field(row.preferred_color, live.color, replace,
                     [&] { return rules::normalize_leader_color(reader_.next_integer()); });
        leader_field(row.city_style, live.city_style, replace,
                     [&] { return rules::normalize_leader_style(reader_.next_integer()); });
        leader_name(row.plural, live.plural, true, replace);
        leader_name(row.adjective, live.adjective, true, replace);
        leader_field(row.personality[0], live.attack, replace,
                     [&] { return rules::signed8(reader_.next_integer()); });
        leader_field(row.personality[1], live.expand, replace,
                     [&] { return rules::signed8(reader_.next_integer()); });
        leader_field(row.personality[2], live.civilize, replace,
                     [&] { return rules::signed8(reader_.next_integer()); });
        if (replace)
            for (std::size_t g = 0; g < row.titles.size(); ++g) {
                row.titles[g] = rules_.governments[g].titles;
                for (std::size_t sex = 0; sex < 2; ++sex)
                    live.titles[g][sex] =
                        NameReference{retained_.names.government_titles[g][sex], false};
            }
        while (true) {
            const auto government = reader_.next_integer();
            if (government <= 0)
                break;
            if (government > 6)
                fail(RuleLoadFault::invalid_title_index, "LEADERS", i, government, 0);
            const auto g = static_cast<std::size_t>(government);
            leader_name(row.titles[g].male, live.titles[g][0], false, replace);
            leader_name(row.titles[g].female, live.titles[g][1], false, replace);
            if (conversion_)
                conversion_->title_overrides[i].push_back(
                    {static_cast<std::uint8_t>(g), row.titles[g]});
        }
    }
}
void RuleLoader::commodities() {
    for (std::size_t i = 0; i < rules_.commodities.size(); ++i) {
        reader_.next_line();
        name_into(rules_.commodities[i].name, retained_.names.commodities[i], true);
    }
}
void RuleLoader::orders() {
    for (std::size_t i = 0; i < rules_.orders.size(); ++i) {
        reader_.next_line();
        auto& row = rules_.orders[i];
        name_into(row.identity.name, retained_.names.orders[i + 1], false);
        const auto key = reader_.next_token();
        row.shortcut = key.empty() ? 0 : static_cast<std::uint8_t>(key.front());
    }
}
void RuleLoader::difficulties() {
    for (std::size_t i = 0; i < rules_.difficulties.size(); ++i) {
        reader_.next_line();
        store_into(rules_.difficulties[i].name, retained_.names.difficulties[i],
                   std::string(reader_.line()), 0);
    }
}
void RuleLoader::attitudes() {
    for (std::size_t i = 0; i < rules_.diplomatic_attitudes.size(); ++i) {
        reader_.next_line();
        store_into(rules_.diplomatic_attitudes[i].name, retained_.names.attitudes[i],
                   std::string(reader_.line()), 0);
    }
}
} // namespace openciv::formats::classic
