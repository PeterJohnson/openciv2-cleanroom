#include "application.hpp"
#include <catch2/catch_test_macros.hpp>
#include <imgui.h>
#include <imgui_internal.h> // Observe the pinned library's processed input trail.
#include <thread>
#include <memory>
#include <string_view>

using namespace openciv;

namespace {
void fractional_modal_rendering() {
    // Fractional framebuffer density must not add AA geometry to the dimming
    // rectangle, which ImGui moves as a single six-index command.
    const std::unique_ptr<ImGuiContext, decltype(&ImGui::DestroyContext)> context(
        ImGui::CreateContext(), ImGui::DestroyContext);
    auto& io = ImGui::GetIO();
    io.IniFilename = nullptr;
    io.DisplaySize = {960, 640};
    io.DeltaTime = 1.0F / 60;
    unsigned char* pixels = nullptr;
    int width = 0, height = 0;
    io.Fonts->GetTexDataAsRGBA32(&pixels, &width, &height);
    REQUIRE(pixels != nullptr);
    REQUIRE(width > 0);
    REQUIRE(height > 0);
    for (const float density : {1.0F, 1.25F, 1.5F, 1.65F, 2.0F}) {
        io.DisplayFramebufferScale = {density, density};
        for (int frame = 0; frame < 3; ++frame) {
            ImGui::NewFrame();
            if (frame == 0)
                ImGui::OpenPopup("Fractional density modal");
            if (ImGui::BeginPopupModal("Fractional density modal", nullptr,
                                       ImGuiWindowFlags_AlwaysAutoResize)) {
                ImGui::TextUnformatted("About OpenCiv2");
                ImGui::EndPopup();
            }
            ImGui::Render();
            if (frame > 0)
                REQUIRE(ImGui::GetDrawData()->TotalIdxCount > 0);
        }
    }
}

void frame(Application& app, int count = 1) {
    for (int i = 0; i < count; ++i)
        REQUIRE(app.iterate() == SDL_APP_CONTINUE);
}
SDL_Event key_event(Application& app, SDL_Keycode code, Uint32 type,
                    SDL_Keymod mod = SDL_KMOD_NONE) {
    SDL_Event event{};
    event.type = type;
    event.key.windowID = SDL_GetWindowID(app.window());
    event.key.key = code;
    event.key.scancode = SDL_GetScancodeFromKey(code, nullptr);
    event.key.mod = mod;
    event.key.down = type == SDL_EVENT_KEY_DOWN;
    return event;
}
void key(Application& app, SDL_Keycode code, SDL_Keymod mod = SDL_KMOD_NONE) {
    app.post_event(key_event(app, code, SDL_EVENT_KEY_DOWN, mod));
    frame(app);
    app.post_event(key_event(app, code, SDL_EVENT_KEY_UP));
    frame(app, 2); // ImGui applies focus/navigation requests on subsequent frames.
}
void motion(Application& app, float x, float y) {
    SDL_Event event{};
    event.type = SDL_EVENT_MOUSE_MOTION;
    event.motion.windowID = SDL_GetWindowID(app.window());
    event.motion.x = x;
    event.motion.y = y;
    app.post_event(event);
    frame(app);
}
void pointer(Application& app, Uint32 type, float x, float y) {
    SDL_Event event{};
    event.type = type;
    event.button.windowID = SDL_GetWindowID(app.window());
    event.button.button = SDL_BUTTON_LEFT;
    event.button.x = x;
    event.button.y = y;
    event.button.down = type == SDL_EVENT_MOUSE_BUTTON_DOWN;
    app.post_event(event);
    frame(app);
}
void click(Application& app, float x, float y) {
    motion(app, x, y);
    pointer(app, SDL_EVENT_MOUSE_BUTTON_DOWN, x, y);
    pointer(app, SDL_EVENT_MOUSE_BUTTON_UP, x, y);
    frame(app, 2);
}
void command(Application& app, Command value) {
    app.post_command(value);
    SDL_Event event{};
    while (SDL_PollEvent(&event))
        app.post_event(event);
    frame(app, 3);
}
} // namespace

TEST_CASE("Fractional density modal geometry remains valid", "[runtime]") {
    fractional_modal_rendering();
}

TEST_CASE("Stock TrueType fonts rasterize at multiple sizes and densities", "[runtime][fonts]") {
    // Destroy and recreate the context: atlas shutdown must not free embedded TTF bytes.
    for (int lifetime = 0; lifetime < 2; ++lifetime) {
        Application app({.native_menus = false, .smoke_test = false, .screenshot = {}});
        frame(app);
        auto& io = ImGui::GetIO();
        REQUIRE(io.FontDefault != nullptr);
        REQUIRE(std::string_view(io.FontDefault->GetDebugName()) == "DejaVuSans");
        REQUIRE(io.Fonts->Fonts.Size == 12);
        for (auto* font : io.Fonts->Fonts) {
            INFO(font->GetDebugName());
            for (const float size : {16.0F, 24.0F}) {
                for (const float density : {1.0F, 1.65F, 2.0F}) {
                    auto* baked = font->GetFontBaked(size, density);
                    REQUIRE(baked != nullptr);
                    for (const ImWchar codepoint :
                         {ImWchar('A'), ImWchar(0x00e9), ImWchar(0x03a9)}) {
                        const auto* glyph = baked->FindGlyphNoFallback(codepoint);
                        REQUIRE(glyph != nullptr);
                        CHECK(glyph->AdvanceX > 0.0F);
                        CHECK(glyph->X1 > glyph->X0);
                        CHECK(glyph->Y1 > glyph->Y0);
                        CHECK(glyph->U1 > glyph->U0);
                        CHECK(glyph->V1 > glyph->V0);
                    }
                }
            }
        }
        frame(app, 2); // SDL uploads the atlas changes created by these glyph requests.
    }
}

TEST_CASE("SDL input routing and backend lifecycle", "[runtime]") {
    {
        Application app({.native_menus = false, .smoke_test = false, .screenshot = {}});
        frame(app, 3);
        REQUIRE(ImGui::GetCurrentContext() != nullptr);
        REQUIRE(ImGui::GetIO().IniFilename == nullptr);
        click(app, 480, 342);
        REQUIRE(app.shell().dialog());
        REQUIRE(app.shell().dialog()->kind == DialogKind::about);
        frame(app, 12);
        key(app, SDLK_ESCAPE);
        REQUIRE(!app.shell().dialog());
        motion(app, 480, 290);
        REQUIRE(app.ui_captures_mouse());
        pointer(app, SDL_EVENT_MOUSE_BUTTON_UP, 480, 290);
        REQUIRE(!app.shell().dialog());
        pointer(app, SDL_EVENT_MOUSE_BUTTON_DOWN, 480, 290);
        SDL_Event focus{};
        focus.type = SDL_EVENT_WINDOW_FOCUS_LOST;
        focus.window.windowID = SDL_GetWindowID(app.window());
        app.post_event(focus);
        frame(app);
        pointer(app, SDL_EVENT_MOUSE_BUTTON_UP, 480, 290);
        REQUIRE(!app.shell().dialog());
        focus.type = SDL_EVENT_WINDOW_FOCUS_GAINED;
        app.post_event(focus);
        frame(app);

        click(app, 480, 290);
        REQUIRE(app.shell().dialog());
        REQUIRE(app.shell().dialog()->kind == DialogKind::new_session);
        REQUIRE(app.ui_captures_keyboard());
        key(app, SDLK_TAB);
        key(app, SDLK_RETURN);
        REQUIRE(!app.shell().dialog());
        REQUIRE(app.shell().screen() == Screen::title);
        click(app, 480, 290);
        command(app, Command::about);
        REQUIRE(app.shell().dialog());
        REQUIRE(app.shell().dialog()->kind == DialogKind::new_session);
        click(app, 40, 16); // Main menu behind a modal is blocked.
        REQUIRE(app.shell().dialog());
        REQUIRE(app.shell().dialog()->kind == DialogKind::new_session);
        key(app, SDLK_G, SDL_KMOD_CTRL);
        REQUIRE(app.shell().grid());
        key(app, SDLK_ESCAPE);
        REQUIRE(!app.shell().dialog());
        REQUIRE(app.shell().screen() == Screen::title);

        key(app, SDLK_F10);
        key(app, SDLK_DOWN);
        key(app, SDLK_RETURN);
        REQUIRE(app.shell().dialog());
        REQUIRE(app.shell().dialog()->kind == DialogKind::new_session);
        key(app, SDLK_RETURN);
        REQUIRE(app.shell().screen() == Screen::session);
        key(app, SDLK_G, SDL_KMOD_CTRL);
        REQUIRE(!app.shell().grid());
        command(app, Command::toggle_grid);
        REQUIRE(app.shell().grid());
        command(app, Command::return_to_title);

        // The UI follows window coordinates, independently of the world's
        // letterboxing. A resize recenters the title without scaling input.
        REQUIRE(SDL_SetWindowSize(app.window(), 1200, 640));
        frame(app, 3);
        click(app, 40, 290);
        REQUIRE(!app.shell().dialog());
        click(app, 600, 290);
        REQUIRE(app.shell().dialog());
        REQUIRE(app.shell().dialog()->kind == DialogKind::new_session);
        key(app, SDLK_ESCAPE);

        // Borrowed UTF-8 from a producer must survive until main-thread use.
        const auto window_id = SDL_GetWindowID(app.window());
        std::thread producer([&] {
            std::string temporary = "caf\xc3\xa9";
            SDL_Event text{};
            text.type = SDL_EVENT_TEXT_INPUT;
            text.text.windowID = window_id;
            text.text.text = temporary.c_str();
            app.post_event(text);
            temporary.assign(1000, 'x');
        });
        producer.join();
        frame(app);
        bool found_accent = false;
        for (const auto& event : ImGui::GetCurrentContext()->InputEventsTrail)
            if (event.Type == ImGuiInputEventType_Text && event.Text.Char == 0xe9)
                found_accent = true;
        REQUIRE(found_accent);
        SDL_Event wheel{};
        wheel.type = SDL_EVENT_MOUSE_WHEEL;
        wheel.wheel.windowID = SDL_GetWindowID(app.window());
        wheel.wheel.y = 2;
        app.post_event(wheel);
        frame(app);
        bool found_wheel = false;
        for (const auto& event : ImGui::GetCurrentContext()->InputEventsTrail)
            if (event.Type == ImGuiInputEventType_MouseWheel && event.MouseWheel.WheelY == 2)
                found_wheel = true;
        REQUIRE(found_wheel);

        SDL_Event close{};
        close.type = SDL_EVENT_QUIT;
        app.post_event(close);
        frame(app, 3);
        REQUIRE(app.shell().dialog());
        REQUIRE(app.shell().dialog()->kind == DialogKind::quit);
        key(app, SDLK_RETURN); // Default Cancel.
        REQUIRE(!app.shell().quitting());
        REQUIRE(!app.shell().dialog());
        app.post_event(close);
        frame(app, 3);
        key(app, SDLK_LEFT);
        app.post_event(key_event(app, SDLK_RETURN, SDL_EVENT_KEY_DOWN));
        REQUIRE(app.iterate() == SDL_APP_SUCCESS);
    }
    REQUIRE(ImGui::GetCurrentContext() == nullptr);
    // Reinitialization verifies complete backend/texture/context teardown.
    {
        Application app({.native_menus = false, .smoke_test = false, .screenshot = {}});
        frame(app, 2);
    }
    REQUIRE(ImGui::GetCurrentContext() == nullptr);
}
