#include "content_source.hpp"
#include "content_json.hpp"
#include <algorithm>

namespace openciv::formats::content {
void validate_relative_path(std::string_view path) {
    const auto bad = [&] {
        throw Error("invalid-path", std::string(path),
                    "expected a relative slash-separated mounted path");
    };
    if (path.empty() || path.front() == '/' || path.find('\\') != std::string_view::npos)
        bad();
    for (char c : path)
        if (static_cast<unsigned char>(c) < 32 || c == 127)
            bad();
    const auto colon = path.find(':');
    if (colon != std::string_view::npos) {
        const auto scheme = path.substr(0, colon);
        if (!scheme.empty() &&
            ((scheme.front() >= 'A' && scheme.front() <= 'Z') ||
             (scheme.front() >= 'a' && scheme.front() <= 'z')) &&
            std::all_of(scheme.begin(), scheme.end(), [](char c) {
                return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
                       c == '+' || c == '-' || c == '.';
            }))
            bad();
    }
    while (!path.empty()) {
        const auto slash = path.find('/');
        const auto part = path.substr(0, slash);
        if (part.empty() || part == "." || part == "..")
            bad();
        if (slash == std::string_view::npos)
            return;
        path.remove_prefix(slash + 1);
    }
    bad(); // trailing slash
}
} // namespace openciv::formats::content
