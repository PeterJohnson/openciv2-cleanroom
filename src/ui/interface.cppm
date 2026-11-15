module;
#include <SDL3/SDL.h>

export module openciv.ui.interface;

import std;
import imgui_internal;
export import openciv.viewmodel.menu;

export namespace openciv::ui {

inline constexpr int width = 960;
inline constexpr int height = 640;

/// Owns one ImGui context and its SDL backends. All methods run on the main
/// thread, and this object must be destroyed before the renderer and window.
class Interface {
public:
    /// Initialize ImGui and its SDL backends against borrowed window/renderer resources.
    Interface(SDL_Window* window, SDL_Renderer* renderer, bool native_menu);
    /// Shut down graphical backends and release owned ImGui resources.
    ~Interface();
    Interface(const Interface&) = delete;
    Interface& operator=(const Interface&) = delete;

    /// Feed an SDL event to the ImGui input backend.
    void process_event(const SDL_Event& event); // Original window coordinates.
    /// Draw one frame of shell presentation and dispatch accepted UI actions.
    void draw(Shell& shell);
    /// Report whether ImGui requests mouse input capture.
    [[nodiscard]] bool captures_mouse() const;
    /// Report whether ImGui requests keyboard input capture.
    [[nodiscard]] bool captures_keyboard() const;

private:
    /// Release initialized ImGui backends and context.
    void shutdown();
    /// Apply the application visual style at the requested display scale.
    void apply_style(float scale);
    /// Draw the shell menu and dispatch selected commands.
    void draw_menu(Shell& shell);
    /// Draw the title-screen presentation.
    void draw_title(Shell& shell);
    /// Draw the current session controls.
    void draw_session_controls(Shell& shell);
    /// Present and resolve the shell current modal dialog.
    void draw_dialog(Shell& shell);
    /// Translate accepted keyboard shortcuts into semantic shell commands.
    void shortcuts(Shell& shell);
    SDL_Window* window_;
    SDL_Renderer* renderer_;
    bool native_menu_;
    ImGuiContext* context_ = nullptr;
    bool platform_ready_ = false;
    bool renderer_ready_ = false;
    std::optional<DialogKind> displayed_dialog_;
    float scale_ = 0;
};
} // namespace openciv::ui
