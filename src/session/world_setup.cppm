export module openciv.session.world_setup;

import std;
export import openciv.base.callback_ref;
export import openciv.session.classic_preferences;
export import openciv.session.runtime;

export extern "C++" {
    namespace openciv::session {
    struct CustomWorldSize {};
    using WorldSizeChoice = std::variant<std::int32_t, CustomWorldSize>;
    enum class CustomSizeStatus { accepted, cancelled, unavailable };
    struct CustomSizeResponse {
        CustomSizeStatus status = CustomSizeStatus::cancelled;
        std::string width, height;
    };
    class WorldSizeInteraction {
    public:
        WorldSizeInteraction() = default;
        virtual ~WorldSizeInteraction() = default;
        WorldSizeInteraction(const WorldSizeInteraction&) = delete;
        WorldSizeInteraction& operator=(const WorldSizeInteraction&) = delete;
        // Ordinary 0=Small, 2=Large, other nonnegative=Normal; negative=cancel.
        virtual WorldSizeChoice choose_preset(state::WorldSize remembered_preset) const = 0;
        virtual CustomSizeResponse choose_custom(std::string_view width_default,
                                                 std::string_view height_default) const = 0;
    };

    // F05 section 3. Clears status flags; retains partial dimensions on failure;
    // classifies area and doubles working width on every normal helper exit.
    // Does not allocate a map, save preferences or initialize a session.
    bool select_world_size(SessionRuntime& runtime, const WorldSizeInteraction& services);

    struct RandomWorldParameter {};
    using WorldParameterChoice = std::variant<std::int32_t, RandomWorldParameter>;
    // Borrowed providers and callable owners must outlive the consuming operation.
    struct WorldParameterDependencies {
        // Remembered value is normalized (-1..1 nominally), not a menu index.
        // Ordinary results are 0..2 or negative cancellation. An unsupported
        // positive row stops at consumption with all earlier effects retained.
        base::CallbackRef<WorldParameterChoice(state::WorldParameter, std::int16_t remembered)>
            choose;
        const FileStorage* preferences = nullptr;
        void validate() const;
    };
    // F05 section 2: five choices L,F,T,C,A, leaving preferences unchanged.
    void randomize_world_parameters(SessionRuntime& runtime, base::GameplayRandom& random,
                                    std::string_view call);
    // F05 section 3: ordered L,F,C,T,A interactions and real F03 preference save.
    // Negative ordinary results retain choice-1 in world state and stop. Returned
    // persistence failure does not turn an accepted parameter sequence into cancel.
    bool customize_world_parameters(SessionRuntime& runtime, base::GameplayRandom& random,
                                    std::string_view call,
                                    const WorldParameterDependencies& services);
    // F05 section 2's post-size/map dispatch. Failed selection returns without
    // discarded parameter choices. The result gates the later player wizard.
    bool select_world_parameters(SessionRuntime& runtime, base::GameplayRandom& random,
                                 std::string_view call, bool customize, bool world_selected,
                                 const WorldParameterDependencies& services);
    } // namespace openciv::session
}
