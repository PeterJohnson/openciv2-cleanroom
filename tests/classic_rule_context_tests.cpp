#include "session/classic_rules.hpp"
#include "rules/rules_install.hpp"
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
        services.exists = [&](std::string_view dir, std::string_view name) {
            REQUIRE(dir == "scenario");
            REQUIRE(name == "RULES.TXT");
            trace.push_back("probe");
            return scenario;
        };
        context = std::make_unique<ClassicRuleContext>(files, workspace, text, services);
    }
};
} // namespace
TEST_CASE("F03 classic source returns owned rules and labels without installing or retaining files",
          "[session][rule-context]") {
    for (const bool custom : {false, true}) {
        Fixture f;
        f.scenario = custom;
        auto input = f.context->load();
        REQUIRE(input.rules);
        REQUIRE(input.labels);
        CHECK(f.rules->definitions.cosmic.values[0] == 0);
        CHECK(f.rules->text.size() == 0);
        CHECK(f.files.text.search == "old");
        CHECK(f.files.text.working == "home");
        f.context.reset();
        state::install_labels(*input.labels, f.rules->text, f.rules->labels);
        rules::RuleInstaller installer(*input.rules, *f.rules);
        installer.load(true);
        CHECK(f.rules->definitions.cosmic.values[0] == 3);
        CHECK(f.rules->labels.label(0, f.rules->text) == "Label one");
        const auto names = f.rules->names;
        const auto count = f.rules->text.size();
        installer.advances(true);
        installer.improvements(true);
        installer.units(true);
        CHECK(f.rules->names == names);
        CHECK(f.rules->text.size() == count);
    }
}
TEST_CASE("F03 failed source parsing restores selection and leaves installation untouched",
          "[session][rule-context]") {
    Fixture f;
    f.body = "@COSMIC\n9\n@CIVILIZE\nInvalid,3,1,unknown,nil,0,3\n";
    CHECK_THROWS(f.context->load());
    CHECK(f.files.text.search == "old");
    CHECK(f.rules->labels.entries.empty());
    CHECK(f.rules->definitions.cosmic.values[0] == 0);
}
