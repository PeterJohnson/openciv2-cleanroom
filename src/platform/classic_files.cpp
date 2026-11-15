module;
#include <cstdio>
#ifndef _WIN32
#include <sys/types.h>
#endif

module openciv.platform.classic_files;

import std;

namespace openciv::io {
namespace {
namespace fs = std::filesystem;
namespace classic = formats::classic;
using formats::Byte;
std::string path_text(const fs::path& path) {
    const auto text = path.generic_u8string();
    return {text.begin(), text.end()};
}
fs::path native_path(std::string_view text) {
    if (text.find('\0') != std::string_view::npos)
        throw std::invalid_argument("Classic file path contains NUL");
    std::u8string bytes(text.begin(), text.end());
    std::replace(bytes.begin(), bytes.end(), u8'\\', u8'/');
    return fs::path(bytes);
}
bool alias(std::string_view a, std::string_view b) {
    if (a.size() != b.size())
        return false;
    for (std::size_t i = 0; i < a.size(); ++i) {
        if (a[i] == b[i])
            continue;
        // Do not invent a host-locale-dependent non-ASCII folding profile.
        const auto upper = [](unsigned char c) { return c >= 'a' && c <= 'z' ? c - 32 : c; };
        if (upper(static_cast<unsigned char>(a[i])) != upper(static_cast<unsigned char>(b[i])))
            return false;
    }
    return true;
}
class NativeFile {
public:
    explicit NativeFile(const fs::path& path, bool output) {
#ifdef _WIN32
        (void)_wfopen_s(&file_, path.c_str(), output ? L"wb" : L"rb");
#else
        file_ = std::fopen(path.c_str(), output ? "wb" : "rb");
#endif
    }
    ~NativeFile() {
        if (file_)
            std::fclose(file_);
    }
    NativeFile(const NativeFile&) = delete;
    NativeFile& operator=(const NativeFile&) = delete;
    bool is_open() const noexcept { return file_ != nullptr; }
    std::FILE* get() const {
        if (!file_)
            throw std::logic_error("Classic file service used after close");
        return file_;
    }
    bool close() {
        const auto opened = get();
        file_ = nullptr;
        return std::fclose(opened) == 0;
    }

private:
    std::FILE* file_ = nullptr;
};
class NativeInput final : public io::CoreInput {
public:
    explicit NativeInput(std::unique_ptr<NativeFile> file) : file_(std::move(file)) {}
    ~NativeInput() override { close_on_destruction(); }

private:
    io::ReadResult read_item(std::size_t size) override {
        formats::Bytes bytes(size);
        const auto count = std::fread(bytes.data(), 1, size, file_->get());
        bytes.resize(count);
        return {std::move(bytes), count == size};
    }
    std::optional<Byte> read_byte() override {
        const auto value = std::fgetc(file_->get());
        return value == EOF ? std::nullopt : std::optional{static_cast<Byte>(value)};
    }
    bool finish() override {
        const bool input_ok = std::ferror(file_->get()) == 0;
        const bool closed = file_->close();
        return input_ok && closed;
    }
    std::unique_ptr<NativeFile> file_;
};
class NativeOutput final : public io::CoreOutput {
public:
    explicit NativeOutput(std::unique_ptr<NativeFile> file) : file_(std::move(file)) {}
    ~NativeOutput() override { close_on_destruction(); }

private:
    bool write_item(std::span<const Byte> bytes) override {
        return std::fwrite(bytes.data(), 1, bytes.size(), file_->get()) == bytes.size();
    }
    bool finish() override { return file_->close(); }
    std::unique_ptr<NativeFile> file_;
};
class NativeTextStream final : public classic::TextStream {
public:
    explicit NativeTextStream(std::unique_ptr<NativeFile> file) : file_(std::move(file)) {}
    classic::TextRead read() override {
        auto* file = file_->get();
        classic::TextRead result;
        while (result.characters.size() < 254) {
            auto c = std::fgetc(file);
            if (c == EOF) {
                if (std::ferror(file))
                    return {classic::TextReadStatus::failure, {}};
                break;
            }
            if (c == 0 || c == 0x1a) {
                std::ungetc(c, file);
                throw classic::TextError(classic::TextFault::unsupported_control,
                                         "NUL/control-Z needs supplied classic text semantics");
            }
            if (c == '\r') {
                const auto next = std::fgetc(file);
                if (next == '\n')
                    c = '\n';
                else if (next != EOF)
                    std::ungetc(next, file);
                else if (std::ferror(file))
                    return {classic::TextReadStatus::failure, {}};
            }
            result.characters.push_back(static_cast<char>(c));
            if (c == '\n')
                break;
        }
        if (!result.characters.empty())
            result.status = classic::TextReadStatus::line;
        return result;
    }
    bool seek(std::size_t offset) override {
        if (offset > static_cast<std::uint64_t>(std::numeric_limits<std::int64_t>::max()))
            return false;
#ifdef _WIN32
        return _fseeki64(file_->get(), static_cast<__int64>(offset), SEEK_SET) == 0;
#else
        if (offset > static_cast<std::uint64_t>(std::numeric_limits<off_t>::max()))
            return false;
        return fseeko(file_->get(), static_cast<off_t>(offset), SEEK_SET) == 0;
#endif
    }
    void close() override { (void)file_->close(); }

private:
    std::unique_ptr<NativeFile> file_;
};
} // namespace
std::string classic_path(const std::filesystem::path& path) {
    auto text = path_text(std::filesystem::absolute(path));
    std::replace(text.begin(), text.end(), '/', '\\');
    return text;
}
ClassicFiles::ClassicFiles(const std::filesystem::path& working)
    : working_(std::filesystem::absolute(working)) {
    if (!std::filesystem::is_directory(working_))
        throw std::invalid_argument("Classic working path must be a directory");
}
std::optional<std::filesystem::path>
ClassicFiles::resolve(std::string_view directory, std::string_view name, bool missing_leaf) const {
    auto requested = native_path(name);
    if (!requested.is_absolute())
        requested = native_path(directory) / requested;
    if (!requested.is_absolute())
        requested = working_ / requested;
    // Walk actual components; lexically collapsing '..' before traversing a
    // symbolic link would select a different native file.
    auto selected = requested.root_path();
    const auto relative = requested.relative_path();
    for (auto i = relative.begin(); i != relative.end(); ++i) {
        const auto component = path_text(*i);
        if (component.empty() || component == ".")
            continue;
        if (component == "..") {
            selected /= *i;
            continue;
        }
        std::error_code error;
        fs::directory_iterator entries(selected, error);
        if (error)
            return {};
        std::optional<fs::path> match;
        for (const auto& entry : entries) {
            if (alias(path_text(entry.path().filename()), component)) {
                if (match)
                    throw std::invalid_argument("Ambiguous classic filename case aliases");
                match = entry.path();
            }
        }
#ifdef _WIN32
        // Windows temp/home paths can contain DOS short names (RUNNER~1).
        // Directory iteration returns the long name. After checking ambiguous
        // textual aliases, allow names resolved directly by the native filesystem.
        if (!match) {
            const auto native_alias = selected / *i;
            if (fs::exists(native_alias, error) && !error)
                match = native_alias;
        }
#endif
        if (match)
            selected = *match;
        else if (missing_leaf && std::next(i) == relative.end())
            selected /= *i;
        else
            return {};
    }
    return selected;
}
void ClassicFiles::change_directory(std::string_view directory) {
    const auto path = resolve({}, directory);
    if (!path || !std::filesystem::is_directory(*path))
        throw std::runtime_error("Cannot select classic working directory");
    working_ = *path;
}
bool ClassicFiles::exists(std::string_view directory, std::string_view name) const {
    const auto path = resolve(directory, name);
    return path && std::filesystem::is_regular_file(*path);
}
std::unique_ptr<io::CoreInput> ClassicFiles::open_input(std::string_view requested) const {
    const auto path = resolve({}, requested);
    if (!path || !std::filesystem::is_regular_file(*path))
        return {};
    auto file = std::make_unique<NativeFile>(*path, false);
    if (!file->is_open())
        return {};
    return std::make_unique<NativeInput>(std::move(file));
}
std::unique_ptr<io::CoreOutput> ClassicFiles::open_output(std::string_view requested) const {
    const auto path = resolve({}, requested, true);
    if (!path)
        return {};
    std::error_code error;
    const auto status = std::filesystem::status(*path, error);
    if (std::filesystem::exists(status) && !std::filesystem::is_regular_file(status))
        return {};
    auto file = std::make_unique<NativeFile>(*path, true);
    if (!file->is_open())
        return {};
    return std::make_unique<NativeOutput>(std::move(file));
}
bool ClassicFiles::remove_file(std::string_view requested) const {
    const auto path = resolve({}, requested);
    if (!path)
        return false;
    std::error_code error;
    if (!std::filesystem::is_regular_file(*path, error) || error)
        return false;
    return std::filesystem::remove(*path, error) && !error;
}
formats::classic::OpenedText ClassicFiles::open_text(std::string_view directory,
                                                     std::string_view name) const {
    const auto path = resolve(directory, name);
    if (!path || !std::filesystem::is_regular_file(*path))
        return {};
    auto file = std::make_unique<NativeFile>(*path, false);
    if (!file->is_open())
        return {};
    return {classic_path(*path), std::make_unique<NativeTextStream>(std::move(file))};
}
} // namespace openciv::io
