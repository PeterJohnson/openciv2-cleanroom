import std;
import openciv.formats.content_json;

namespace openciv::formats::content {
namespace {
bool digit(char c) {
    return c >= '0' && c <= '9';
}
[[noreturn]] void fail(std::string_view source, std::size_t offset, std::string message) {
    throw Error("invalid-json", std::string(source) + "@" + std::to_string(offset),
                std::move(message));
}
unsigned hex4(std::string_view raw, std::size_t& offset, std::string_view source) {
    unsigned value = 0;
    for (unsigned i = 0; i < 4; ++i) {
        if (offset == raw.size())
            fail(source, offset, "incomplete Unicode escape");
        const char c = raw[offset++];
        unsigned n;
        if (digit(c))
            n = static_cast<unsigned>(c - '0');
        else if (c >= 'a' && c <= 'f')
            n = static_cast<unsigned>(c - 'a') + 10;
        else if (c >= 'A' && c <= 'F')
            n = static_cast<unsigned>(c - 'A') + 10;
        else
            fail(source, offset - 1, "invalid Unicode escape");
        value = value * 16 + n;
    }
    return value;
}
std::size_t string_end(std::string_view raw, std::size_t start, std::string_view source) {
    std::size_t i = start + 1;
    while (i < raw.size()) {
        const char c = raw[i++];
        if (c == '"')
            return i;
        if (c != '\\')
            continue; // jt::Json validates UTF-8 and raw controls.
        if (i == raw.size())
            fail(source, i, "unfinished escape");
        const char escape = raw[i++];
        if (escape == 'u') {
            const auto first = hex4(raw, i, source);
            if (first >= 0xdc00 && first <= 0xdfff)
                fail(source, i, "unpaired low surrogate");
            if (first >= 0xd800 && first <= 0xdbff) {
                if (i + 2 > raw.size() || raw.substr(i, 2) != "\\u")
                    fail(source, i, "unpaired high surrogate");
                i += 2;
                const auto second = hex4(raw, i, source);
                if (second < 0xdc00 || second > 0xdfff)
                    fail(source, i, "unpaired high surrogate");
            }
        } else if (std::string_view("\"\\/bfnrt").find(escape) == std::string_view::npos) {
            fail(source, i - 1, "non-JSON string escape");
        }
    }
    fail(source, i, "unfinished string");
}
// Supplement the library's deliberately permissive policy without replacing its
// JSON grammar/DOM. A second DOM contains only raw numeric tokens; all other
// scalar values are null. Object keys and array positions remain identical.
std::string numeric_document(std::string_view raw, std::string_view source) {
    struct Frame {
        bool object, key;
        std::set<std::string> keys;
    };
    std::vector<Frame> stack;
    std::string result;
    result.reserve(raw.size());
    for (std::size_t i = 0; i < raw.size();) {
        const char c = raw[i];
        if (c == '"') {
            const auto end = string_end(raw, i, source);
            const auto token = std::string(raw.substr(i, end - i));
            if (!stack.empty() && stack.back().object && stack.back().key) {
                auto [status, key] = jt::Json::parse(token);
                if (status != jt::Json::success || !key.isString())
                    fail(source, i, "invalid key");
                if (!stack.back().keys.insert(key.getString()).second)
                    fail(source, i, "duplicate object key");
                stack.back().key = false;
                result += token;
            } else
                result += "null";
            i = end;
        } else if (c == '-' || digit(c)) {
            const auto start = i++;
            while (i < raw.size() && (digit(raw[i]) || std::string_view(".eE+-").find(raw[i]) !=
                                                           std::string_view::npos))
                ++i;
            result += '"';
            result += raw.substr(start, i - start);
            result += '"';
        } else {
            if (c == '{' || c == '[')
                stack.push_back({c == '{', c == '{', {}});
            else if (c == '}' || c == ']') {
                if (stack.empty())
                    fail(source, i, "unbalanced structure");
                stack.pop_back();
            } else if (c == ',' && !stack.empty() && stack.back().object)
                stack.back().key = true;
            if (raw.substr(i, 4) == "true") {
                result += "null";
                i += 4;
            } else if (raw.substr(i, 5) == "false") {
                result += "null";
                i += 5;
            } else {
                result += c;
                ++i;
            }
        }
    }
    return result;
}
} // namespace
JsonDocument parse_json(std::string_view bytes, std::string_view source) {
    if (bytes.size() > 16 * 1024 * 1024)
        throw Error("resource-limit", std::string(source), "JSON document exceeds 16 MiB");
    auto [status, value] = jt::Json::parse(std::string(bytes));
    if (status != jt::Json::success) {
        const auto code = status == jt::Json::depth_exceeded || status == jt::Json::stack_overflow
                              ? "resource-limit"
                              : "invalid-json";
        throw Error(code, std::string(source), jt::Json::StatusToString(status));
    }
    auto [mask_status, numbers] = jt::Json::parse(numeric_document(bytes, source));
    if (mask_status != jt::Json::success)
        throw Error("invalid-json", std::string(source), "invalid numeric token structure");
    return {std::move(value), std::move(numbers)};
}
std::string integer_text(std::string_view token, std::string_view location) {
    const auto reject = [&]() -> void {
        throw Error("invalid-integer", std::string(location),
                    "expected an exact mathematical integer");
    };
    if (token.empty()) {
        reject();
        return {};
    }
    const bool negative = token.front() == '-';
    std::size_t i = negative ? 1 : 0;
    std::string digits;
    if (i == token.size() || !digit(token[i])) {
        reject();
        return {};
    }
    if (token[i] == '0') {
        digits += token[i++];
    } else
        while (i < token.size() && digit(token[i]))
            digits += token[i++];
    std::int64_t fraction = 0;
    if (i < token.size() && token[i] == '.') {
        ++i;
        const auto start = i;
        while (i < token.size() && digit(token[i])) {
            digits += token[i++];
            ++fraction;
        }
        if (i == start)
            reject();
    }
    std::int64_t exponent = 0;
    if (i < token.size() && (token[i] == 'e' || token[i] == 'E')) {
        ++i;
        bool minus = false;
        if (i < token.size() && (token[i] == '+' || token[i] == '-'))
            minus = token[i++] == '-';
        const auto start = i;
        // Saturation exceeds both input and expansion limits, while allowing
        // any exponent spelling of zero to normalize to zero.
        while (i < token.size() && digit(token[i]))
            exponent = std::min<std::int64_t>(100000000, exponent * 10 + token[i++] - '0');
        if (i == start)
            reject();
        if (minus)
            exponent = -exponent;
    }
    if (i != token.size())
        reject();
    const auto nonzero = digits.find_first_not_of('0');
    if (nonzero == std::string::npos)
        return "0";
    digits.erase(0, nonzero);
    const auto scale = exponent - fraction;
    if (scale < 0) {
        const auto remove = static_cast<std::uint64_t>(-scale);
        if (remove >= digits.size() ||
            !std::all_of(digits.end() - static_cast<std::ptrdiff_t>(remove), digits.end(),
                         [](char c) { return c == '0'; }))
            reject();
        digits.resize(digits.size() - static_cast<std::size_t>(remove));
    } else {
        if (scale > 16 * 1024 * 1024 ||
            digits.size() + static_cast<std::uint64_t>(scale) > 16 * 1024 * 1024)
            throw Error("resource-limit", std::string(location),
                        "integer expansion exceeds 16 MiB");
        digits.append(static_cast<std::size_t>(scale), '0');
    }
    return negative ? "-" + digits : digits;
}
std::int32_t integer32(std::string_view token, std::string_view location) {
    const auto decimal = integer_text(token, location);
    std::int32_t value{};
    const auto result = std::from_chars(decimal.data(), decimal.data() + decimal.size(), value);
    if (result.ec != std::errc{} || result.ptr != decimal.data() + decimal.size())
        throw Error("integer-range", std::string(location),
                    "integer outside signed32 authoring domain");
    return value;
}
} // namespace openciv::formats::content
