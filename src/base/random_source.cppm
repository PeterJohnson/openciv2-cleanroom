export module openciv.base.random_source;

import std;

export extern "C++" {
    namespace openciv::base {
    // Owned raw-word source. Contexts retain sampling laws, semantic choices and draw
    // accounting. Implementations must not call game services or share mutable state.
    class RandomSource {
    public:
        virtual ~RandomSource() = default;
        // A stable implementation identity, not a context or decision-site name.
        virtual std::string_view id() const noexcept = 0;
        // On failure, leave the source unchanged. Exhausted mocks may throw.
        virtual std::uint64_t next() = 0;
        // An independent copy at exactly the same position, never null.
        virtual std::unique_ptr<RandomSource> clone() const = 0;
        virtual bool same_state(const RandomSource& other) const noexcept = 0;
        void discard(std::uint64_t count);
    };
    // Production policy: use the standard library engine, with no local generator.
    std::unique_ptr<RandomSource> make_mt19937_source(std::uint64_t seed);
    // M1-01 bounded replay representation. Seed is the engine's already-derived
    // seed, not the context owner's input. Unknown/injected sources are unsupported.
    struct SeededRandomState {
        std::uint64_t seed, draws;
        bool operator==(const SeededRandomState&) const = default;
    };
    std::optional<SeededRandomState> seeded_random_state(const RandomSource& source) noexcept;
    std::unique_ptr<RandomSource> restore_seeded_random(SeededRandomState state,
                                                        std::uint64_t replay_limit);
    } // namespace openciv::base
}
