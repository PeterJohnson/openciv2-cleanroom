#pragma once
#include "viewmodel/shell.hpp"
#include <span>

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
