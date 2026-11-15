export module openciv.viewmodel.shell;

import std;

export namespace openciv {

/// Shell commands are intentionally distinct from classic P05 gameplay IDs.
enum class Command { new_session = 100, return_to_title, toggle_grid, about, quit };
/// The currently displayed shell screen.
enum class Screen { title, session };
/// The kind of modal interaction presented by the shell.
enum class DialogKind { new_session, about, quit };
/// Semantic outcomes accepted by a shell dialog.
enum class DialogResult { accept, cancel };

/// A dialog button label and its semantic result.
struct DialogButton {
    std::string_view label;
    DialogResult result;
};

/// Owned dialog content and offered result buttons.
struct Dialog {
    DialogKind kind;
    std::string_view title;
    std::string_view body;
    std::vector<DialogButton> buttons;
    std::size_t default_button = 0; // Initial selection; live focus belongs to the UI.
};

/// Application state only. Simulation state and classic conformance belong in
/// separate portable game modules; no frame timing or platform state enters those rules.
class Shell {
public:
    /// Return the currently selected shell screen.
    [[nodiscard]] Screen screen() const { return screen_; }
    /// Report whether the map-grid overlay is enabled.
    [[nodiscard]] bool grid() const { return grid_; }
    /// Report whether application shutdown has been accepted.
    [[nodiscard]] bool quitting() const { return quitting_; }
    /// Inspect the pending modal dialog, if any.
    [[nodiscard]] const std::optional<Dialog>& dialog() const { return dialog_; }
    /// Report whether a semantic command is currently available.
    [[nodiscard]] bool enabled(Command command) const;
    /// Report the checkmark state of a toggle command.
    [[nodiscard]] bool checked(Command command) const;
    /// Apply an available semantic shell command.
    void dispatch(Command command);
    /// Request application closure, presenting confirmation when needed.
    void request_close();
    /// Apply a semantic response to the current dialog.
    void resolve_dialog(DialogResult result);

private:
    Screen screen_ = Screen::title;
    bool grid_ = true;
    bool quitting_ = false;
    std::optional<Dialog> dialog_;
};
} // namespace openciv

module :private;
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
