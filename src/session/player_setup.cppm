export module openciv.session.player_setup;

import std;
export import openciv.session.player_setup_types;
export import openciv.session.runtime;
export import openciv.session.world_setup_types;
export import openciv.state.gameplay;

export namespace openciv::session {
// F05 section 4, ending after accepted city style and opponent overrides.
// Ordered, nontransactional writes; entry locals survive retries. Does not
// save files, generate a world, create civilizations or apply acceleration.
// Required services execute at explicit published boundaries. The caller owns
// scheduling/modal presentation and supplies the selected validated calendar.
// Sampling keys use three F05.setup.* sites, call/<per-site draw ordinal>, and
// attempt zero; ordinals persist across all retries in this invocation.
PlayerSetupResult select_players(SessionRuntime& runtime, base::GameplayRandom& random,
                                 std::string_view call, WorldSource source,
                                 const state::CalendarValues& calendar,
                                 const PlayerSetupInteraction& services);
} // namespace openciv::session
