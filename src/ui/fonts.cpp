#include "ui/fonts.hpp"
#include "ui/font_data.hpp"
#include <imgui.h>
#include <cstdio>
#include <stdexcept>
#include <string_view>

namespace openciv::ui {

void load_fonts(ImGuiIO& io) {
    for (const auto& source : font_sources()) {
        ImFontConfig config;
        // ImGui's API accepts void*, but only reads these process-lifetime bytes.
        config.FontDataOwnedByAtlas = false;
        std::snprintf(config.Name, sizeof(config.Name), "%s", source.name);
        auto* font =
            io.Fonts->AddFontFromMemoryTTF(const_cast<unsigned char*>(source.bytes.data()),
                                           static_cast<int>(source.bytes.size()), 16.0F, &config);
        if (!font)
            throw std::runtime_error("Cannot load stock TrueType font");
        if (std::string_view(source.id) == "sans.regular")
            io.FontDefault = font;
    }
    if (!io.FontDefault)
        throw std::runtime_error("Stock sans font is missing");
}

} // namespace openciv::ui
