#pragma once

#include "session/classic_load.hpp"
#include "formats/classic_labels.hpp"
#include "formats/classic_rules.hpp"

namespace openciv::session {
struct RuleContextServices {
    std::function<bool(std::string_view, std::string_view)> exists;
    formats::classic::TextCase comparison;
    formats::classic::LabelServices labels;
    formats::classic::RuleLoadServices rules;
};
// Classic content source. Borrows file selection/workspace, owns its reader.
// Each load returns independent data and closes every file before returning.
class ClassicRuleContext {
public:
    ClassicRuleContext(ClassicFileState& files, formats::classic::TextWorkspace& workspace,
                       formats::classic::TextFiles text_files, RuleContextServices services);
    ClassicRuleContext(const ClassicRuleContext&) = delete;
    ClassicRuleContext& operator=(const ClassicRuleContext&) = delete;
    state::LoadedContent load();

private:
    ClassicFileState& files_;
    RuleContextServices services_;
    formats::classic::TextReader reader_;
};
} // namespace openciv::session
