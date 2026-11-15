module;
#include <SDL3/SDL.h>

export module openciv.application;

import std;
export import openciv.viewmodel.shell;
export import openciv.platform.menu;
export import openciv.ui.interface;

export extern "C++" {
    namespace openciv {

    struct AppOptions {
        bool native_menus = true;
        bool smoke_test = false;
        std::string screenshot;
    };

    class SdlContext {
    public:
        SdlContext();
        ~SdlContext();
        SdlContext(const SdlContext&) = delete;
        SdlContext& operator=(const SdlContext&) = delete;
    };
    struct DeleteWindow {
        void operator()(SDL_Window* value) const { SDL_DestroyWindow(value); }
    };
    struct DeleteRenderer {
        void operator()(SDL_Renderer* value) const { SDL_DestroyRenderer(value); }
    };

    class Application {
    public:
        explicit Application(AppOptions options = {});
        ~Application();
        // SDL_AppEvent can run on a producer thread. Retained text is copied;
        // all SDL/ImGui work runs on the main thread in iterate().
        void post_event(const SDL_Event& event);
        void post_command(Command command) noexcept;
        SDL_AppResult iterate();
        [[nodiscard]] const Shell& shell() const { return shell_; }
        [[nodiscard]] SDL_Window* window() const { return window_.get(); }
        [[nodiscard]] bool ui_captures_mouse() const {
            return shell_.dialog() || interface_->captures_mouse();
        }
        [[nodiscard]] bool ui_captures_keyboard() const {
            return shell_.dialog() || interface_->captures_keyboard();
        }

    private:
        struct QueuedEvent {
            SDL_Event event;
            std::string text;
        };
        void handle(QueuedEvent& queued);
        void sync_menu();
        SdlContext context_; // First constructed, last destroyed.
        AppOptions options_;
        std::unique_ptr<SDL_Window, DeleteWindow> window_;
        std::unique_ptr<SDL_Renderer, DeleteRenderer> renderer_;
        Shell shell_;
        std::unique_ptr<platform::Menu> menu_;
        std::unique_ptr<ui::Interface> interface_;
        Uint32 command_event_ = 0;
        std::mutex inbox_mutex_;
        std::vector<QueuedEvent> inbox_;
        unsigned frame_count_ = 0;
        unsigned menu_state_ = ~0U;
    };

    } // namespace openciv
}
