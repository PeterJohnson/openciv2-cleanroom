#pragma once

#include <span>

namespace openciv::ui {

struct FontSource {
    const char* id;
    const char* name;
    std::span<const unsigned char> bytes;
};

// Generated from hash-verified upstream TrueType data. Storage lasts for the
// process lifetime; no original-game bitmap cache or platform font lookup.
[[nodiscard]] std::span<const FontSource> font_sources();

} // namespace openciv::ui
