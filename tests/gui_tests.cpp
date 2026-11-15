#include <SDL3/SDL.h>
#include <SDL3/SDL_main.h>
#include <imgui_te_engine.h>
#include <imgui_te_exporters.h>

import std;
import imgui_internal;
import openciv.tests.gui_fixture;
import openciv.ui.interface;

namespace {
void require(bool success, const char* operation) {
    if (!success)
        throw std::runtime_error(std::string(operation) + ": " + SDL_GetError());
}
struct Options {
    bool visible = false;
    float density = 1;
    std::string filter = "shell";
    std::filesystem::path output;
};
struct SdlLifetime {
    explicit SdlLifetime(bool visible) {
        if (!visible)
            SDL_SetHint(SDL_HINT_VIDEO_DRIVER, "dummy");
        require(SDL_Init(SDL_INIT_VIDEO), "SDL_Init");
    }
    ~SdlLifetime() { SDL_Quit(); }
};
using Surface = std::unique_ptr<SDL_Surface, decltype(&SDL_DestroySurface)>;

class Runner {
public:
    explicit Runner(const Options& options) : sdl_(options.visible), visible_(options.visible) {
        fixture_.density = options.density;
        const auto flags = SDL_WINDOW_RESIZABLE | SDL_WINDOW_HIGH_PIXEL_DENSITY |
                           (visible_ ? 0 : SDL_WINDOW_HIDDEN);
        window_.reset(SDL_CreateWindow("OpenCiv2 GUI tests", 960, 640, flags));
        require(window_ != nullptr, "SDL_CreateWindow");
        renderer_.reset(SDL_CreateRenderer(window_.get(), visible_ ? nullptr : "software"));
        require(renderer_ != nullptr, "SDL_CreateRenderer");
        SDL_SetRenderVSync(renderer_.get(), visible_ ? 1 : 0);
        interface_ =
            std::make_unique<openciv::ui::Interface>(window_.get(), renderer_.get(), false);
        engine_.reset(ImGuiTestEngine_CreateContext());
        auto& io = ImGuiTestEngine_GetIO(engine_.get());
        io.ConfigSavedSettings = false;
        io.ConfigRunSpeed = visible_ ? ImGuiTestRunSpeed_Normal : ImGuiTestRunSpeed_Fast;
        io.ConfigFixedDeltaTime = 1.0F / 60;
        io.ConfigLogToTTY = true;
        io.ConfigVerboseLevel = ImGuiTestVerboseLevel_Info;
        io.ConfigVerboseLevelOnError = ImGuiTestVerboseLevel_Debug;
        io.ConfigCaptureOnError = true;
        io.CheckDrawDataIntegrity = true;
        io.ScreenCaptureFunc = capture;
        io.ScreenCaptureUserData = this;
        io.ExportResultsFilename = "results.xml";
        io.ExportResultsFormat = ImGuiTestEngineExportFormat_JUnitXml;
        register_gui_tests(engine_.get(), fixture_);
        ImGuiTestEngine_Start(engine_.get(), ImGui::GetCurrentContext());
        ImGuiTestEngine_InstallDefaultCrashHandler();
        if (!visible_) {
            ImGuiContextHook hook;
            hook.Type = ImGuiContextHookType_NewFramePre;
            hook.UserData = &fixture_;
            hook.Callback = [](ImGuiContext* context, ImGuiContextHook* value) {
                const auto& data = *static_cast<GuiFixture*>(value->UserData);
                context->IO.DisplaySize = {static_cast<float>(data.width),
                                           static_cast<float>(data.height)};
                context->IO.DisplayFramebufferScale = {data.density, data.density};
            };
            ImGui::AddContextHook(ImGui::GetCurrentContext(), &hook);
        }
        ImGuiTestEngine_QueueTests(engine_.get(), ImGuiTestGroup_Tests, options.filter.c_str(),
                                   ImGuiTestRunFlags_RunFromCommandLine);
    }
    ~Runner() {
        if (engine_)
            ImGuiTestEngine_Stop(engine_.get());
        interface_.reset(); // Context/backends before engine, renderer and SDL.
    }

    int run() {
        if (ImGuiTestEngine_IsTestQueueEmpty(engine_.get())) {
            std::cerr << "No GUI tests matched the filter\n";
            return 2;
        }
        const auto deadline =
            std::chrono::steady_clock::now() + std::chrono::seconds(visible_ ? 600 : 100);
        int last_width = 0, last_height = 0;
        auto& io = ImGuiTestEngine_GetIO(engine_.get());
        while (!ImGuiTestEngine_IsTestQueueEmpty(engine_.get()) || io.IsRunningTests) {
            if (std::chrono::steady_clock::now() > deadline) {
                std::cerr << "GUI test run timed out\n";
                return 1;
            }
            const float density = visible_ ? 1 : fixture_.density;
            const int width =
                static_cast<int>(std::lround(static_cast<float>(fixture_.width) * density));
            const int height =
                static_cast<int>(std::lround(static_cast<float>(fixture_.height) * density));
            if (width != last_width || height != last_height) {
                require(SDL_SetWindowSize(window_.get(), width, height), "SDL_SetWindowSize");
                last_width = width;
                last_height = height;
            }
            SDL_Event event;
            while (SDL_PollEvent(&event)) {
                if (event.type == SDL_EVENT_QUIT || event.type == SDL_EVENT_WINDOW_CLOSE_REQUESTED)
                    return 1;
                interface_->process_event(event);
            }
            // Exercise the real presentation and shell state, including actual
            // SDL rendering. The runner continues after a tested Quit command.
            interface_->draw(fixture_.shell);
            // Instant error captures can run from TestFunc before the next
            // draw. Keep the last completed frame available for that path.
            if (io.ConfigCaptureOnError || io.IsCapturing) {
                framebuffer_.reset(SDL_RenderReadPixels(renderer_.get(), nullptr));
                require(framebuffer_ != nullptr, "SDL_RenderReadPixels");
                SDL_SetSurfaceBlendMode(framebuffer_.get(), SDL_BLENDMODE_NONE);
            }
            ImGuiTestEngine_PreSwap(engine_.get());
            require(SDL_RenderPresent(renderer_.get()), "SDL_RenderPresent");
            ImGuiTestEngine_PostSwap(engine_.get());
        }
        ImGuiTestEngine_PrintResultSummary(engine_.get());
        ImGuiTestEngineResultSummary result;
        ImGuiTestEngine_GetResultSummary(engine_.get(), &result);
        return result.CountTested > 0 && result.CountTested == result.CountSuccess &&
                       result.CountInQueue == 0
                   ? 0
                   : 1;
    }

private:
    static bool capture(ImGuiID, int x, int y, int width, int height, unsigned int* pixels,
                        void* user_data) {
        auto& self = *static_cast<Runner*>(user_data);
        if (!self.framebuffer_)
            return false;
        // The engine requests window points. Resample the saved framebuffer to
        // that size; reading before Present also works on hardware backends.
        const auto scale = ImGui::GetIO().DisplayFramebufferScale;
        const SDL_Rect source{static_cast<int>(std::lround(static_cast<float>(x) * scale.x)),
                              static_cast<int>(std::lround(static_cast<float>(y) * scale.y)),
                              static_cast<int>(std::lround(static_cast<float>(width) * scale.x)),
                              static_cast<int>(std::lround(static_cast<float>(height) * scale.y))};
        Surface target(
            SDL_CreateSurfaceFrom(width, height, SDL_PIXELFORMAT_RGBA32, pixels, width * 4),
            SDL_DestroySurface);
        return target && SDL_BlitSurfaceScaled(self.framebuffer_.get(), &source, target.get(),
                                               nullptr, SDL_SCALEMODE_LINEAR);
    }
    SdlLifetime sdl_;
    bool visible_;
    GuiFixture fixture_;
    std::unique_ptr<SDL_Window, decltype(&SDL_DestroyWindow)> window_{nullptr, SDL_DestroyWindow};
    std::unique_ptr<SDL_Renderer, decltype(&SDL_DestroyRenderer)> renderer_{nullptr,
                                                                            SDL_DestroyRenderer};
    std::unique_ptr<ImGuiTestEngine, decltype(&ImGuiTestEngine_DestroyContext)> engine_{
        nullptr, ImGuiTestEngine_DestroyContext};
    std::unique_ptr<openciv::ui::Interface> interface_;
    Surface framebuffer_{nullptr, SDL_DestroySurface};
};
} // namespace

int main(int argc, char** argv) {
    try {
        Options options;
        for (int i = 1; i < argc; ++i) {
            const std::string argument = argv[i];
            if (argument == "--visible")
                options.visible = true;
            else if (argument == "--filter" && i + 1 < argc)
                options.filter = argv[++i];
            else if (argument == "--output" && i + 1 < argc)
                options.output = argv[++i];
            else if (argument == "--density" && i + 1 < argc) {
                const std::string value = argv[++i];
                std::size_t parsed = 0;
                options.density = std::stof(value, &parsed);
                if (parsed != value.size() || !std::isfinite(options.density) ||
                    options.density < 1 || options.density > 4)
                    throw std::runtime_error("Density must be between 1 and 4");
            } else {
                std::cerr << "Usage: oc2_ui_tests [--visible] [--filter pattern] [--density 1..4] "
                             "[--output directory]\n";
                return 2;
            }
        }
        if (options.output.empty())
            options.output = std::filesystem::path(SDL_GetBasePath()) / "gui-results";
        options.output = std::filesystem::absolute(options.output);
        std::filesystem::create_directories(options.output / "output/failures");
        std::filesystem::current_path(options.output);
        std::cout << "GUI test results: " << options.output << '\n';
        Runner runner(options);
        return runner.run();
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
