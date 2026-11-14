#pragma once

#include "state/civilization.hpp"

namespace openciv::civilization {
// V03 section 2 / Appendix A identity mapping. Branches precede civilization
// access; Future Technology remains false even if its stored membership is set.
inline constexpr std::int32_t future_technology = 89;
bool knows_advance(std::span<const state::Civilization> civilizations, std::int32_t civilization,
                   std::int32_t advance);
} // namespace openciv::civilization
