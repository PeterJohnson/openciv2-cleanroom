#pragma once
#include "formats/classic_save.hpp"
#include "state/game_input.hpp"

namespace openciv::formats {
// Convert admitted stored representations to common restoration operands.
state::GameInput game_input(StoredSave save);
world::MapInput map_input(RawMapFile map);
} // namespace openciv::formats
