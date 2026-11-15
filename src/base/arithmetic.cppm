module;
#include "base/arithmetic.hpp"

export module openciv.base.arithmetic;

// Export existing declarations so include-based and import-based clients share
// the same types and compiled implementation during the migration experiment.
export namespace openciv::base {
using ::openciv::base::add32;
using ::openciv::base::clamp_ab;
using ::openciv::base::clamp_c;
using ::openciv::base::divide32;
using ::openciv::base::game_ticks_from_milliseconds;
using ::openciv::base::map_pattern_seed_from_ticks;
using ::openciv::base::multiply32;
using ::openciv::base::primary_modulo_from_code;
using ::openciv::base::remainder32;
using ::openciv::base::secondary_range_from_code;
using ::openciv::base::secondary_unit_from_code;
using ::openciv::base::shift_signed;
using ::openciv::base::signed32;
using ::openciv::base::subtract32;
using ::openciv::base::WrappingI32;
} // namespace openciv::base
