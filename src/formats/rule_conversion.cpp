module openciv.formats.content_conversion;

import std;
import openciv.formats.classic_rules;
import openciv.formats.classic_text;
import openciv.rules.rules_install;
import openciv.formats.rules;
import openciv.base.hash;

// M1-02, F03 tables and data-format.md section 4 / Appendix B.

namespace openciv::formats::content {
namespace {
using namespace state;
jt::Json array() {
    jt::Json out;
    out.setArray();
    return out;
}
std::string id(std::string_view collection, std::size_t slot) {
    auto name = std::string(collection);
    for (auto& c : name)
        if (c >= 'A' && c <= 'Z')
            c = static_cast<char>(c - 'A' + 'a');
    return "classic." + name + "." + std::to_string(slot);
}
jt::Json reference(AdvanceReference value) {
    jt::Json out;
    if (value == AdvanceReference::none)
        out["special"] = "none";
    else if (value == AdvanceReference::never)
        out["special"] = "never";
    else
        out["advance"] = id("advances", static_cast<std::size_t>(value));
    return out;
}
jt::Json terrain_result(TerrainResult value) {
    jt::Json out;
    if (value == TerrainResult::no_result)
        out["special"] = "no-result";
    else if (value == TerrainResult::improve_in_place)
        out["special"] = "improve-in-place";
    else
        out["terrain"] = id("terrain", static_cast<std::size_t>(value));
    return out;
}
jt::Json work(const TerrainWork& value) {
    jt::Json out;
    out["result"] = terrain_result(value.result);
    out["yieldBonus"] = int(value.yield_bonus);
    out["workTurns"] = int(value.work_turns);
    out["aiGovernmentThreshold"] = int(value.ai_government_threshold);
    return out;
}
std::string named(unsigned value, std::initializer_list<const char*> names) {
    if (value >= names.size())
        throw Error("unsupported-classic-value", "rules",
                    "Classic numeric code has no native named value");
    return *(names.begin() + value);
}
std::string hash(std::string_view bytes) {
    return base::sha256({reinterpret_cast<const std::uint8_t*>(bytes.data()), bytes.size()});
}
} // namespace
RuleConversion convert_rules(const Source& source, const classic::TextCase& comparison) {
    if (source.identity.empty() || source.bytes.size() > 16 * 1024 * 1024)
        throw Error("source-error", source.identity,
                    "Requires an identified RULES source of at most 16 MiB");
    const std::string text(source.bytes.begin(), source.bytes.end());
    auto state = std::make_unique<RuleState>();
    state->text.initialize();
    auto decoded = std::make_unique<state::RuleInput>();
    auto& operands = *decoded;
    classic::TextWorkspace workspace;
    classic::TextDirectories directories;
    classic::TextReader reader(workspace, directories,
                               {[&](auto, auto) -> classic::OpenedText {
                                    return {source.identity,
                                            std::make_unique<classic::MemoryTextStream>(text)};
                                },
                                {}},
                               comparison);
    try {
        classic::RuleLoader(reader, operands, comparison).load("RULES");
        rules::RuleInstaller(operands, *state).load(true);
    } catch (const classic::RuleLoadError& error) {
        throw Error("invalid-classic-rules", source.identity + "/" + error.section,
                    std::string(error.what()) + " at row " + std::to_string(error.row));
    } catch (const classic::TextError& error) {
        throw Error(error.fault == classic::TextFault::comparison_unavailable
                        ? "comparison-unavailable"
                        : "unsupported-classic-text",
                    source.identity, error.what());
    }
    auto& values = state->definitions;
    jt::Json rules, compatibility;
    rules["format"] = "openciv2-rules/1";
    compatibility["format"] = "openciv2-classic-compatibility/1";
    compatibility["profile"] = "classic-mge-5.4.0f";
    compatibility["encoding"] = "windows-1252";
    compatibility["textLookups"].setObject();
    compatibility["scenarioFiles"] = array();
    compatibility["scenarioResources"] = array();
    compatibility["retainedUnitFlags"] = array();
    constexpr std::array<const char*, 22> cosmic{"roadMovementMultiplier",
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
    for (std::size_t i = 0; i < cosmic.size(); ++i)
        rules["globalRules"][cosmic[i]] = int(values.cosmic.values[i]);
    auto collection = [&](const char* name, auto& table, auto emit) {
        rules[name] = array();
        compatibility["ruleSlots"][name] = array();
        for (std::size_t i = 0; i < table.size(); ++i) {
            auto& value = table[i];
            const auto identity = id(name, i);
            if constexpr (requires { value.identity.id; })
                value.identity.id = identity;
            else
                value.id = identity;
            jt::Json row;
            row["id"] = identity;
            emit(row, value, i);
            rules[name].getArray().push_back(std::move(row));
            jt::Json mapping;
            mapping["id"] = identity;
            mapping["slot"] = int(i + (std::string_view(name) == "orders" ? 1 : 0));
            compatibility["ruleSlots"][name].getArray().push_back(std::move(mapping));
        }
    };
    collection("advances", values.advances, [&](auto& row, const AdvanceRule& v, auto i) {
        row["name"] = v.identity.name.utf8;
        row["aiValue"] = int(v.ai_value);
        row["civilizedAiModifier"] = int(v.civilized_ai_modifier);
        row["prerequisites"] = array();
        for (auto p : operands.definitions.advances[i].prerequisites)
            row["prerequisites"].getArray().push_back(reference(p));
        row["epoch"] = named(unsigned(v.epoch), {"ancient", "renaissance", "industrial", "modern"});
        row["category"] =
            named(unsigned(v.category), {"military", "economic", "social", "academic", "applied"});
    });
    collection(
        "improvements", values.improvements, [&](auto& row, const ImprovementRule& v, auto i) {
            row["name"] = v.identity.name.utf8;
            row["costRows"] = int(v.cost_rows);
            row["upkeep"] = int(v.upkeep);
            row["prerequisite"] = reference(operands.definitions.improvements[i].prerequisite);
        });
    rules["wonderObsolescence"] = array();
    for (std::size_t i = 0; i < 28; ++i) {
        jt::Json row;
        row["wonder"] = id("improvements", i + 39);
        row["advance"] = reference(values.wonder_obsolescence[i]);
        rules["wonderObsolescence"].getArray().push_back(std::move(row));
    }
    constexpr std::array<const char*, 15> abilities{"see-two",
                                                    "ignore-zoc",
                                                    "amphibious",
                                                    "submarine",
                                                    "attack-air",
                                                    "coast-bound",
                                                    "ignore-walls",
                                                    "carry-aircraft",
                                                    "paradrop",
                                                    "alpine",
                                                    "anti-horse-defense",
                                                    "fundamentalism-free-support",
                                                    "destroyed-after-attack",
                                                    "anti-air-defense",
                                                    "detect-submarines"};
    collection("units", values.units, [&](auto& row, const UnitRule& v, auto i) {
        row["name"] = v.identity.name.utf8;
        row["obsoleteBy"] = reference(v.obsolete_by);
        row["prerequisite"] = reference(v.prerequisite);
        row["movementPoints"] = operands.movement_points[i];
        row["domain"] = named(unsigned(v.domain), {"land", "air", "sea"});
        row["role"] =
            named(unsigned(v.role), {"attack", "defend", "naval-superiority", "air-superiority",
                                     "sea-transport", "settle", "diplomacy", "trade"});
        row["range"] = int(v.range);
        row["attack"] = int(v.attack);
        row["defense"] = int(v.defense);
        row["hitPoints"] = int(v.hit_points);
        row["firepower"] = int(v.firepower);
        row["costRows"] = int(v.cost_rows);
        row["capacity"] = int(v.capacity);
        row["abilities"] = array();
        for (std::size_t bit = 0; bit < abilities.size(); ++bit)
            if (v.abilities & (1u << bit))
                row["abilities"].getArray().emplace_back(abilities[bit]);
        if (v.abilities & 0x8000) {
            jt::Json flag;
            flag["unit"] = id("units", i);
            flag["positions"] = array();
            flag["positions"].getArray().emplace_back(15);
            compatibility["retainedUnitFlags"].getArray().push_back(std::move(flag));
        }
    });
    collection("terrain", values.terrain, [&](auto& row, const TerrainRule& v, auto i) {
        row["name"] = v.identity.name.utf8;
        row["movementCost"] = int(v.movement_cost);
        row["defenseMultiplier"]["numerator"] = int(v.defense_factor);
        row["defenseMultiplier"]["denominator"] = 2;
        row["food"] = int(v.food);
        row["shields"] = int(v.shields);
        row["trade"] = int(v.trade);
        if (i < 11) {
            row["irrigation"] = work(v.work->irrigation);
            row["mining"] = work(v.work->mining);
            row["transformation"] = terrain_result(v.work->transformation);
        }
    });
    collection("governments", values.governments, [&](auto& row, const GovernmentRule& v, auto) {
        row["name"] = v.identity.name.utf8;
        row["maleTitle"] = v.titles.male.utf8;
        row["femaleTitle"] = v.titles.female.utf8;
    });
    collection("tribes", values.tribes, [&](auto& row, const TribeRule& v, auto i) {
        row["pluralName"] = v.plural.utf8;
        row["adjective"] = v.adjective.utf8;
        row["leaderNames"]["male"] = v.leaders[0].utf8;
        row["leaderNames"]["female"] = v.leaders[1].utf8;
        row["preferredLeaderGender"] =
            v.preferred_gender == state::Gender::female ? "female" : "male";
        row["preferredColor"] = int(v.preferred_color);
        row["cityStyle"] = int(v.city_style);
        row["personality"]["aggression"] = int(v.personality[0]);
        row["personality"]["expansion"] = int(v.personality[1]);
        row["personality"]["civilizationPreference"] = int(v.personality[2]);
        row["governmentTitles"] = array();
        for (const auto& title : operands.title_overrides[i]) {
            jt::Json entry;
            entry["government"] = id("governments", static_cast<std::size_t>(title.government));
            entry["male"] = title.titles.male.utf8;
            entry["female"] = title.titles.female.utf8;
            row["governmentTitles"].getArray().push_back(std::move(entry));
        }
    });
    const auto simple = [&](auto& row, const RuleName& v, auto) { row["name"] = v.name.utf8; };
    collection("commodities", values.commodities, simple);
    collection("difficulties", values.difficulties, simple);
    collection("diplomaticAttitudes", values.diplomatic_attitudes, simple);
    collection("orders", values.orders, [&](auto& row, const OrderRule& v, auto) {
        row["name"] = v.identity.name.utf8;
        row["shortcutHint"] =
            v.shortcut
                ? decode_windows1252(std::string(1, static_cast<char>(v.shortcut)), source.identity)
                : std::string{};
    });
    RuleConversion out{rules.toString() + "\n", compatibility.toString() + "\n", {}};
    ResolvedPack pack;
    pack.profile = "classic-mge-5.4.0f";
    pack.gameplay.bundle = builtin_gameplay();
    pack.components["rules"] = {nullptr, parse_json(out.rules, "converted-rules")};
    pack.compatibility =
        ResolvedDocument{nullptr, parse_json(out.compatibility, "converted-compatibility")};
    const auto native = load_rules(pack);
    auto installed = std::make_unique<RuleState>();
    installed->text.initialize();
    rules::RuleInstaller(native->input, *installed).load(true);
    if (installed->definitions != values)
        throw Error("unsupported-classic-value", source.identity,
                    "Native installation does not preserve classic rule values");
    auto& receipt = out.receipt;
    receipt["format"] = "openciv2-content-conversion/1";
    receipt["conversion"] = "classic-rules/1";
    receipt["profile"] = pack.profile;
    receipt["source"]["identity"] = source.identity;
    receipt["source"]["sha256"] = base::sha256(source.bytes);
    receipt["outputs"]["rules"]["sha256"] = hash(out.rules);
    receipt["outputs"]["rules"]["format"] = "openciv2-rules/1";
    receipt["outputs"]["compatibility"]["sha256"] = hash(out.compatibility);
    receipt["outputs"]["compatibility"]["format"] = "openciv2-classic-compatibility/1";
    receipt["validationGameplaySemanticSha256"] = pack.gameplay.bundle->semantic_sha256;
    receipt["unsupportedFields"] = array();
    receipt["lossyFields"] = array();
    receipt["limitations"] = array();
    receipt["limitations"].getArray().emplace_back(
        "Component outputs, not a playable pack. Identities derive from classic slots, never "
        "display names.");
    return out;
}
} // namespace openciv::formats::content
