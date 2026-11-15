#pragma once
#include "session/classic_stream.hpp"

namespace openciv::session {
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
} // namespace openciv::session
