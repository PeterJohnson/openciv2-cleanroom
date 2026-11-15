#include <cstdint>
#include <limits>
#include <stdexcept>

import openciv.base.arithmetic;

using openciv::base::WrappingI32;
WrappingI32 header_add(WrappingI32 left, WrappingI32 right);

// F01 §§1, 4–5: exercise the existing specification-derived arithmetic cases
// through the module. Test constexpr functions, templates/operators, compiled functions,
// exception identity and interoperability with an ordinary header consumer.
constexpr auto maximum = std::numeric_limits<std::int32_t>::max();
constexpr auto minimum = std::numeric_limits<std::int32_t>::min();
static_assert(openciv::base::add32(maximum, 1) == minimum);
static_assert(static_cast<std::int32_t>(WrappingI32{maximum} + WrappingI32{1}) == minimum);
static_assert(openciv::base::map_pattern_seed_from_ticks(0) == 1);

int main() {
    using namespace openciv::base;
    if (static_cast<std::int32_t>(header_add(WrappingI32{maximum}, WrappingI32{1})) != minimum)
        return 1;
    if (divide32(-7, 3) != -2 || remainder32(-7, 3) != -1)
        return 2;
    if (clamp_ab(5, 10, 0) != 0 || clamp_c(5, 10, 0) != 0)
        return 3;
    if (secondary_unit_from_code(0) != 0.0 ||
        secondary_unit_from_code(std::numeric_limits<std::uint32_t>::max()) != 1.0)
        return 4;
    try {
        (void)divide32(1, 0);
        return 5;
    } catch (const std::domain_error&) {
    }
    return 0;
}
