export module openciv.tools.conformance_io;

import std;

export extern "C++" {
    namespace openciv::conformance::io {
    template <class T> T number(std::string_view text) {
        T value{};
        const auto result = std::from_chars(text.data(), text.data() + text.size(), value);
        if (result.ec != std::errc{} || result.ptr != text.data() + text.size() ||
            std::to_string(value) != text)
            throw std::invalid_argument("Noncanonical adapter input integer");
        return value;
    }
    inline std::string word() {
        std::string value;
        if (!(std::cin >> value))
            throw std::invalid_argument("Missing adapter input");
        return value;
    }
    template <class T> T input() {
        return number<T>(word());
    }
    inline std::string unhex(std::string_view value) {
        if (value.empty() || value.size() % 2)
            throw std::invalid_argument("Invalid encoded adapter identity");
        std::string result;
        for (std::size_t i = 0; i < value.size(); i += 2) {
            unsigned byte = 0;
            const auto parsed = std::from_chars(value.data() + i, value.data() + i + 2, byte, 16);
            if (parsed.ec != std::errc{} || parsed.ptr != value.data() + i + 2)
                throw std::invalid_argument("Invalid encoded adapter identity");
            result += static_cast<char>(byte);
        }
        return result;
    }
    inline std::string quote(std::string_view value) {
        constexpr char digits[] = "0123456789abcdef";
        std::string result = "\"";
        for (const char byte : value) {
            const auto c = static_cast<unsigned char>(byte);
            if (c == '"' || c == '\\') {
                result += '\\';
                result += static_cast<char>(c);
            } else if (c < 32) {
                result += "\\u00";
                result += digits[c >> 4];
                result += digits[c & 15];
            } else
                result += static_cast<char>(c);
        }
        return result + '"';
    }
    inline std::string text(std::string_view value) {
        return "{\"$type\":\"text\",\"value\":" + quote(value) + "}";
    }
    template <class T> std::string scalar(std::string_view kind, T value) {
        return "{\"$type\":" + quote(kind) + ",\"value\":" + quote(std::to_string(value)) + "}";
    }
    } // namespace openciv::conformance::io
}
