export module openciv.session.configuration;

import std;
export import openciv.base.callback_ref;
export import openciv.session.classic_preferences;
export import openciv.session.runtime;

export extern "C++" {
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
    class MusicControl {
    public:
        MusicControl() = default;
        virtual ~MusicControl() = default;
        MusicControl(const MusicControl&) = delete;
        MusicControl& operator=(const MusicControl&) = delete;
        // P10 reinitialization includes its stop/close/open/layout sequence;
        // synchronization is a separate child operation.
        virtual void stop() const = 0;
        virtual void reinitialize() const = 0;
        virtual void synchronize() const = 0;
    };
    struct GameOptionDependencies {
        const FileStorage* preferences = nullptr;
        const MusicControl* music = nullptr;
    };
    struct GraphicsOptionDependencies {
        const FileStorage* preferences = nullptr;
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
    bool configure_message_options(SessionRuntime& runtime,
                                   std::optional<MessageOptionChoices> choices,
                                   const FileStorage& services);

    // Clear chat on new game, clear chat on boot, double production, double movement.
    using MultiplayerOptionChoices = std::array<bool, 4>;
    struct OptionProposal {
        std::string sender, description;
        bool production = false, movement = false;
        std::int32_t expected_votes = 0;
    };
    struct OptionVoteProgress {
        std::int32_t pending = 0, status = 0;
    };
    // Owned N02 vote registration/transport. Each poll does bounded, nonblocking
    // work; presentation observes the operation instead of running a modal loop.
    class OptionVoteChannel {
    public:
        OptionVoteChannel() = default;
        virtual ~OptionVoteChannel() = default;
        OptionVoteChannel(const OptionVoteChannel&) = delete;
        OptionVoteChannel& operator=(const OptionVoteChannel&) = delete;
        virtual void begin(OptionProposal proposal) = 0;
        virtual OptionVoteProgress poll() = 0;
        virtual void cancel() noexcept = 0;
    };
    class OptionBroadcaster {
    public:
        OptionBroadcaster() = default;
        virtual ~OptionBroadcaster() = default;
        OptionBroadcaster(const OptionBroadcaster&) = delete;
        OptionBroadcaster& operator=(const OptionBroadcaster&) = delete;
        virtual void rejected() const = 0;
        virtual void accepted() const = 0;
        virtual void units() const = 0;
        virtual void options(bool production, bool movement, bool simultaneous) const = 0;
    };
    struct MultiplayerOptionDependencies {
        const FileStorage* preferences = nullptr;
        base::CallbackRef<std::uint32_t()> clock;
        // N01 owns local player/name mapping; a civilization is not a player slot.
        base::CallbackRef<std::string()> local_player_name, separator;
        base::CallbackRef<std::string(bool)> production_label, movement_label;
        const OptionBroadcaster* broadcaster = nullptr;
        base::CallbackRef<void(std::string_view)> notice;
        base::CallbackRef<void(std::int32_t)> redraw_status;
    };
    enum class MultiplayerOptionResult {
        cancelled,
        client_only,
        unchanged,
        rejected,
        applied,
        failed
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
}
