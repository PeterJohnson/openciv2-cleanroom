export module openciv.session.text_configuration;

import std;
export import openciv.formats.classic_text_reader;
export import openciv.state.labels;

export extern "C++" {
    namespace openciv::session {
    // Process language/cache setup is independent of parsing any label source.
    void initialize_text(std::int32_t language, formats::classic::TextDirectories& directories,
                         state::TextPool& pool, state::Labels& labels,
                         const std::function<void()>& build_game_cache);
    } // namespace openciv::session
}
