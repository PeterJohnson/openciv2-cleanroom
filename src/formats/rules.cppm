export module openciv.formats.rules;

import std;
export import openciv.formats.content_pack;
export import openciv.state.rule_input;

export namespace openciv::formats::content {
struct RuleContent {
    state::RuleInput input;
    std::shared_ptr<const ResolvedFile> rules_source, compatibility_source;
};
// Complete decoded native rule inputs and rule-related compatibility mappings.
// Requires resolved bytes; does not install/reload tables or reset retained state.
// Text lookup/scenario metadata in compatibility still needs its owning validator.
std::shared_ptr<const RuleContent> load_rules(const ResolvedPack& pack);
} // namespace openciv::formats::content
