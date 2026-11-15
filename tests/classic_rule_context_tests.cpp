// Authored F03 section 1 wrapper / F06 section 6 final-table expectations.
#include "session/classic_rules.hpp"
#include "classic_rule_fixture.hpp"
#include <catch2/catch_test_macros.hpp>

using namespace openciv;
using namespace openciv::session;
namespace classic = formats::classic;
namespace {
struct Fixture {
    std::unique_ptr<state::RuleState> rules = std::make_unique<state::RuleState>();
    ClassicFileState files;
    classic::TextWorkspace workspace;
    std::string body;
    std::vector<std::string> trace;
    std::function<void()> sprites_hook;
    bool scenario = true;
    std::unique_ptr<ClassicRuleContext> context;
    Fixture() {
        files.text = {"home", "old", "home", "TXT"};
        files.scenario_directory = "scenario";
        for (const auto& section : classic_rule_fixture::complete_sections())
            body += section;
        classic::TextFiles text{
            [&](std::string_view dir, std::string_view name) -> classic::OpenedText {
                trace.push_back("open:" + std::string(dir) + "/" + std::string(name));
                if (name == "LABELS.TXT")
                    return {std::string(dir) + "/LABELS",
                            std::make_unique<classic::MemoryTextStream>(
                                "@STRINGHEAP\n1000\n@LABELS\n2\nLabel one\nLabel "
                                "two\n@POPUPS\nP1\nP2\nP3\n")};
                return {std::string(dir) + "/RULES",
                        std::make_unique<classic::MemoryTextStream>(body)};
            },
            {}};
        RuleContextServices services;
        services.comparison = classic::ascii_text_case();
        services.reload_sprites = [&] {
            REQUIRE(rules->labels.label(0, rules->text) == "Label one");
            REQUIRE(rules->labels.popup(2, rules->text) == "P3");
            REQUIRE(files.text.search == "old");
            trace.push_back("sprites");
            if (sprites_hook)
                sprites_hook();
        };
        services.exists = [&](std::string_view dir, std::string_view name) {
            REQUIRE(dir == "scenario");
            REQUIRE(name == "RULES.TXT");
            trace.push_back("probe");
            return scenario;
        };
        services.change_directory = [&](std::string_view dir) {
            REQUIRE(dir == files.text.working);
            trace.push_back("cd:" + std::string(dir));
        };
        services.flush = [&] { trace.push_back("flush"); };
        context = std::make_unique<ClassicRuleContext>(*rules, files, workspace, text, services);
    }
};
} // namespace
TEST_CASE("F03 reload rebuilds labels before sprites and selects rules before returning home",
          "[session][rule-context]") {
    for (const bool custom : {false, true}) {
        Fixture f;
        f.scenario = custom;
        f.context->load(true);
        const auto selected = custom ? "scenario" : "home";
        REQUIRE(f.files.text.search == selected);
        REQUIRE(f.files.text.working == "home");
        REQUIRE(f.rules->definitions.cosmic.values[0] == 3);
        REQUIRE(f.rules->text.text(f.rules->names.advances[0]) == "advances 0");
        REQUIRE_FALSE(f.context->is_open());
        REQUIRE(f.trace == std::vector<std::string>{
                               "open:old/LABELS.TXT", "open:home/LABELS.TXT", "open:old/LABELS.TXT",
                               "sprites", "probe", "cd:" + std::string(selected),
                               "open:" + std::string(selected) + "/RULES.TXT",
                               "open:" + std::string(selected) + "/RULES.TXT",
                               "open:" + std::string(selected) + "/RULES.TXT", "cd:home", "flush"});
    }
}
TEST_CASE("F03 wrapper failure retains labels and selected directory without installing rules",
          "[session][rule-context]") {
    Fixture f;
    f.body = classic_rule_fixture::complete_sections()[0];
    f.body[8] = '9';
    f.body += "@CIVILIZE\nInvalid,3,1,unknown,nil,0,3\n";
    REQUIRE_THROWS_AS(f.context->load(true), classic::RuleLoadError);
    REQUIRE(f.files.text.working == "scenario");
    REQUIRE(f.files.text.search == "scenario");
    REQUIRE(f.rules->definitions.cosmic.values[0] == 0);
    REQUIRE(f.rules->labels.declared_count == 2);
    REQUIRE(std::find(f.trace.begin(), f.trace.end(), "flush") == f.trace.end());
    REQUIRE(std::find(f.trace.begin(), f.trace.end(), "cd:home") == f.trace.end());
    f.context->close();
    REQUIRE_FALSE(f.context->is_open());
}
TEST_CASE("F03 resource failure follows label publication and precedes directory selection",
          "[session][rule-context]") {
    Fixture f;
    f.rules->definitions.cosmic.values[0] = 41;
    f.sprites_hook = [] { throw std::runtime_error("Resource failed"); };
    REQUIRE_THROWS_AS(f.context->load(true), std::runtime_error);
    REQUIRE(f.rules->text.size() == 7);
    REQUIRE(f.rules->definitions.cosmic.values[0] == 41);
    REQUIRE(f.files.text.search == "old");
    REQUIRE(f.files.text.working == "home");
    REQUIRE(f.trace.back() == "sprites");
    REQUIRE(std::find(f.trace.begin(), f.trace.end(), "probe") == f.trace.end());
}
TEST_CASE("F06 final rule context reloads keep names while applying new numeric inputs",
          "[session][rule-context]") {
    Fixture f;
    f.context->load(true);
    const auto names = f.rules->names;
    const auto count = f.rules->text.size();
    auto sections = classic_rule_fixture::complete_sections();
    sections[0] = "@COSMIC\n99\n"; // Final reloads must not reload cosmic values.
    sections[1].replace(sections[1].find("advances 0,3,1"), 14, "Renamed,5,1");
    f.body.clear();
    for (const auto& section : sections)
        f.body += section;
    f.context->reload(LoadedRuleTable::advances, true);
    f.context->reload(LoadedRuleTable::improvements, true);
    f.context->reload(LoadedRuleTable::units, true);
    REQUIRE(f.context->is_open());
    REQUIRE(f.rules->names == names);
    REQUIRE(f.rules->text.size() == count);
    REQUIRE(f.rules->definitions.cosmic.values[0] == 3);
    REQUIRE(f.rules->definitions.advances[0].ai_value == 5);
    f.context->close();
    REQUIRE_FALSE(f.context->is_open());
}
