export module openciv.formats.content_reader;

import std;
export import openciv.formats.content_json;

export extern "C++" {
    namespace openciv::formats::content::detail {
    // Shared native authoring checks. Reader references one owned document and its
    // exact numeric-token tree; it never performs I/O or installs runtime state.
    struct Reader {
        jt::Json& value;
        const jt::Json& numbers;
        std::string path;
        [[noreturn]] void error(std::string message) const;
        void members(std::span<const std::string_view> required,
                     std::span<const std::string_view> optional = {},
                     bool extensions = false) const;
        void members(std::initializer_list<std::string_view> required,
                     std::initializer_list<std::string_view> optional,
                     bool extensions = false) const;
        void fields(std::span<const std::string_view> names, bool extensions = false) const;
        void fields(std::initializer_list<std::string_view> names, bool extensions = false) const;
        bool has(std::string_view key) const;
        Reader at(std::string_view key) const;
        Reader element(std::size_t i) const;
        std::size_t array_size() const;
        const std::map<std::string, jt::Json>& object() const;
        std::string text(bool nonempty = true) const;
        std::string id() const;
        std::string sha256() const;
        void constant(std::string_view expected) const;
        std::int32_t integer(std::int32_t low = std::numeric_limits<std::int32_t>::min(),
                             std::int32_t high = std::numeric_limits<std::int32_t>::max()) const;
        std::string revision() const;
        void texts(bool nonempty) const;
    };
    } // namespace openciv::formats::content::detail
}
