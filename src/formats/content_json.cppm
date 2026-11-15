module;
#include <json.h>

export module openciv.formats.content_json;

import std;
export import openciv.formats.error;

export namespace jt {
using ::jt::Json;
}

export namespace openciv::formats::content {
using formats::Error;
// Native content conventions, data-format.md §2. jt::Json owns the decoded DOM.
// Numeric lexemes are retained separately, so admission never relies on doubles.
struct JsonDocument {
    jt::Json value;
    jt::Json numbers;
};
JsonDocument parse_json(std::string_view bytes, std::string_view source);
// Mathematically integral JSON decimal token -> canonical signed decimal text.
// Reject fractions; expansion is bounded independently of the input exponent.
std::string integer_text(std::string_view token, std::string_view location);
std::int32_t integer32(std::string_view token, std::string_view location);
} // namespace openciv::formats::content
