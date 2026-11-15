export module openciv.state.starting_positions;

import std;

export namespace openciv::state {
/// The 21 classic authored civilization starting-coordinate pairs.
struct StartingPositions {
    std::array<std::int16_t, 21> x, y;
    /// Mark every starting coordinate unspecified using the -1 sentinel.
    StartingPositions() {
        x.fill(-1);
        y.fill(-1);
    }
    bool operator==(const StartingPositions&) const = default;
};

} // namespace openciv::state
