export module openciv.state.unit_types;

import std;

export namespace openciv::state {
/// A unit-type slot in the selected ruleset.
enum class UnitType : std::uint8_t {};
/// Retained classic order codes, including the no-order sentinel.
enum class UnitOrder : std::int8_t {
    none = -1,
    fortifying = 1,
    fortified = 2,
    sleeping = 3,
    fortress = 4,
    road = 5,
    irrigate = 6,
    mine = 7,
    transform = 8,
    clean = 9,
    airbase = 10,
    go_to = 11,
    ai_idle = 16,
    ai_go_to = 27
};
} // namespace openciv::state
