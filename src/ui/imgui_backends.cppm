module;
#include <imgui.h>
#include <imgui_impl_sdlrenderer3.h>

export module openciv.ui.imgui_backends;
export import imgui;
export import imgui_impl_sdl3;

export {
    using ::ImGui_ImplSDLRenderer3_Init;
    using ::ImGui_ImplSDLRenderer3_NewFrame;
    using ::ImGui_ImplSDLRenderer3_RenderDrawData;
    using ::ImGui_ImplSDLRenderer3_Shutdown;
}
export namespace openciv::ui {
/// Check the compiled Dear ImGui version and data-layout compatibility.
inline void check_imgui_version() {
    IMGUI_CHECKVERSION();
}
} // namespace openciv::ui
