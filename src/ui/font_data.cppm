export module openciv.ui.font_data;

import std;

export namespace openciv::ui {

/// A borrowed embedded font byte range and its face identity.
struct FontSource {
    const char* id;
    const char* name;
    std::span<const unsigned char> bytes;
};

/// Generated from hash-verified upstream TrueType data. Storage lasts for the
/// process lifetime; no original-game bitmap cache or platform font lookup.
[[nodiscard]] std::span<const FontSource> font_sources();
} // namespace openciv::ui
