#pragma once

#include "session/classic_load.hpp"

namespace openciv::session {
enum class WorldSource { generated, premade_cylindrical, premade_flat };
struct PremadeWorldChoices {
    // Zero discards; every nonzero result, including a negative result, keeps.
    std::function<std::int32_t()> use_seed, use_start_locations;
    // F01 game ticks, not milliseconds or a gameplay random draw.
    std::function<std::uint32_t()> ticks;
    void validate() const;
};
// F05 section 3 after a successful F06 read. Normalizes the live map, applies
// the two conditional choices and finally the current stored topology rule.
// Requires complete allocated tile/remembered backing. Does not rebuild regions,
// classify area, clear status flags, allocate, generate terrain or save a file.
WorldSource prepare_premade_world(SessionRuntime& runtime, formats::StartingPositions& starts,
                                  const PremadeWorldChoices& choices);
struct PremadeWorldServices {
    std::function<std::unique_ptr<CoreInput>(std::string_view)> open;
    std::function<void()> load_failure;
    PremadeWorldChoices choices;
    void validate() const;
};
// F05 section 3 / F06 section 4 accepted-file path. nullopt path means cancel;
// nullopt result means cancel or returned input failure (which reports notice).
// The native chooser/directory policy remains a separate interaction boundary.
// Input closes once before installation. Decode/allocation failures retain the
// old map; successful installation publishes setup dimensions before choices. Paths
// are opaque to this parent except that embedded NUL is outside its domain.
std::optional<WorldSource> load_premade_world(SessionRuntime& runtime,
                                              formats::StartingPositions& starts,
                                              std::optional<std::string> selected_path,
                                              const PremadeWorldServices& services);
} // namespace openciv::session
