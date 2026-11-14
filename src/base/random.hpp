#pragma once
#include "base/arithmetic.hpp"
#include "base/choices.hpp"
#include <cstdint>
#include <map>
#include <random>
#include <set>
#include <string>
#include <string_view>
#include <utility>

namespace openciv::base {
// F01 §§2,6 and docs/random-policy.md. Domains are distinct interface types.
enum class RandomDomain { gameplay, presentation_content, cosmetic };
inline constexpr std::string_view random_policy_id = "oc2-mt19937-64";
inline constexpr unsigned random_policy_version = 2;

struct RandomCheckpoint {
    unsigned version;
    RandomDomain domain;
    std::string context;
    // Owned engine copy includes the complete sequence state and current position.
    // This is an in-memory checkpoint, not a portable binary representation.
    std::mt19937_64 engine;
    std::uint64_t draws;
    // Controlled mode does not use/advance engine. Its input and chronological
    // consumed prefix are owned, so restore cannot silently reuse a decision.
    std::optional<ControlledChoices> controlled;
    std::optional<ChoiceTrace> trace;
    bool operator==(const RandomCheckpoint&) const = default;
};

namespace detail {
class RandomState {
public:
    RandomState(RandomDomain domain, std::uint64_t seed, std::string context);
    RandomState(RandomDomain domain, ControlledChoices choices, std::string context);
    std::uint64_t next(std::string_view site);
    ChoiceResult sample(ChoiceKey key, SamplingLaw law);
    void record_choices(std::size_t limit);
    void require_choices_consumed() const;
    RandomCheckpoint checkpoint() const;
    void restore(const RandomCheckpoint& checkpoint);

private:
    RandomCheckpoint state_;
    std::map<ChoiceKey, std::size_t> choices_;
    std::set<ChoiceKey> consumed_;
};
} // namespace detail

template <RandomDomain Domain> class RandomContext {
public:
    explicit RandomContext(std::uint64_t seed, std::string context)
        : state_(Domain, seed, std::move(context)) {}
    explicit RandomContext(ControlledChoices choices, std::string context)
        : state_(Domain, std::move(choices), std::move(context)) {}
    std::int32_t primary_code(std::string_view site) {
        return static_cast<std::int32_t>(state_.next(site) >> 49);
    }
    std::uint32_t secondary_code(std::string_view site) {
        return static_cast<std::uint32_t>(state_.next(site) >> 32);
    }
    std::int32_t primary_modulo(std::string_view site, std::int32_t n) {
        // Validate before drawing; the versioned policy omits singleton draws.
        primary_modulo_from_code(0, n);
        return n == 1 ? 0 : primary_modulo_from_code(primary_code(site), n);
    }
    double secondary_unit(std::string_view site) {
        secondary_unit_from_code(0); // Check arithmetic environment before drawing.
        return secondary_unit_from_code(secondary_code(site));
    }
    std::int32_t secondary_range(std::string_view site, std::int32_t lo, std::int32_t hi) {
        if (lo == hi)
            return lo;
        secondary_unit_from_code(0);
        return secondary_range_from_code(lo, hi, secondary_code(site));
    }
    std::int32_t primary_code(ChoiceKey key) {
        return std::get<std::int32_t>(state_.sample(std::move(key), PrimaryCode{}));
    }
    std::int32_t primary_modulo(ChoiceKey key, std::int32_t n) {
        primary_modulo_from_code(0, n);
        return n == 1 ? 0 : std::get<std::int32_t>(state_.sample(std::move(key), PrimaryModulo{n}));
    }
    double secondary_unit(ChoiceKey key) {
        return std::get<double>(state_.sample(std::move(key), SecondaryUnit{}));
    }
    std::int32_t secondary_range(ChoiceKey key, std::int32_t lo, std::int32_t hi) {
        return lo == hi
                   ? lo
                   : std::get<std::int32_t>(state_.sample(std::move(key), SecondaryRange{lo, hi}));
    }
    // Opt-in, bounded generated-decision capture. Legacy site-only calls cannot
    // bypass an active semantic trace or controlled input. No provider callbacks.
    void record_choices(std::size_t limit) { state_.record_choices(limit); }
    void require_choices_consumed() const { state_.require_choices_consumed(); }
    RandomCheckpoint checkpoint() const { return state_.checkpoint(); }
    void restore(const RandomCheckpoint& checkpoint) { state_.restore(checkpoint); }

private:
    detail::RandomState state_;
};
using GameplayRandom = RandomContext<RandomDomain::gameplay>;
using PresentationRandom = RandomContext<RandomDomain::presentation_content>;
using CosmeticRandom = RandomContext<RandomDomain::cosmetic>;
} // namespace openciv::base
