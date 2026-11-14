#pragma once
#include "formats/gameplay.hpp"
#include <filesystem>

namespace openciv::io {
class ContentDirectory {
public:
    explicit ContentDirectory(const std::filesystem::path& mounted_root);
    formats::content::Source read(std::string_view relative);

private:
    std::filesystem::path root_;
    std::vector<std::filesystem::path> backing_;
};
} // namespace openciv::io
