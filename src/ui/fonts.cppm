export module openciv.ui.fonts;

import std;

export extern "C++" {
    struct ImGuiIO;

    namespace openciv::ui {

    // Register stock TrueType faces in the current context. ImGui rasterizes glyphs
    // on demand at the requested size/density; SDL owns the resulting atlas textures.
    void load_fonts(ImGuiIO& io);

    } // namespace openciv::ui
}
