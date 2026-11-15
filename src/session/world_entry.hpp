#pragma once
#include "base/callback_ref.hpp"

#include "session/new_game.hpp"
#include "session/player_setup.hpp"
#include "session/world_setup.hpp"

namespace openciv::session {
enum class NewWorldChoice { random, premade, customize };
// Borrowed providers and callable owners must outlive the consuming operation.
struct WorldEntryDependencies {
    // Selected source closes its readers and returns the same owned inputs used
    // for load/resume. Installation and final name-preserving updates live here.
    base::CallbackRef<state::LoadedContent()> load_content;
    const WorldSizeInteraction* size = nullptr;
    base::CallbackRef<std::optional<std::string>()> choose_map;
    PremadeWorldDependencies premade;
    // parameters.preferences serves both parameter-completion and final saves.
    WorldParameterDependencies parameters;
    const PlayerSetupInteraction* players = nullptr;
    base::CallbackRef<void()> stop_menu_sound;
    // Required W02: false generates terrain; true retains/finalizes it. Reads
    // current world_setup, map, rules and starts; no allocated-map substitute.
    base::CallbackRef<void(bool keep)> generate_world;
    NewGameDependencies initialize;
    // Required F05 sections 9-10, including any retained negative selection.
    base::CallbackRef<void(std::int32_t selection)> accelerate;
    // Required P03/P04 presentation resolves current leader/civilization names.
    // Advance text is owned classic bytes with ", " after every listed name.
    base::CallbackRef<void(state::CivilizationSlot, std::string advances)> introduce;
    void validate(NewWorldChoice choice) const;
};
// F05 section 2 / V03 section 2: highest at most twelve known advances,
// displayed ascending, excluding zero and both-No definitions. The extra
// identifier-100 probe cannot contribute under V03, so no unbacked read is made.
// Does not grant advances, alter state, update the year or draw randomness.
std::string starting_advance_list(const state::GameState& world, const state::RuleState& rules,
                                  std::int32_t civilization);
// F05 section 2, after a new-world menu choice and before application admission.
// Validates selected-branch services before effects, then prepares an isolated
// new game, revoking old clients and discarding all
// previous world/rule/file scratch. Composes size/map, parameter, player and initialization
// operations. False returns with current-attempt effects retained. A failed
// size/map selection does not consume discarded parameter draws.
// Required child exceptions publish and escape without rollback. The caller
// supplies a unique call identity; child paths append /parameters, /players and
// /initialize. Every retry starts fresh; within-attempt accepted effects remain
// until the next attempt. Successful setup does not itself admit client leases.
bool enter_new_world(SessionRuntime& runtime, ClassicFileState& files, base::GameplayRandom& random,
                     std::string_view call, NewWorldChoice choice,
                     const state::CalendarValues& calendar, const WorldEntryDependencies& services);
} // namespace openciv::session
