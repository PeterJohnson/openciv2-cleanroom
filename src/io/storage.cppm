export module openciv.io.storage;

import std;
export import openciv.io.stream;

export namespace openciv::io {
// A noncopyable backend. Returned files own individual open-file lifetimes.
class FileStorage {
public:
    FileStorage() = default;
    virtual ~FileStorage() = default;
    FileStorage(const FileStorage&) = delete;
    FileStorage& operator=(const FileStorage&) = delete;
    virtual std::unique_ptr<CoreInput> read(std::string_view path) const = 0;
    virtual std::unique_ptr<CoreOutput> replace(std::string_view path) const = 0;
    virtual bool remove(std::string_view path) const = 0;
};
} // namespace openciv::io
