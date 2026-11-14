// Independently authored read-only comparison driver. Loads supplied original
// data through the portable P04/F03 implementation, then compares F06 labels.
// No original execution, sign-marker transformation or session loading.
#include "base/hash.hpp"
#include "formats/classic_labels.hpp"
#include "formats/classic_rules.hpp"
#include "formats/classic_save.hpp"
#include "platform/native_files.hpp"
#include <charconv>
#include <iostream>
#include <json.h>
#include <tuple>

int main(int argc, char** argv) {
    using namespace openciv;
    namespace classic = formats::classic;
    try {
        if (argc != 5)
            throw std::invalid_argument("Usage: oc2_rule_labels LABELS RULES SAVE TRIBE");
        unsigned tribe = 21;
        const std::string_view input = argv[4];
        const auto parsed = std::from_chars(input.data(), input.data() + input.size(), tribe);
        if (parsed.ec != std::errc{} || parsed.ptr != input.data() + input.size() || tribe >= 21)
            throw std::invalid_argument("Tribe must be a decimal slot 0..20");
        const auto label_bytes = io::read_file(argv[1]);
        const auto rule_bytes = io::read_file(argv[2]);
        const auto save_bytes = io::read_file(argv[3]);
        const auto saved = formats::decode_stored_save(save_bytes);
        if (saved.leaders.size() != 1)
            throw std::invalid_argument("This comparison requires a single saved leader");
        classic::TextWorkspace workspace;
        classic::TextDirectories directories{"supplied", "supplied", "supplied"};
        classic::TextFiles files{
            [&](std::string_view, std::string_view path) -> classic::OpenedText {
                const auto* bytes = path == "LABELS.TXT"  ? &label_bytes
                                    : path == "RULES.TXT" ? &rule_bytes
                                                          : nullptr;
                if (!bytes)
                    return {};
                return {std::string(path), std::make_unique<classic::MemoryTextStream>(
                                               std::string(bytes->begin(), bytes->end()))};
            },
            {}};
        auto rules = std::make_unique<state::RuleState>();
        classic::TextReader reader(workspace, directories, files, classic::ascii_text_case());
        classic::LabelLoader(reader, directories, rules->text, rules->labels).rebuild();
        const auto prefix = rules->text.size();
        classic::RuleLoader(reader, *rules, classic::ascii_text_case()).load("RULES", true);
        jt::Json report;
        report["format"] = jt::Json("openciv2-rule-label-comparison/1");
        report["scope"] = jt::Json("P04 label rebuild, F03 rules, F06 saved label magnitudes");
        report["textSearch"] = jt::Json("uncached supplied files; ASCII syntax comparison");
        for (const auto& [key, bytes, path] : {std::tuple{"labels", &label_bytes, argv[1]},
                                               std::tuple{"rules", &rule_bytes, argv[2]},
                                               std::tuple{"save", &save_bytes, argv[3]}}) {
            report["inputs"][key]["path"] = jt::Json(path);
            report["inputs"][key]["sha256"] = jt::Json(base::sha256(*bytes));
        }
        report["tribe"] = jt::Json(tribe);
        report["declaredLabels"] = jt::Json(rules->labels.declared_count);
        report["poolCapacity"] = jt::Json(rules->labels.pool_capacity);
        report["preRuleStrings"] = jt::Json(static_cast<std::uint64_t>(prefix));
        report["postRuleStrings"] = jt::Json(static_cast<std::uint64_t>(rules->text.size()));
        report["comparisons"].setArray();
        bool matched = true;
        const auto& actual = rules->leaders[tribe];
        const auto& expected = saved.leaders[0];
        auto compare = [&](std::string field, std::int16_t loaded, std::int16_t stored) {
            const bool equal = state::label_magnitude(loaded) == state::label_magnitude(stored);
            jt::Json row;
            row["field"] = jt::Json(field);
            row["loaded"] = jt::Json(loaded);
            row["stored"] = jt::Json(stored);
            row["magnitudeMatches"] = jt::Json(equal);
            report["comparisons"].getArray().push_back(std::move(row));
            matched = matched && equal;
        };
        for (std::size_t sex = 0; sex < 2; ++sex)
            compare("names[" + std::to_string(sex) + "]", actual.names[sex], expected.names[sex]);
        compare("plural", actual.plural, expected.plural);
        compare("adjective", actual.adjective, expected.adjective);
        for (std::size_t g = 0; g < 7; ++g)
            for (std::size_t sex = 0; sex < 2; ++sex)
                compare("titles[" + std::to_string(g) + "][" + std::to_string(sex) + "]",
                        actual.titles[g][sex], expected.titles[g][sex]);
        report["status"] = jt::Json(matched ? "matched" : "mismatched");
        std::cout << report.toString() << '\n';
        return matched ? 0 : 1;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 2;
    }
}
