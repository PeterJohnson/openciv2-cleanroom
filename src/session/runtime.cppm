export module openciv.session.runtime;

import std;
export import openciv.session.client;
export import openciv.session.commands;
export import openciv.state.game;

export extern "C++" {
    namespace openciv::session {
    // Pins world storage during a workflow or nested engine call, including setup
    // before client admission. It does not block command steps or deadlines.
    class SessionActivity {
    public:
        SessionActivity(SessionActivity&&) noexcept = default;
        SessionActivity(const SessionActivity&) = delete;
        SessionActivity& operator=(const SessionActivity&) = delete;
        ~SessionActivity();

    private:
        friend class SessionRuntime;
        explicit SessionActivity(std::shared_ptr<detail::RuntimeState> state);
        std::shared_ptr<detail::RuntimeState> state_;
    };
    struct Participant {
        ParticipantId id{0};
        state::CivilizationSlot civilization{0};
        std::optional<ConnectionId> connection;
        bool operator==(const Participant&) const = default;
    };
    struct Seeds {
        std::uint64_t gameplay = 0, presentation_content = 0;
    };
    // M1-01 owner-supplied state projection. The codec must reject state outside its
    // declared closure. Decode changes only the supplied fresh world/process values;
    // it must not access services, clients, or the world's shared fallback tile.
    // Selected immutable content is supplied by the owner and bound separately by hash.
    struct CheckpointProfile {
        std::string id;
        std::function<CheckpointBytes(const state::GameState&, const state::ProcessContext&)>
            encode;
        std::function<void(std::span<const std::uint8_t>, state::GameState&,
                           state::ProcessContext&)>
            decode;
    };
    // Trusted composition/engine owner, never given to player code. One cooperative
    // mutation lane; not thread safe. Process state must outlive this runtime.
    class SessionRuntime {
    public:
        // The composition root supplies a stable, nonzero identity unique among
        // runtimes whose observations may be compared; never derive it from an address.
        SessionRuntime(state::ProcessContext& process, RuntimeId identity);
        ~SessionRuntime();
        SessionRuntime(const SessionRuntime&) = delete;
        SessionRuntime& operator=(const SessionRuntime&) = delete;

        // Client-session lifetimes, separate from F05 new-game/F06 load operations.
        // No world/process reset is implied. The owner performs specified setup/load
        // steps explicitly, then issues new participants and controller bindings.
        SessionId begin_client_session(Seeds seeds);
        void end_client_session();
        // X01 fresh-game isolation / F05 section 2. Before loading chosen content or
        // running setup, replace all world/workspace state and mutable rule/text
        // state. Requires completed/cancelled commands and drained transport outboxes;
        // revoke old clients and clear their queues. Preferences,
        // identity generators and explicitly supplied random contexts survive.
        // Invalidates engine references into the previous world and rule contents.
        // Load/resume and within-game civilization creation do not call this.
        void prepare_new_game();
        bool active() const noexcept;
        // At most one owner workflow; runtime and its injected process/services
        // must outlive it. Starting requires settled commands, then ordinary command
        // and timeout processing remains available while the workflow is pending.
        SessionActivity begin_activity();
        // Nested storage guard, without starting a new owner workflow. Valid before
        // client admission and inside commands; blocks replacement until scope exit.
        SessionActivity pin_world();
        ParticipantId add_participant(state::CivilizationSlot civilization,
                                      std::optional<ConnectionId> connection = {});
        Participant participant(ParticipantId id) const;
        void reassign_participant(ParticipantId id, state::CivilizationSlot civilization,
                                  std::optional<ConnectionId> connection = {});
        void remove_participant(ParticipantId id);
        // Revokes mapped input access while retaining the seats/world for their
        // owning mode's disconnect continuation. Does not perform N01 host transfer.
        void disconnect(ConnectionId connection);
        PlayerClient bind(ParticipantId participant, ControllerKind kind,
                          AccessClass access = AccessClass::player);
        void revoke(ControllerId controller);

        // Register trusted owning operations while inactive. No built-in fake game
        // commands are installed. Local work is an owned FIFO driven at explicit
        // engine boundaries; one call resumes at most one continuation.
        void register_command(CommandDefinition definition);
        bool advance_one();
        bool continue_command(CommandId command);
        InputReceipt
        cancel_command(CommandId command); // Owning lifecycle/mode input, no client lease required.
        std::size_t expire_interactions(std::uint64_t now);
        bool has_pending_commands() const noexcept;
        // Owner-selected result lifetime. Retire a terminal local result only after
        // all live clients have drained its updates and consumed any desired result.
        // Returns false for pending work, unread notifications or retained peer
        // duplicate receipts/outboxes. Already returned owned values remain valid.
        bool retire_command(CommandId command);
        CommandUpdate receive_request(ConnectionId source, ParticipantId participant,
                                      CommandCorrelation correlation, CommandRequest request);
        InputReceipt receive_result(ConnectionId source, const RoutedReply& reply);
        // Taking an outbox transfers responsibility to the mode/transport adapter.
        std::vector<RoutedRequest> take_requests();
        std::vector<RoutedReply> take_replies();

        // Engine-only ownership ports. Call publish after each specified publication
        // and before exposing a query boundary; no automatic recomputation occurs.
        state::GameState& world() noexcept;
        const state::GameState& world() const noexcept;
        state::ProcessContext& process() noexcept;
        void publish();
        base::GameplayRandom& gameplay_random();
        base::PresentationRandom& presentation_random();
        // Local foundation boundary: one civilization-1 participant, zero commands
        // or one awaiting interaction, no peer work/queued inputs. No leases persist.
        // Owner codecs validate their state closure; all decoding precedes replacement.
        CheckpointBytes checkpoint(const CheckpointProfile& profile,
                                   std::string_view content_hash) const;
        void restore_checkpoint(std::span<const std::uint8_t> bytes,
                                const CheckpointProfile& profile, std::string_view content_hash);

    private:
        std::shared_ptr<detail::RuntimeState> state_;
    };
    } // namespace openciv::session
}
