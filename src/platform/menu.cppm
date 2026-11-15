export module openciv.platform.menu;

import std;
export import openciv.viewmodel.menu;

export extern "C++" {
    struct SDL_Window;

    namespace openciv::platform {

    struct CommandSink {
        void* context;
        void (*post)(void*, Command) noexcept;
        void operator()(Command command) const noexcept { post(context, command); }
    };

    // Owned by the application, destroyed before its SDL window. Native callbacks
    // enqueue commands; they never mutate application state or open a modal loop.
    class Menu {
    public:
        virtual ~Menu() = default;
        [[nodiscard]] virtual bool native() const = 0;
        virtual void sync(const Shell& shell) = 0;
    };

    std::unique_ptr<Menu> create_menu(SDL_Window* window, CommandSink sink);
    std::unique_ptr<Menu> create_drawn_menu();

    } // namespace openciv::platform
}
