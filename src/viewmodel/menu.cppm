export module openciv.viewmodel.menu;

import std;
export import openciv.viewmodel.shell;

export namespace openciv {

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

module :private;
namespace openciv {
namespace {
constexpr std::array game{MenuItem{Command::new_session, "New Session...", "Ctrl+N", 'n'},
                          MenuItem{Command::return_to_title, "Return to Title", "Ctrl+T", 't'},
                          MenuItem{Command::quit, "Quit...", "Ctrl+Q", 'q'}};
constexpr std::array view{MenuItem{Command::toggle_grid, "Show Grid", "Ctrl+G", 'g'}};
constexpr std::array help{MenuItem{Command::about, "About OpenCiv2", "", '\0'}};
constexpr std::array groups{MenuGroup{"Game", game}, MenuGroup{"View", view},
                            MenuGroup{"Help", help}};
} // namespace

std::span<const MenuGroup> main_menu() {
    return groups;
}

const MenuItem* find_menu_item(Command command) {
    for (const auto& group : groups)
        for (const auto& item : group.items)
            if (item.command == command)
                return &item;
    return nullptr;
}

} // namespace openciv
