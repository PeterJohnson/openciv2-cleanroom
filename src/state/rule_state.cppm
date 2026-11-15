export module openciv.state.rule_state;

import std;
export import openciv.state.civilization;
export import openciv.state.labels;
export import openciv.state.rules;
export import openciv.state.text_pool;

export namespace openciv::state {
struct RuleNameBindings {
    std::array<TextId, 100> advances{};
    std::array<TextId, 67> improvements{};
    std::array<TextId, 62> units{};
    std::array<TextId, 33> terrain{};
    std::array<TextId, 7> governments{};
    std::array<std::array<TextId, 2>, 7> government_titles{};
    std::array<TextId, 16> commodities{};
    std::array<TextId, 13> orders{}; // External slot zero is retained.
    std::array<TextId, 6> difficulties{};
    std::array<TextId, 9> attitudes{};
    bool operator==(const RuleNameBindings&) const = default;
};
// F02/F03 logical rule state. Text references are implementation-local; custom
// name status is explicit. Original pool traces belong to comparison tooling,
// and signed name markers belong to the F06 codec. Initialize text at setup.
struct RuleState {
    RuleDefinitions definitions;
    TextPool text;
    Labels labels;
    RuleNameBindings names;
    std::array<Leader, 21> leaders{};
    bool double_movement_applied = false;
};
} // namespace openciv::state
