export module openciv.tests.gui_fixture;

import std;
export import openciv.viewmodel.shell;

export extern "C++" {
    struct ImGuiTestEngine;
    // Shared by the main loop and the engine's cooperatively scheduled test thread.
    // SDL calls stay in the main loop; tests request a new logical window size here.
    struct GuiFixture {
        openciv::Shell shell;
        int width = 960;
        int height = 640;
        float density = 1;
    };

    void register_gui_tests(ImGuiTestEngine * engine, GuiFixture & fixture);
}
