export module openciv.base.random;

import std;
export import openciv.base.random_policy;
export import openciv.base.arithmetic;
export import openciv.base.choices;
export import openciv.base.random_source;

export namespace openciv::base {
/// F01 §§2,6 and docs/random-policy.md. Domains are distinct interface types.
enum class RandomDomain { gameplay, presentation_content, cosmetic };

/// An owned snapshot of a random domain, including its decision tape and trace.
struct RandomCheckpoint {
    unsigned version;
    RandomDomain domain;
    std::string context;
    // Owned source snapshot. Copying a checkpoint clones the source state.
    // This is an in-memory checkpoint, not a portable binary representation.
    std::unique_ptr<RandomSource> engine;
    std::uint64_t draws;
    // Controlled mode does not use/advance engine. Its input and chronological
    // consumed prefix are owned, so restore cannot silently reuse a decision.
    std::optional<ControlledChoices> controlled;
    std::optional<ChoiceTrace> trace;
    /// Take ownership of a source snapshot and its associated replay metadata.
    RandomCheckpoint(unsigned version, RandomDomain domain, std::string context,
                     std::unique_ptr<RandomSource> engine, std::uint64_t draws,
                     std::optional<ControlledChoices> controlled = {},
                     std::optional<ChoiceTrace> trace = {});
    /// Clone the source so the copied checkpoint can advance independently.
    RandomCheckpoint(const RandomCheckpoint& other);
    RandomCheckpoint& operator=(const RandomCheckpoint& other);
    RandomCheckpoint(RandomCheckpoint&&) noexcept = default;
    RandomCheckpoint& operator=(RandomCheckpoint&&) noexcept = default;
    bool operator==(const RandomCheckpoint& other) const;
};

namespace detail {
/// Track one random source, semantic choice consumption, and optional tracing.
class RandomState {
public:
    /// Create a generated source using the domain, owner seed, and context name.
    RandomState(RandomDomain domain, std::uint64_t seed, std::string context);
    /// Take ownership of an initialized source without reseeding it.
    RandomState(RandomDomain domain, std::unique_ptr<RandomSource> source, std::string context);
    /// Create controlled execution from owned decisions instead of a generator.
    RandomState(RandomDomain domain, ControlledChoices choices, std::string context);
    /// Draw a raw word at a legacy site; reject bypassing semantic choice tracking.
    std::uint64_t next(std::string_view site);
    /// Consume or generate one decision with the requested identity and sampling law.
    ChoiceResult sample(ChoiceKey key, SamplingLaw law);
    /// Enable generated-choice recording with an explicit event limit.
    void record_choices(std::size_t limit);
    /// Reject a controlled tape with decisions that have not been consumed.
    void require_choices_consumed() const;
    /// Return an independent snapshot including the consumed decision prefix.
    RandomCheckpoint checkpoint() const;
    /// Validate and replace the current state from a compatible checkpoint.
    void restore(const RandomCheckpoint& checkpoint);

private:
    RandomCheckpoint state_;
    std::map<ChoiceKey, std::size_t> choices_;
    std::set<ChoiceKey> consumed_;
};
} // namespace detail

/// Keep gameplay, presentation-content, and cosmetic randomness in distinct types.
template <RandomDomain Domain> class RandomContext {
public:
    /// Create a reproducible source scoped by the compile-time domain and context name.
    explicit RandomContext(std::uint64_t seed, std::string context)
        : state_(Domain, seed, std::move(context)) {}
    /// Trusted owner/test injection. The source is already initialized; context
    /// naming does not seed it. Semantic tape injection remains a separate mode.
    explicit RandomContext(std::unique_ptr<RandomSource> source, std::string context)
        : state_(Domain, std::move(source), std::move(context)) {}
    /// Use owned semantic decisions as the source for this random domain.
    explicit RandomContext(ControlledChoices choices, std::string context)
        : state_(Domain, std::move(choices), std::move(context)) {}
    /// Draw a 15-bit primary code at a legacy decision site.
    std::int32_t primary_code(std::string_view site) {
        return static_cast<std::int32_t>(state_.next(site) >> 49);
    }
    /// Draw a 32-bit secondary code at a legacy decision site.
    std::uint32_t secondary_code(std::string_view site) {
        return static_cast<std::uint32_t>(state_.next(site) >> 32);
    }
    /// Draw and reduce a primary code modulo n; singleton ranges consume no draw.
    std::int32_t primary_modulo(std::string_view site, std::int32_t n) {
        // Validate before drawing; the versioned policy omits singleton draws.
        primary_modulo_from_code(0, n);
        return n == 1 ? 0 : primary_modulo_from_code(primary_code(site), n);
    }
    /// Draw a secondary code and map it to an inclusive unit fraction.
    double secondary_unit(std::string_view site) {
        secondary_unit_from_code(0); // Check arithmetic environment before drawing.
        return secondary_unit_from_code(secondary_code(site));
    }
    /// Sample the F01 secondary range; equal bounds consume no draw.
    std::int32_t secondary_range(std::string_view site, std::int32_t lo, std::int32_t hi) {
        if (lo == hi)
            return lo;
        secondary_unit_from_code(0);
        return secondary_range_from_code(lo, hi, secondary_code(site));
    }
    /// Sample a primary code identified by a semantic decision key.
    std::int32_t primary_code(ChoiceKey key) {
        return std::get<std::int32_t>(state_.sample(std::move(key), PrimaryCode{}));
    }
    /// Sample a keyed primary modulo decision; singleton ranges consume no decision.
    std::int32_t primary_modulo(ChoiceKey key, std::int32_t n) {
        primary_modulo_from_code(0, n);
        return n == 1 ? 0 : std::get<std::int32_t>(state_.sample(std::move(key), PrimaryModulo{n}));
    }
    /// Sample a keyed secondary unit fraction.
    double secondary_unit(ChoiceKey key) {
        return std::get<double>(state_.sample(std::move(key), SecondaryUnit{}));
    }
    /// Sample a keyed secondary range; equal bounds consume no decision.
    std::int32_t secondary_range(ChoiceKey key, std::int32_t lo, std::int32_t hi) {
        return lo == hi
                   ? lo
                   : std::get<std::int32_t>(state_.sample(std::move(key), SecondaryRange{lo, hi}));
    }
    /// Enable bounded capture of generated semantic decisions.
    /// Opt-in, bounded generated-decision capture. Legacy site-only calls cannot
    /// bypass an active semantic trace or controlled input. No provider callbacks.
    void record_choices(std::size_t limit) { state_.record_choices(limit); }
    /// Require every supplied controlled decision to have been consumed.
    void require_choices_consumed() const { state_.require_choices_consumed(); }
    /// Return an independent snapshot of this context and its replay state.
    RandomCheckpoint checkpoint() const { return state_.checkpoint(); }
    /// Restore a checkpoint compatible with this domain and context.
    void restore(const RandomCheckpoint& checkpoint) { state_.restore(checkpoint); }

private:
    detail::RandomState state_;
};
using GameplayRandom = RandomContext<RandomDomain::gameplay>;
using PresentationRandom = RandomContext<RandomDomain::presentation_content>;
using CosmeticRandom = RandomContext<RandomDomain::cosmetic>;
} // namespace openciv::base
