#pragma once
#include "base/arithmetic.hpp"
#include <cstdint>
#include <random>
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
    bool operator==(const RandomCheckpoint&) const = default;
};

namespace detail {
class RandomState {
public:
    RandomState(RandomDomain domain, std::uint64_t seed, std::string context);
    std::uint64_t next(std::string_view site);
    RandomCheckpoint checkpoint() const;
    void restore(const RandomCheckpoint& checkpoint);

private:
    RandomCheckpoint state_;
};
} // namespace detail

template <RandomDomain Domain> class RandomContext {
public:
    explicit RandomContext(std::uint64_t seed, std::string context)
        : state_(Domain, seed, std::move(context)) {}
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
    RandomCheckpoint checkpoint() const { return state_.checkpoint(); }
    void restore(const RandomCheckpoint& checkpoint) { state_.restore(checkpoint); }

private:
    detail::RandomState state_;
};
using GameplayRandom = RandomContext<RandomDomain::gameplay>;
using PresentationRandom = RandomContext<RandomDomain::presentation_content>;
using CosmeticRandom = RandomContext<RandomDomain::cosmetic>;
} // namespace openciv::base
