#pragma once
#include "rules.hpp"
#include "state/rule_state.hpp"

namespace openciv::formats::content {
// data-format sections 1/4 and Appendix B; F03 sections 3-7/Appendix A.
// Apply immutable, admitted load_rules output through the retained rule model.
// The caller owns complete pack admission and permission to invoke an owning
// reload/setup operation. This neither admits a session nor opens virtual files.
class RuleInstaller {
public:
    RuleInstaller(std::shared_ptr<const RuleContent> input, state::RuleState& retained);
    // Same table order and leader policy as the full classic loader. Names are
    // replaced; individual advance/improvement/unit methods also support keep.
    void load(bool replace_leader_data);
    void cosmic();
    void advances(bool keep_names = false);
    void improvements(bool keep_names = false);
    void wonder_expiry();
    void units(bool keep_names = false);
    void terrain();
    void governments();
    void leaders(bool replace);
    void commodities();
    void orders();
    void difficulties();
    void attitudes();

private:
    void text_into(state::RuleText& target, state::TextId& binding, const state::RuleText& value,
                   bool blank_as_space);
    void name_into(state::RuleName& target, state::TextId& binding, const state::RuleName& value,
                   bool blank_as_space, bool keep = false);
    void leader_name(state::RuleText& target, state::NameReference& binding,
                     const state::RuleText& value, bool blank_as_space, bool replace);
    std::shared_ptr<const RuleContent> input_;
    state::RuleState& retained_;
};
} // namespace openciv::formats::content
