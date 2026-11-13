#include "viewmodel/shell.hpp"

namespace openciv {

bool Shell::enabled(Command command) const {
    if (dialog_ || quitting_)
        return false;
    switch (command) {
    case Command::new_session:
    case Command::about:
    case Command::quit:
        return true;
    case Command::return_to_title:
    case Command::toggle_grid:
        return screen_ == Screen::session;
    }
    return false;
}

bool Shell::checked(Command command) const {
    return command == Command::toggle_grid && grid_;
}

void Shell::dispatch(Command command) {
    // Always enforce availability here, including for stale native callbacks.
    if (!enabled(command))
        return;
    switch (command) {
    case Command::new_session:
        dialog_ = Dialog{DialogKind::new_session,
                         "New session",
                         "Open the development game view.\n\n"
                         "World generation and game rules are not connected yet.",
                         {{"Open", DialogResult::accept}, {"Cancel", DialogResult::cancel}}};
        break;
    case Command::return_to_title:
        screen_ = Screen::title;
        break;
    case Command::toggle_grid:
        grid_ = !grid_;
        break;
    case Command::about:
        dialog_ = Dialog{DialogKind::about,
                         "About OpenCiv2",
                         "OpenCiv2 Cleanroom\nC++20 / SDL3 / Dear ImGui\n\n"
                         "A new implementation from behavioral specifications.\n"
                         "This build demonstrates the application infrastructure.",
                         {{"Close", DialogResult::accept}}};
        break;
    case Command::quit:
        request_close();
        break;
    }
}

void Shell::request_close() {
    // Repeated close requests never dismiss an unrelated modal or accept Quit.
    if (dialog_ || quitting_)
        return;
    dialog_ = Dialog{DialogKind::quit,
                     "Quit OpenCiv2?",
                     "Close this session?",
                     {{"Quit", DialogResult::accept}, {"Cancel", DialogResult::cancel}},
                     1};
}

void Shell::resolve_dialog(DialogResult result) {
    if (!dialog_)
        return;
    const auto kind = dialog_->kind;
    dialog_.reset();
    if (result == DialogResult::cancel)
        return;
    switch (kind) {
    case DialogKind::new_session:
        screen_ = Screen::session;
        break;
    case DialogKind::quit:
        quitting_ = true;
        break;
    case DialogKind::about:
        break;
    }
}

} // namespace openciv
