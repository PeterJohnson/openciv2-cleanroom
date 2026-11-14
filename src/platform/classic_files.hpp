#pragma once

#include "session/classic_save.hpp"
#include <filesystem>

namespace openciv::io {
// Native F03/F06 services with an explicit working directory. They do not change
// the process cwd, merge files, or publish atomically. ASCII component matching is
// case insensitive on every host; ambiguous aliases stop. Non-ASCII folding and
// embedded NUL/control-Z text semantics remain outside this adapter's domain.
class ClassicFiles {
public:
    explicit ClassicFiles(const std::filesystem::path& working);
    void change_directory(std::string_view directory);
    bool exists(std::string_view directory, std::string_view name) const;
    std::unique_ptr<session::CoreInput> open_input(std::string_view path) const;
    std::unique_ptr<session::CoreOutput> open_output(std::string_view path) const;
    formats::classic::OpenedText open_text(std::string_view directory, std::string_view name) const;
    const std::filesystem::path& working() const noexcept { return working_; }

private:
    std::optional<std::filesystem::path> resolve(std::string_view directory, std::string_view name,
                                                 bool missing_leaf = false) const;
    std::filesystem::path working_;
};
// Absolute native path represented with F06's backslash separators. This is a
// path adapter, not a reinterpretation of gameplay text or its Windows-1252 data.
std::string classic_path(const std::filesystem::path& path);
} // namespace openciv::io
