#include "session/classic_rules.hpp"

namespace openciv::session {
namespace classic = formats::classic;
ClassicRuleContext::ClassicRuleContext(ClassicFileState& files, classic::TextWorkspace& workspace,
                                       classic::TextFiles text_files,
                                       RuleContextDependencies services)
    : files_(files), services_(services),
      reader_(workspace, files.text, std::move(text_files),
              services_.comparison
                  ? *services_.comparison
                  : throw std::invalid_argument("Content comparison policy is required")) {
    if (!services_.exists)
        throw std::invalid_argument("Content source requires resource lookup");
}
state::LoadedContent ClassicRuleContext::load() {
    struct Cleanup {
        classic::TextReader& reader;
        ~Cleanup() {
            try {
                reader.close();
            } catch (...) {
            }
        }
    } cleanup{reader_};
    state::LoadedContent result;
    result.labels = classic::load_labels(reader_, files_.text, services_.labels);
    const auto resource = "RULES." + files_.text.extension;
    const auto scenario = files_.scenario_directory;
    const auto previous = files_.text.search;
    struct Search {
        std::string& target;
        std::string prior;
        ~Search() { target.swap(prior); }
    } search{files_.text.search, previous};
    files_.text.search = services_.exists(scenario, resource) ? scenario : files_.text.home;
    result.rules = std::make_unique<state::RuleInput>();
    classic::RuleLoader(reader_, *result.rules, *services_.comparison, services_.rules)
        .load("RULES");
    reader_.close();
    return result;
}
} // namespace openciv::session
