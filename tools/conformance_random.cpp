module openciv.tools.conformance_random;

import std;
import openciv.tools.conformance_io;
import openciv.base.random;

// X02 §§2,5–6: test-only sampler adapter. No expected operation result enters here.

namespace openciv::conformance {
namespace {
using namespace io;
std::string result_json(const base::ChoiceResult& value) {
    if (const auto* integer = std::get_if<std::int32_t>(&value))
        return scalar("i32", *integer);
    std::ostringstream bits;
    bits.imbue(std::locale::classic());
    bits << std::hex << std::setfill('0') << std::setw(16)
         << std::bit_cast<std::uint64_t>(std::get<double>(value));
    return "{\"$type\":\"f64\",\"bits\":" + quote(bits.str()) + "}";
}
std::string law_name(const base::SamplingLaw& law) {
    constexpr std::string_view names[]{"primary-code", "primary-modulo", "secondary-unit",
                                       "secondary-range"};
    return std::string(names[law.index()]);
}
std::string law_arguments(const base::SamplingLaw& law) {
    if (const auto* modulo = std::get_if<base::PrimaryModulo>(&law))
        return "{\"n\":" + scalar("i32", modulo->n) + "}";
    if (const auto* range = std::get_if<base::SecondaryRange>(&law))
        return "{\"lo\":" + scalar("i32", range->lo) + ",\"hi\":" + scalar("i32", range->hi) + "}";
    return "{}";
}
base::ControlledChoices read_choices() {
    base::ControlledChoices choices;
    const auto count = input<std::uint32_t>();
    if (count > 100000)
        throw std::invalid_argument("Choice count exceeds adapter input bound");
    for (std::uint32_t i = 0; i < count; ++i) {
        base::ChoiceKey key{unhex(word()), unhex(word()), input<std::int32_t>()};
        base::SamplingLaw law;
        const auto index = input<unsigned>();
        switch (index) {
        case 0:
            law = base::PrimaryCode{};
            break;
        case 1:
            law = base::PrimaryModulo{input<std::int32_t>()};
            break;
        case 2:
            law = base::SecondaryUnit{};
            break;
        case 3:
            law = base::SecondaryRange{input<std::int32_t>(), input<std::int32_t>()};
            break;
        default:
            throw std::invalid_argument("Unknown sampling law");
        }
        const auto code = index < 2 ? base::ChoiceCode{input<std::int32_t>()}
                                    : base::ChoiceCode{input<std::uint32_t>()};
        choices.records.push_back(base::make_choice(std::move(key), law, code));
    }
    return choices;
}
std::string event(unsigned sequence, std::string_view id, std::string_view kind,
                  const std::string& payload) {
    return "{\"seq\":" + std::to_string(sequence) + ",\"id\":" + quote(id) +
           ",\"kind\":" + quote(kind) + ",\"call\":\"root\",\"payload\":" + payload + "}";
}
template <base::RandomDomain Domain>
void execute(std::string_view operation, std::string_view domain, const base::SamplingLaw& law,
             bool replay) {
    std::uint64_t seed = 0, draws = 0;
    if (replay) {
        seed = input<std::uint64_t>();
        draws = input<std::uint64_t>();
    }
    base::RandomContext<Domain> random =
        replay ? base::RandomContext<Domain>{seed, "fixture"}
               : base::RandomContext<Domain>{read_choices(), "fixture"};
    if (replay) {
        auto entry = random.checkpoint();
        entry.engine->discard(draws);
        entry.draws = draws;
        random.restore(entry);
        random.record_choices(1);
    }
    std::string extra;
    if (std::cin >> extra)
        throw std::invalid_argument("Trailing random input");
    const auto arguments = law_arguments(law);
    const std::string boundary_args =
        "{\"domain\":" + text(domain) + (arguments == "{}" ? "}" : "," + arguments.substr(1));
    std::string trace = event(0, "root.enter", "enter",
                              "{\"operation\":" + quote(operation) +
                                  ",\"parent\":null,\"arguments\":" + boundary_args + "}");
    const base::ChoiceKey key{std::string(operation), "root", 0};
    base::ChoiceResult value;
    if (std::holds_alternative<base::PrimaryCode>(law))
        value = random.primary_code(key);
    else if (const auto* modulo = std::get_if<base::PrimaryModulo>(&law))
        value = random.primary_modulo(key, modulo->n);
    else if (std::holds_alternative<base::SecondaryUnit>(law))
        value = random.secondary_unit(key);
    else {
        const auto range = std::get<base::SecondaryRange>(law);
        value = random.secondary_range(key, range.lo, range.hi);
    }
    random.require_choices_consumed();
    const auto final = random.checkpoint();
    const std::string context = std::string(domain) + "/fixture";
    std::string state = "{}";
    if (replay) {
        // The seed+position representation is complete only if it reconstructs
        // the exact generator. Check this invariant before claiming a snapshot.
        auto reconstructed = base::RandomContext<Domain>{seed, "fixture"}.checkpoint();
        reconstructed.engine->discard(final.draws);
        if (!reconstructed.engine->same_state(*final.engine))
            throw std::logic_error(
                "Generator cannot be represented by its recorded origin and position");
        state = "{\"F01.randomContexts\":{" + quote(context) + ":{\"domain\":" + text(domain) +
                ",\"name\":" + text("fixture") + ",\"seed\":" + scalar("u64", seed) +
                ",\"draws\":" + scalar("u64", final.draws) + "}}}";
    }
    unsigned sequence = 1;
    for (const auto& choice : final.trace->events) {
        const auto code = std::visit(
            [](auto c) {
                return scalar(std::is_same_v<decltype(c), std::int32_t> ? "i32" : "u32", c);
            },
            choice.code);
        trace += "," + event(sequence++, "root.sample", "random",
                             "{\"site\":" + quote(choice.key.site) +
                                 ",\"attempt\":0,\"law\":" + quote(law_name(choice.law)) +
                                 ",\"arguments\":" + law_arguments(choice.law) +
                                 ",\"code\":" + code + ",\"result\":" + result_json(choice.result) +
                                 (replay ? ",\"context\":" + text(context) : "") + "}");
    }
    const auto result = result_json(value);
    trace += "," + event(sequence++, "root.return", "return", "{\"result\":" + result + "}");
    trace += "," + event(sequence, "root.exit", "snapshot",
                         "{\"label\":\"exit\",\"state\":" + state + "}");
    std::cout << "{\"outcome\":\"return\",\"result\":" << result << ",\"state\":" << state
              << ",\"trace\":[" << trace << "]}\n";
}
} // namespace
bool run_random(std::string_view operation, std::span<char*> args) {
    base::SamplingLaw law;
    std::size_t expected = 1;
    if (operation == "F01.primaryCode")
        law = base::PrimaryCode{};
    else if (operation == "F01.primaryModulo") {
        law = base::PrimaryModulo{0};
        expected = 2;
    } else if (operation == "F01.secondaryUnit")
        law = base::SecondaryUnit{};
    else if (operation == "F01.secondaryRange") {
        law = base::SecondaryRange{0, 0};
        expected = 3;
    } else
        return false;
    if (args.size() != expected)
        throw std::invalid_argument("Wrong sampler argument count");
    if (expected == 2)
        law = base::PrimaryModulo{number<std::int32_t>(args[1])};
    if (expected == 3)
        law = base::SecondaryRange{number<std::int32_t>(args[1]), number<std::int32_t>(args[2])};
    const auto mode = word();
    const auto domain = input<unsigned>();
    constexpr std::string_view domains[]{"gameplay", "presentation-content", "cosmetic"};
    if ((mode != "controlled-choices" && mode != "implementation-replay") || domain >= 3 ||
        args[0] != domains[domain])
        throw std::invalid_argument("Unknown or inconsistent random mode/domain");
    const bool replay = mode == "implementation-replay";
    switch (domain) {
    case 0:
        execute<base::RandomDomain::gameplay>(operation, domains[domain], law, replay);
        break;
    case 1:
        execute<base::RandomDomain::presentation_content>(operation, domains[domain], law, replay);
        break;
    case 2:
        execute<base::RandomDomain::cosmetic>(operation, domains[domain], law, replay);
        break;
    }
    return true;
}
} // namespace openciv::conformance
