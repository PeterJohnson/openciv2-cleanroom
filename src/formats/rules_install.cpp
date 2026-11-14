#include "rules_install.hpp"
#include "rules/normalization.hpp"

namespace openciv::formats::content {
using namespace state;
namespace {
std::int16_t narrow_label(TextId id) {
    return std::bit_cast<std::int16_t>(static_cast<std::uint16_t>(id));
}
} // namespace
RuleInstaller::RuleInstaller(std::shared_ptr<const RuleContent> input, RuleState& retained,
                             TextAllocation allocate)
    : input_(std::move(input)), retained_(retained), allocate_(std::move(allocate)) {
    if (!input_)
        throw std::invalid_argument("Rule installation requires admitted native content");
    if (!retained_.text.size())
        throw std::invalid_argument("Rule installation requires an initialized shared text pool");
}
void RuleInstaller::text_into(RuleText& target, TextId& binding, const RuleText& value,
                              std::size_t minimum) {
    auto text = value;
    const auto id = retained_.text.store(text.classic, minimum, allocate_);
    binding = id;
    target = std::move(text);
}
void RuleInstaller::name_into(RuleName& target, TextId& binding, const RuleName& value,
                              std::size_t minimum, bool keep) {
    if (!keep)
        text_into(target.name, binding, value.name, minimum);
    target.id = value.id;
}
void RuleInstaller::leader_name(RuleText& target, std::int16_t& binding, const RuleText& value,
                                std::size_t minimum, bool replace) {
    auto previous = target;
    const auto previous_binding = binding;
    auto text = value;
    const auto id = retained_.text.store(text.classic, minimum, allocate_);
    binding = narrow_label(id);
    target = std::move(text);
    if (!replace) {
        target = std::move(previous);
        binding = previous_binding;
    }
}
void RuleInstaller::load(bool replace_leader_data) {
    cosmic();
    advances();
    improvements();
    wonder_expiry();
    units();
    terrain();
    governments();
    leaders(replace_leader_data);
    commodities();
    orders();
    difficulties();
    attitudes();
}
void RuleInstaller::cosmic() {
    retained_.definitions.cosmic = input_->definitions.cosmic;
}
void RuleInstaller::advances(bool keep_names) {
    auto& values = retained_.definitions;
    values.enabled_advance_count = 0;
    for (auto& row : values.advances) {
        row.prerequisites.fill(AdvanceReference::never);
        row.enabled = false;
    }
    for (std::size_t i = 0; i < values.advances.size(); ++i) {
        auto& row = values.advances[i];
        const auto& source = input_->definitions.advances[i];
        name_into(row.identity, retained_.names.advances[i], source.identity, 20, keep_names);
        row.ai_value = source.ai_value;
        row.civilized_ai_modifier = source.civilized_ai_modifier;
        row.prerequisites = input_->advance_prerequisites[i];
        row.epoch = source.epoch;
        row.category = source.category;
        row.enabled = row.prerequisites[0] != AdvanceReference::never;
    }
    for (auto& row : values.advances)
        if (row.enabled) {
            ++values.enabled_advance_count;
            for (auto& prerequisite : row.prerequisites)
                prerequisite = rules::follow_disabled(prerequisite, values.advances);
        }
}
void RuleInstaller::improvements(bool keep_names) {
    auto& values = retained_.definitions;
    for (std::size_t i = 0; i < values.improvements.size(); ++i) {
        auto& row = values.improvements[i];
        const auto& source = input_->definitions.improvements[i];
        name_into(row.identity, retained_.names.improvements[i], source.identity, 25, keep_names);
        row.cost_rows = source.cost_rows;
        row.upkeep = source.upkeep;
        row.prerequisite = input_->improvement_prerequisites[i];
        row.prerequisite = rules::follow_disabled(row.prerequisite, values.advances);
    }
}
void RuleInstaller::wonder_expiry() {
    retained_.definitions.wonder_obsolescence = input_->definitions.wonder_obsolescence;
}
void RuleInstaller::units(bool keep_names) {
    auto& values = retained_.definitions;
    retained_.double_movement_applied = false;
    for (auto& row : values.units)
        row.prerequisite = AdvanceReference::never;
    for (std::size_t i = 0; i < values.units.size(); ++i) {
        auto& row = values.units[i];
        const auto& source = input_->definitions.units[i];
        name_into(row.identity, retained_.names.units[i], source.identity, 15, keep_names);
        if (!keep_names)
            retained_.names.interface_units[i] = retained_.text.last();
        row.obsolete_by = source.obsolete_by;
        row.domain = source.domain;
        row.movement = rules::normalize_movement(
            input_->movement_points[i], values.cosmic[CosmicParameter::road_movement_multiplier]);
        row.range = source.range;
        row.attack = source.attack;
        row.defense = source.defense;
        row.hit_points = source.hit_points;
        row.firepower = source.firepower;
        row.cost_rows = source.cost_rows;
        row.capacity = source.capacity;
        row.role = source.role;
        row.prerequisite = source.prerequisite;
        row.abilities = source.abilities;
    }
}
void RuleInstaller::terrain() {
    for (std::size_t i = 0; i < retained_.definitions.terrain.size(); ++i) {
        auto& row = retained_.definitions.terrain[i];
        const auto& source = input_->definitions.terrain[i];
        name_into(row.identity, retained_.names.terrain[i], source.identity, 15);
        row.movement_cost = source.movement_cost;
        row.defense_factor = source.defense_factor;
        row.food = source.food;
        row.shields = source.shields;
        row.trade = source.trade;
        if (i < 11)
            row.work = source.work;
    }
}
void RuleInstaller::governments() {
    for (std::size_t i = 0; i < retained_.definitions.governments.size(); ++i) {
        auto& row = retained_.definitions.governments[i];
        const auto& source = input_->definitions.governments[i];
        name_into(row.identity, retained_.names.governments[i], source.identity, 15);
        text_into(row.titles.male, retained_.names.government_titles[i][0], source.titles.male, 15);
        text_into(row.titles.female, retained_.names.government_titles[i][1], source.titles.female,
                  15);
    }
}
void RuleInstaller::leaders(bool replace) {
    auto& values = retained_.definitions;
    for (std::size_t i = 0; i < values.tribes.size(); ++i) {
        auto& row = values.tribes[i];
        auto& live = retained_.leaders[i];
        const auto& source = input_->definitions.tribes[i];
        row.id = source.id;
        for (std::size_t sex = 0; sex < 2; ++sex)
            leader_name(row.leaders[sex], live.names[sex], source.leaders[sex], 24, replace);
        if (replace) {
            row.preferred_gender = live.female = source.preferred_gender;
            live.leader_name = live.names[live.female];
            row.preferred_color = source.preferred_color;
            live.color = source.preferred_color;
            row.city_style = source.city_style;
            live.city_style = source.city_style;
        }
        leader_name(row.plural, live.plural, source.plural, 24, replace);
        leader_name(row.adjective, live.adjective, source.adjective, 24, replace);
        if (replace) {
            row.personality = source.personality;
            live.attack = source.personality[0];
            live.expand = source.personality[1];
            live.civilize = source.personality[2];
            for (std::size_t g = 0; g < row.titles.size(); ++g) {
                row.titles[g] = values.governments[g].titles;
                for (std::size_t sex = 0; sex < 2; ++sex)
                    live.titles[g][sex] = narrow_label(retained_.names.government_titles[g][sex]);
            }
        }
        for (const auto& override : input_->title_overrides[i]) {
            const auto g = override.government;
            if (g == 0 || g >= row.titles.size())
                throw std::invalid_argument("Native title override requires government 1..6");
            leader_name(row.titles[g].male, live.titles[g][0], override.titles.male, 0, replace);
            leader_name(row.titles[g].female, live.titles[g][1], override.titles.female, 0,
                        replace);
        }
    }
}
void RuleInstaller::commodities() {
    for (std::size_t i = 0; i < retained_.definitions.commodities.size(); ++i)
        name_into(retained_.definitions.commodities[i], retained_.names.commodities[i],
                  input_->definitions.commodities[i], 10);
}
void RuleInstaller::orders() {
    for (std::size_t i = 0; i < retained_.definitions.orders.size(); ++i) {
        auto& row = retained_.definitions.orders[i];
        const auto& source = input_->definitions.orders[i];
        name_into(row.identity, retained_.names.orders[i + 1], source.identity, 0);
        row.shortcut = source.shortcut;
    }
}
void RuleInstaller::difficulties() {
    for (std::size_t i = 0; i < retained_.definitions.difficulties.size(); ++i)
        name_into(retained_.definitions.difficulties[i], retained_.names.difficulties[i],
                  input_->definitions.difficulties[i], 0);
}
void RuleInstaller::attitudes() {
    for (std::size_t i = 0; i < retained_.definitions.diplomatic_attitudes.size(); ++i)
        name_into(retained_.definitions.diplomatic_attitudes[i], retained_.names.attitudes[i],
                  input_->definitions.diplomatic_attitudes[i], 0);
}
} // namespace openciv::formats::content
