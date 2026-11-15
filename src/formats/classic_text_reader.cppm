export module openciv.formats.classic_text_reader;

import std;

export extern "C++" {
    namespace openciv::formats::classic {
    // F03 section 2 and Appendix A. Byte text, independent of presentation encoding.
    enum class TextFault {
        unavailable_stream,
        unbacked_cursor,
        unterminated_buffer,
        unsupported_control,
        invalid_service_result,
        seek_failed,
        comparison_unavailable
    };
    class TextError : public std::runtime_error {
    public:
        TextError(TextFault fault, const char* message)
            : std::runtime_error(message), fault(fault) {}
        const TextFault fault;
    };

    struct TextWorkspace {
        // Construction defaults are not claims about original process initialization.
        // Successful reads establish only the visible prefix and its terminator.
        // Bytes beyond that prefix are not a claim about original trimming scratch.
        std::array<char, 255> buffer{};
        std::size_t backed_bytes = buffer.size();
        std::optional<std::size_t> cursor;
        std::string_view line() const;
    };
    enum class TextReadStatus { line, end, failure };
    struct TextRead {
        TextReadStatus status = TextReadStatus::end;
        // Already text-mode translated. A successful read has 1..254 bytes, ending
        // at the first LF or the limit/EOF. Failure/end have no supplied bytes.
        std::string characters;
    };
    class TextStream {
    public:
        virtual ~TextStream() = default;
        virtual TextRead read() = 0;
        virtual bool seek(std::size_t file_offset) = 0;
        virtual void close() = 0;
    };
    // Owned raw-byte adapter with CRLF translation and physical file offsets. It
    // stops on NUL/control-Z rather than inventing unspecified text-mode behavior.
    class MemoryTextStream final : public TextStream {
    public:
        explicit MemoryTextStream(std::string bytes) : bytes_(std::move(bytes)) {}
        TextRead read() override;
        bool seek(std::size_t file_offset) override;
        void close() override { closed_ = true; }
        std::size_t position() const noexcept { return position_; }

    private:
        std::string bytes_;
        std::size_t position_ = 0;
        bool closed_ = false;
    };

    struct TextCase {
        std::function<bool(std::string_view, std::string_view)> equal;
        std::function<std::string(std::string_view)> uppercase_path;
    };
    // Explicit ASCII folding for syntax and authored fixtures. Equal bytes need
    // no folding; an encountered unequal pair containing a high byte stops, as do
    // high-byte path operands. This is not a Windows-1252 folding table.
    TextCase ascii_text_case();

    struct TextDirectories {
        std::string working, search, home, extension = "TXT";
    };
    struct OpenedText {
        std::string path; // Service-supplied source identity, also the index key.
        std::unique_ptr<TextStream> stream;
    };
    struct TextFiles {
        std::function<OpenedText(std::string_view directory, std::string_view uppercase_path)> open;
        // Supplied retained index, not an inferred dictionary or index builder.
        // Only offsets greater than zero cause a seek (F03 Appendix A).
        std::function<std::optional<std::size_t>(std::string_view path, std::string_view section)>
            section_offset;
    };

    class TextReader {
    public:
        TextReader(TextWorkspace& workspace, TextDirectories& directories, TextFiles files,
                   TextCase comparison);
        TextReader(const TextReader&) = delete;
        TextReader& operator=(const TextReader&) = delete;
        // Open exactly one source for staged decoders, with no directory fallback.
        bool open_source(std::string_view directory, std::string_view filename);
        // F03 section 2: low-level open, then wrapper with temporary home fallback.
        // null filename reuses the stream; null section opens without searching.
        bool open_low(std::optional<std::string_view> filename,
                      std::optional<std::string_view> section);
        bool open(std::optional<std::string_view> filename,
                  std::optional<std::string_view> section);
        void close();
        bool is_open() const noexcept { return stream_ != nullptr; }
        std::string_view source_path() const noexcept { return path_; }
        std::string_view line() const { return workspace_.line(); }
        bool next_line();
        std::string next_token();
        std::int32_t next_integer();
        std::uint16_t next_binary_flags();
        std::string next_name(bool padded);

    private:
        TextReadStatus read_buffer();
        bool find_section(std::string_view section, bool rewind_once);
        TextWorkspace& workspace_;
        TextDirectories& directories_;
        TextFiles files_;
        TextCase comparison_;
        std::unique_ptr<TextStream> stream_;
        std::string path_;
    };

    // Pure F03 section 2 token conversions. Input is a token, already edge-trimmed
    // by next_token; decimal conversion alone accepts its additional ASCII spaces.
    std::int32_t parse_integer(std::string_view token) noexcept;
    std::uint16_t parse_binary_flags(std::string_view token) noexcept;
    } // namespace openciv::formats::classic
}
