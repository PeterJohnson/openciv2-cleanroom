export module openciv.rules.rules_install;

import std;
export import openciv.state.rule_input;
export import openciv.state.rule_state;

export namespace openciv::rules {
/// data-format sections 1/4 and Appendix B; F03 sections 3-7/Appendix A.
/// Apply admitted format-independent inputs through the retained rule model.
/// Both references must outlive this installer.
/// The caller owns complete pack admission and permission to invoke an owning
/// reload/setup operation. This neither admits a session nor opens virtual files.
class RuleInstaller {
public:
    /// Borrow validated rule input and the mutable rule/text state to receive it.
    RuleInstaller(const state::RuleInput& input, state::RuleState& retained);
    RuleInstaller(const RuleInstaller&) = delete;
    RuleInstaller& operator=(const RuleInstaller&) = delete;
    /// Same table order and leader policy as the full classic loader. Names are
    /// replaced; individual advance/improvement/unit methods also support keep.
    void load(bool replace_leader_data);
    /// Install normalized cosmic values.
    void cosmic();
    /// Install advances, optionally preserving their current names.
    void advances(bool keep_names = false);
    /// Install improvements, optionally preserving their current names.
    void improvements(bool keep_names = false);
    /// Install wonder-expiry advance references.
    void wonder_expiry();
    /// Install unit types, optionally preserving their current names.
    void units(bool keep_names = false);
    /// Install terrain definitions and their text bindings.
    void terrain();
    /// Install government names and titles.
    void governments();
    /// Install leader definitions using the requested name-replacement policy.
    void leaders(bool replace);
    /// Install commodity names and values.
    void commodities();
    /// Install unit-order text.
    void orders();
    /// Install difficulty labels.
    void difficulties();
    /// Install attitude labels.
    void attitudes();

private:
    /// Replace text and its pool binding using the selected blank-name policy.
    void text_into(state::RuleText& target, state::TextId& binding, const state::RuleText& value,
                   bool blank_as_space);
    /// Install a rule name and binding, optionally preserving the existing name.
    void name_into(state::RuleName& target, state::TextId& binding, const state::RuleName& value,
                   bool blank_as_space, bool keep = false);
    /// Install a leader name while respecting custom-name and replacement rules.
    void leader_name(state::RuleText& target, state::NameReference& binding,
                     const state::RuleText& value, bool blank_as_space, bool replace);
    const state::RuleInput& input_;
    state::RuleState& retained_;
};
} // namespace openciv::rules
