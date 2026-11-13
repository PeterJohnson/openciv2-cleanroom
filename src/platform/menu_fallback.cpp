#include "platform/menu.hpp"

namespace openciv::platform {
namespace {
class DrawnMenu final : public Menu {
public:
    bool native() const override { return false; }
    void sync(const Shell&) override {}
};
}

std::unique_ptr<Menu> create_drawn_menu() {
    return std::make_unique<DrawnMenu>();
}
#if (!defined(_WIN32) && !defined(__APPLE__)) || defined(__EMSCRIPTEN__)
std::unique_ptr<Menu> create_menu(SDL_Window*, CommandSink) { return create_drawn_menu(); }
#endif
} // namespace openciv::platform
