#pragma once

#include "viewmodel/shell.hpp"
#include <imgui_te_engine.h>

// Shared by the main loop and the engine's cooperatively scheduled test thread.
// SDL calls stay in the main loop; tests request a new logical window size here.
struct GuiFixture {
    openciv::Shell shell;
    int width = 960;
    int height = 640;
    float density = 1;
};

void register_gui_tests(ImGuiTestEngine* engine, GuiFixture& fixture);
