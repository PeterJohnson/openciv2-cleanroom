export module openciv.rules.rules_install;

import std;
export import openciv.state.rule_input;
export import openciv.state.rule_state;

export extern "C++" {
    namespace openciv::rules {
    // data-format sections 1/4 and Appendix B; F03 sections 3-7/Appendix A.
    // Apply admitted format-independent inputs through the retained rule model.
    // Both references must outlive this installer.
    // The caller owns complete pack admission and permission to invoke an owning
    // reload/setup operation. This neither admits a session nor opens virtual files.
    class RuleInstaller {
    public:
        RuleInstaller(const state::RuleInput& input, state::RuleState& retained);
        RuleInstaller(const RuleInstaller&) = delete;
        RuleInstaller& operator=(const RuleInstaller&) = delete;
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
        void text_into(state::RuleText& target, state::TextId& binding,
                       const state::RuleText& value, bool blank_as_space);
        void name_into(state::RuleName& target, state::TextId& binding,
                       const state::RuleName& value, bool blank_as_space, bool keep = false);
        void leader_name(state::RuleText& target, state::NameReference& binding,
                         const state::RuleText& value, bool blank_as_space, bool replace);
        const state::RuleInput& input_;
        state::RuleState& retained_;
    };
    } // namespace openciv::rules
}
