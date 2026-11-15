export module openciv.state.world_setup;

import std;
export import openciv.state.choices;

export namespace openciv::state {
// F05 sections 2-3. Order matches the F06 CIV2.DAT preference fields, not
// the customized-dialog order (which visits climate before temperature).

/// Retained world-generation choices and derived setup dimensions.
struct WorldSetup {
    // Retained working dimensions, separate from currently allocated map
    // storage. The size helper consumes width as U and leaves s16(2*U), even
    // on returned cancellation. Entry does not implicitly halve/reset it.
    std::int16_t width = 0, height = 0;
    std::array<std::int32_t, 5> parameters{};
    bool operator==(const WorldSetup&) const = default;
};
} // namespace openciv::state
