#pragma once

#include <cstddef>
#include <optional>
#include <string_view>
#include <vector>

namespace openciv {

// Shell commands are intentionally distinct from classic P05 gameplay IDs.
enum class Command { new_session = 100, return_to_title, toggle_grid, about, quit };
enum class Screen { title, session };
enum class DialogKind { new_session, about, quit };
enum class DialogResult { accept, cancel };

struct DialogButton {
    std::string_view label;
    DialogResult result;
};

struct Dialog {
    DialogKind kind;
    std::string_view title;
    std::string_view body;
    std::vector<DialogButton> buttons;
    std::size_t default_button = 0; // Initial selection; live focus belongs to the UI.
};

// Application state only. Simulation state and classic conformance belong in
// separate portable game modules; no frame timing or platform state enters those rules.
class Shell {
public:
    [[nodiscard]] Screen screen() const { return screen_; }
    [[nodiscard]] bool grid() const { return grid_; }
    [[nodiscard]] bool quitting() const { return quitting_; }
    [[nodiscard]] const std::optional<Dialog>& dialog() const { return dialog_; }
    [[nodiscard]] bool enabled(Command command) const;
    [[nodiscard]] bool checked(Command command) const;
    void dispatch(Command command);
    void request_close();
    void resolve_dialog(DialogResult result);

private:
    Screen screen_ = Screen::title;
    bool grid_ = true;
    bool quitting_ = false;
    std::optional<Dialog> dialog_;
};

} // namespace openciv
