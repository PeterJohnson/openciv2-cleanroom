#include "application.hpp"

#include <stdexcept>
#include <utility>

namespace openciv {
namespace {
void require(bool success, const char* operation) {
    if (!success) throw std::runtime_error(std::string(operation) + ": " + SDL_GetError());
}
}

SdlContext::SdlContext() {
    SDL_SetAppMetadata("OpenCiv2", "0.1.0", "org.openciv2.cleanroom");
#if !defined(SDL_PLATFORM_EMSCRIPTEN)
    SDL_SetHint(SDL_HINT_MAIN_CALLBACK_RATE, "60");
#endif
    SDL_SetHint(SDL_HINT_TOUCH_MOUSE_EVENTS, "1");
    if (!SDL_Init(SDL_INIT_VIDEO)) {
        const std::string error = SDL_GetError();
        SDL_Quit();
        throw std::runtime_error("SDL_Init: " + error);
    }
}
SdlContext::~SdlContext() { SDL_Quit(); }

Application::Application(AppOptions options) : options_(std::move(options)) {
    window_.reset(SDL_CreateWindow("OpenCiv2", ui::width, ui::height,
        SDL_WINDOW_RESIZABLE | SDL_WINDOW_HIGH_PIXEL_DENSITY));
    require(window_ != nullptr, "SDL_CreateWindow");
    require(SDL_SetWindowMinimumSize(window_.get(), 480, 320), "SDL_SetWindowMinimumSize");
    renderer_.reset(SDL_CreateRenderer(window_.get(), nullptr));
    require(renderer_ != nullptr, "SDL_CreateRenderer");
    require(SDL_SetRenderLogicalPresentation(renderer_.get(), ui::width, ui::height,
        SDL_LOGICAL_PRESENTATION_LETTERBOX), "SDL_SetRenderLogicalPresentation");
    require(SDL_SetRenderDrawBlendMode(renderer_.get(), SDL_BLENDMODE_BLEND), "SDL_SetRenderDrawBlendMode");
    // A failed vsync request is harmless: SDL's callback rate still caps desktop work.
    SDL_SetRenderVSync(renderer_.get(), 1);
    command_event_ = SDL_RegisterEvents(1);
    require(command_event_ != 0, "SDL_RegisterEvents");
    const platform::CommandSink sink{this, [](void* context, Command command) noexcept {
        static_cast<Application*>(context)->post_command(command);
    }};
    menu_ = options_.native_menus ? platform::create_menu(window_.get(), sink) : platform::create_drawn_menu();
    interface_ = std::make_unique<ui::Interface>(window_.get(), renderer_.get(), menu_->native());
    sync_menu();
}

Application::~Application() {
    // Detach native callbacks while the inbox and all SDL resources still exist.
    menu_.reset();
}

void Application::post_command(Command command) noexcept {
    SDL_Event event{};
    event.type = command_event_;
    event.user.code = static_cast<Sint32>(command);
    if (!SDL_PushEvent(&event)) SDL_LogError(SDL_LOG_CATEGORY_APPLICATION, "Cannot enqueue menu command: %s", SDL_GetError());
}

void Application::post_event(const SDL_Event& event) {
    switch (event.type) {
    case SDL_EVENT_QUIT:
    case SDL_EVENT_WINDOW_CLOSE_REQUESTED:
    case SDL_EVENT_WINDOW_FOCUS_LOST:
    case SDL_EVENT_WINDOW_FOCUS_GAINED:
    case SDL_EVENT_WINDOW_MOUSE_ENTER:
    case SDL_EVENT_WINDOW_MOUSE_LEAVE:
    case SDL_EVENT_KEY_DOWN:
    case SDL_EVENT_KEY_UP:
    case SDL_EVENT_TEXT_INPUT:
    case SDL_EVENT_MOUSE_MOTION:
    case SDL_EVENT_MOUSE_WHEEL:
    case SDL_EVENT_MOUSE_BUTTON_DOWN:
    case SDL_EVENT_MOUSE_BUTTON_UP: break;
    default:
        if (event.type != command_event_) return;
    }
    QueuedEvent copy{event, {}};
    if (event.type == SDL_EVENT_TEXT_INPUT) {
        copy.text = event.text.text ? event.text.text : "";
        copy.event.text.text = nullptr;
    }
    const std::lock_guard lock(inbox_mutex_);
    inbox_.push_back(std::move(copy));
}

void Application::handle(QueuedEvent& queued) {
    auto& event = queued.event;
    if (event.type == SDL_EVENT_TEXT_INPUT) event.text.text = queued.text.c_str();
    // Feed ImGui original SDL coordinates, including key-up, wheel, focus and
    // owned UTF-8 text. Never apply the game's logical transform to UI input.
    interface_->process_event(event);
    if (event.type == SDL_EVENT_QUIT || event.type == SDL_EVENT_WINDOW_CLOSE_REQUESTED) {
        shell_.request_close();
    } else if (event.type == command_event_) {
        shell_.dispatch(static_cast<Command>(event.user.code));
    }
    sync_menu();
}

void Application::sync_menu() {
    const unsigned state = (shell_.screen() == Screen::session ? 1U : 0U)
        | (shell_.dialog() ? 2U : 0U) | (shell_.grid() ? 4U : 0U) | (shell_.quitting() ? 8U : 0U);
    if (state != menu_state_) { menu_->sync(shell_); menu_state_ = state; }
}

SDL_AppResult Application::iterate() {
    std::vector<QueuedEvent> events;
    { const std::lock_guard lock(inbox_mutex_); events.swap(inbox_); }
    for (auto& event : events) {
        handle(event);
        if (shell_.quitting()) return SDL_APP_SUCCESS;
    }
    interface_->draw(shell_);
    sync_menu(); // ImGui actions may have opened/closed a modal during this frame.
    if (shell_.quitting()) return SDL_APP_SUCCESS;
    if (!options_.screenshot.empty()) {
        std::unique_ptr<SDL_Surface, decltype(&SDL_DestroySurface)> surface(SDL_RenderReadPixels(renderer_.get(), nullptr), SDL_DestroySurface);
        require(surface != nullptr, "SDL_RenderReadPixels");
        require(SDL_SaveBMP(surface.get(), options_.screenshot.c_str()), "SDL_SaveBMP");
        options_.screenshot.clear();
    }
    require(SDL_RenderPresent(renderer_.get()), "SDL_RenderPresent");
    ++frame_count_;
    return options_.smoke_test && frame_count_ >= 3 ? SDL_APP_SUCCESS : SDL_APP_CONTINUE;
}

} // namespace openciv
