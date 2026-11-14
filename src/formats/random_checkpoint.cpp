#include "formats/random_checkpoint.hpp"
#include "base/hash.hpp"
#include "formats/content_reader.hpp"
#include <bit>
#include <charconv>
#include <type_traits>

namespace openciv::formats {
namespace {
using namespace base;
using content::detail::Reader;
constexpr std::size_t max_bytes = 16 * 1024 * 1024, max_records = 16384, max_name = 4096;
constexpr std::string_view format = "openciv2-random-checkpoint/1";
std::string_view domain_name(RandomDomain domain) {
    switch (domain) {
    case RandomDomain::gameplay:
        return "gameplay";
    case RandomDomain::presentation_content:
        return "presentation-content";
    case RandomDomain::cosmetic:
        return "cosmetic";
    }
    throw std::invalid_argument("Unknown checkpoint random domain");
}
RandomDomain read_domain(const Reader& reader) {
    const auto name = reader.text();
    for (auto domain :
         {RandomDomain::gameplay, RandomDomain::presentation_content, RandomDomain::cosmetic})
        if (name == domain_name(domain))
            return domain;
    reader.error("unknown random domain");
}
template <typename T> T number(const Reader& reader) {
    const auto text = reader.text();
    T value{};
    const auto result = std::from_chars(text.data(), text.data() + text.size(), value);
    if (result.ec != std::errc{} || result.ptr != text.data() + text.size() ||
        std::to_string(value) != text)
        reader.error("expected canonical decimal string in range");
    return value;
}
std::string bytes_hex(std::string_view bytes) {
    return hex_bytes(std::span(reinterpret_cast<const std::uint8_t*>(bytes.data()), bytes.size()));
}
unsigned nibble(char c, const Reader& reader) {
    if (c >= '0' && c <= '9')
        return static_cast<unsigned>(c - '0');
    if (c >= 'a' && c <= 'f')
        return static_cast<unsigned>(c - 'a' + 10);
    reader.error("expected lowercase hexadecimal");
}
std::string read_bytes(const Reader& reader) {
    const auto text = reader.text();
    if (text.size() % 2 != 0 || text.size() > max_name * 2)
        reader.error("invalid byte string length");
    std::string result(text.size() / 2, '\0');
    for (std::size_t i = 0; i < result.size(); ++i)
        result[i] =
            static_cast<char>((nibble(text[2 * i], reader) << 4) | nibble(text[2 * i + 1], reader));
    return result;
}
std::string bits_hex(double value) {
    auto bits = std::bit_cast<std::uint64_t>(value);
    std::string text(16, '0');
    for (std::size_t i = 16; i != 0; --i) {
        text[i - 1] = "0123456789abcdef"[bits & 15];
        bits >>= 4;
    }
    return text;
}
double read_bits(const Reader& reader) {
    const auto text = reader.text();
    if (text.size() != 16)
        reader.error("expected 16 hexadecimal binary64 digits");
    std::uint64_t bits = 0;
    for (char c : text)
        bits = (bits << 4) | nibble(c, reader);
    return std::bit_cast<double>(bits);
}
void validate(const RandomCheckpoint& checkpoint) {
    if (checkpoint.context.size() > max_name)
        throw std::invalid_argument("Checkpoint context exceeds name limit");
    std::size_t count = 0, budget = 4096 + checkpoint.context.size();
    const auto records = [&](const std::vector<Choice>& choices) {
        if (choices.size() > max_records - count)
            throw std::invalid_argument("Checkpoint exceeds choice record limit");
        count += choices.size();
        for (const auto& choice : choices) {
            if (choice.key.site.size() > max_name || choice.key.call.size() > max_name)
                throw std::invalid_argument("Checkpoint choice name exceeds limit");
            const auto size = 512 + 2 * (choice.key.site.size() + choice.key.call.size());
            if (size > max_bytes - budget)
                throw std::invalid_argument("Checkpoint exceeds encoding budget");
            budget += size;
        }
    };
    if (checkpoint.controlled)
        records(checkpoint.controlled->records);
    if (checkpoint.trace) {
        if (checkpoint.trace->limit > UINT32_MAX)
            throw std::invalid_argument("Checkpoint trace capacity exceeds u32");
        records(checkpoint.trace->events);
    }
    base::detail::RandomState prepared(checkpoint.domain, 0, checkpoint.context);
    prepared.restore(checkpoint);
}
jt::Json encode_choice(const Choice& choice) {
    jt::Json result;
    result["siteHex"] = bytes_hex(choice.key.site);
    result["callHex"] = bytes_hex(choice.key.call);
    result["attempt"] = std::to_string(choice.key.attempt);
    auto& law = result["law"];
    std::visit(
        [&](const auto& value) {
            using T = std::decay_t<decltype(value)>;
            if constexpr (std::is_same_v<T, PrimaryCode>)
                law["kind"] = "primary-code";
            else if constexpr (std::is_same_v<T, PrimaryModulo>) {
                law["kind"] = "primary-modulo";
                law["n"] = std::to_string(value.n);
            } else if constexpr (std::is_same_v<T, SecondaryUnit>)
                law["kind"] = "secondary-unit";
            else {
                law["kind"] = "secondary-range";
                law["lo"] = std::to_string(value.lo);
                law["hi"] = std::to_string(value.hi);
            }
        },
        choice.law);
    result["code"] = std::visit([](auto value) { return std::to_string(value); }, choice.code);
    if (const auto* integer = std::get_if<std::int32_t>(&choice.result))
        result["result"]["i32"] = std::to_string(*integer);
    else
        result["result"]["f64"] = bits_hex(std::get<double>(choice.result));
    return result;
}
Choice read_choice(const Reader& reader) {
    reader.fields({"siteHex", "callHex", "attempt", "law", "code", "result"});
    ChoiceKey key{read_bytes(reader.at("siteHex")), read_bytes(reader.at("callHex")),
                  number<std::int32_t>(reader.at("attempt"))};
    const auto law = reader.at("law");
    const auto kind = law.at("kind").text();
    SamplingLaw sampling;
    if (kind == "primary-code") {
        law.fields({"kind"});
        sampling = PrimaryCode{};
    } else if (kind == "primary-modulo") {
        law.fields({"kind", "n"});
        sampling = PrimaryModulo{number<std::int32_t>(law.at("n"))};
    } else if (kind == "secondary-unit") {
        law.fields({"kind"});
        sampling = SecondaryUnit{};
    } else if (kind == "secondary-range") {
        law.fields({"kind", "lo", "hi"});
        sampling =
            SecondaryRange{number<std::int32_t>(law.at("lo")), number<std::int32_t>(law.at("hi"))};
    } else
        law.error("unknown sampling law");
    const bool primary = std::holds_alternative<PrimaryCode>(sampling) ||
                         std::holds_alternative<PrimaryModulo>(sampling);
    const auto code = reader.at("code");
    Choice result{std::move(key),
                  sampling,
                  primary ? ChoiceCode{number<std::int32_t>(code)}
                          : ChoiceCode{number<std::uint32_t>(code)},
                  {}};
    const auto observed = reader.at("result");
    if (std::holds_alternative<SecondaryUnit>(sampling)) {
        observed.fields({"f64"});
        result.result = read_bits(observed.at("f64"));
    } else {
        observed.fields({"i32"});
        result.result = number<std::int32_t>(observed.at("i32"));
    }
    validate_choice(result);
    return result;
}
jt::Json encode_choices(const std::vector<Choice>& choices) {
    jt::Json result;
    result.setArray();
    for (const auto& choice : choices)
        result.getArray().push_back(encode_choice(choice));
    return result;
}
std::vector<Choice> read_choices(const Reader& reader, std::size_t& remaining) {
    const auto size = reader.array_size();
    if (size > remaining)
        reader.error("too many checkpoint choice records");
    remaining -= size;
    std::vector<Choice> result;
    result.reserve(size);
    for (std::size_t i = 0; i < size; ++i)
        result.push_back(read_choice(reader.element(i)));
    return result;
}
} // namespace
std::string encode_random_checkpoint(const base::RandomCheckpoint& checkpoint,
                                     std::uint64_t replay_limit) {
    validate(checkpoint);
    const auto origin = base::seeded_random_state(*checkpoint.engine);
    if (!origin || origin->draws > replay_limit || origin->draws != checkpoint.draws)
        throw std::invalid_argument("Unsupported checkpoint source or replay position");
    jt::Json root;
    root["format"] = std::string(format);
    root["policy"]["id"] = std::string(base::random_policy_id);
    root["policy"]["version"] = std::to_string(checkpoint.version);
    root["domain"] = std::string(domain_name(checkpoint.domain));
    root["context"] = checkpoint.context;
    root["engineSeed"] = std::to_string(origin->seed);
    root["draws"] = std::to_string(origin->draws);
    root["controlled"] =
        checkpoint.controlled ? encode_choices(checkpoint.controlled->records) : jt::Json{};
    auto& trace = root["trace"];
    if (checkpoint.trace) {
        trace["limit"] = std::to_string(checkpoint.trace->limit);
        trace["events"] = encode_choices(checkpoint.trace->events);
    }
    return root.toString();
}
base::RandomCheckpoint decode_random_checkpoint(std::string_view json, std::uint64_t replay_limit) {
    auto document = content::parse_json(json, "random-checkpoint");
    const Reader root{document.value, document.numbers, "random-checkpoint"};
    root.fields(
        {"format", "policy", "domain", "context", "engineSeed", "draws", "controlled", "trace"});
    root.at("format").constant(format);
    const auto policy = root.at("policy");
    policy.fields({"id", "version"});
    policy.at("id").constant(base::random_policy_id);
    const auto version = number<unsigned>(policy.at("version"));
    if (version != base::random_policy_version)
        policy.error("unsupported random policy version");
    const base::SeededRandomState origin{number<std::uint64_t>(root.at("engineSeed")),
                                         number<std::uint64_t>(root.at("draws"))};
    if (origin.draws > replay_limit)
        root.error("checkpoint exceeds replay budget");
    base::RandomCheckpoint result{version, read_domain(root.at("domain")),
                                  root.at("context").text(), base::make_mt19937_source(origin.seed),
                                  origin.draws};
    std::size_t remaining = max_records;
    const auto controlled = root.at("controlled");
    if (!controlled.value.isNull())
        result.controlled = base::ControlledChoices{read_choices(controlled, remaining)};
    const auto trace = root.at("trace");
    if (!trace.value.isNull()) {
        trace.fields({"limit", "events"});
        result.trace = base::ChoiceTrace{number<std::uint32_t>(trace.at("limit")),
                                         read_choices(trace.at("events"), remaining)};
    }
    // Validate metadata/tapes first; rejected input cannot trigger replay work.
    validate(result);
    result.engine = base::restore_seeded_random(origin, replay_limit);
    return result;
}
} // namespace openciv::formats
