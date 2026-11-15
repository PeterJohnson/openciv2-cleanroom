export module openciv.base.choices;

import std;

export extern "C++" {
    namespace openciv::base {
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
}
