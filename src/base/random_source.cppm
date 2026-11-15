export module openciv.base.random_source;
import openciv.base.random_policy;

import std;

export namespace openciv::base {
/// Owned raw-word source. Contexts retain sampling laws, semantic choices and draw
/// accounting. Implementations must not call game services or share mutable state.
class RandomSource {
public:
    virtual ~RandomSource() = default;
    /// A stable implementation identity, not a context or decision-site name.
    virtual std::string_view id() const noexcept = 0;
    /// On failure, leave the source unchanged. Exhausted mocks may throw.
    virtual std::uint64_t next() = 0;
    /// An independent copy at exactly the same position, never null.
    virtual std::unique_ptr<RandomSource> clone() const = 0;
    /// Compare generator state with another source without consuming a draw.
    virtual bool same_state(const RandomSource& other) const noexcept = 0;
    /// Advance through count raw words using the normal next() failure semantics.
    void discard(std::uint64_t count);
};
/// Production policy: use the standard library engine, with no local generator.
std::unique_ptr<RandomSource> make_mt19937_source(std::uint64_t seed);
/// M1-01 bounded replay representation. Seed is the engine's already-derived
/// seed, not the context owner's input. Unknown/injected sources are unsupported.
struct SeededRandomState {
    std::uint64_t seed, draws;
    bool operator==(const SeededRandomState&) const = default;
};
/// Return seed/draw metadata for the production source, or nullopt for other sources.
std::optional<SeededRandomState> seeded_random_state(const RandomSource& source) noexcept;
/// Recreate a production source and replay its draws within the supplied budget.
std::unique_ptr<RandomSource> restore_seeded_random(SeededRandomState state,
                                                    std::uint64_t replay_limit);
} // namespace openciv::base

module :private;
namespace openciv::base {
namespace {
class Mt19937Source final : public RandomSource {
public:
    explicit Mt19937Source(std::uint64_t seed) : engine_(seed), origin_{seed, 0} {}
    std::string_view id() const noexcept override { return random_policy_id; }
    std::uint64_t next() override {
        if (origin_.draws == std::numeric_limits<std::uint64_t>::max())
            throw std::overflow_error("Random source draw counter exhausted");
        const auto value = engine_();
        ++origin_.draws;
        return value;
    }
    SeededRandomState origin() const noexcept { return origin_; }
    std::unique_ptr<RandomSource> clone() const override {
        return std::make_unique<Mt19937Source>(*this);
    }
    bool same_state(const RandomSource& other) const noexcept override {
        const auto* source = dynamic_cast<const Mt19937Source*>(&other);
        return source && engine_ == source->engine_;
    }

private:
    std::mt19937_64 engine_;
    SeededRandomState origin_;
};
} // namespace
void RandomSource::discard(std::uint64_t count) {
    while (count-- != 0)
        next();
}
std::unique_ptr<RandomSource> make_mt19937_source(std::uint64_t seed) {
    return std::make_unique<Mt19937Source>(seed);
}
std::optional<SeededRandomState> seeded_random_state(const RandomSource& source) noexcept {
    const auto* engine = dynamic_cast<const Mt19937Source*>(&source);
    return engine ? std::optional{engine->origin()} : std::nullopt;
}
std::unique_ptr<RandomSource> restore_seeded_random(SeededRandomState state,
                                                    std::uint64_t replay_limit) {
    if (state.draws > replay_limit)
        throw std::invalid_argument("Random source exceeds checkpoint replay budget");
    auto source = make_mt19937_source(state.seed);
    source->discard(state.draws);
    return source;
}
} // namespace openciv::base
