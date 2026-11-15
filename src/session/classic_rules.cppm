export module openciv.session.classic_rules;

import std;
export import openciv.base.callback_ref;
export import openciv.session.classic_load;
export import openciv.formats.classic_labels;
export import openciv.formats.classic_rules;

export namespace openciv::session {
// Borrowed providers and callable owners must outlive the consuming operation.
struct RuleContextDependencies {
    base::CallbackRef<bool(std::string_view, std::string_view)> exists;
    const formats::classic::TextCase* comparison = nullptr;
    formats::classic::MissingLabels labels;
    formats::classic::RuleFailureObserver rules;
};
// Classic content source. Borrows file selection/workspace, owns its reader.
// Providers, policies and callback owners outlive this source.
// Each load returns independent data and closes every file before returning.
class ClassicRuleContext {
public:
    ClassicRuleContext(ClassicFileState& files, formats::classic::TextWorkspace& workspace,
                       formats::classic::TextFiles text_files, RuleContextDependencies services);
    ClassicRuleContext(const ClassicRuleContext&) = delete;
    ClassicRuleContext& operator=(const ClassicRuleContext&) = delete;
    state::LoadedContent load();

private:
    ClassicFileState& files_;
    RuleContextDependencies services_;
    formats::classic::TextReader reader_;
};
} // namespace openciv::session

module :private;
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
