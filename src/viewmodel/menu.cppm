export module openciv.viewmodel.menu;

import std;
export import openciv.viewmodel.shell;

export extern "C++" {
    namespace openciv {

    struct MenuItem {
        Command command;
        std::string_view label;
        std::string_view shortcut;
        char key; // Primary modifier (Command on macOS, Control elsewhere).
    };
    struct MenuGroup {
        std::string_view label;
        std::span<const MenuItem> items;
    };
    [[nodiscard]] std::span<const MenuGroup> main_menu();
    [[nodiscard]] const MenuItem* find_menu_item(Command command);

    } // namespace openciv
}
