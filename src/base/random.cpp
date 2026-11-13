#include "base/random.hpp"
#include <limits>
#include <stdexcept>
#include <utility>

namespace openciv::base::detail {
namespace {
std::string_view label(RandomDomain domain) {
    switch (domain) {
    case RandomDomain::gameplay:
        return "gameplay";
    case RandomDomain::presentation_content:
        return "presentation-content";
    case RandomDomain::cosmetic:
        return "cosmetic";
    }
    throw std::invalid_argument("Unknown random domain");
}
void validate_name(std::string_view name) {
    if (name.empty())
        throw std::invalid_argument("Random context/site must have a stable name");
    for (const char c : name)
        if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
              c == '.' || c == '-' || c == '_' || c == '/'))
            throw std::invalid_argument("Random names require ASCII letters/digits or .-_/");
}
std::uint64_t append(std::uint64_t hash, std::string_view text) {
    for (const char byte : text)
        hash = (hash ^ static_cast<unsigned char>(byte)) * UINT64_C(1099511628211);
    return hash;
}
} // namespace
RandomState::RandomState(RandomDomain domain, std::uint64_t seed, std::string context)
    : state_{random_policy_version, domain, std::move(context), std::mt19937_64{}, 0} {
    validate_name(state_.context);
    auto hash = append(UINT64_C(14695981039346656037), label(domain));
    hash *= UINT64_C(1099511628211); // Domain/context separator: one zero byte.
    state_.engine.seed(seed ^ append(hash, state_.context));
}
std::uint64_t RandomState::next(std::string_view site) {
    validate_name(site);
    if (state_.draws == std::numeric_limits<std::uint64_t>::max())
        throw std::overflow_error("Random checkpoint draw counter exhausted");
    ++state_.draws;
    return state_.engine();
}
RandomCheckpoint RandomState::checkpoint() const {
    return state_;
}
void RandomState::restore(const RandomCheckpoint& checkpoint) {
    if (checkpoint.version != random_policy_version || checkpoint.domain != state_.domain ||
        checkpoint.context != state_.context)
        throw std::invalid_argument("Random checkpoint policy/domain/context mismatch");
    state_ = checkpoint;
}
} // namespace openciv::base::detail
