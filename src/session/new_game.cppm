export module openciv.session.new_game;

import std;
export import openciv.base.callback_ref;
export import openciv.session.runtime;

export extern "C++" {
    namespace openciv::session {
    // Borrowed providers and callable owners must outlive the consuming operation.
    struct NewGameDependencies {
        // V01 shared creation, with the live turn set to zero by the parent. Invoke
        // for slots 0..7, including absent slots; returned failures are ignored.
        base::CallbackRef<std::int32_t(std::int32_t)> create_civilization;
        // F05 section 8, after all creation attempts and the at-start mask copy.
        base::CallbackRef<void()> balance_starts;
        void validate() const;
    };
    // F05 section 5: the complete inner new-game initialization parent, after W02.
    // Requires prepare_new_game before this attempt's content/map/wizard. Retained
    // inputs below belong only to that attempt, never a previous game.
    // This is not the outer new-session isolation, setup wizard, accelerated
    // start or a load operation. Required children are not fabricated here.
    // The owner supplies its declared gameplay context and a unique semantic call
    // identity; this operation neither seeds RNGs nor creates/revokes client leases.
    // All preceding effects survive a child exception or unavailable choice. An
    // unsupported playerCiv stops at the single-human rate assignment, not earlier.
    void initialize_new_game(SessionRuntime& runtime, base::GameplayRandom& random,
                             std::string_view call, const NewGameDependencies& services);
    } // namespace openciv::session
}
