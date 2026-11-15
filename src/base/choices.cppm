export module openciv.base.choices;
import openciv.base.arithmetic;

import std;

export namespace openciv::base {
// F01 §§2–4,6; X02 §§2,5–6. These are semantic identities, never draw indices.
struct ChoiceKey {
    std::string site;
    std::string call;
    std::int32_t attempt = 0;
    auto operator<=>(const ChoiceKey&) const = default;
};
struct PrimaryCode {
    bool operator==(const PrimaryCode&) const = default;
};
struct PrimaryModulo {
    std::int32_t n;
    bool operator==(const PrimaryModulo&) const = default;
};
struct SecondaryUnit {
    bool operator==(const SecondaryUnit&) const = default;
};
struct SecondaryRange {
    std::int32_t lo, hi;
    bool operator==(const SecondaryRange&) const = default;
};
using SamplingLaw = std::variant<PrimaryCode, PrimaryModulo, SecondaryUnit, SecondaryRange>;
using ChoiceCode = std::variant<std::int32_t, std::uint32_t>;
using ChoiceResult = std::variant<std::int32_t, double>;
struct Choice {
    ChoiceKey key;
    SamplingLaw law;
    ChoiceCode code;
    ChoiceResult result;
    // X02 f64 comparisons retain the exact bit pattern, including signed zero.
    bool operator==(const Choice& other) const;
};
enum class ChoiceFailure {
    invalid_record,
    duplicate_key,
    semantic_key_required,
    missing,
    already_consumed,
    law_mismatch,
    unused,
    trace_limit,
    invalid_checkpoint
};
std::string_view choice_failure_code(ChoiceFailure failure) noexcept;
class ChoiceError : public std::runtime_error {
public:
    explicit ChoiceError(ChoiceFailure failure, std::optional<ChoiceKey> key = {});
    ChoiceFailure failure() const noexcept { return failure_; }
    const std::optional<ChoiceKey>& key() const noexcept { return key_; }

private:
    ChoiceFailure failure_;
    std::optional<ChoiceKey> key_;
};
// Construct/validate the F01 mapping independently of a generator or tape.
ChoiceResult choice_result(SamplingLaw law, ChoiceCode code);
Choice make_choice(ChoiceKey key, SamplingLaw law, ChoiceCode code);
void validate_choice(const Choice& choice);
struct ControlledChoices {
    std::vector<Choice> records;
    bool operator==(const ControlledChoices&) const = default;
};
struct ChoiceTrace {
    // An explicit harness budget, not game state or a game-clock timeout.
    std::size_t limit;
    std::vector<Choice> events;
    bool operator==(const ChoiceTrace&) const = default;
};
} // namespace openciv::base

module :private;
namespace openciv::base {
namespace {
bool equal_result(const ChoiceResult& left, const ChoiceResult& right) {
    if (left.index() != right.index())
        return false;
    if (const auto* value = std::get_if<double>(&left))
        return std::bit_cast<std::uint64_t>(*value) ==
               std::bit_cast<std::uint64_t>(std::get<double>(right));
    return left == right;
}
} // namespace
bool Choice::operator==(const Choice& other) const {
    return key == other.key && law == other.law && code == other.code &&
           equal_result(result, other.result);
}
std::string_view choice_failure_code(ChoiceFailure failure) noexcept {
    switch (failure) {
    case ChoiceFailure::invalid_record:
        return "invalid_record";
    case ChoiceFailure::duplicate_key:
        return "duplicate_key";
    case ChoiceFailure::semantic_key_required:
        return "semantic_key_required";
    case ChoiceFailure::missing:
        return "missing";
    case ChoiceFailure::already_consumed:
        return "already_consumed";
    case ChoiceFailure::law_mismatch:
        return "law_mismatch";
    case ChoiceFailure::unused:
        return "unused";
    case ChoiceFailure::trace_limit:
        return "trace_limit";
    case ChoiceFailure::invalid_checkpoint:
        return "invalid_checkpoint";
    }
    return "unknown";
}
ChoiceError::ChoiceError(ChoiceFailure failure, std::optional<ChoiceKey> key)
    : std::runtime_error("Random choice: " + std::string(choice_failure_code(failure))),
      failure_(failure), key_(std::move(key)) {}
ChoiceResult choice_result(SamplingLaw law, ChoiceCode code) {
    ChoiceResult result;
    if (std::holds_alternative<PrimaryCode>(law) || std::holds_alternative<PrimaryModulo>(law)) {
        const auto* value = std::get_if<std::int32_t>(&code);
        const auto* modulo = std::get_if<PrimaryModulo>(&law);
        if (!value || *value < 0 || *value > 32767 || (modulo && modulo->n <= 0))
            throw ChoiceError(ChoiceFailure::invalid_record);
        result = modulo ? primary_modulo_from_code(*value, modulo->n) : *value;
    } else {
        const auto* value = std::get_if<std::uint32_t>(&code);
        if (!value)
            throw ChoiceError(ChoiceFailure::invalid_record);
        if (const auto* range = std::get_if<SecondaryRange>(&law))
            result = secondary_range_from_code(range->lo, range->hi, *value);
        else
            result = secondary_unit_from_code(*value);
    }
    return result;
}
Choice make_choice(ChoiceKey key, SamplingLaw law, ChoiceCode code) {
    if (key.site.empty() || key.call.empty() || key.attempt < 0)
        throw ChoiceError(ChoiceFailure::invalid_record, key);
    try {
        auto result = choice_result(law, code);
        return {std::move(key), law, code, result};
    } catch (const ChoiceError& error) {
        throw ChoiceError(error.failure(), key);
    }
}
void validate_choice(const Choice& choice) {
    if (choice != make_choice(choice.key, choice.law, choice.code))
        throw ChoiceError(ChoiceFailure::invalid_record, choice.key);
}
} // namespace openciv::base
