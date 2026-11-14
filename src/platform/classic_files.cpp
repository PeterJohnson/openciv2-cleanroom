#include "platform/classic_files.hpp"
#include <algorithm>
#include <cstdio>
#include <limits>

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
struct NativeFile {
    std::FILE* file = nullptr;
    explicit NativeFile(const fs::path& path, bool output) {
#ifdef _WIN32
        (void)_wfopen_s(&file, path.c_str(), output ? L"wb" : L"rb");
#else
        file = std::fopen(path.c_str(), output ? "wb" : "rb");
#endif
    }
    ~NativeFile() {
        if (file)
            std::fclose(file);
    }
    NativeFile(const NativeFile&) = delete;
    NativeFile& operator=(const NativeFile&) = delete;
    std::FILE* get() const {
        if (!file)
            throw std::logic_error("Classic file service used after close");
        return file;
    }
    bool close() {
        const auto opened = get();
        file = nullptr;
        return std::fclose(opened) == 0;
    }
};
class NativeTextStream final : public classic::TextStream {
public:
    explicit NativeTextStream(std::shared_ptr<NativeFile> file) : file_(std::move(file)) {}
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
    std::shared_ptr<NativeFile> file_;
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
std::unique_ptr<session::CoreInput> ClassicFiles::open_input(std::string_view requested) const {
    const auto path = resolve({}, requested);
    if (!path || !std::filesystem::is_regular_file(*path))
        return {};
    auto file = std::make_shared<NativeFile>(*path, false);
    if (!file->file)
        return {};
    return std::make_unique<session::CoreInput>(session::CoreInput{
        [file](std::size_t size) {
            formats::Bytes bytes(size);
            const auto count = std::fread(bytes.data(), 1, size, file->get());
            bytes.resize(count);
            return formats::ReadResult{std::move(bytes), count == size};
        },
        [file]() -> std::optional<Byte> {
            const auto value = std::fgetc(file->get());
            return value == EOF ? std::nullopt : std::optional{static_cast<Byte>(value)};
        },
        [file] { return file->close(); }});
}
std::unique_ptr<session::CoreOutput> ClassicFiles::open_output(std::string_view requested) const {
    const auto path = resolve({}, requested, true);
    if (!path)
        return {};
    std::error_code error;
    const auto status = std::filesystem::status(*path, error);
    if (std::filesystem::exists(status) && !std::filesystem::is_regular_file(status))
        return {};
    auto file = std::make_shared<NativeFile>(*path, true);
    if (!file->file)
        return {};
    const formats::WriteItem write = [file](std::span<const Byte> bytes) {
        return std::fwrite(bytes.data(), 1, bytes.size(), file->get()) == bytes.size();
    };
    return std::make_unique<session::CoreOutput>(session::CoreOutput{
        write, write, [file](Byte value) { return std::fputc(value, file->get()) != EOF; },
        [file] { return file->close(); }});
}
formats::classic::OpenedText ClassicFiles::open_text(std::string_view directory,
                                                     std::string_view name) const {
    const auto path = resolve(directory, name);
    if (!path || !std::filesystem::is_regular_file(*path))
        return {};
    auto file = std::make_shared<NativeFile>(*path, false);
    if (!file->file)
        return {};
    return {classic_path(*path), std::make_unique<NativeTextStream>(std::move(file))};
}
} // namespace openciv::io
