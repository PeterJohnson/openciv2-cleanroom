#include "content_files.hpp"
#include <fstream>

namespace openciv::io {
ContentDirectory::ContentDirectory(const std::filesystem::path& root)
    : root_(std::filesystem::canonical(root)) {
    if (!std::filesystem::is_directory(root_))
        throw std::invalid_argument("Content mount must be a directory");
}
formats::content::Source ContentDirectory::read(std::string_view relative) {
    namespace fs = std::filesystem;
    formats::content::validate_relative_path(relative);
#ifdef _WIN32
    if (relative.find(':') != std::string_view::npos)
        throw std::invalid_argument("Windows content paths cannot select alternate data streams");
#endif
    const auto path =
        fs::canonical(root_ / fs::path(std::u8string(relative.begin(), relative.end())));
    auto parent = path.parent_path();
    while (!fs::equivalent(parent, root_)) {
        const auto next = parent.parent_path();
        if (next == parent || next.empty())
            throw std::invalid_argument("Content path leaves its mounted directory");
        parent = next;
    }
    if (!fs::is_regular_file(path))
        throw std::invalid_argument("Content source must be a regular file");
    fs::path identity = path;
    for (const auto& previous : backing_) {
        if (fs::equivalent(previous, path)) {
            identity = previous;
            break;
        }
    }
    std::ifstream file(path, std::ios::binary);
    if (!file)
        throw std::runtime_error("Cannot open content source");
    formats::Bytes bytes;
    std::array<char, 8192> buffer{};
    while (file) {
        file.read(buffer.data(), static_cast<std::streamsize>(buffer.size()));
        const auto count = static_cast<std::size_t>(file.gcount());
        if (bytes.size() + count > 16 * 1024 * 1024)
            throw std::runtime_error("Content source exceeds 16 MiB");
        bytes.insert(bytes.end(), buffer.begin(),
                     buffer.begin() + static_cast<std::ptrdiff_t>(count));
    }
    if (!file.eof())
        throw std::runtime_error("Content source read failed");
    backing_.push_back(path);
    const auto utf8 = identity.generic_u8string();
    return {std::string(utf8.begin(), utf8.end()), std::move(bytes)};
}
} // namespace openciv::io
