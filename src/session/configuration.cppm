export module openciv.session.configuration;

import std;
export import openciv.session.configuration_types;
export import openciv.base.callback_ref;
export import openciv.session.classic_preferences;
export import openciv.session.runtime;

export namespace openciv::session {
/// Borrowed services used when applying game-option choices.
struct GameOptionDependencies {
    const io::FileStorage* preferences = nullptr;
    const MusicControl* music = nullptr;
};
/// Borrowed graphics availability, notice, and preference-storage services.
struct GraphicsOptionDependencies {
    const io::FileStorage* preferences = nullptr;
    base::CallbackRef<bool()> heralds_available;
    base::CallbackRef<void()> low_memory_notice;
};
/// nullopt is cancellation: no changes or service calls. Accepted changes use
/// real F03 preference saving and ignore its returned failure without rollback.
/// Missing bindings reject before effects; bound child exceptions retain prior
/// effects. Providers and callback targets are borrowed for the call. No command, native
/// widget or audio implementation is fabricated by these application ports.
bool configure_game_options(SessionRuntime& runtime, std::optional<GameOptionChoices> choices,
                            const GameOptionDependencies& services);
/// Apply accepted graphics choices and their required effects; handle cancellation explicitly.
bool configure_graphics_options(SessionRuntime& runtime,
                                std::optional<GraphicsOptionChoices> choices,
                                const GraphicsOptionDependencies& services);
/// Apply accepted message-option choices and persist the resulting preferences.
bool configure_message_options(SessionRuntime& runtime, std::optional<MessageOptionChoices> choices,
                               const io::FileStorage& services);

/// Explicit services for multiplayer option voting, notifications, and publication.
struct MultiplayerOptionDependencies {
    const io::FileStorage* preferences = nullptr;
    base::CallbackRef<std::uint32_t()> clock;
    /// N01 owns local player/name mapping; a civilization is not a player slot.
    base::CallbackRef<std::string()> local_player_name, separator;
    base::CallbackRef<std::string(bool)> production_label, movement_label;
    const OptionBroadcaster* broadcaster = nullptr;
    base::CallbackRef<void(std::string_view)> notice;
    base::CallbackRef<void(std::int32_t)> redraw_status;
};

/// F03/N03 accepted-dialog boundary. Chat preferences save before host/change
/// gates. The operation owns pending vote lifetime and always yields after begin.
/// Providers and callback owners outlive the operation. The owned channel is cancelled
/// on completion; polling uses the normal scheduler without a nested presentation loop.
class MultiplayerOptionOperation {
public:
    /// Start an option-change workflow with owned choices and an optional vote channel.
    MultiplayerOptionOperation(SessionRuntime& runtime, std::int32_t dialog_result,
                               MultiplayerOptionChoices choices,
                               MultiplayerOptionDependencies services,
                               std::unique_ptr<OptionVoteChannel> channel = {});
    /// Release any pending option-vote resources.
    ~MultiplayerOptionOperation();
    MultiplayerOptionOperation(const MultiplayerOptionOperation&) = delete;
    MultiplayerOptionOperation& operator=(const MultiplayerOptionOperation&) = delete;
    /// Perform bounded voting/application work and return a result when complete.
    std::optional<MultiplayerOptionResult> poll();
    /// Request cancellation of the pending option change.
    void cancel();
    /// Inspect the proposal presented to peers, if voting was required.
    const std::optional<OptionProposal>& proposal() const noexcept { return proposal_; }

private:
    /// Store a terminal result and release operation state without throwing.
    void finish(MultiplayerOptionResult result) noexcept;
    /// Apply the rejected-vote continuation and its notifications.
    void reject();
    /// Apply accepted gameplay options and required synchronization effects.
    void apply();
    /// Complete cancellation at an admitted operation boundary.
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
