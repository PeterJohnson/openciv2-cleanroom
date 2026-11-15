#include "conformance_identity.hpp"

import std;
import openciv.base.arithmetic;
import openciv.tools.conformance_random;
import openciv.tools.conformance_script;
import openciv.tools.conformance_session;
import openciv.state.identities;
import openciv.world.geometry;

// Test-only X02 adapter for registered F01/F02/W01 foundation operations. No fixture
// expectations enter this process. Python owns fixture validation and comparison.

namespace {
template <class T> T number(std::string_view input) {
    T result{};
    const auto parsed = std::from_chars(input.data(), input.data() + input.size(), result);
    if (parsed.ec != std::errc{} || parsed.ptr != input.data() + input.size() ||
        std::to_string(result) != input)
        throw std::invalid_argument("Noncanonical or out-of-range argument");
    return result;
}
template <class T> std::string scalar(const char* type, T value) {
    return std::string{"{\"$type\":\""} + type + "\",\"value\":\"" + std::to_string(value) + "\"}";
}
template <class T> T input_number() {
    std::string input;
    if (!(std::cin >> input))
        throw std::invalid_argument("Missing registered state input");
    return number<T>(input);
}
template <class Range, class Encode> std::string array_json(const Range& values, Encode encode) {
    std::string result = "[";
    bool first = true;
    for (const auto value : values) {
        if (!first)
            result += ',';
        first = false;
        result += encode(value);
    }
    return result + "]";
}
} // namespace
int main(int argc, char** argv) {
    try {
        if (argc == 2 && std::string_view(argv[1]) == "--identity") {
            std::cout << oc2_conformance_identity << '\n';
            return 0;
        }
        if (argc < 2)
            throw std::invalid_argument("Expected a registered operation and decimal arguments");
        const std::string_view operation = argv[1];
        if (openciv::conformance::run_session(operation,
                                              {argv + 2, static_cast<std::size_t>(argc - 2)}))
            return 0;
        if (openciv::conformance::run_script(operation,
                                             {argv + 2, static_cast<std::size_t>(argc - 2)}))
            return 0;
        if (openciv::conformance::run_random(operation,
                                             {argv + 2, static_cast<std::size_t>(argc - 2)}))
            return 0;
        std::string arguments;
        std::function<std::string()> invoke;
        std::function<std::string()> snapshot = [] { return "{}"; };
        openciv::state::ProcessTokens process;
        std::uint32_t unit_end = 0;
        std::array<openciv::state::UnitToken, openciv::state::unit_capacity> token_backing;
        std::span<const openciv::state::UnitToken> scan;
        std::array<std::int16_t, 8> military_counts{};
        std::uint8_t humans = 0;
        if (operation == "F01.primaryModuloFromCode" && argc == 4) {
            const auto code = number<std::int32_t>(argv[2]);
            const auto n = number<std::int32_t>(argv[3]);
            arguments = "{\"code\":" + scalar("i32", code) + ",\"n\":" + scalar("i32", n) + "}";
            invoke = [code, n] {
                return scalar("i32", openciv::base::primary_modulo_from_code(code, n));
            };
        } else if (operation == "F01.secondaryRangeFromCode" && argc == 5) {
            const auto lo = number<std::int32_t>(argv[2]);
            const auto hi = number<std::int32_t>(argv[3]);
            const auto code = number<std::uint32_t>(argv[4]);
            arguments = "{\"lo\":" + scalar("i32", lo) + ",\"hi\":" + scalar("i32", hi) +
                        ",\"U\":" + scalar("u32", code) + "}";
            invoke = [lo, hi, code] {
                return scalar("i32", openciv::base::secondary_range_from_code(lo, hi, code));
            };
        } else if ((operation == "F02.assignUnitToken" || operation == "F02.assignCityToken") &&
                   argc == 2) {
            process.next_unit = input_number<std::uint32_t>();
            process.next_city = input_number<std::uint32_t>();
            arguments = "{}";
            invoke = [&process, operation] {
                const auto token = operation == "F02.assignUnitToken"
                                       ? static_cast<std::uint32_t>(process.assign_unit())
                                       : static_cast<std::uint32_t>(process.assign_city());
                return scalar("u32", token);
            };
            snapshot = [&process] {
                return "{\"F02.nextUnitToken\":" + scalar("u32", process.next_unit) +
                       ",\"F02.nextCityToken\":" + scalar("u32", process.next_city) + "}";
            };
        } else if (operation == "F02.reserveUnitSlot" && argc == 3) {
            const auto owner = number<std::uint8_t>(argv[2]);
            if (owner >= 8)
                throw std::invalid_argument("Civilization owner outside 0..7");
            unit_end = input_number<std::uint32_t>();
            if (unit_end > openciv::state::unit_capacity)
                throw std::invalid_argument("Unit scan bound exceeds capacity");
            for (std::uint32_t i = 0; i < unit_end; ++i)
                token_backing[i] = openciv::state::UnitToken{input_number<std::uint32_t>()};
            // Only the declared entry window is initialized, read and snapshotted.
            scan = std::span{token_backing}.first(unit_end);
            for (auto& count : military_counts)
                count = input_number<std::int16_t>();
            humans = input_number<std::uint8_t>();
            arguments = "{\"owner\":" + scalar("u8", owner) + "}";
            invoke = [&, owner] {
                const auto slot = openciv::state::reserve_unit_slot(
                    scan, unit_end, openciv::state::CivilizationSlot{owner}, military_counts[owner],
                    humans);
                return slot ? scalar("u16", static_cast<std::uint16_t>(*slot))
                            : "{\"$type\":\"null\"}";
            };
            snapshot = [&] {
                return "{\"F02.unitEnd\":" + scalar("u32", unit_end) + ",\"F02.unitTokenScan\":" +
                       array_json(scan,
                                  [](auto token) {
                                      return scalar("u32", static_cast<std::uint32_t>(token));
                                  }) +
                       ",\"F02.militaryCounts\":" +
                       array_json(military_counts,
                                  [](auto count) { return scalar("i16", count); }) +
                       ",\"F02.effectiveHumanSet\":" + scalar("u8", humans) + "}";
            };
        } else if ((operation == "W01.wrapX" && argc == 3) ||
                   (operation == "W01.tileIndex" && argc == 4) ||
                   ((operation == "W01.stepDistance" || operation == "W01.weightedDistance") &&
                    argc == 6)) {
            const auto width = input_number<std::int32_t>();
            const auto height = input_number<std::int32_t>();
            const openciv::world::Geometry geometry{width, height};
            const auto flat_code = operation == "W01.tileIndex" ? 0 : input_number<std::uint8_t>();
            if (flat_code > 1)
                throw std::invalid_argument("Flat-world boolean must be 0 or 1");
            const bool flat = flat_code != 0;
            if (operation == "W01.wrapX") {
                const auto x = number<std::int32_t>(argv[2]);
                arguments = "{\"x\":" + scalar("i32", x) + "}";
                invoke = [geometry, flat, x] { return scalar("i32", geometry.wrap_x(x, flat)); };
            } else if (operation == "W01.tileIndex") {
                const openciv::world::Position position{number<std::int32_t>(argv[2]),
                                                        number<std::int32_t>(argv[3])};
                arguments = "{\"x\":" + scalar("i32", position.x) +
                            ",\"y\":" + scalar("i32", position.y) + "}";
                invoke = [geometry, position] {
                    const auto index = geometry.tile_index(position);
                    return index ? scalar("u32", *index) : "{\"$type\":\"null\"}";
                };
            } else {
                const openciv::world::Position first{number<std::int32_t>(argv[2]),
                                                     number<std::int32_t>(argv[3])};
                const openciv::world::Position second{number<std::int32_t>(argv[4]),
                                                      number<std::int32_t>(argv[5])};
                arguments = "{\"x1\":" + scalar("i32", first.x) +
                            ",\"y1\":" + scalar("i32", first.y) +
                            ",\"x2\":" + scalar("i32", second.x) +
                            ",\"y2\":" + scalar("i32", second.y) + "}";
                invoke = [geometry, flat, first, second, operation] {
                    return scalar("i32", operation == "W01.stepDistance"
                                             ? geometry.step_distance(first, second, flat)
                                             : geometry.weighted_distance(first, second, flat));
                };
            }
            snapshot = [width, height, flat, operation] {
                std::string state = "{\"W01.width\":" + scalar("i32", width) +
                                    ",\"W01.height\":" + scalar("i32", height);
                if (operation != "W01.tileIndex")
                    state += std::string{",\"W01.flat\":{\"$type\":\"bool\",\"value\":"} +
                             (flat ? "true}" : "false}");
                return state + "}";
            };
        } else {
            throw std::invalid_argument("Unknown operation or wrong argument count");
        }
        if (operation.starts_with("F02.") || operation.starts_with("W01.")) {
            std::string extra;
            if (std::cin >> extra)
                throw std::invalid_argument("Unexpected trailing state input");
        }
        const std::string entry =
            "{\"seq\":0,\"id\":\"root.enter\",\"kind\":\"enter\",\"call\":\"root\","
            "\"payload\":{\"operation\":\"" +
            std::string(operation) + "\",\"parent\":null,\"arguments\":" + arguments + "}}";
        // Record entry before invoking the operation. Return/exit are only
        // constructed after normal completion; no expected fixture enters here.
        const auto value = invoke();
        const auto state = snapshot();
        std::cout << "{\"outcome\":\"return\",\"result\":" << value << ",\"state\":" << state
                  << ",\"trace\":[" << entry << ","
                  << "{\"seq\":1,\"id\":\"root.return\",\"kind\":\"return\",\"call\":\"root\","
                     "\"payload\":{\"result\":"
                  << value << "}},"
                  << "{\"seq\":2,\"id\":\"root.exit\",\"kind\":\"snapshot\",\"call\":\"root\","
                     "\"payload\":{\"label\":\"exit\",\"state\":"
                  << state << "}}]}\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 2;
    }
}
