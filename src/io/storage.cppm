export module openciv.io.storage;

import std;
export import openciv.io.stream;

export namespace openciv::io {
/// A noncopyable backend. Returned files own individual open-file lifetimes.
class FileStorage {
public:
    FileStorage() = default;
    virtual ~FileStorage() = default;
    FileStorage(const FileStorage&) = delete;
    FileStorage& operator=(const FileStorage&) = delete;
    /// Open an input stream for a path; return null when opening fails.
    virtual std::unique_ptr<CoreInput> read(std::string_view path) const = 0;
    /// Open a replacement output stream; return null when opening fails.
    virtual std::unique_ptr<CoreOutput> replace(std::string_view path) const = 0;
    /// Remove the named file and report whether removal succeeded.
    virtual bool remove(std::string_view path) const = 0;
};
} // namespace openciv::io
