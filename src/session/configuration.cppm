export module openciv.session.configuration;

import std;
export import openciv.session.configuration_types;
export import openciv.base.callback_ref;
export import openciv.session.classic_preferences;
export import openciv.session.runtime;

export namespace openciv::session {
struct GameOptionDependencies {
    const io::FileStorage* preferences = nullptr;
    const MusicControl* music = nullptr;
};
struct GraphicsOptionDependencies {
    const io::FileStorage* preferences = nullptr;
    base::CallbackRef<bool()> heralds_available;
    base::CallbackRef<void()> low_memory_notice;
};
// nullopt is cancellation: no changes or service calls. Accepted changes use
// real F03 preference saving and ignore its returned failure without rollback.
// Missing bindings reject before effects; bound child exceptions retain prior
// effects. Providers and callback targets are borrowed for the call. No command, native
// widget or audio implementation is fabricated by these application ports.
bool configure_game_options(SessionRuntime& runtime, std::optional<GameOptionChoices> choices,
                            const GameOptionDependencies& services);
bool configure_graphics_options(SessionRuntime& runtime,
                                std::optional<GraphicsOptionChoices> choices,
                                const GraphicsOptionDependencies& services);
bool configure_message_options(SessionRuntime& runtime, std::optional<MessageOptionChoices> choices,
                               const io::FileStorage& services);

struct MultiplayerOptionDependencies {
    const io::FileStorage* preferences = nullptr;
    base::CallbackRef<std::uint32_t()> clock;
    // N01 owns local player/name mapping; a civilization is not a player slot.
    base::CallbackRef<std::string()> local_player_name, separator;
    base::CallbackRef<std::string(bool)> production_label, movement_label;
    const OptionBroadcaster* broadcaster = nullptr;
    base::CallbackRef<void(std::string_view)> notice;
    base::CallbackRef<void(std::int32_t)> redraw_status;
};

// F03/N03 accepted-dialog boundary. Chat preferences save before host/change
// gates. The operation owns pending vote lifetime and always yields after begin.
// Providers and callback owners outlive the operation. The owned channel is cancelled
// on completion; polling uses the normal scheduler without a nested presentation loop.
class MultiplayerOptionOperation {
public:
    MultiplayerOptionOperation(SessionRuntime& runtime, std::int32_t dialog_result,
                               MultiplayerOptionChoices choices,
                               MultiplayerOptionDependencies services,
                               std::unique_ptr<OptionVoteChannel> channel = {});
    ~MultiplayerOptionOperation();
    MultiplayerOptionOperation(const MultiplayerOptionOperation&) = delete;
    MultiplayerOptionOperation& operator=(const MultiplayerOptionOperation&) = delete;
    std::optional<MultiplayerOptionResult> poll();
    void cancel();
    const std::optional<OptionProposal>& proposal() const noexcept { return proposal_; }

private:
    void finish(MultiplayerOptionResult result) noexcept;
    void reject();
    void apply();
    void cancel_now();
    SessionRuntime& runtime_;
    MultiplayerOptionChoices choices_;
    MultiplayerOptionDependencies services_;
    std::unique_ptr<OptionVoteChannel> channel_;
    std::optional<SessionActivity> activity_;
    std::optional<OptionProposal> proposal_;
    std::optional<MultiplayerOptionResult> result_;
    bool started_ = false, polling_ = false, cancel_requested_ = false, chosen_ = false;
};
} // namespace openciv::session
