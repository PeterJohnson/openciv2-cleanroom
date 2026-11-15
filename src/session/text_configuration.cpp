#include "session/text_configuration.hpp"
#include "base/arithmetic.hpp"

namespace openciv::session {
void initialize_text(std::int32_t language, formats::classic::TextDirectories& directories,
                     state::TextPool& pool, state::Labels& labels,
                     const std::function<void()>& build_game_cache) {
    if (!build_game_cache)
        throw std::invalid_argument("Text initialization requires a GAME-cache service");
    const auto cache = build_game_cache;
    labels.status_language =
        static_cast<std::int32_t>(base::WrappingI32{language} - base::WrappingI32{1});
    if (labels.status_language > 2)
        labels.status_language = 0;
    static constexpr std::array<std::string_view, 5> extensions{"TXT", "FRE", "GER", "ITA", "SPA"};
    directories.extension =
        language >= 1 && language <= 5 ? extensions[static_cast<std::size_t>(language - 1)] : "TXT";
    cache();
    pool.initialize();
}
} // namespace openciv::session
