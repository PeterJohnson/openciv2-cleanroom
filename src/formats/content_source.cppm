export module openciv.formats.content_source;
import openciv.formats.content_json;

import std;
export import openciv.base.callback_ref;
export import openciv.formats.bytes;

export namespace openciv::formats::content {
/// Owned source bytes and a stable backing identity used for content receipts.
struct Source {
    std::string identity; // Stable backing identity, including aliases.
    Bytes bytes;
};
// Only explicitly mounted sources; adapters enforce containment and never fetch URLs.
using Resolver = base::CallbackRef<Source(std::string_view)>;
/// A mounted relative path, resolved source identity, and verified content hash.
struct FileReceipt {
    std::string path, identity, sha256;
};
/// Reject absolute paths, traversal components, schemes, and invalid mounted-path syntax.
void validate_relative_path(std::string_view path);
} // namespace openciv::formats::content

module :private;
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
