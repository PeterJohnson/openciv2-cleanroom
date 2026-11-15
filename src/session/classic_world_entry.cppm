export module openciv.session.classic_world_entry;

import std;
export import openciv.session.classic_load;
export import openciv.session.world_entry;
export namespace openciv::session {
// Composition adapter: reset classic scratch after runtime isolation and before
// loading content. Common world entry never depends on classic paths or codecs.
bool enter_classic_new_world(SessionRuntime& runtime, ClassicFileState& files,
                             base::GameplayRandom& random, std::string_view call,
                             NewWorldChoice choice, const state::CalendarValues& calendar,
                             WorldEntryDependencies services);
} // namespace openciv::session

module :private;
namespace openciv::session {
bool enter_classic_new_world(SessionRuntime& runtime, ClassicFileState& files,
                             base::GameplayRandom& random, std::string_view call,
                             NewWorldChoice choice, const state::CalendarValues& calendar,
                             WorldEntryDependencies services) {
    const auto reset = [&] {
        auto fresh = ClassicFileState{};
        fresh.text = files.text;
        fresh.text.working = fresh.text.search = fresh.text.home;
        fresh.current_screen = files.current_screen;
        fresh.default_game_stem = files.default_game_stem;
        fresh.game_stem = fresh.default_game_stem;
        files = std::move(fresh);
    };
    services.reset_source = reset;
    return enter_new_world(runtime, files.starts, random, call, choice, calendar, services);
}
} // namespace openciv::session
