export module openciv.state.starting_positions;

import std;

export namespace openciv::state {
struct StartingPositions {
    std::array<std::int16_t, 21> x, y;
    StartingPositions() {
        x.fill(-1);
        y.fill(-1);
    }
    bool operator==(const StartingPositions&) const = default;
};

} // namespace openciv::state
