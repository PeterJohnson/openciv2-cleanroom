module;
#include <SDL3/SDL.h>

export module openciv.application;

import std;
export import openciv.viewmodel.shell;
export import openciv.platform.menu;
export import openciv.ui.interface;

export namespace openciv {

/// Startup options for the graphical shell and its smoke-test capture.
struct AppOptions {
    bool native_menus = true;
    bool smoke_test = false;
    std::string screenshot;
};

/// Own SDL initialization for the lifetime of the graphical application.
class SdlContext {
public:
    /// Initialize the SDL subsystems required by the shell.
    SdlContext();
    /// Shut down SDL after dependent graphical resources have been destroyed.
    ~SdlContext();
    SdlContext(const SdlContext&) = delete;
    SdlContext& operator=(const SdlContext&) = delete;
};
/// Release an SDL window owned by a smart pointer.
struct DeleteWindow {
    /// Destroy the supplied SDL window.
    void operator()(SDL_Window* value) const { SDL_DestroyWindow(value); }
};
/// Release an SDL renderer owned by a smart pointer.
struct DeleteRenderer {
    /// Destroy the supplied SDL renderer.
    void operator()(SDL_Renderer* value) const { SDL_DestroyRenderer(value); }
};

/// Compose the SDL window, native menu, shell model, and ImGui interface.
class Application {
public:
    /// Create the graphical shell with the requested platform and capture options.
    explicit Application(AppOptions options = {});
    /// Release the interface and platform resources in dependency order.
    ~Application();
    /// SDL_AppEvent can run on a producer thread. Retained text is copied;
    /// all SDL/ImGui work runs on the main thread in iterate().
    void post_event(const SDL_Event& event);
    /// Queue a semantic shell command through the SDL event channel.
    void post_command(Command command) noexcept;
    /// Process queued input and draw one frame; return SDL continuation status.
    SDL_AppResult iterate();
    /// Inspect the current shell model without transferring ownership.
    [[nodiscard]] const Shell& shell() const { return shell_; }
    /// Return the borrowed native SDL window.
    [[nodiscard]] SDL_Window* window() const { return window_.get(); }
    /// Report whether a dialog or ImGui currently consumes mouse input.
    [[nodiscard]] bool ui_captures_mouse() const {
        return shell_.dialog() || interface_->captures_mouse();
    }
    /// Report whether a dialog or ImGui currently consumes keyboard input.
    [[nodiscard]] bool ui_captures_keyboard() const {
        return shell_.dialog() || interface_->captures_keyboard();
    }

private:
    /// An SDL event with owned text that survives the producer callback.
    struct QueuedEvent {
        SDL_Event event;
        std::string text;
    };
    /// Dispatch one queued event on the main thread.
    void handle(QueuedEvent& queued);
    /// Refresh native menu state when the shell command state changes.
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
