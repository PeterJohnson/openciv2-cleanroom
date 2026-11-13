#include "platform/menu.hpp"

#include <SDL3/SDL.h>
#include <windows.h>
#include <commctrl.h>
#include <stdexcept>
#include <string>
#include <type_traits>

namespace openciv::platform {
namespace {
struct DeleteMenu {
    void operator()(HMENU menu) const { DestroyMenu(menu); }
};
using MenuHandle = std::unique_ptr<std::remove_pointer_t<HMENU>, DeleteMenu>;

std::wstring wide(std::string_view text) {
    const int length = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, text.data(),
                                           static_cast<int>(text.size()), nullptr, 0);
    if (length <= 0)
        throw std::runtime_error("Invalid UTF-8 menu label");
    std::wstring result(static_cast<std::size_t>(length), L'\0');
    MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, text.data(), static_cast<int>(text.size()),
                        result.data(), length);
    return result;
}

class WindowsMenu final : public Menu {
public:
    WindowsMenu(SDL_Window* window, CommandSink sink) : sink_(sink), menu_(CreateMenu()) {
        window_ = static_cast<HWND>(SDL_GetPointerProperty(
            SDL_GetWindowProperties(window), SDL_PROP_WINDOW_WIN32_HWND_POINTER, nullptr));
        if (!window_ || !menu_)
            throw std::runtime_error("Cannot create Win32 application menu");
        for (const auto& group : main_menu()) {
            MenuHandle popup(CreatePopupMenu());
            if (!popup)
                throw std::runtime_error("CreatePopupMenu failed");
            for (const auto& item : group.items) {
                auto label = wide(item.label);
                if (!item.shortcut.empty())
                    label += L'\t' + wide(item.shortcut);
                if (!AppendMenuW(popup.get(), MF_STRING, static_cast<UINT_PTR>(item.command),
                                 label.c_str()))
                    throw std::runtime_error("AppendMenuW failed");
            }
            const auto title = L'&' + wide(group.label);
            if (!AppendMenuW(menu_.get(), MF_POPUP, reinterpret_cast<UINT_PTR>(popup.get()),
                             title.c_str()))
                throw std::runtime_error("AppendMenuW group failed");
            popup.release(); // Parent menu now owns the submenu.
        }
        previous_ = GetMenu(window_);
        if (!SetWindowSubclass(window_, procedure, subclass_id, reinterpret_cast<DWORD_PTR>(this)))
            throw std::runtime_error("SetWindowSubclass failed");
        if (!SetMenu(window_, menu_.get())) {
            RemoveWindowSubclass(window_, procedure, subclass_id);
            throw std::runtime_error("SetMenu failed");
        }
        DrawMenuBar(window_);
    }

    ~WindowsMenu() override {
        if (window_) {
            RemoveWindowSubclass(window_, procedure, subclass_id);
            SetMenu(window_, previous_);
            DrawMenuBar(window_);
        }
    }
    bool native() const override { return true; }
    void sync(const Shell& shell) override {
        if (!window_)
            return;
        for (const auto& group : main_menu()) {
            for (const auto& item : group.items) {
                const auto id = static_cast<UINT>(item.command);
                EnableMenuItem(menu_.get(), id,
                               MF_BYCOMMAND |
                                   (shell.enabled(item.command) ? MF_ENABLED : MF_GRAYED));
                CheckMenuItem(menu_.get(), id,
                              MF_BYCOMMAND |
                                  (shell.checked(item.command) ? MF_CHECKED : MF_UNCHECKED));
            }
        }
        DrawMenuBar(window_);
    }

private:
    // Menu selections are sent directly to the window procedure. SDL's
    // pre-TranslateMessage hook alone would miss these synchronous messages.
    static LRESULT CALLBACK procedure(HWND window, UINT message, WPARAM wparam, LPARAM lparam,
                                      UINT_PTR, DWORD_PTR data) {
        auto& self = *reinterpret_cast<WindowsMenu*>(data);
        if (message == WM_COMMAND && lparam == 0 && HIWORD(wparam) == 0) {
            const auto command = static_cast<Command>(LOWORD(wparam));
            if (find_menu_item(command)) {
                self.sink_(command);
                return 0;
            }
        }
        if (message == WM_NCDESTROY) {
            RemoveWindowSubclass(window, procedure, subclass_id);
            self.window_ = nullptr;
            self.menu_.release(); // Windows destroys an attached menu with its window.
        }
        return DefSubclassProc(window, message, wparam, lparam);
    }
    static constexpr UINT_PTR subclass_id = 1;
    CommandSink sink_;
    MenuHandle menu_;
    HWND window_ = nullptr;
    HMENU previous_ = nullptr;
};
} // namespace

std::unique_ptr<Menu> create_menu(SDL_Window* window, CommandSink sink) {
    return std::make_unique<WindowsMenu>(window, sink);
}
} // namespace openciv::platform
