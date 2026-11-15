#pragma once

#include "session/file_storage.hpp"
#include "formats/classic_text_reader.hpp"
#include <filesystem>

namespace openciv::io {
// Native F03/F06 services with an explicit working directory. They do not change
// the process cwd, merge files, or publish atomically. ASCII component matching is
// case insensitive on every host; ambiguous aliases stop. Non-ASCII folding and
// embedded NUL/control-Z text semantics remain outside this adapter's domain.
class ClassicFiles : public session::FileStorage {
public:
    explicit ClassicFiles(const std::filesystem::path& working);
    void change_directory(std::string_view directory);
    bool exists(std::string_view directory, std::string_view name) const;
    std::unique_ptr<session::CoreInput> open_input(std::string_view path) const;
    std::unique_ptr<session::CoreOutput> open_output(std::string_view path) const;
    bool remove_file(std::string_view path) const;
    formats::classic::OpenedText open_text(std::string_view directory, std::string_view name) const;
    const std::filesystem::path& working() const noexcept { return working_; }

    std::unique_ptr<session::CoreInput> read(std::string_view path) const override {
        return open_input(path);
    }
    std::unique_ptr<session::CoreOutput> replace(std::string_view path) const override {
        return open_output(path);
    }
    bool remove(std::string_view path) const override { return remove_file(path); }

private:
    std::optional<std::filesystem::path> resolve(std::string_view directory, std::string_view name,
                                                 bool missing_leaf = false) const;
    std::filesystem::path working_;
};
// Absolute native path represented with F06's backslash separators. This is a
// path adapter, not a reinterpretation of gameplay text or its Windows-1252 data.
std::string classic_path(const std::filesystem::path& path);
} // namespace openciv::io
