#include "base/random.hpp"
#include <algorithm>
#include <limits>
#include <stdexcept>
#include <type_traits>
#include <utility>

namespace openciv::base {
RandomCheckpoint::RandomCheckpoint(unsigned version, RandomDomain domain, std::string context,
                                   std::unique_ptr<RandomSource> engine, std::uint64_t draws,
                                   std::optional<ControlledChoices> controlled,
                                   std::optional<ChoiceTrace> trace)
    : version(version), domain(domain), context(std::move(context)), engine(std::move(engine)),
      draws(draws), controlled(std::move(controlled)), trace(std::move(trace)) {}
RandomCheckpoint::RandomCheckpoint(const RandomCheckpoint& other)
    : RandomCheckpoint(other.version, other.domain, other.context,
                       other.engine ? other.engine->clone() : nullptr, other.draws,
                       other.controlled, other.trace) {}
RandomCheckpoint& RandomCheckpoint::operator=(const RandomCheckpoint& other) {
    if (this != &other) {
        auto prepared = other;
        *this = std::move(prepared);
    }
    return *this;
}
bool RandomCheckpoint::operator==(const RandomCheckpoint& other) const {
    const bool same_engine =
        engine && other.engine ? engine->same_state(*other.engine) : !engine && !other.engine;
    return version == other.version && domain == other.domain && context == other.context &&
           same_engine && draws == other.draws && controlled == other.controlled &&
           trace == other.trace;
}
namespace detail {
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
    : state_{random_policy_version, domain, std::move(context), nullptr, 0, {}, {}} {
    validate_name(state_.context);
    auto hash = append(UINT64_C(14695981039346656037), label(domain));
    hash *= UINT64_C(1099511628211); // Domain/context separator: one zero byte.
    state_.engine = make_mt19937_source(seed ^ append(hash, state_.context));
}
RandomState::RandomState(RandomDomain domain, std::unique_ptr<RandomSource> source,
                         std::string context)
    : state_{random_policy_version, domain, std::move(context), std::move(source), 0, {}, {}} {
    label(domain);
    validate_name(state_.context);
    if (!state_.engine || state_.engine->id().empty())
        throw std::invalid_argument("Random source requires an owned implementation and identity");
}
RandomState::RandomState(RandomDomain domain, ControlledChoices choices, std::string context)
    : RandomState(domain, 0, std::move(context)) {
    auto initial = state_;
    initial.trace = ChoiceTrace{choices.records.size(), {}};
    initial.controlled = std::move(choices);
    restore(initial);
}
std::uint64_t RandomState::next(std::string_view site) {
    if (state_.controlled || state_.trace)
        throw ChoiceError(ChoiceFailure::semantic_key_required);
    validate_name(site);
    if (state_.draws == std::numeric_limits<std::uint64_t>::max())
        throw std::overflow_error("Random checkpoint draw counter exhausted");
    const auto result = state_.engine->next();
    ++state_.draws;
    return result;
}
ChoiceResult RandomState::sample(ChoiceKey key, SamplingLaw law) {
    const bool primary =
        std::holds_alternative<PrimaryCode>(law) || std::holds_alternative<PrimaryModulo>(law);
    // Validate arithmetic/environment and prepare owned strings before any state
    // change. Sampling has no callbacks; no floating-mode change can intervene.
    auto event = make_choice(std::move(key), law,
                             primary ? ChoiceCode{std::int32_t{0}} : ChoiceCode{std::uint32_t{0}});
    if (state_.controlled) {
        const auto found = choices_.find(event.key);
        if (found == choices_.end())
            throw ChoiceError(ChoiceFailure::missing, event.key);
        const auto& supplied = state_.controlled->records[found->second];
        if (supplied.law != law)
            throw ChoiceError(ChoiceFailure::law_mismatch, event.key);
        event.code = supplied.code;
        event.result = supplied.result;
    } else if (state_.draws == std::numeric_limits<std::uint64_t>::max()) {
        throw std::overflow_error("Random checkpoint draw counter exhausted");
    }
    std::optional<std::set<ChoiceKey>::iterator> inserted;
    if (state_.trace) {
        auto& trace = *state_.trace;
        if (consumed_.contains(event.key))
            throw ChoiceError(ChoiceFailure::already_consumed, event.key);
        if (trace.events.size() >= trace.limit)
            throw ChoiceError(ChoiceFailure::trace_limit, event.key);
        // Allocate before consuming either a supplied code or engine output.
        // Geometric growth avoids quadratic copying on long captures.
        if (trace.events.size() == trace.events.capacity()) {
            const auto capacity = trace.events.capacity();
            trace.events.reserve(
                capacity == 0 ? 1 : capacity + std::min(capacity, trace.limit - capacity));
        }
        inserted = consumed_.insert(event.key).first;
    }
    if (!state_.controlled) {
        std::uint64_t raw;
        try {
            raw = state_.engine->next();
        } catch (...) {
            if (inserted)
                consumed_.erase(*inserted);
            throw;
        }
        ++state_.draws;
        event.code = primary ? ChoiceCode{static_cast<std::int32_t>(raw >> 49)}
                             : ChoiceCode{static_cast<std::uint32_t>(raw >> 32)};
        event.result = choice_result(law, event.code);
    }
    const auto result = event.result;
    if (state_.trace)
        state_.trace->events.push_back(std::move(event));
    return result;
}
void RandomState::record_choices(std::size_t limit) {
    if (state_.trace)
        throw std::logic_error("Random choices are already being recorded");
    state_.trace = ChoiceTrace{limit, {}};
}
void RandomState::require_choices_consumed() const {
    if (state_.controlled)
        for (const auto& [key, index] : choices_)
            if (!consumed_.contains(key))
                throw ChoiceError(ChoiceFailure::unused, key);
}
RandomCheckpoint RandomState::checkpoint() const {
    return state_;
}
void RandomState::restore(const RandomCheckpoint& checkpoint) {
    if (checkpoint.version != random_policy_version || checkpoint.domain != state_.domain ||
        checkpoint.context != state_.context || !checkpoint.engine ||
        checkpoint.engine->id() != state_.engine->id())
        throw std::invalid_argument("Random checkpoint policy/domain/context/source mismatch");
    // Rebuild lookup/consumption from logical records. Reject malformed state
    // without disturbing the destination or replaying any decision.
    auto prepared = checkpoint;
    std::map<ChoiceKey, std::size_t> choices;
    std::set<ChoiceKey> consumed;
    if (prepared.controlled) {
        if (prepared.draws != 0 || !prepared.trace ||
            prepared.trace->limit != prepared.controlled->records.size())
            throw ChoiceError(ChoiceFailure::invalid_checkpoint);
        for (std::size_t i = 0; i < prepared.controlled->records.size(); ++i) {
            const auto& choice = prepared.controlled->records[i];
            validate_choice(choice);
            if (!choices.emplace(choice.key, i).second)
                throw ChoiceError(ChoiceFailure::duplicate_key, choice.key);
        }
    }
    if (prepared.trace) {
        const auto& trace = *prepared.trace;
        if (trace.events.size() > trace.limit ||
            (!prepared.controlled && trace.events.size() > prepared.draws))
            throw ChoiceError(ChoiceFailure::invalid_checkpoint);
        for (const auto& event : trace.events) {
            validate_choice(event);
            if (!consumed.insert(event.key).second)
                throw ChoiceError(ChoiceFailure::duplicate_key, event.key);
            if (prepared.controlled) {
                const auto found = choices.find(event.key);
                if (found == choices.end() || prepared.controlled->records[found->second] != event)
                    throw ChoiceError(ChoiceFailure::invalid_checkpoint, event.key);
            }
        }
    }
    using std::swap;
    static_assert(std::is_nothrow_swappable_v<RandomCheckpoint>);
    swap(state_, prepared);
    swap(choices_, choices);
    swap(consumed_, consumed);
}
} // namespace detail
} // namespace openciv::base
