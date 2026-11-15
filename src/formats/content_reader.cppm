export module openciv.formats.content_reader;

import std;
export import openciv.formats.content_json;

export namespace openciv::formats::content::detail {
/// Shared native authoring checks. Reader references one owned document and its
/// exact numeric-token tree; it never performs I/O or installs runtime state.
struct Reader {
    jt::Json& value;
    const jt::Json& numbers;
    std::string path;
    /// Throw a content error at the current document path.
    [[noreturn]] void error(std::string message) const;
    /// Require named members and reject unknown fields, optionally admitting extensions.
    void members(std::span<const std::string_view> required,
                 std::span<const std::string_view> optional = {}, bool extensions = false) const;
    /// Validate required/optional members supplied as initializer lists.
    void members(std::initializer_list<std::string_view> required,
                 std::initializer_list<std::string_view> optional, bool extensions = false) const;
    /// Require exactly the listed fields, with optional namespaced extensions.
    void fields(std::span<const std::string_view> names, bool extensions = false) const;
    /// Validate an exact field list supplied as an initializer list.
    void fields(std::initializer_list<std::string_view> names, bool extensions = false) const;
    /// Report whether the current object contains a field.
    bool has(std::string_view key) const;
    /// Return a reader for a required object member, extending its diagnostic path.
    Reader at(std::string_view key) const;
    /// Return a reader for an array element, rejecting an out-of-range index.
    Reader element(std::size_t i) const;
    /// Require an array and return its length.
    std::size_t array_size() const;
    /// Require an object and return its borrowed member map.
    const std::map<std::string, jt::Json>& object() const;
    /// Read Unicode text, optionally rejecting an empty string.
    std::string text(bool nonempty = true) const;
    /// Read and validate a semantic identifier.
    std::string id() const;
    /// Read a 64-character lowercase hexadecimal SHA-256 value.
    std::string sha256() const;
    /// Require the current string to equal the specified format constant.
    void constant(std::string_view expected) const;
    /// Validate an exact integer token within the supplied bounds and normalize its JSON value.
    std::int32_t integer(std::int32_t low = std::numeric_limits<std::int32_t>::min(),
                         std::int32_t high = std::numeric_limits<std::int32_t>::max()) const;
    /// Return a positive integer revision as exact decimal text.
    std::string revision() const;
    /// Validate every element of a text array, optionally rejecting empty strings.
    void texts(bool nonempty) const;
};
} // namespace openciv::formats::content::detail

module :private;
namespace openciv::formats::content::detail {
namespace {
bool lower(char c) {
    return c >= 'a' && c <= 'z';
}
bool identifier(std::string_view text) {
    return !text.empty() && lower(text.front()) &&
           std::all_of(text.begin(), text.end(), [](char c) {
               return lower(c) || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.';
           });
}
bool extension_key(std::string_view key) {
    bool dot = false;
    while (!key.empty()) {
        const auto end = key.find('.');
        if (!identifier(key.substr(0, end)))
            return false;
        if (end == std::string_view::npos)
            return dot;
        dot = true;
        key.remove_prefix(end + 1);
    }
    return false;
}
} // namespace
void Reader::error(std::string message) const {
    throw Error("invalid-content", path, std::move(message));
}
const std::map<std::string, jt::Json>& Reader::object() const {
    if (!value.isObject())
        error("expected object");
    return value.getObject();
}
void Reader::members(std::span<const std::string_view> required,
                     std::span<const std::string_view> optional, bool extensions) const {
    const auto& items = object();
    for (const auto name : required)
        if (!has(name))
            error("missing field " + std::string(name));
    for (const auto& [key, child] : items) {
        if (std::find(required.begin(), required.end(), key) != required.end() ||
            std::find(optional.begin(), optional.end(), key) != optional.end())
            continue;
        if (extensions && key == "extensions") {
            if (!child.isObject())
                error("extensions must be an object");
            for (const auto& [extension, metadata] : child.getObject()) {
                (void)metadata;
                if (!extension_key(extension))
                    error("invalid metadata namespace " + extension);
            }
        } else
            error("unknown field " + key);
    }
}
void Reader::members(std::initializer_list<std::string_view> required,
                     std::initializer_list<std::string_view> optional, bool extensions) const {
    members(std::span(required.begin(), required.size()),
            std::span(optional.begin(), optional.size()), extensions);
}
void Reader::fields(std::span<const std::string_view> names, bool extensions) const {
    members(names, {}, extensions);
}
void Reader::fields(std::initializer_list<std::string_view> names, bool extensions) const {
    fields(std::span(names.begin(), names.size()), extensions);
}
bool Reader::has(std::string_view key) const {
    return value.isObject() && value.contains(std::string(key));
}
Reader Reader::at(std::string_view key) const {
    if (!has(key))
        error("missing object field " + std::string(key));
    return {value.getObject().at(std::string(key)), numbers.getObject().at(std::string(key)),
            path + "/" + std::string(key)};
}
std::size_t Reader::array_size() const {
    if (!value.isArray())
        error("expected array");
    return value.getArray().size();
}
Reader Reader::element(std::size_t i) const {
    if (i >= array_size())
        error("array index out of bounds");
    return {value.getArray().at(i), numbers.getArray().at(i), path + "/" + std::to_string(i)};
}
std::string Reader::text(bool nonempty) const {
    if (!value.isString() || (nonempty && value.getString().empty()))
        error("expected Unicode text");
    return value.getString();
}
std::string Reader::id() const {
    auto result = text();
    if (!identifier(result))
        error("invalid semantic identity");
    return result;
}
std::string Reader::sha256() const {
    auto result = text();
    if (result.size() != 64 || !std::all_of(result.begin(), result.end(), [](char c) {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
        }))
        error("invalid SHA-256");
    return result;
}
void Reader::constant(std::string_view expected) const {
    if (text() != expected)
        error("expected " + std::string(expected));
}
std::int32_t Reader::integer(std::int32_t low, std::int32_t high) const {
    if (!value.isNumber() || !numbers.isString())
        error("expected number");
    const auto result = integer32(numbers.getString(), path);
    if (result < low || result > high)
        error("integer outside field domain");
    value = jt::Json(result);
    return result;
}
std::string Reader::revision() const {
    if (!value.isNumber() || !numbers.isString())
        error("expected revision number");
    auto result = integer_text(numbers.getString(), path);
    if (result == "0" || result.front() == '-')
        error("revision must be positive");
    return result;
}
void Reader::texts(bool nonempty) const {
    for (std::size_t i = 0; i < array_size(); ++i)
        element(i).text(nonempty);
}
} // namespace openciv::formats::content::detail
