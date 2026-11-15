#pragma once
#include "base/callback_ref.hpp"

#include "session/classic_load.hpp"
#include "formats/classic_labels.hpp"
#include "formats/classic_rules.hpp"

namespace openciv::session {
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
