#pragma once
#include "state/civilization.hpp"
#include "state/rule_state.hpp"
#include <string>
#include <string_view>

namespace openciv::city {
enum class NameOutcome { unavailable, exhausted, selected };
struct NameAttempt {
    NameOutcome outcome = NameOutcome::unavailable;
    std::string bytes;
};
struct NameProvider {
    // One attempt, after incrementing the live leader's counter. Implements
    // ordered list/fallback selection, including remaining=0 header behavior.
    std::function<NameAttempt(std::string_view plural, std::uint8_t remaining)> attempt;
    std::function<void()> close;
};
// C01 "Default city naming". The resolver receives the magnitude of the live
// signed plural label, including 32768 for -32768. No pool layout is assumed.
// Captures owner and section text once; tribe/counter reads remain live across
// provider calls. Publishes at most 15 classic bytes and a cleared tail.
bool default_name(state::City& city, std::span<const state::Civilization> civilizations,
                  std::span<state::Leader> leaders,
                  const std::function<std::string(std::int32_t)>& resolve_plural,
                  const NameProvider& provider);
// Process-owned rule/leader/pool binding of the same operation.
bool default_name(state::City& city, std::span<const state::Civilization> civilizations,
                  state::RuleState& rules, const NameProvider& provider);
} // namespace openciv::city
