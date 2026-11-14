#pragma once

#include "session/classic_load.hpp"
#include "formats/classic_labels.hpp"
#include "formats/classic_rules.hpp"

namespace openciv::session {
struct RuleContextServices {
    std::function<void()> reload_sprites, flush;
    std::function<void(std::string_view)> change_directory;
    std::function<bool(std::string_view, std::string_view)> exists;
    formats::classic::TextCase comparison;
    formats::classic::LabelServices labels;
    formats::classic::RuleLoadServices rules;
};
// F03 section 1 reload wrapper and F06 section 6 final name-preserving reloads.
// Retains the reader/workspace across calls. File context and rule state must
// outlive this object; all callbacks are synchronous on the owning lane.
class ClassicRuleContext {
public:
    ClassicRuleContext(state::RuleState& rules, ClassicFileState& files,
                       formats::classic::TextWorkspace& workspace,
                       formats::classic::TextFiles text_files, RuleContextServices services);
    void load(bool replace_leader_data);
    void reload(LoadedRuleTable table, bool keep_names);
    void close();
    bool is_open() const noexcept { return reader_.is_open(); }

private:
    state::RuleState& rules_;
    ClassicFileState& files_;
    RuleContextServices services_;
    formats::classic::TextReader reader_;
};
} // namespace openciv::session
