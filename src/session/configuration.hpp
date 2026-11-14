#pragma once

#include "session/classic_preferences.hpp"

namespace openciv::session {
class SessionRuntime;
// F03 section 8 accepted checkbox values; presentation scratch is separate.
// Game order: sound, music, wait, autosave, enemy moves, skip enemy pauses,
// fast slide, advice, tutorial, mouse goto, Enter closes city.
using GameOptionChoices = std::array<bool, 11>;
// Graphics order: throne, diplomacy, heralds, advance Civilopedia, council,
// wonder movies. Message order: growth halted, improvement built, unit built,
// invalid build, disorder, order restored, celebration, food low, pollution,
// production penalty, zoom-to-city. Checked message choices mean enabled.
using GraphicsOptionChoices = std::array<bool, 6>;
using MessageOptionChoices = std::array<bool, 11>;
struct GameOptionServices {
    PreferenceFileServices preferences;
    // Required P10 owning operations. Reinitialization includes its specified
    // stop/close/open/layout sequence; synchronization is a separate child.
    std::function<void()> stop_music, reinitialize_music, synchronize_music;
};
struct GraphicsOptionServices {
    PreferenceFileServices preferences;
    std::function<bool()> heralds_available;
    std::function<void()> low_memory_notice;
};
// nullopt is cancellation: no changes or service calls. Accepted changes use
// real F03 preference saving and ignore its returned failure without rollback.
// Required child exceptions stop with prior effects retained. No command, native
// widget or audio implementation is fabricated by these application ports.
bool configure_game_options(SessionRuntime& runtime, std::optional<GameOptionChoices> choices,
                            const GameOptionServices& services);
bool configure_graphics_options(SessionRuntime& runtime,
                                std::optional<GraphicsOptionChoices> choices,
                                const GraphicsOptionServices& services);
bool configure_message_options(SessionRuntime& runtime, std::optional<MessageOptionChoices> choices,
                               const PreferenceFileServices& services);

struct OptionVoteServices {
    std::function<void(std::int32_t, std::int32_t)> poll;
    std::function<std::uint32_t()> clock;
    // Required P04 child: set modal completion before ending its loop.
    std::function<void()> finish_modal;
};
// N03 section 11 shared vote callback, including signed wrapping elapsed time,
// live gate order and the fresh completion tick. Does not dispatch N02 replies.
void poll_option_vote(SessionRuntime& runtime, const OptionVoteServices& services);

// Clear chat on new game, clear chat on boot, double production, double movement.
using MultiplayerOptionChoices = std::array<bool, 4>;
struct MultiplayerOptionServices {
    PreferenceFileServices preferences;
    OptionVoteServices vote;
    // N01 owns local player/name mapping; a civilization is not a player slot.
    std::function<std::string()> local_player_name, separator;
    std::function<std::string(bool)> production_label, movement_label;
    // Proposal consumes text_parameters[0/1]. N02 owns the actual wire dispatch.
    std::function<void()> broadcast_proposal, broadcast_rejection, broadcast_acceptance;
    // The retained callback owns its service functions. The installing modal
    // owner must keep runtime alive while that callback can still be delivered.
    std::function<void(std::function<void()>)> install_vote_callback;
    std::function<std::int32_t()> show_vote_dialog;
    std::function<void()> send_units;
    std::function<void(std::int32_t production, std::int32_t movement, std::int32_t simultaneous)>
        send_options;
    std::function<void(std::string_view)> notice;
    std::function<void(std::int32_t)> redraw_status;
};
enum class MultiplayerOptionResult { cancelled, client_only, unchanged, rejected, applied };
// F03/N03 accepted-dialog boundary. Only result exactly zero applies. Chat
// preferences save before host/changed/vote gates; double options do not get a
// second save. Uses real double-movement rules and the shared vote callback.
// Initial dialog construction, N02 transport and P04 modal execution are children.
MultiplayerOptionResult configure_multiplayer_options(SessionRuntime& runtime,
                                                      std::int32_t dialog_result,
                                                      MultiplayerOptionChoices choices,
                                                      const MultiplayerOptionServices& services);
} // namespace openciv::session
