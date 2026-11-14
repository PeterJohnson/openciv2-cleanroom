#pragma once
// Independently authored complete identities, not stock rules or engine outputs.
// Collection counts and row fields follow data-format.md section 4 / Appendix B.
#include "formats/rules.hpp"
#include <string>

namespace rule_fixture {
using namespace openciv::formats::content;
inline jt::Json json(std::string_view text) {
    return parse_json(text, "authored fixture").value;
}
struct Input {
    jt::Json rules, compatibility;
    std::shared_ptr<const GameplayBundle> gameplay = builtin_gameplay();
    Input() {
        rules = json(R"({"format":"openciv2-rules/1","globalRules":{},"wonderObsolescence":[]})");
        compatibility = json(
            R"({"format":"openciv2-classic-compatibility/1","profile":"classic-mge-5.4.0f","encoding":"windows-1252","ruleSlots":{},"textLookups":{},"scenarioFiles":[],"scenarioResources":[],"retainedUnitFlags":[]})");
        constexpr std::array<std::string_view, 22> cosmic{"roadMovementMultiplier",
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
        constexpr std::array<int, 22> values{3,  2,  2, 5, 5, 1, 2,  7,  14, 8,  12,
                                             10, 20, 3, 3, 8, 1, 50, 50, 10, 75, 5};
        for (std::size_t i = 0; i < cosmic.size(); ++i)
            rules["globalRules"][std::string(cosmic[i])] = jt::Json(values[i]);
        constexpr std::array<std::string_view, 10> names{
            "advances", "improvements", "units",  "terrain",      "governments",
            "tribes",   "commodities",  "orders", "difficulties", "diplomaticAttitudes"};
        constexpr std::array<int, 10> counts{100, 67, 62, 33, 7, 21, 16, 12, 6, 9};
        constexpr std::array<std::string_view, 10> prototype{
            R"({"name":"Example","aiValue":3,"civilizedAiModifier":1,"prerequisites":[{"special":"none"},{"special":"none"}],"epoch":"ancient","category":"academic"})",
            R"({"name":"Example","costRows":10,"upkeep":1,"prerequisite":{"special":"none"}})",
            R"({"name":"Example","obsoleteBy":{"special":"never"},"domain":"land","movementPoints":2,"range":0,"attack":1,"defense":1,"hitPoints":30,"firepower":1,"costRows":10,"capacity":0,"role":"attack","prerequisite":{"special":"none"},"abilities":[]})",
            R"({"name":"Example","movementCost":1,"defenseMultiplier":{"numerator":3,"denominator":2},"food":1,"shields":1,"trade":0})",
            R"({"name":"Example","maleTitle":"Leader","femaleTitle":"Leader"})",
            R"({"pluralName":"Example","adjective":"Example","leaderNames":{"male":"Leader","female":"Leader"},"preferredLeaderGender":"female","preferredColor":1,"cityStyle":0,"personality":{"aggression":0,"expansion":0,"civilizationPreference":0},"governmentTitles":[]})",
            R"({"name":"Example"})",
            R"({"name":"Example","shortcutHint":"x"})",
            R"({"name":"Example"})",
            R"({"name":"Example"})"};
        for (std::size_t kind = 0; kind < names.size(); ++kind) {
            const std::string collection(names[kind]);
            rules[collection].setArray();
            compatibility["ruleSlots"][collection].setArray();
            for (int slot = 0; slot < counts[kind]; ++slot) {
                auto row = json(prototype[kind]);
                const auto id = (kind == 9 ? std::string("attitudes") : collection) + ".authored." +
                                std::to_string(slot);
                row["id"] = jt::Json(id);
                row[kind == 5 ? "pluralName" : "name"] =
                    jt::Json(collection + " " + std::to_string(slot));
                if (kind == 3 && slot < 11) {
                    row["irrigation"] = json(
                        R"({"result":{"special":"improve-in-place"},"yieldBonus":1,"workTurns":3,"aiGovernmentThreshold":2})");
                    row["mining"] = json(
                        R"({"result":{"special":"no-result"},"yieldBonus":0,"workTurns":4,"aiGovernmentThreshold":1})");
                    row["transformation"] = json(R"({"terrain":"terrain.authored.0"})");
                }
                rules[collection].getArray().push_back(std::move(row));
                jt::Json mapping;
                mapping["id"] = jt::Json(id);
                mapping["slot"] = jt::Json(slot + (kind == 7 ? 1 : 0));
                compatibility["ruleSlots"][collection].getArray().push_back(std::move(mapping));
            }
        }
        for (int slot = 39; slot <= 66; ++slot) {
            auto row = json(R"({"advance":{"special":"none"}})");
            row["wonder"] = jt::Json("improvements.authored." + std::to_string(slot));
            rules["wonderObsolescence"].getArray().push_back(std::move(row));
        }
    }
    ResolvedPack pack() const {
        ResolvedPack out;
        out.components["rules"] = {nullptr, parse_json(rules.toString(), "rules.json")};
        out.compatibility =
            ResolvedDocument{nullptr, parse_json(compatibility.toString(), "compatibility.json")};
        out.gameplay.bundle = gameplay;
        return out;
    }
    std::shared_ptr<const RuleContent> load() const { return load_rules(pack()); }
};
} // namespace rule_fixture
