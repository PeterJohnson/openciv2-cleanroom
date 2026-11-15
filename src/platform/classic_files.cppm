export module openciv.platform.classic_files;

import std;
export import openciv.io.storage;
export import openciv.formats.classic_text_reader;

export namespace openciv::io {
/// Native F03/F06 services with an explicit working directory. They do not change
/// the process cwd, merge files, or publish atomically. ASCII component matching is
/// case insensitive on every host; ambiguous aliases stop. Non-ASCII folding and
/// embedded NUL/control-Z text semantics remain outside this adapter's domain.
class ClassicFiles : public io::FileStorage {
public:
    /// Anchor classic file operations at an explicit working directory.
    explicit ClassicFiles(const std::filesystem::path& working);
    /// Change this adapter working directory without changing process-global state.
    void change_directory(std::string_view directory);
    /// Check for a classic filename using the adapter lookup policy.
    bool exists(std::string_view directory, std::string_view name) const;
    /// Open a classic binary input relative to the working directory.
    std::unique_ptr<io::CoreInput> open_input(std::string_view path) const;
    /// Open a classic binary replacement output relative to the working directory.
    std::unique_ptr<io::CoreOutput> open_output(std::string_view path) const;
    /// Remove a resolved classic file and report success.
    bool remove_file(std::string_view path) const;
    /// Open a classic text file with its resolved source identity.
    formats::classic::OpenedText open_text(std::string_view directory, std::string_view name) const;
    /// Return the adapter current working-directory path.
    const std::filesystem::path& working() const noexcept { return working_; }

    /// Open input through the FileStorage interface.
    std::unique_ptr<io::CoreInput> read(std::string_view path) const override {
        return open_input(path);
    }
    /// Open replacement output through the FileStorage interface.
    std::unique_ptr<io::CoreOutput> replace(std::string_view path) const override {
        return open_output(path);
    }
    /// Remove a file through the FileStorage interface.
    bool remove(std::string_view path) const override { return remove_file(path); }

private:
    /// Resolve a classic path, optionally admitting a missing final component for output.
    std::optional<std::filesystem::path> resolve(std::string_view directory, std::string_view name,
                                                 bool missing_leaf = false) const;
    std::filesystem::path working_;
};
/// Absolute native path represented with F06's backslash separators. This is a
/// path adapter, not a reinterpretation of gameplay text or its Windows-1252 data.
std::string classic_path(const std::filesystem::path& path);
} // namespace openciv::io
