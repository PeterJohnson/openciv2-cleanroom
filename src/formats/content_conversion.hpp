#pragma once
#include "formats/content_pack.hpp"
#include "formats/classic_text_reader.hpp"

namespace openciv::formats::content {
struct ContentConversion {
    std::string component; // Exact UTF-8 native component bytes, ready for validation/publication.
    jt::Json receipt;
};
// M1-02 component conversion. Explicit owned classic source and resolved native
// rule selection; no filesystem access, mutation, fallback mounts or publication.
ContentConversion convert_city_preferences(const Source& source, const ResolvedPack& selected,
                                           const classic::TextCase& comparison);
} // namespace openciv::formats::content
