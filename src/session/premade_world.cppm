export module openciv.session.premade_world;

import std;
export import openciv.base.callback_ref;
export import openciv.session.classic_load;

export extern "C++" {
    namespace openciv::session {
    enum class WorldSource { generated, premade_cylindrical, premade_flat };
    struct PremadeWorldChoices {
        // Zero discards; every nonzero result, including a negative result, keeps.
        base::CallbackRef<std::int32_t()> use_seed, use_start_locations;
        // F01 game ticks, not milliseconds or a gameplay random draw.
        base::CallbackRef<std::uint32_t()> ticks;
        void validate() const;
    };
    // F05 section 3 after a successful F06 read. Normalizes the live map, applies
    // the two conditional choices and finally the current stored topology rule.
    // Requires complete allocated tile/remembered backing. Does not rebuild regions,
    // classify area, clear status flags, allocate, generate terrain or save a file.
    WorldSource prepare_premade_world(SessionRuntime& runtime, formats::StartingPositions& starts,
                                      const PremadeWorldChoices& choices);
    // Borrowed providers and callable owners must outlive the consuming operation.
    struct PremadeWorldDependencies {
        const FileStorage* storage = nullptr;
        base::CallbackRef<void()> load_failure;
        PremadeWorldChoices choices;
        void validate() const;
    };
    // F05 section 3 / F06 section 4 accepted-file path. nullopt path means cancel;
    // nullopt result means cancel or returned input failure (which reports notice).
    // The native chooser/directory policy remains a separate interaction boundary.
    // Input closes once before installation. Decode/allocation failures retain the
    // old map; successful installation publishes setup dimensions before choices. Paths
    // are opaque to this parent except that embedded NUL is outside its domain.
    std::optional<WorldSource> load_premade_world(SessionRuntime& runtime,
                                                  formats::StartingPositions& starts,
                                                  std::optional<std::string> selected_path,
                                                  const PremadeWorldDependencies& services);
    } // namespace openciv::session
}
