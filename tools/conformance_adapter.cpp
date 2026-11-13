// Test-only X02 adapter for the two registered pure F01 operations. No fixture
// expectations enter this process. Python owns fixture validation and comparison.
#include "base/arithmetic.hpp"
#include "conformance_identity.hpp"
#include <charconv>
#include <functional>
#include <iostream>
#include <stdexcept>
#include <string>
#include <string_view>

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
} // namespace
int main(int argc, char** argv) {
    try {
        if (argc == 2 && std::string_view(argv[1]) == "--identity") {
            std::cout << oc2_conformance_identity << '\n';
            return 0;
        }
        if (argc < 2)
            throw std::invalid_argument(
                "Expected a registered F01 operation and decimal arguments");
        const std::string_view operation = argv[1];
        std::string arguments;
        std::function<std::int32_t()> invoke;
        if (operation == "F01.primaryModuloFromCode" && argc == 4) {
            const auto code = number<std::int32_t>(argv[2]);
            const auto n = number<std::int32_t>(argv[3]);
            arguments = "{\"code\":" + scalar("i32", code) + ",\"n\":" + scalar("i32", n) + "}";
            invoke = [code, n] { return openciv::base::primary_modulo_from_code(code, n); };
        } else if (operation == "F01.secondaryRangeFromCode" && argc == 5) {
            const auto lo = number<std::int32_t>(argv[2]);
            const auto hi = number<std::int32_t>(argv[3]);
            const auto code = number<std::uint32_t>(argv[4]);
            arguments = "{\"lo\":" + scalar("i32", lo) + ",\"hi\":" + scalar("i32", hi) +
                        ",\"U\":" + scalar("u32", code) + "}";
            invoke = [lo, hi, code] {
                return openciv::base::secondary_range_from_code(lo, hi, code);
            };
        } else {
            throw std::invalid_argument("Unknown operation or wrong argument count");
        }
        const std::string entry =
            "{\"seq\":0,\"id\":\"root.enter\",\"kind\":\"enter\",\"call\":\"root\","
            "\"payload\":{\"operation\":\"" +
            std::string(operation) + "\",\"parent\":null,\"arguments\":" + arguments + "}}";
        // Record entry before invoking the pure operation. Return/exit are only
        // constructed after normal completion; no expected fixture enters here.
        const auto value = scalar("i32", invoke());
        std::cout << "{\"outcome\":\"return\",\"result\":" << value << ",\"state\":{},\"trace\":["
                  << entry << ","
                  << "{\"seq\":1,\"id\":\"root.return\",\"kind\":\"return\",\"call\":\"root\","
                     "\"payload\":{\"result\":"
                  << value << "}},"
                  << "{\"seq\":2,\"id\":\"root.exit\",\"kind\":\"snapshot\",\"call\":\"root\","
                     "\"payload\":{\"label\":\"exit\",\"state\":{}}}]}\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 2;
    }
}
