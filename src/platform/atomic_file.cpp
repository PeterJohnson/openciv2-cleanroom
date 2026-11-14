#include "platform/native_files.hpp"
#include "base/cancellation.hpp"
#include <atomic>
#include <system_error>
#ifdef _WIN32
#define NOMINMAX
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#else
#include <cerrno>
#include <fcntl.h>
#include <unistd.h>
#endif

namespace openciv::io {
namespace {
std::atomic<std::uint64_t> next_file{0};
[[noreturn]] void fail(const char* operation) {
#ifdef _WIN32
    throw std::system_error(static_cast<int>(GetLastError()), std::system_category(), operation);
#else
    throw std::system_error(errno, std::generic_category(), operation);
#endif
}
class StagedFile {
public:
    explicit StagedFile(const std::filesystem::path& target) : target_(target) {
#ifdef _WIN32
        const auto pid = GetCurrentProcessId();
#else
        const auto pid = getpid();
#endif
        for (int attempt = 0; attempt < 100; ++attempt) {
            path_ = target_;
            path_ += ".oc2-tmp-" + std::to_string(pid) + "-" + std::to_string(next_file++);
#ifdef _WIN32
            file_ = CreateFileW(path_.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW,
                                FILE_ATTRIBUTE_NORMAL, nullptr);
            if (file_ != INVALID_HANDLE_VALUE)
                return;
            if (GetLastError() != ERROR_FILE_EXISTS && GetLastError() != ERROR_ALREADY_EXISTS)
                fail("Create export staging file");
#else
            file_ = open(path_.c_str(), O_WRONLY | O_CREAT | O_EXCL | O_CLOEXEC, 0600);
            if (file_ >= 0)
                return;
            if (errno != EEXIST)
                fail("Create export staging file");
#endif
        }
        throw std::runtime_error("Cannot reserve an export staging file");
    }
    StagedFile(const StagedFile&) = delete;
    StagedFile& operator=(const StagedFile&) = delete;
    ~StagedFile() {
#ifdef _WIN32
        if (file_ != INVALID_HANDLE_VALUE)
            CloseHandle(file_);
#else
        if (file_ >= 0)
            close(file_);
#endif
        std::error_code ignored;
        if (!path_.empty())
            std::filesystem::remove(path_, ignored);
    }
    std::size_t write(std::span<const formats::Byte> bytes) {
#ifdef _WIN32
        DWORD count = 0;
        if (!WriteFile(file_, bytes.data(), static_cast<DWORD>(bytes.size()), &count, nullptr))
            fail("Write export staging file");
#else
        ssize_t count;
        do {
            count = ::write(file_, bytes.data(), bytes.size());
        } while (count < 0 && errno == EINTR);
        if (count < 0)
            fail("Write export staging file");
#endif
        if (count == 0)
            throw std::runtime_error("Export write made no progress");
        return static_cast<std::size_t>(count);
    }
    void finish() {
#ifdef _WIN32
        if (!FlushFileBuffers(file_))
            fail("Flush export staging file");
        const auto handle = file_;
        file_ = INVALID_HANDLE_VALUE;
        if (!CloseHandle(handle))
            fail("Close export staging file");
#else
        if (fsync(file_) != 0)
            fail("Flush export staging file");
        const auto descriptor = file_;
        file_ = -1;
        if (close(descriptor) != 0)
            fail("Close export staging file");
#endif
    }
    void publish() {
#ifdef _WIN32
        // No REPLACE_EXISTING: a concurrently created file also wins.
        if (!MoveFileExW(path_.c_str(), target_.c_str(), MOVEFILE_WRITE_THROUGH))
            fail("Publish export without overwrite");
        path_.clear();
#else
        // rename() overwrites on POSIX; link() atomically refuses any existing name.
        if (link(path_.c_str(), target_.c_str()) != 0)
            fail("Publish export without overwrite");
#endif
    }

private:
    std::filesystem::path target_, path_;
#ifdef _WIN32
    HANDLE file_ = INVALID_HANDLE_VALUE;
#else
    int file_ = -1;
#endif
};
} // namespace
void write_new_file(const std::filesystem::path& path, std::span<const formats::Byte> bytes,
                    base::CancellationToken stop,
                    const std::function<void(std::size_t)>& progress) {
    const auto checkpoint = [&](std::size_t written) {
        base::check_stop(stop);
        if (progress)
            progress(written);
        base::check_stop(stop);
    };
    checkpoint(0);
    if (std::filesystem::exists(path))
        throw std::runtime_error("Export destination already exists");
    StagedFile file{path};
    std::size_t written = 0;
    while (written < bytes.size()) {
        const auto size = std::min<std::size_t>(bytes.size() - written, 64 * 1024);
        written += file.write(bytes.subspan(written, size));
        checkpoint(written);
    }
    file.finish();
    checkpoint(written);
    file.publish();
}
} // namespace openciv::io
