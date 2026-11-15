#include <SDL3/SDL.h>
#define SDL_MAIN_USE_CALLBACKS 1
#include <SDL3/SDL_main.h>

import std;
import openciv.application;

namespace {
SDL_AppResult failure(const char* message) {
    SDL_LogError(SDL_LOG_CATEGORY_APPLICATION, "%s", message);
    return SDL_APP_FAILURE;
}
} // namespace

SDL_AppResult SDL_AppInit(void** appstate, int argc, char* argv[]) {
    try {
        openciv::AppOptions options;
        for (int i = 1; i < argc; ++i) {
            const std::string_view argument(argv[i]);
            if (argument == "--smoke-test") {
                options.smoke_test = true;
                options.native_menus = false;
            } else if (argument == "--screenshot" && i + 1 < argc)
                options.screenshot = argv[++i];
            else
                return failure("Usage: openciv2 [--smoke-test] [--screenshot output.bmp]");
        }
        *appstate = new openciv::Application(std::move(options));
        return SDL_APP_CONTINUE;
    } catch (const std::exception& error) {
        return failure(error.what());
    } catch (...) {
        return failure("Unknown startup error");
    }
}

SDL_AppResult SDL_AppEvent(void* appstate, SDL_Event* event) {
    try {
        static_cast<openciv::Application*>(appstate)->post_event(*event);
        return SDL_APP_CONTINUE;
    } catch (const std::exception& error) {
        return failure(error.what());
    } catch (...) {
        return failure("Unknown input error");
    }
}

SDL_AppResult SDL_AppIterate(void* appstate) {
    try {
        return static_cast<openciv::Application*>(appstate)->iterate();
    } catch (const std::exception& error) {
        return failure(error.what());
    } catch (...) {
        return failure("Unknown frame error");
    }
}

void SDL_AppQuit(void* appstate, SDL_AppResult) {
    delete static_cast<openciv::Application*>(appstate);
}
