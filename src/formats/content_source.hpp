#pragma once
#include "bytes.hpp"
#include <functional>
#include <string>
#include <string_view>

namespace openciv::formats::content {
struct Source {
    std::string identity; // Stable backing identity, including aliases.
    Bytes bytes;
};
// Only explicitly mounted sources; adapters enforce containment and never fetch URLs.
using Resolver = std::function<Source(std::string_view)>;
struct FileReceipt {
    std::string path, identity, sha256;
};
void validate_relative_path(std::string_view path);
} // namespace openciv::formats::content
