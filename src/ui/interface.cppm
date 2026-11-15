module;
#include <SDL3/SDL.h>

export module openciv.ui.interface;

import std;
export import openciv.viewmodel.menu;

export extern "C++" {
    struct ImGuiContext;

    namespace openciv::ui {

    inline constexpr int width = 960;
    inline constexpr int height = 640;

    // Owns one ImGui context and its SDL backends. All methods run on the main
    // thread, and this object must be destroyed before the renderer and window.
    class Interface {
    public:
        Interface(SDL_Window* window, SDL_Renderer* renderer, bool native_menu);
        ~Interface();
        Interface(const Interface&) = delete;
        Interface& operator=(const Interface&) = delete;

        void process_event(const SDL_Event& event); // Original window coordinates.
        void draw(Shell& shell);
        [[nodiscard]] bool captures_mouse() const;
        [[nodiscard]] bool captures_keyboard() const;

    private:
        void shutdown();
        void apply_style(float scale);
        void draw_menu(Shell& shell);
        void draw_title(Shell& shell);
        void draw_session_controls(Shell& shell);
        void draw_dialog(Shell& shell);
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
}
