#include "platform/menu_macos_bridge.h"

import std;
import openciv.platform.menu;

namespace openciv::platform {
namespace {
class MacMenu final : public Menu {
public:
    explicit MacMenu(CommandSink sink) : sink_(sink) {
        const auto& about = *find_menu_item(Command::about);
        const auto& quit = *find_menu_item(Command::quit);
        menu_.reset(oc2_mac_menu_create(
            this, post, static_cast<int>(about.command), std::string(about.label).c_str(),
            about.key, static_cast<int>(quit.command), std::string(quit.label).c_str(), quit.key));
        for (const auto& group : main_menu()) {
            bool started = false;
            for (const auto& item : group.items) {
                if (item.command == Command::quit || item.command == Command::about)
                    continue;
                if (!started) {
                    oc2_mac_menu_begin_group(menu_.get(), std::string(group.label).c_str());
                    started = true;
                }
                oc2_mac_menu_add_item(menu_.get(), static_cast<int>(item.command),
                                      std::string(item.label).c_str(), item.key);
            }
        }
    }
    bool native() const override { return true; }
    void sync(const Shell& shell) override {
        for (const auto& group : main_menu())
            for (const auto& item : group.items)
                oc2_mac_menu_sync_item(menu_.get(), static_cast<int>(item.command),
                                       shell.enabled(item.command), shell.checked(item.command));
    }

private:
    static void post(void* context, int command) {
        static_cast<MacMenu*>(context)->sink_(static_cast<Command>(command));
    }
    CommandSink sink_;
    std::unique_ptr<Oc2MacMenu, decltype(&oc2_mac_menu_destroy)> menu_{nullptr,
                                                                       oc2_mac_menu_destroy};
};
} // namespace
std::unique_ptr<Menu> create_menu(SDL_Window*, CommandSink sink) {
    return std::make_unique<MacMenu>(sink);
}
} // namespace openciv::platform
