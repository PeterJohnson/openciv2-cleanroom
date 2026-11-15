import std;
import openciv.formats.classic_text_reader;
import openciv.base.arithmetic;
import openciv.base.text_comparison;

namespace openciv::formats::classic {
namespace {
bool edge_space(char c) {
    return c == ' ' || c == '\t';
}
std::string_view trim(std::string_view text) {
    while (!text.empty() && edge_space(text.front()))
        text.remove_prefix(1);
    while (!text.empty() && edge_space(text.back()))
        text.remove_suffix(1);
    return text;
}
std::string_view backed_string(const TextWorkspace& workspace, std::size_t start) {
    if (workspace.backed_bytes > workspace.buffer.size() || start >= workspace.backed_bytes)
        throw TextError(TextFault::unbacked_cursor,
                        "Classic text cursor is outside supplied backing");
    const auto begin = workspace.buffer.begin() + static_cast<std::ptrdiff_t>(start);
    const auto limit =
        workspace.buffer.begin() + static_cast<std::ptrdiff_t>(workspace.backed_bytes);
    const auto end = std::find(begin, limit, '\0');
    if (end == limit)
        throw TextError(TextFault::unterminated_buffer, "Classic text has no backed terminator");
    return {&*begin, static_cast<std::size_t>(end - begin)};
}
char ascii_upper(char c) {
    if (static_cast<unsigned char>(c) >= 128)
        throw TextError(TextFault::comparison_unavailable,
                        "Non-ASCII classic text comparison requires an established profile");
    return c >= 'a' && c <= 'z' ? static_cast<char>(c - 'a' + 'A') : c;
}
std::string uppercase(std::string_view text) {
    std::string result;
    result.reserve(text.size());
    for (const auto c : text)
        result += ascii_upper(c);
    return result;
}
class RestoreString {
public:
    explicit RestoreString(std::string& target) : target_(target), saved_(target) {}
    ~RestoreString() { target_.swap(saved_); }
    RestoreString(const RestoreString&) = delete;
    RestoreString& operator=(const RestoreString&) = delete;

private:
    std::string& target_;
    std::string saved_;
};
unsigned digit(char c) {
    if (c >= '0' && c <= '9')
        return static_cast<unsigned>(c - '0');
    if (c >= 'a' && c <= 'f')
        return static_cast<unsigned>(c - 'a' + 10);
    if (c >= 'A' && c <= 'F')
        return static_cast<unsigned>(c - 'A' + 10);
    return 16;
}
} // namespace

std::string_view TextWorkspace::line() const {
    return backed_string(*this, 0);
}
TextRead MemoryTextStream::read() {
    if (closed_)
        throw TextError(TextFault::unavailable_stream, "Read from closed classic text stream");
    TextRead result;
    while (position_ < bytes_.size() && result.characters.size() < 254) {
        char c = bytes_[position_];
        if (c == '\0' || c == '\x1a')
            throw TextError(TextFault::unsupported_control,
                            "NUL/control-Z needs a supplied classic text-mode service");
        ++position_;
        if (c == '\r' && position_ < bytes_.size() && bytes_[position_] == '\n') {
            ++position_;
            c = '\n';
        }
        result.characters += c;
        if (c == '\n')
            break;
    }
    if (!result.characters.empty())
        result.status = TextReadStatus::line;
    return result;
}
bool MemoryTextStream::seek(std::size_t file_offset) {
    if (closed_ || file_offset > bytes_.size())
        return false;
    position_ = file_offset;
    return true;
}
TextCase ascii_text_case() {
    return {[](std::string_view a, std::string_view b) {
                try {
                    return base::ascii_name_equal(a, b);
                } catch (const base::ComparisonUnavailable& error) {
                    throw TextError(TextFault::comparison_unavailable, error.what());
                }
            },
            uppercase};
}

TextReader::TextReader(TextWorkspace& workspace, TextDirectories& directories, TextFiles files,
                       TextCase comparison)
    : workspace_(workspace), directories_(directories), files_(std::move(files)),
      comparison_(std::move(comparison)) {
    if (!files_.open || !comparison_.equal || !comparison_.uppercase_path)
        throw std::invalid_argument("Classic text requires file and comparison services");
}
void TextReader::close() {
    if (auto closing = std::move(stream_))
        closing->close();
}
TextReadStatus TextReader::read_buffer() {
    if (!stream_)
        throw TextError(TextFault::unavailable_stream, "Classic text has no open stream");
    const auto read = stream_->read();
    if (read.status != TextReadStatus::line && read.status != TextReadStatus::end &&
        read.status != TextReadStatus::failure)
        throw TextError(TextFault::invalid_service_result, "Unknown classic text read status");
    if (read.status != TextReadStatus::line) {
        if (!read.characters.empty())
            throw TextError(TextFault::invalid_service_result,
                            "Failed classic text read supplied unspecified partial buffer bytes");
        workspace_.buffer[0] = '\0';
        return read.status;
    }
    const auto newline = read.characters.find('\n');
    if (read.characters.empty() || read.characters.size() > 254 ||
        (newline != std::string::npos && newline + 1 != read.characters.size()))
        throw TextError(TextFault::invalid_service_result, "Invalid classic text read window");
    if (read.characters.find('\0') != std::string::npos)
        throw TextError(TextFault::unsupported_control,
                        "Embedded NUL needs explicit buffer semantics");
    std::string_view line = read.characters;
    if (line.ends_with('\n'))
        line.remove_suffix(1);
    line = trim(line);
    std::copy(line.begin(), line.end(), workspace_.buffer.begin());
    workspace_.buffer[line.size()] = '\0';
    workspace_.backed_bytes = line.size() + 1;
    workspace_.cursor = 0;
    return TextReadStatus::line;
}
bool TextReader::next_line() {
    if (read_buffer() == TextReadStatus::line)
        return true;
    close();
    return false;
}
bool TextReader::find_section(std::string_view section, bool rewind_once) {
    if (!stream_)
        throw TextError(TextFault::unavailable_stream, "Section search has no classic text stream");
    if (files_.section_offset) {
        const auto offset = files_.section_offset(path_, section);
        if (offset && *offset > 0 && !stream_->seek(*offset))
            throw TextError(TextFault::seek_failed,
                            "Cannot seek to supplied classic section offset");
    }
    const auto wanted = "@" + std::string(section);
    for (;;) {
        const auto status = read_buffer();
        if (status == TextReadStatus::line) {
            if (comparison_.equal(workspace_.line(), wanted)) {
                workspace_.cursor = workspace_.line().size();
                return true;
            }
        } else if (status == TextReadStatus::end && rewind_once) {
            rewind_once = false;
            if (!stream_->seek(0))
                throw TextError(TextFault::seek_failed, "Cannot rewind classic text stream");
        } else {
            close();
            return false;
        }
    }
}
bool TextReader::open_source(std::string_view directory, std::string_view filename) {
    const std::string selected(directory);
    auto name = std::string(filename);
    if (name.find('.') == std::string::npos)
        name += "." + directories_.extension;
    name = comparison_.uppercase_path(name);
    close();
    RestoreString restore(directories_.working);
    directories_.working = selected;
    auto opened = files_.open(selected, name);
    path_ = std::move(opened.path);
    stream_ = std::move(opened.stream);
    return stream_ != nullptr;
}
bool TextReader::open_low(std::optional<std::string_view> filename,
                          std::optional<std::string_view> section) {
    const std::string retained_filename(filename.value_or(""));
    const std::string retained_section(section.value_or(""));
    if (filename)
        filename = retained_filename;
    if (section)
        section = retained_section;
    if (filename) {
        close();
        auto name = std::string(*filename);
        if (name.find('.') == std::string::npos)
            name += "." + directories_.extension;
        name = comparison_.uppercase_path(name);
        RestoreString restore(directories_.working);
        directories_.working = directories_.search;
        auto opened = files_.open(directories_.working, name);
        if (!opened.stream) {
            directories_.working = directories_.home;
            opened = files_.open(directories_.working, name);
        }
        path_ = std::move(opened.path);
        stream_ = std::move(opened.stream);
        if (!stream_)
            return false;
    } else if (!stream_) {
        throw TextError(TextFault::unavailable_stream, "Reused classic text stream is closed");
    }
    return !section || find_section(*section, !filename);
}
bool TextReader::open(std::optional<std::string_view> filename,
                      std::optional<std::string_view> section) {
    const std::string retained_filename(filename.value_or(""));
    const std::string retained_section(section.value_or(""));
    if (filename)
        filename = retained_filename;
    if (section)
        section = retained_section;
    if (open_low(filename, section))
        return true;
    RestoreString restore(directories_.search);
    directories_.search = directories_.home;
    return open_low(filename, section);
}
std::string TextReader::next_token() {
    if (!workspace_.cursor)
        throw TextError(TextFault::unbacked_cursor,
                        "Classic token cursor was not supplied or initialized");
    auto remaining = backed_string(workspace_, *workspace_.cursor);
    const auto comma = remaining.find(',');
    if (comma == std::string_view::npos)
        *workspace_.cursor += remaining.size();
    else {
        *workspace_.cursor += comma + 1;
        remaining = remaining.substr(0, comma);
    }
    return std::string(trim(remaining));
}
std::int32_t TextReader::next_integer() {
    return parse_integer(next_token());
}
std::uint16_t TextReader::next_binary_flags() {
    return parse_binary_flags(next_token());
}
std::string TextReader::next_name(bool padded) {
    auto result = next_token();
    if (padded && result.empty())
        result = " ";
    return result;
}
std::int32_t parse_integer(std::string_view token) noexcept {
    unsigned radix = 10;
    if (token.size() >= 2 && token[0] == '0') {
        if (token[1] == 'x' || token[1] == 'X')
            radix = 16;
        else if (token[1] == 'b' || token[1] == 'B')
            radix = 2;
        if (radix != 10 || token[1] == 'd' || token[1] == 'D')
            token.remove_prefix(2);
    }
    bool negative = false;
    if (radix == 10) {
        while (!token.empty() &&
               (token.front() == ' ' || (token.front() >= '\t' && token.front() <= '\r')))
            token.remove_prefix(1);
        if (!token.empty() && (token.front() == '+' || token.front() == '-')) {
            negative = token.front() == '-';
            token.remove_prefix(1);
        }
    }
    std::uint32_t value = 0;
    for (const auto c : token) {
        const auto d = digit(c);
        if (d >= radix)
            break;
        value = value * radix + d;
    }
    if (negative)
        value = std::uint32_t{0} - value;
    return base::signed32(value);
}
std::uint16_t parse_binary_flags(std::string_view token) noexcept {
    std::uint16_t value = 0;
    for (const auto c : token) {
        if (c != '0' && c != '1')
            break;
        value = static_cast<std::uint16_t>(value * 2U + static_cast<unsigned>(c - '0'));
    }
    return value;
}
} // namespace openciv::formats::classic
