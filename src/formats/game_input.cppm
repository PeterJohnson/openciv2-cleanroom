export module openciv.formats.game_input;

import std;
export import openciv.formats.classic_save;
export import openciv.state.game_input;

export extern "C++" {
    namespace openciv::formats {
    // Convert admitted stored representations to common restoration operands.
    state::GameInput game_input(StoredSave save);
    world::MapInput map_input(RawMapFile map);
    } // namespace openciv::formats
}
