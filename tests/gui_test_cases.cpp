#include "gui_fixture.hpp"
#include <imgui_te_context.h>

using namespace openciv;

namespace {
GuiFixture& fixture(ImGuiTestContext* ctx) { return *static_cast<GuiFixture*>(ctx->Test->UserData); }
Shell& shell(ImGuiTestContext* ctx) { return fixture(ctx).shell; }

void prepare(ImGuiTest* test, GuiFixture& data) {
    test->UserData = &data;
    test->GuiFunc = [](ImGuiTestContext* ctx) {
        if (ctx->IsFirstGuiFrame()) {
            shell(ctx) = Shell{};
            fixture(ctx).width = 960;
            fixture(ctx).height = 640;
        }
    };
    test->TeardownFunc = [](ImGuiTestContext* ctx) {
        ctx->PopupCloseAll();
        shell(ctx) = Shell{};
        ctx->Yield(2);
    };
}
}

void register_gui_tests(ImGuiTestEngine* engine, GuiFixture& data) {
    auto* test = IM_REGISTER_TEST(engine, "shell", "about_dialog");
    prepare(test, data);
    test->TestFunc = [](ImGuiTestContext* ctx) {
        ctx->ItemClick("//###title/About");
        IM_CHECK(shell(ctx).dialog().has_value());
        IM_CHECK(shell(ctx).dialog()->kind == DialogKind::about);
        ctx->Yield(12); // Exercise dimming across its animation, at every density.
        IM_CHECK(ctx->ItemIsVisible("//###active_dialog/Close"));
        ctx->ItemClick("//###active_dialog/Close");
        IM_CHECK(!shell(ctx).dialog());
        ctx->MenuClick("//##MainMenuBar/Help/About OpenCiv2");
        IM_CHECK(shell(ctx).dialog().has_value());
        IM_CHECK(shell(ctx).dialog()->kind == DialogKind::about);
        ctx->KeyPress(ImGuiKey_Escape);
        IM_CHECK(!shell(ctx).dialog());
    };

    test = IM_REGISTER_TEST(engine, "shell", "dialog_keyboard_focus");
    prepare(test, data);
    test->TestFunc = [](ImGuiTestContext* ctx) {
        ctx->ItemClick("//###title/New Session");
        IM_CHECK(shell(ctx).dialog().has_value());
        IM_CHECK_EQ(ctx->UiContext->NavId, ctx->ItemInfo("//###active_dialog/Open").ID);
        ctx->KeyPress(ImGuiKey_Tab);
        IM_CHECK_EQ(ctx->UiContext->NavId, ctx->ItemInfo("//###active_dialog/Cancel").ID);
        ctx->KeyPress(ImGuiMod_Shift | ImGuiKey_Tab);
        IM_CHECK_EQ(ctx->UiContext->NavId, ctx->ItemInfo("//###active_dialog/Open").ID);
        ctx->KeyPress(ImGuiKey_Tab);
        ctx->KeyPress(ImGuiKey_Enter);
        IM_CHECK(!shell(ctx).dialog());
        IM_CHECK(shell(ctx).screen() == Screen::title);
        ctx->ItemClick("//###title/New Session");
        ctx->KeyPress(ImGuiKey_Enter);
        IM_CHECK(shell(ctx).screen() == Screen::session);
        ctx->ItemClick("//###session/Return to Title");
        IM_CHECK(shell(ctx).screen() == Screen::title);
    };

    test = IM_REGISTER_TEST(engine, "shell", "menus_and_checkmarks");
    prepare(test, data);
    test->TestFunc = [](ImGuiTestContext* ctx) {
        ctx->MenuClick("//##MainMenuBar/Game");
        IM_CHECK(ctx->ItemInfo("//###Menu_00/Return to Title").ItemFlags & ImGuiItemFlags_Disabled);
        ctx->KeyPress(ImGuiKey_Escape);
        ctx->MenuClick("//##MainMenuBar/Game/New Session...");
        ctx->ItemClick("//###active_dialog/Open");
        IM_CHECK(shell(ctx).screen() == Screen::session);
        ctx->MenuUncheck("//##MainMenuBar/View/Show Grid");
        IM_CHECK(!shell(ctx).grid());
        ctx->MenuCheck("//##MainMenuBar/View/Show Grid");
        IM_CHECK(shell(ctx).grid());
        ctx->MenuClick("//##MainMenuBar/Game/Return to Title");
        IM_CHECK(shell(ctx).screen() == Screen::title);
    };

    test = IM_REGISTER_TEST(engine, "shell", "f10_and_shortcuts");
    prepare(test, data);
    test->TestFunc = [](ImGuiTestContext* ctx) {
        ctx->KeyPress(ImGuiKey_F10);
        ctx->KeyPress(ImGuiKey_DownArrow);
        ctx->KeyPress(ImGuiKey_Enter);
        IM_CHECK(shell(ctx).dialog().has_value());
        IM_CHECK(shell(ctx).dialog()->kind == DialogKind::new_session);
        ctx->KeyPress(ImGuiKey_Enter);
        IM_CHECK(shell(ctx).screen() == Screen::session);
        ctx->KeyPress(ImGuiMod_Ctrl | ImGuiKey_G);
        IM_CHECK(!shell(ctx).grid());
        ctx->KeyPress(ImGuiMod_Ctrl | ImGuiKey_T);
        IM_CHECK(shell(ctx).screen() == Screen::title);
    };

    test = IM_REGISTER_TEST(engine, "shell", "modal_blocks_background");
    prepare(test, data);
    test->TestFunc = [](ImGuiTestContext* ctx) {
        ctx->ItemClick("//###title/New Session");
        ctx->ItemClick("//###active_dialog/Open");
        ctx->MenuClick("//##MainMenuBar/Help/About OpenCiv2");
        IM_CHECK(shell(ctx).dialog().has_value());
        IM_CHECK(shell(ctx).dialog()->kind == DialogKind::about);
        IM_CHECK(ctx->ItemInfo("//###session/Return to Title").ItemFlags & ImGuiItemFlags_Disabled);
        // Aim for a blocked widget without the engine focusing/unblocking it.
        const auto menu = ctx->ItemInfo("//##MainMenuBar/##MenuBar/Game");
        IM_CHECK(menu.ItemFlags & ImGuiItemFlags_Disabled);
        ctx->MouseMoveToPos(menu.RectFull.GetCenter());
        ctx->MouseClick();
        ctx->KeyPress(ImGuiMod_Ctrl | ImGuiKey_G);
        ctx->KeyPress(ImGuiMod_Ctrl | ImGuiKey_T);
        shell(ctx).dispatch(Command::return_to_title); // Same guard used by OS menu commands.
        IM_CHECK(shell(ctx).screen() == Screen::session);
        IM_CHECK(shell(ctx).grid());
        IM_CHECK(shell(ctx).dialog().has_value());
        IM_CHECK(shell(ctx).dialog()->kind == DialogKind::about);
        IM_CHECK(!ctx->ItemIsOpened("//##MainMenuBar/##MenuBar/Game"));
        ctx->KeyPress(ImGuiKey_Escape);
        IM_CHECK(!shell(ctx).dialog());
        ctx->ItemClick("//###session/Return to Title");
        IM_CHECK(shell(ctx).screen() == Screen::title);
    };

    test = IM_REGISTER_TEST(engine, "shell", "resize_and_scroll");
    prepare(test, data);
    test->TestFunc = [](ImGuiTestContext* ctx) {
        auto& state = fixture(ctx);
        for (const auto size : {ImVec2{1200, 720}, ImVec2{480, 320}, ImVec2{960, 640}}) {
            state.width = static_cast<int>(size.x);
            state.height = static_cast<int>(size.y);
            ctx->Yield(4);
            ctx->ItemClick("//###title/About");
            IM_CHECK(shell(ctx).dialog().has_value());
            IM_CHECK(shell(ctx).dialog()->kind == DialogKind::about);
            ctx->ItemClick("//###active_dialog/Close");
            IM_CHECK(!shell(ctx).dialog());
            ctx->ItemClick("//###title/Quit");
            ctx->KeyPress(ImGuiKey_Escape);
            IM_CHECK(!shell(ctx).quitting());
        }
    };

    test = IM_REGISTER_TEST(engine, "shell", "quit_defaults_to_cancel");
    prepare(test, data);
    test->TestFunc = [](ImGuiTestContext* ctx) {
        ctx->ItemClick("//###title/Quit");
        IM_CHECK(shell(ctx).dialog().has_value());
        IM_CHECK(shell(ctx).dialog()->kind == DialogKind::quit);
        IM_CHECK_EQ(ctx->UiContext->NavId, ctx->ItemInfo("//###active_dialog/Cancel").ID);
        ctx->KeyPress(ImGuiKey_Enter);
        IM_CHECK(!shell(ctx).quitting());
        IM_CHECK(!shell(ctx).dialog());
        ctx->MenuClick("//##MainMenuBar/Game/Quit...");
        ctx->KeyPress(ImGuiKey_LeftArrow);
        IM_CHECK_EQ(ctx->UiContext->NavId, ctx->ItemInfo("//###active_dialog/Quit").ID);
        ctx->KeyPress(ImGuiKey_Enter);
        IM_CHECK(shell(ctx).quitting());
    };
}
