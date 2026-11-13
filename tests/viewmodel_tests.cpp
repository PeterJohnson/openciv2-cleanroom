#include "viewmodel/menu.hpp"
#include <catch2/catch_test_macros.hpp>
#include <set>

using namespace openciv;

// Application-shell contracts only; these are not classic P05 conformance cases.
TEST_CASE("Title commands reject unavailable and unknown actions", "[shell]") {
    Shell shell;
    CHECK(shell.screen() == Screen::title);
    CHECK_FALSE(shell.enabled(Command::return_to_title));
    CHECK_FALSE(shell.enabled(Command::toggle_grid));
    shell.dispatch(Command::toggle_grid);
    CHECK(shell.grid());
    shell.dispatch(static_cast<Command>(-1));
    CHECK_FALSE(shell.dialog());
}

TEST_CASE("A pending dialog blocks stale commands and preserves cancel", "[shell]") {
    Shell shell;
    shell.dispatch(Command::new_session);
    REQUIRE(shell.dialog());
    REQUIRE(shell.dialog()->kind == DialogKind::new_session);
    shell.dispatch(Command::about);
    shell.request_close();
    REQUIRE(shell.dialog());
    CHECK(shell.dialog()->kind == DialogKind::new_session);
    for (const auto& group : main_menu())
        for (const auto& item : group.items) CHECK_FALSE(shell.enabled(item.command));
    CHECK(shell.dialog()->default_button == 0);
    shell.resolve_dialog(DialogResult::cancel);
    CHECK_FALSE(shell.dialog());
    CHECK(shell.screen() == Screen::title);
}

TEST_CASE("Session commands update shared menu state", "[shell]") {
    Shell shell;
    shell.dispatch(Command::new_session);
    shell.resolve_dialog(DialogResult::accept);
    CHECK(shell.screen() == Screen::session);
    CHECK(shell.enabled(Command::toggle_grid));
    shell.dispatch(Command::toggle_grid);
    CHECK_FALSE(shell.checked(Command::toggle_grid));
    shell.dispatch(Command::about);
    REQUIRE(shell.dialog());
    CHECK(shell.dialog()->default_button == 0);
    shell.dispatch(Command::return_to_title);
    CHECK(shell.screen() == Screen::session);
    shell.resolve_dialog(DialogResult::cancel);
    shell.dispatch(Command::return_to_title);
    CHECK(shell.screen() == Screen::title);
}

TEST_CASE("Quit defaults to Cancel and requires explicit acceptance", "[shell]") {
    Shell shell;
    shell.request_close();
    REQUIRE(shell.dialog());
    REQUIRE(shell.dialog()->default_button < shell.dialog()->buttons.size());
    CHECK(shell.dialog()->default_button == 1);
    shell.request_close();
    shell.resolve_dialog(shell.dialog()->buttons[shell.dialog()->default_button].result);
    CHECK_FALSE(shell.quitting());
    shell.request_close();
    shell.resolve_dialog(DialogResult::accept);
    CHECK(shell.quitting());
    CHECK_FALSE(shell.enabled(Command::new_session));
    shell.resolve_dialog(DialogResult::accept);
    CHECK(shell.quitting());
}

TEST_CASE("Menu actions and shortcuts have unique identities", "[shell]") {
    std::set<Command> ids;
    std::set<char> shortcuts;
    for (const auto& group : main_menu()) {
        CHECK_FALSE(group.items.empty());
        for (const auto& item : group.items) {
            CHECK(ids.insert(item.command).second);
            CHECK(find_menu_item(item.command) == &item);
            if (item.key) CHECK(shortcuts.insert(item.key).second);
        }
    }
    CHECK(find_menu_item(static_cast<Command>(0)) == nullptr);
}
