#define SDL_MAIN_HANDLED
#include <SDL3/SDL_main.h>
#include <catch2/catch_session.hpp>

int main(int argc, char** argv) {
    SDL_SetMainReady();
    return Catch::Session().run(argc, argv);
}
