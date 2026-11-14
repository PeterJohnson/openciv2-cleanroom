// data-format.md sections 2,4 and appendices A/B; F03 sections 3-7.
#include "rules.hpp"
#include "classic_text.hpp"
#include "content_reader.hpp"
#include "rules/normalization.hpp"
#include <algorithm>
#include <set>

namespace openciv::formats::content {
namespace {
using detail::Reader;
using namespace state;
constexpr std::array<std::string_view, 10> collections{
    "advances", "improvements", "units",  "terrain",      "governments",
    "tribes",   "commodities",  "orders", "difficulties", "diplomaticAttitudes"};
constexpr std::array<std::size_t, 10> counts{100, 67, 62, 33, 7, 21, 16, 12, 6, 9};
constexpr std::array<std::string_view, 22> cosmic_names{"roadMovementMultiplier",
                                                        "triremeLossDenominator",
                                                        "foodPerCitizen",
                                                        "foodBoxRows",
                                                        "shieldBoxRows",
                                                        "settlerFoodLowGovernment",
                                                        "settlerFoodHighGovernment",
                                                        "unhappinessCitySize",
                                                        "unhappinessEmpireSize",
                                                        "aqueductSize",
                                                        "sewerSize",
                                                        "technologyCostParadigm",
                                                        "transformationWork",
                                                        "monarchyFreeSupport",
                                                        "communismFreeSupport",
                                                        "fundamentalismFreeSupport",
                                                        "communismPalaceDistance",
                                                        "fundamentalismScienceLossPercent",
                                                        "productionSwitchLossPercent",
                                                        "paradropRange",
                                                        "spaceshipMassThrustParadigm",
                                                        "fundamentalismScienceCapTenths"};
// Fixed identities explicitly named by gameplay-format.md section 4 and F03's
// reference vocabulary. Other authored identities require explicit ruleSlots.
std::optional<std::int16_t> canonical_advance(std::string_view id) {
    if (id == "advance.monarchy")
        return 54;
    if (id == "advance.communism")
        return 15;
    if (id == "advance.fundamentalism")
        return 31;
    if (id == "advance.republic")
        return 71;
    if (id == "advance.democracy")
        return 21;
    return {};
}
RuleText text(Reader field, bool padded, bool token = true, bool early_termination = false) {
    auto unicode = field.text(false);
    auto encoded = encode_windows1252(unicode, field.path);
    if (encoded.find_first_of(std::string("\0\r\n\x1a", 4)) != std::string::npos ||
        encoded.size() > 254)
        field.error("name cannot pass the classic single-line byte boundary");
    if (token && encoded.find(',') != std::string::npos)
        field.error("name contains a classic token delimiter");
    if (padded && encoded.empty()) {
        encoded = " ";
        unicode = " ";
    }
    const bool padded_space = padded && encoded == " ";
    if (!encoded.empty() && !padded_space &&
        (encoded.front() == ' ' || encoded.front() == '\t' || encoded.back() == ' ' ||
         encoded.back() == '\t'))
        field.error("name would change under classic edge trimming");
    if (early_termination && !encoded.empty() && encoded.front() == ';')
        field.error("name would terminate the classic advance/unit table");
    return {std::move(unicode), std::move(encoded)};
}
RuleName name(Reader row, bool padded = true, bool token = true, bool early = false) {
    return {row.at("id").id(), text(row.at("name"), padded, token, early)};
}
std::uint8_t named(Reader field, std::initializer_list<std::string_view> names) {
    const auto value = field.text();
    const auto found = std::find(names.begin(), names.end(), value);
    if (found == names.end())
        field.error("unknown named domain value");
    return static_cast<std::uint8_t>(std::distance(names.begin(), found));
}
struct IdMap {
    std::map<std::string, std::int16_t> ids;
    std::vector<std::size_t> rows;
    std::int16_t lookup(Reader field) const {
        const auto id = field.id();
        const auto found = ids.find(id);
        if (found == ids.end())
            field.error("unresolved rule identity " + id);
        return found->second;
    }
};
using Maps = std::array<IdMap, 10>;
Maps map_identities(Reader rules_root, const std::optional<Reader>& compatibility) {
    Maps maps;
    if (compatibility)
        compatibility->at("ruleSlots").members({}, collections);
    for (std::size_t kind = 0; kind < collections.size(); ++kind) {
        auto list = rules_root.at(collections[kind]);
        if (list.array_size() != counts[kind])
            list.error("wrong rule identity count");
        std::map<std::string, std::size_t> definitions;
        for (std::size_t row = 0; row < counts[kind]; ++row)
            if (!definitions.emplace(list.element(row).at("id").id(), row).second)
                list.element(row).error("duplicate rule identity");
        const auto first = kind == 7 ? 1 : 0;
        std::map<std::string, std::int16_t> explicit_slots;
        if (compatibility && compatibility->at("ruleSlots").has(collections[kind])) {
            auto slots = compatibility->at("ruleSlots").at(collections[kind]);
            for (std::size_t i = 0; i < slots.array_size(); ++i) {
                auto entry = slots.element(i);
                entry.fields({"id", "slot"});
                const auto id = entry.at("id").id();
                const auto slot = static_cast<std::int16_t>(entry.at("slot").integer(
                    first, first + static_cast<std::int32_t>(counts[kind]) - 1));
                if (!definitions.contains(id))
                    entry.error("mapping has no selected rule definition");
                if (!explicit_slots.emplace(id, slot).second)
                    entry.error("duplicate explicit rule mapping");
                if (kind == 0) {
                    const auto fixed = canonical_advance(id);
                    if (fixed && slot != *fixed)
                        entry.error("mapping relocates a registered identity");
                }
            }
        }
        maps[kind].rows.assign(counts[kind], counts[kind]);
        for (const auto& [id, row] : definitions) {
            std::optional<std::int16_t> slot;
            if (const auto found = explicit_slots.find(id); found != explicit_slots.end())
                slot = found->second;
            else if (kind == 0)
                slot = canonical_advance(id);
            if (!slot)
                list.element(row).error("unmapped rule identity; declare compatibility.ruleSlots");
            const auto index = static_cast<std::size_t>(*slot - first);
            if (maps[kind].rows[index] != counts[kind])
                list.error("multiple rule definitions occupy one classic identity");
            maps[kind].rows[index] = row;
            maps[kind].ids.emplace(id, *slot);
        }
    }
    return maps;
}
AdvanceReference advance(Reader field, const IdMap& map) {
    if (field.has("advance")) {
        field.fields({"advance"});
        return static_cast<AdvanceReference>(map.lookup(field.at("advance")));
    }
    field.fields({"special"});
    return named(field.at("special"), {"none", "never"}) == 0 ? AdvanceReference::none
                                                              : AdvanceReference::never;
}
TerrainResult terrain_result(Reader field, const IdMap& map) {
    if (field.has("terrain")) {
        field.fields({"terrain"});
        const auto slot = map.lookup(field.at("terrain"));
        if (slot > 10)
            field.error("terrain result must select a base terrain identity");
        return static_cast<TerrainResult>(slot);
    }
    field.fields({"special"});
    return named(field.at("special"), {"no-result", "improve-in-place"}) == 0
               ? TerrainResult::no_result
               : TerrainResult::improve_in_place;
}
TerrainWork work(Reader field, const IdMap& map) {
    field.fields({"result", "yieldBonus", "workTurns", "aiGovernmentThreshold"});
    return {terrain_result(field.at("result"), map), rules::code8(field.at("yieldBonus").integer()),
            rules::code8(field.at("workTurns").integer()),
            rules::code8(field.at("aiGovernmentThreshold").integer())};
}
} // namespace
std::shared_ptr<const RuleContent> load_rules(const ResolvedPack& pack) {
    const auto input = pack.components.find("rules");
    if (input == pack.components.end() || !pack.gameplay.bundle)
        throw Error("invalid-content", "rules", "requires resolved rules and gameplay selections");
    auto document = input->second.json;
    Reader root{document.value, document.numbers,
                input->second.file ? input->second.file->receipt.path : "rules.json"};
    root.fields({"format", "globalRules", "advances", "improvements", "wonderObsolescence", "units",
                 "terrain", "governments", "tribes", "commodities", "orders", "difficulties",
                 "diplomaticAttitudes"},
                true);
    root.at("format").constant("openciv2-rules/1");
    std::optional<JsonDocument> compat_document;
    std::optional<Reader> compat;
    if (pack.compatibility) {
        compat_document = pack.compatibility->json;
        compat.emplace(
            Reader{compat_document->value, compat_document->numbers, "compatibility.json"});
        compat->fields({"format", "profile", "encoding", "ruleSlots", "textLookups",
                        "scenarioFiles", "scenarioResources", "retainedUnitFlags"},
                       true);
        compat->at("format").constant("openciv2-classic-compatibility/1");
        compat->at("profile").constant(gameplay_target);
        compat->at("encoding").constant("windows-1252");
        compat->at("textLookups").object();
        compat->at("scenarioFiles").array_size();
        compat->at("scenarioResources").array_size();
    }
    const auto maps = map_identities(root, compat);
    const auto row = [&](std::size_t kind, std::size_t slot) {
        return root.at(collections[kind]).element(maps[kind].rows[slot]);
    };
    auto out = std::make_shared<RuleContent>();
    out->rules_source = input->second.file;
    if (pack.compatibility)
        out->compatibility_source = pack.compatibility->file;
    auto& values = out->definitions;
    auto cosmic = root.at("globalRules");
    cosmic.fields(cosmic_names);
    std::array<std::int32_t, 22> raw_cosmic{};
    for (std::size_t i = 0; i < raw_cosmic.size(); ++i)
        raw_cosmic[i] = cosmic.at(cosmic_names[i]).integer();
    values.cosmic = rules::normalize_cosmic(raw_cosmic);
    for (std::size_t i = 0; i < values.advances.size(); ++i) {
        auto r = row(0, i);
        r.fields(
            {"id", "name", "aiValue", "civilizedAiModifier", "prerequisites", "epoch", "category"});
        auto& target = values.advances[i];
        target.identity = name(r, true, true, true);
        target.ai_value = rules::signed8(r.at("aiValue").integer());
        target.civilized_ai_modifier = rules::signed8(r.at("civilizedAiModifier").integer());
        auto prerequisites = r.at("prerequisites");
        if (prerequisites.array_size() != 2)
            prerequisites.error("expected two prerequisites");
        for (std::size_t j = 0; j < 2; ++j)
            target.prerequisites[j] = advance(prerequisites.element(j), maps[0]);
        target.enabled = target.prerequisites[0] != AdvanceReference::never;
        target.epoch = static_cast<AdvanceEpoch>(
            named(r.at("epoch"), {"ancient", "renaissance", "industrial", "modern"}));
        target.category = static_cast<AdvanceCategory>(
            named(r.at("category"), {"military", "economic", "social", "academic", "applied"}));
    }
    values.enabled_advance_count = rules::fix_advance_references(values.advances);
    for (std::size_t i = 0; i < values.improvements.size(); ++i) {
        auto r = row(1, i);
        r.fields({"id", "name", "costRows", "upkeep", "prerequisite"});
        values.improvements[i] = {
            name(r), rules::code8(r.at("costRows").integer()),
            rules::code8(r.at("upkeep").integer()),
            rules::follow_disabled(advance(r.at("prerequisite"), maps[0]), values.advances)};
    }
    auto wonders = root.at("wonderObsolescence");
    if (wonders.array_size() != 28)
        wonders.error("expected 28 wonder expiry definitions");
    std::set<std::int16_t> seen_wonders;
    for (std::size_t i = 0; i < 28; ++i) {
        auto r = wonders.element(i);
        r.fields({"wonder", "advance"});
        const auto slot = maps[1].lookup(r.at("wonder"));
        if (slot < 39 || !seen_wonders.insert(slot).second)
            r.error("expected each wonder identity exactly once");
        values.wonder_obsolescence[static_cast<std::size_t>(slot - 39)] =
            advance(r.at("advance"), maps[0]);
    }
    for (std::size_t i = 0; i < values.units.size(); ++i) {
        auto r = row(2, i);
        r.fields({"id", "name", "obsoleteBy", "domain", "movementPoints", "range", "attack",
                  "defense", "hitPoints", "firepower", "costRows", "capacity", "role",
                  "prerequisite", "abilities"});
        auto& target = values.units[i];
        target.identity = name(r, true, true, true);
        target.obsolete_by = advance(r.at("obsoleteBy"), maps[0]);
        target.prerequisite = advance(r.at("prerequisite"), maps[0]);
        target.domain = static_cast<RuleDomain>(named(r.at("domain"), {"land", "air", "sea"}));
        target.role = static_cast<RuleRole>(
            named(r.at("role"), {"attack", "defend", "naval-superiority", "air-superiority",
                                 "sea-transport", "settle", "diplomacy", "trade"}));
        target.movement =
            rules::normalize_movement(r.at("movementPoints").integer(),
                                      values.cosmic[CosmicParameter::road_movement_multiplier]);
        target.range = rules::code8(r.at("range").integer());
        target.attack = rules::code8(r.at("attack").integer());
        target.defense = rules::code8(r.at("defense").integer());
        const auto hp = r.at("hitPoints").integer(0, 250);
        if (hp % 10)
            r.at("hitPoints").error("hit points must be a multiple of ten");
        target.hit_points = rules::normalize_classic_hp(hp / 10);
        target.firepower = rules::code8(r.at("firepower").integer());
        target.cost_rows = rules::normalize_unit_cost(r.at("costRows").integer());
        target.capacity = rules::code8(r.at("capacity").integer());
        auto abilities = r.at("abilities");
        for (std::size_t j = 0; j < abilities.array_size(); ++j) {
            const auto bit =
                named(abilities.element(j),
                      {"see-two", "ignore-zoc", "amphibious", "submarine", "attack-air",
                       "coast-bound", "ignore-walls", "carry-aircraft", "paradrop", "alpine",
                       "anti-horse-defense", "fundamentalism-free-support",
                       "destroyed-after-attack", "anti-air-defense", "detect-submarines"});
            const auto flag = static_cast<std::uint16_t>(1u << bit);
            if (target.abilities & flag)
                abilities.error("duplicate ability");
            target.abilities |= flag;
        }
    }
    if (compat) {
        auto flags = compat->at("retainedUnitFlags");
        std::set<std::int16_t> seen;
        for (std::size_t i = 0; i < flags.array_size(); ++i) {
            auto r = flags.element(i);
            r.fields({"unit", "positions"});
            const auto slot = maps[2].lookup(r.at("unit"));
            if (!seen.insert(slot).second)
                r.error("duplicate retained-unit-flag record");
            auto positions = r.at("positions");
            if (positions.array_size() > 1)
                positions.error("only one retained position is defined");
            if (positions.array_size()) {
                positions.element(0).integer(15, 15);
                values.units[static_cast<std::size_t>(slot)].abilities |= std::uint16_t{0x8000};
            }
        }
    }
    for (std::size_t i = 0; i < values.terrain.size(); ++i) {
        auto r = row(3, i);
        if (i < 11)
            r.fields({"id", "name", "movementCost", "defenseMultiplier", "food", "shields", "trade",
                      "irrigation", "mining", "transformation"});
        else
            r.fields(
                {"id", "name", "movementCost", "defenseMultiplier", "food", "shields", "trade"});
        auto& target = values.terrain[i];
        target.identity = name(r);
        target.movement_cost = rules::code8(r.at("movementCost").integer());
        auto ratio = r.at("defenseMultiplier");
        ratio.fields({"numerator", "denominator"});
        const auto doubled = 2 * static_cast<std::int64_t>(ratio.at("numerator").integer(0));
        const auto denominator = ratio.at("denominator").integer(1);
        if (doubled % denominator || doubled / denominator > 255)
            ratio.error("terrain defense must have an integral doubled factor in 0..255");
        target.defense_factor = static_cast<std::uint8_t>(doubled / denominator);
        target.food = rules::code8(r.at("food").integer());
        target.shields = rules::code8(r.at("shields").integer());
        target.trade = rules::code8(r.at("trade").integer());
        if (i < 11)
            target.work =
                BaseTerrainWork{work(r.at("irrigation"), maps[3]), work(r.at("mining"), maps[3]),
                                terrain_result(r.at("transformation"), maps[3])};
    }
    for (std::size_t i = 0; i < values.governments.size(); ++i) {
        auto r = row(4, i);
        r.fields({"id", "name", "maleTitle", "femaleTitle"});
        values.governments[i] = {name(r),
                                 {text(r.at("maleTitle"), true), text(r.at("femaleTitle"), true)}};
    }
    for (std::size_t i = 0; i < values.tribes.size(); ++i) {
        auto r = row(5, i);
        r.fields({"id", "pluralName", "adjective", "leaderNames", "preferredLeaderGender",
                  "preferredColor", "cityStyle", "personality", "governmentTitles"});
        auto& target = values.tribes[i];
        target.id = r.at("id").id();
        target.plural = text(r.at("pluralName"), true);
        target.adjective = text(r.at("adjective"), true);
        auto leaders = r.at("leaderNames");
        leaders.fields({"male", "female"});
        target.leaders = {text(leaders.at("male"), true), text(leaders.at("female"), true)};
        target.preferred_gender = named(r.at("preferredLeaderGender"), {"male", "female"});
        target.preferred_color = rules::normalize_leader_color(r.at("preferredColor").integer());
        target.city_style = rules::normalize_leader_style(r.at("cityStyle").integer());
        auto personality = r.at("personality");
        personality.fields({"aggression", "expansion", "civilizationPreference"});
        target.personality = {rules::signed8(personality.at("aggression").integer()),
                              rules::signed8(personality.at("expansion").integer()),
                              rules::signed8(personality.at("civilizationPreference").integer())};
        for (std::size_t j = 0; j < target.titles.size(); ++j)
            target.titles[j] = values.governments[j].titles;
        auto overrides = r.at("governmentTitles");
        for (std::size_t j = 0; j < overrides.array_size(); ++j) {
            auto title = overrides.element(j);
            title.fields({"government", "male", "female"});
            const auto government = maps[4].lookup(title.at("government"));
            if (!government)
                title.error("Anarchy titles cannot be overridden");
            target.titles[static_cast<std::size_t>(government)] = {text(title.at("male"), false),
                                                                   text(title.at("female"), false)};
        }
    }
    const auto simple_names = [&](auto& output, std::size_t kind, bool padded, bool token) {
        for (std::size_t i = 0; i < output.size(); ++i) {
            auto r = row(kind, i);
            r.fields({"id", "name"});
            output[i] = name(r, padded, token);
        }
    };
    simple_names(values.commodities, 6, true, true);
    simple_names(values.difficulties, 8, false, false);
    simple_names(values.diplomatic_attitudes, 9, false, false);
    for (std::size_t i = 0; i < values.orders.size(); ++i) {
        auto r = row(7, i);
        r.fields({"id", "name", "shortcutHint"});
        auto shortcut = text(r.at("shortcutHint"), false);
        if (shortcut.classic.size() > 1)
            r.at("shortcutHint").error("shortcut must be empty or one encoded character");
        values.orders[i] = {name(r, false), shortcut.classic.empty()
                                                ? std::uint8_t{0}
                                                : static_cast<std::uint8_t>(shortcut.classic[0])};
    }
    for (std::size_t i = 0; i < out->government_advances.size(); ++i) {
        const auto& required = pack.gameplay.bundle->values.governments[i].required_advance;
        if (!required)
            continue;
        if (const auto fixed = canonical_advance(*required))
            out->government_advances[i] = static_cast<AdvanceReference>(*fixed);
        else if (const auto found = maps[0].ids.find(*required); found != maps[0].ids.end())
            out->government_advances[i] = static_cast<AdvanceReference>(found->second);
        else
            throw Error("invalid-content", "gameplay/governments",
                        "unresolved required advance " + *required);
    }
    return out;
}
} // namespace openciv::formats::content
