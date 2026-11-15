export module openciv.formats.classic_text_reader;

import std;

export namespace openciv::formats::classic {
/// F03 section 2 and Appendix A. Byte text, independent of presentation encoding.
enum class TextFault {
    unavailable_stream,
    unbacked_cursor,
    unterminated_buffer,
    unsupported_control,
    invalid_service_result,
    seek_failed,
    comparison_unavailable
};
/// A categorized classic text-reader failure.
class TextError : public std::runtime_error {
public:
    /// Attach a text-reader failure category to a diagnostic message.
    TextError(TextFault fault, const char* message) : std::runtime_error(message), fault(fault) {}
    const TextFault fault;
};

/// Retained line buffer and token cursor shared across classic text reads.
struct TextWorkspace {
    // Construction defaults are not claims about original process initialization.
    // Successful reads establish only the visible prefix and its terminator.
    // Bytes beyond that prefix are not a claim about original trimming scratch.
    std::array<char, 255> buffer{};
    std::size_t backed_bytes = buffer.size();
    std::optional<std::size_t> cursor;
    /// Return the backed, terminated visible line; reject invalid buffer state.
    std::string_view line() const;
};
/// Distinguish a line read from end-of-file and an I/O failure.
enum class TextReadStatus { line, end, failure };
/// One translated text read with its status and supplied characters.
struct TextRead {
    TextReadStatus status = TextReadStatus::end;
    // Already text-mode translated. A successful read has 1..254 bytes, ending
    // at the first LF or the limit/EOF. Failure/end have no supplied bytes.
    std::string characters;
};
/// Abstract classic text stream with physical-offset seeking.
class TextStream {
public:
    virtual ~TextStream() = default;
    /// Read the next translated chunk, bounded by the classic line-buffer contract.
    virtual TextRead read() = 0;
    /// Seek to a physical file offset and report success.
    virtual bool seek(std::size_t file_offset) = 0;
    /// Close the underlying text stream.
    virtual void close() = 0;
};
/// Owned raw-byte adapter with CRLF translation and physical file offsets. It
/// stops on NUL/control-Z rather than inventing unspecified text-mode behavior.
class MemoryTextStream final : public TextStream {
public:
    /// Own raw file bytes for an independent in-memory stream.
    explicit MemoryTextStream(std::string bytes) : bytes_(std::move(bytes)) {}
    /// Translate CRLF and read up to the next line boundary or buffer limit.
    TextRead read() override;
    /// Move to a backed physical offset in the owned bytes.
    bool seek(std::size_t file_offset) override;
    /// Mark the memory stream closed.
    void close() override { closed_ = true; }
    /// Return the current physical offset in the raw input.
    std::size_t position() const noexcept { return position_; }

private:
    std::string bytes_;
    std::size_t position_ = 0;
    bool closed_ = false;
};

/// Explicit policies for classic text equality and path case conversion.
struct TextCase {
    std::function<bool(std::string_view, std::string_view)> equal;
    std::function<std::string(std::string_view)> uppercase_path;
};
/// Explicit ASCII folding for syntax and authored fixtures. Equal bytes need
/// no folding; an encountered unequal pair containing a high byte stops, as do
/// high-byte path operands. This is not a Windows-1252 folding table.
TextCase ascii_text_case();

/// Directory search inputs and the default classic text extension.
struct TextDirectories {
    std::string working, search, home, extension = "TXT";
};
/// An opened text stream paired with its stable source identity.
struct OpenedText {
    std::string path; // Service-supplied source identity, also the index key.
    std::unique_ptr<TextStream> stream;
};
/// Owner-supplied file opening and optional retained section-index services.
struct TextFiles {
    std::function<OpenedText(std::string_view directory, std::string_view uppercase_path)> open;
    /// Supplied retained index, not an inferred dictionary or index builder.
    /// Only offsets greater than zero cause a seek (F03 Appendix A).
    std::function<std::optional<std::size_t>(std::string_view path, std::string_view section)>
        section_offset;
};

/// Read classic sections and tokens with explicit workspace, search, and case policies.
class TextReader {
public:
    /// Borrow parser workspace and directories, retaining the supplied service bindings.
    TextReader(TextWorkspace& workspace, TextDirectories& directories, TextFiles files,
               TextCase comparison);
    TextReader(const TextReader&) = delete;
    TextReader& operator=(const TextReader&) = delete;
    /// Open exactly one source for staged decoders, with no directory fallback.
    bool open_source(std::string_view directory, std::string_view filename);
    /// F03 section 2: low-level open, then wrapper with temporary home fallback.
    /// null filename reuses the stream; null section opens without searching.
    bool open_low(std::optional<std::string_view> filename,
                  std::optional<std::string_view> section);
    /// Open a file/section with the classic temporary home-directory fallback.
    bool open(std::optional<std::string_view> filename, std::optional<std::string_view> section);
    /// Close the active stream and release its binding.
    void close();
    /// Report whether a stream is currently bound.
    bool is_open() const noexcept { return stream_ != nullptr; }
    /// Return the borrowed identity of the active source.
    std::string_view source_path() const noexcept { return path_; }
    /// Inspect the current workspace line without advancing the parser.
    std::string_view line() const { return workspace_.line(); }
    /// Read the next logical content line and reset its token cursor.
    bool next_line();
    /// Consume the next trimmed comma-separated token.
    std::string next_token();
    /// Consume a token and apply classic decimal conversion.
    std::int32_t next_integer();
    /// Consume a token and apply classic binary-flag conversion.
    std::uint16_t next_binary_flags();
    /// Read a name, applying padded-name or ordinary empty-name semantics.
    std::string next_name(bool padded);

private:
    /// Read and validate one stream chunk into the retained workspace.
    TextReadStatus read_buffer();
    /// Locate a section header, optionally searching once more from the beginning.
    bool find_section(std::string_view section, bool rewind_once);
    TextWorkspace& workspace_;
    TextDirectories& directories_;
    TextFiles files_;
    TextCase comparison_;
    std::unique_ptr<TextStream> stream_;
    std::string path_;
};

/// Pure F03 section 2 token conversions. Input is a token, already edge-trimmed
/// by next_token; decimal conversion alone accepts its additional ASCII spaces.
std::int32_t parse_integer(std::string_view token) noexcept;
/// Convert an already trimmed binary token using the F03 flag grammar.
std::uint16_t parse_binary_flags(std::string_view token) noexcept;
} // namespace openciv::formats::classic
