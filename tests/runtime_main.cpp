#include <SDL3/SDL.h>
#include <SDL3/SDL_main.h>
#include <catch2/catch_session.hpp>

import std;

#define SDL_MAIN_HANDLED

int main(int argc, char** argv) {
    SDL_SetMainReady();
    return Catch::Session().run(argc, argv);
}
