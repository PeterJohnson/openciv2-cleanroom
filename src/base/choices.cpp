#include "base/choices.hpp"
#include "base/arithmetic.hpp"
#include <bit>
#include <utility>

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
