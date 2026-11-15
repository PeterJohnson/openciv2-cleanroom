module;
#include <SDL3/SDL_video.h>

export module openciv.platform.menu;

import std;
export import openciv.viewmodel.menu;

export {
    using ::SDL_Window;

    namespace openciv::platform {

    /// Borrow an application callback for queuing semantic native-menu commands.
    struct CommandSink {
        void* context;
        void (*post)(void*, Command) noexcept;
        /// Queue a command through the callback; its context must remain alive.
        void operator()(Command command) const noexcept { post(context, command); }
    };

    /// Owned by the application, destroyed before its SDL window. Native callbacks
    /// enqueue commands; they never mutate application state or open a modal loop.
    class Menu {
    public:
        virtual ~Menu() = default;
        /// Report whether this adapter provides the platform-native menu.
        [[nodiscard]] virtual bool native() const = 0;
        /// Update enabled and checked items from the shell command state.
        virtual void sync(const Shell& shell) = 0;
    };

    /// Create the platform menu adapter using a borrowed window and command sink.
    std::unique_ptr<Menu> create_menu(SDL_Window* window, CommandSink sink);
    /// Create the adapter used when the interface draws its own menu bar.
    std::unique_ptr<Menu> create_drawn_menu();

    } // namespace openciv::platform
}
