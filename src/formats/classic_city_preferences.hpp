#pragma once
#include "classic_text_reader.hpp"
#include "state/city_preferences.hpp"
#include "state/rule_state.hpp"
namespace openciv::formats::classic {
// Decode an update against a snapshot of the supplied names. Installation is
// shared with native content in rules::apply_city_preferences.
state::CityPreferenceUpdate load_city_preferences(TextReader& reader,
                                                  const state::RuleDefinitions& rules,
                                                  const TextCase& comparison);
state::CityPreferenceUpdate load_city_preferences(TextReader& reader, const state::RuleState& rules,
                                                  const TextCase& comparison);
} // namespace openciv::formats::classic
