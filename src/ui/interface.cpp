#include "ui/interface.hpp"
#include "ui/fonts.hpp"

#include <imgui.h>
#include <imgui_internal.h> // Narrow F10 bridge; tested against our pinned release.
#include <imgui_impl_sdl3.h>
#include <imgui_impl_sdlrenderer3.h>
#include <algorithm>
#include <array>
#include <stdexcept>
#include <string>

namespace openciv::ui {
namespace {
constexpr ImVec4 ink{0.90F, 0.92F, 0.89F, 1};
constexpr ImVec4 muted{0.60F, 0.68F, 0.66F, 1};
constexpr ImVec4 panel{0.11F, 0.18F, 0.20F, 1};
constexpr ImVec4 accent{0.80F, 0.69F, 0.41F, 1};
constexpr ImGuiWindowFlags fixed_window = ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoResize |
                                          ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoCollapse |
                                          ImGuiWindowFlags_NoSavedSettings;

void require(bool success, const char* operation) {
    if (!success)
        throw std::runtime_error(std::string(operation) + ": " + SDL_GetError());
}
void text(std::string_view value) {
    ImGui::TextUnformatted(value.data(), value.data() + value.size());
}

void world(SDL_Renderer* renderer, const Shell& shell) {
    require(SDL_SetRenderScale(renderer, 1, 1), "SDL_SetRenderScale");
    require(SDL_SetRenderLogicalPresentation(renderer, width, height,
                                             SDL_LOGICAL_PRESENTATION_LETTERBOX),
            "World presentation");
    SDL_SetRenderDrawColor(renderer, 15, 29, 36, 255);
    SDL_RenderClear(renderer);
    if (shell.screen() != Screen::session)
        return;
    for (int y = 0; y < 9; ++y) {
        for (int x = 0; x < 16; ++x) {
            const SDL_FRect tile{32.0F + static_cast<float>(x) * 56,
                                 116.0F + static_cast<float>(y) * 48, 56, 48};
            const bool land = (x + y * 3) % 7 > 2;
            SDL_SetRenderDrawColor(renderer, land ? 49 : 30, land ? 82 : 62, land ? 65 : 79, 255);
            SDL_RenderFillRect(renderer, &tile);
            if (shell.grid()) {
                SDL_SetRenderDrawColor(renderer, 72, 100, 99, 255);
                SDL_RenderRect(renderer, &tile);
            }
        }
    }
}
} // namespace

Interface::Interface(SDL_Window* window, SDL_Renderer* renderer, bool native_menu)
    : window_(window), renderer_(renderer), native_menu_(native_menu) {
    IMGUI_CHECKVERSION();
    context_ = ImGui::CreateContext();
    if (!context_)
        throw std::runtime_error("Cannot create Dear ImGui context");
    try {
        auto& io = ImGui::GetIO();
        io.ConfigFlags |= ImGuiConfigFlags_NavEnableKeyboard;
        io.IniFilename = nullptr; // Layout is application-owned, including on WASM.
        io.LogFilename = nullptr;
        load_fonts(io);
        platform_ready_ = ImGui_ImplSDL3_InitForSDLRenderer(window_, renderer_);
        require(platform_ready_, "ImGui SDL3 initialization");
        renderer_ready_ = ImGui_ImplSDLRenderer3_Init(renderer_);
        require(renderer_ready_, "ImGui renderer initialization");
    } catch (...) {
        shutdown();
        throw;
    }
}

Interface::~Interface() {
    shutdown();
}

void Interface::shutdown() {
    if (!context_)
        return;
    ImGui::SetCurrentContext(context_);
    if (renderer_ready_)
        ImGui_ImplSDLRenderer3_Shutdown();
    if (platform_ready_)
        ImGui_ImplSDL3_Shutdown();
    ImGui::DestroyContext(context_);
    context_ = nullptr;
}

void Interface::process_event(const SDL_Event& event) {
    ImGui::SetCurrentContext(context_);
    ImGui_ImplSDL3_ProcessEvent(&event);
}

bool Interface::captures_mouse() const {
    ImGui::SetCurrentContext(context_);
    return ImGui::GetIO().WantCaptureMouse;
}

bool Interface::captures_keyboard() const {
    ImGui::SetCurrentContext(context_);
    return ImGui::GetIO().WantCaptureKeyboard;
}

void Interface::apply_style(float scale) {
    scale_ = scale;
    auto& style = ImGui::GetStyle();
    style = ImGuiStyle{}; // Rebuild from the base style, never compound DPI scales.
    ImGui::StyleColorsDark();
    style.WindowPadding = {24, 24};
    style.FramePadding = {12, 8};
    style.ItemSpacing = {12, 12};
    style.WindowRounding = 3;
    style.FrameRounding = 2;
    style.WindowBorderSize = 1;
    style.Colors[ImGuiCol_Text] = ink;
    style.Colors[ImGuiCol_TextDisabled] = muted;
    style.Colors[ImGuiCol_WindowBg] = panel;
    style.Colors[ImGuiCol_PopupBg] = panel;
    style.Colors[ImGuiCol_MenuBarBg] = panel;
    style.Colors[ImGuiCol_TitleBgActive] = panel;
    style.Colors[ImGuiCol_Button] = {0.18F, 0.28F, 0.29F, 1};
    style.Colors[ImGuiCol_ButtonHovered] = {0.26F, 0.38F, 0.37F, 1};
    style.Colors[ImGuiCol_ButtonActive] = {0.32F, 0.44F, 0.39F, 1};
    style.Colors[ImGuiCol_Header] = style.Colors[ImGuiCol_Button];
    style.Colors[ImGuiCol_HeaderHovered] = style.Colors[ImGuiCol_ButtonHovered];
    style.Colors[ImGuiCol_HeaderActive] = style.Colors[ImGuiCol_ButtonActive];
    style.Colors[ImGuiCol_CheckMark] = accent;
    style.Colors[ImGuiCol_NavCursor] = accent;
    style.Colors[ImGuiCol_ModalWindowDimBg] = {0.01F, 0.03F, 0.04F, 0.70F};
    style.FontSizeBase = 16;
    style.ScaleAllSizes(scale);
    style.FontScaleDpi = scale;
}

void Interface::shortcuts(Shell& shell) {
    if (shell.dialog())
        return;
    // Native AppKit owns Command equivalents. Other presenters register chords
    // explicitly: ImGuiMod_Ctrl maps to Command when macOS behavior is enabled.
#if defined(SDL_PLATFORM_MACOS)
    const bool bind_shortcuts = !native_menu_;
#else
    const bool bind_shortcuts = true;
#endif
    if (bind_shortcuts && !ImGui::GetIO().WantTextInput) {
        for (const auto& group : main_menu()) {
            for (const auto& item : group.items) {
                if (!item.key || !shell.enabled(item.command))
                    continue;
                const auto key = static_cast<ImGuiKey>(ImGuiKey_A + item.key - 'a');
                if (ImGui::Shortcut(ImGuiMod_Ctrl | key, ImGuiInputFlags_RouteGlobal))
                    shell.dispatch(item.command);
            }
        }
    }
    // Escape closes ImGui popups first; only an uncaptured key leaves the map.
    if (shell.screen() == Screen::session &&
        !ImGui::IsPopupOpen(nullptr, ImGuiPopupFlags_AnyPopup) &&
        !ImGui::GetIO().WantCaptureKeyboard && ImGui::IsKeyPressed(ImGuiKey_Escape, false))
        shell.dispatch(Command::return_to_title);
}

void Interface::draw_menu(Shell& shell) {
    if (native_menu_)
        return;
    if (ImGui::BeginMainMenuBar()) {
        if (!shell.dialog() && ImGui::IsKeyPressed(ImGuiKey_F10, false)) {
            // SetKeyboardFocusHere cannot select a standalone menu's navigation
            // layer. Keep this version-specific bridge isolated to F10.
            auto* window = ImGui::GetCurrentWindow();
            ImGui::FocusWindow(window);
            ImGui::SetNavID(0, ImGuiNavLayer_Menu, window->NavRootFocusScopeId, ImRect{});
            ImGui::NavInitWindow(window, true);
            ImGui::SetNavCursorVisible(true);
        }
        for (const auto& group : main_menu()) {
            if (ImGui::BeginMenu(group.label.data(), !shell.dialog())) {
                for (const auto& item : group.items)
                    if (ImGui::MenuItem(item.label.data(), item.shortcut.data(),
                                        shell.checked(item.command), shell.enabled(item.command)))
                        shell.dispatch(item.command);
                ImGui::EndMenu();
            }
        }
        ImGui::EndMainMenuBar();
    }
}

void Interface::draw_title(Shell& shell) {
    const auto size = ImGui::GetIO().DisplaySize;
    const float panel_width = std::min(440 * scale_, size.x - 32);
    ImGui::SetNextWindowPos({size.x / 2, size.y / 2}, ImGuiCond_Always, {0.5F, 0.5F});
    ImGui::SetNextWindowSize({panel_width, std::min(330 * scale_, size.y - 64)}, ImGuiCond_Always);
    ImGui::Begin("OpenCiv2###title", nullptr, fixed_window);
    ImGui::PushStyleColor(ImGuiCol_Text, accent);
    text("O P E N C I V 2");
    ImGui::PopStyleColor();
    ImGui::TextWrapped("Build a civilization. Tell a story.");
    ImGui::Dummy({0, 24 * scale_});
    constexpr std::array commands{Command::new_session, Command::about, Command::quit};
    constexpr std::array labels{"New Session", "About", "Quit"};
    ImGui::BeginDisabled(shell.dialog().has_value());
    for (std::size_t i = 0; i < commands.size(); ++i) {
        if (i == 0 && ImGui::IsWindowAppearing()) {
            ImGui::SetKeyboardFocusHere();
            ImGui::SetNavCursorVisible(true);
        }
        if (ImGui::Button(labels[i], {-1, 40 * scale_}))
            shell.dispatch(commands[i]);
    }
    ImGui::EndDisabled();
    ImGui::End();
}

void Interface::draw_session_controls(Shell& shell) {
    const auto* viewport = ImGui::GetMainViewport();
    ImGui::SetNextWindowPos({viewport->WorkPos.x + 16 * scale_, viewport->WorkPos.y + 12 * scale_});
    ImGui::SetNextWindowSize({viewport->WorkSize.x - 32 * scale_, 0});
    ImGui::Begin("Session###session", nullptr,
                 fixed_window | ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoNavFocus);
    ImGui::TextColored(accent, "DEVELOPMENT GAME VIEW");
    ImGui::TextWrapped("Illustrative tiles only. No simulation is running.");
    ImGui::BeginDisabled(!shell.enabled(Command::return_to_title));
    if (ImGui::Button("Return to Title"))
        shell.dispatch(Command::return_to_title);
    ImGui::EndDisabled();
    ImGui::End();
}

void Interface::draw_dialog(Shell& shell) {
    if (!shell.dialog()) {
        displayed_dialog_.reset();
        return;
    }
    const auto& dialog = *shell.dialog();
    const auto name = std::string(dialog.title) + "###active_dialog";
    if (displayed_dialog_ != dialog.kind) {
        ImGui::OpenPopup(name.c_str());
        displayed_dialog_ = dialog.kind;
    }
    const auto size = ImGui::GetIO().DisplaySize;
    ImGui::SetNextWindowPos({size.x / 2, size.y / 2}, ImGuiCond_Always, {0.5F, 0.5F});
    ImGui::SetNextWindowSize({std::min(680 * scale_, size.x - 32), 0});
    ImGui::SetNextWindowSizeConstraints({0, 0}, {size.x - 32, std::max(100.0F, size.y - 64)});
    std::optional<DialogResult> result;
    if (ImGui::BeginPopupModal(name.c_str(), nullptr,
                               ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoResize |
                                   ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoSavedSettings)) {
        // Custom content can mix draw-list artwork, textures, text and widgets.
        const auto cursor = ImGui::GetCursorScreenPos();
        auto* draw = ImGui::GetWindowDrawList();
        draw->AddRectFilled(cursor,
                            {cursor.x + ImGui::GetContentRegionAvail().x, cursor.y + 4 * scale_},
                            ImGui::GetColorU32(accent));
        ImGui::Dummy({0, 8 * scale_});
        ImGui::PushTextWrapPos(0);
        text(dialog.body);
        ImGui::PopTextWrapPos();
        ImGui::Dummy({0, 20 * scale_});
        for (std::size_t i = 0; i < dialog.buttons.size(); ++i) {
            if (i)
                ImGui::SameLine();
            if (i == dialog.default_button && ImGui::IsWindowAppearing()) {
                ImGui::SetKeyboardFocusHere();
                ImGui::SetNavCursorVisible(true);
            }
            if (ImGui::Button(dialog.buttons[i].label.data(), {112 * scale_, 36 * scale_}))
                result = dialog.buttons[i].result;
        }
        if (ImGui::IsKeyPressed(ImGuiKey_Escape, false))
            result = DialogResult::cancel;
        if (result)
            ImGui::CloseCurrentPopup();
        ImGui::EndPopup();
    }
    if (result) {
        shell.resolve_dialog(*result);
        displayed_dialog_.reset();
    }
}

void Interface::draw(Shell& shell) {
    ImGui::SetCurrentContext(context_);
    // ImGui uses window coordinates, independent of the letterboxed game view.
    require(SDL_SetRenderLogicalPresentation(renderer_, 0, 0, SDL_LOGICAL_PRESENTATION_DISABLED),
            "UI presentation");
    require(SDL_SetRenderScale(renderer_, 1, 1), "UI scale");
    const float scale =
        std::max(1.0F, SDL_GetDisplayContentScale(SDL_GetDisplayForWindow(window_)));
    if (scale_ != scale)
        apply_style(scale);
    ImGui_ImplSDLRenderer3_NewFrame();
    ImGui_ImplSDL3_NewFrame();
    ImGui::NewFrame();
    shortcuts(shell);
    draw_menu(shell);
    if (shell.screen() == Screen::title)
        draw_title(shell);
    else
        draw_session_controls(shell);
    draw_dialog(shell);
    ImGui::Render();
    world(renderer_, shell);
    require(SDL_SetRenderLogicalPresentation(renderer_, 0, 0, SDL_LOGICAL_PRESENTATION_DISABLED),
            "UI presentation");
    const auto& io = ImGui::GetIO();
    require(
        SDL_SetRenderScale(renderer_, io.DisplayFramebufferScale.x, io.DisplayFramebufferScale.y),
        "UI framebuffer scale");
    ImGui_ImplSDLRenderer3_RenderDrawData(ImGui::GetDrawData(), renderer_);
}

} // namespace openciv::ui
