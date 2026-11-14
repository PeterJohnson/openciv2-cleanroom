#pragma once

#include "formats/bytes.hpp"
#include <filesystem>

namespace openciv::io {
// Native read-only client adapter. Portable formats/queries never open a path.
formats::Bytes read_file(const std::filesystem::path& path);
} // namespace openciv::io
