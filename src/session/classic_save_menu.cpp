#include "classic_save_menu.hpp"
#include "session/runtime.hpp"
#include <cstdlib>
#include <utility>

namespace openciv::session {
namespace {
std::string text(std::string value) {
    value.resize(std::min(value.size(), value.find('\0')));
    return value;
}
std::string extension_name(const ClassicFileState& files) {
    if (files.extension.empty() || files.extension.front() != '.')
        throw std::invalid_argument("Invalid save extension");
    return files.extension.substr(1);
}
SaveNotice describe_save(SessionRuntime& runtime, bool scenario,
                         const SaveMenuDependencies& services) {
    const auto& world = runtime.world();
    const auto mode = world.globals.multiplayer;
    const auto civ = world.multiplayer.local_human;
    const auto year = world.globals.year;
    const auto difficulty = static_cast<std::size_t>(world.globals.difficulty);
    if (scenario) {
        const auto bytes = world.scenario.name;
        return {"SCENOK", {std::string(bytes.begin(), bytes.end())}};
    }
    if (mode == state::GameMode::email)
        return {"EMAILSAVED", {}};
    const auto& rules = *runtime.process().rules;
    const std::string difficulty_name(rules.text.text(rules.names.difficulties.at(difficulty)));
    return {"SAVEOK",
            {text(services.ruler_title(civ)), text(services.ruler_name(civ)),
             text(services.civilization_plural(civ)), text(services.format_year(year)),
             difficulty_name}};
}
} // namespace
ClassicSaveOperation::ClassicSaveOperation(SessionRuntime& runtime, ClassicFileState& files,
                                           ClassicSaveMenuState& menu, bool scenario,
                                           SaveMenuDependencies services,
                                           std::unique_ptr<FileChooser> chooser)
    : runtime_(runtime), files_(files), menu_(menu), scenario_(scenario), services_(services),
      chooser_(std::move(chooser)) {}
ClassicSaveOperation::~ClassicSaveOperation() {
    abandon();
}
FileChoiceRequest ClassicSaveOperation::prepare_request() {
    // Values captured before formatting: presentation is not a game event.
    const auto& world = runtime_.world();
    const auto mode = world.globals.multiplayer;
    const auto year = world.globals.year;
    const auto civ = world.multiplayer.local_human;
    const bool monthly =
        (scenario_ || (world.globals.game_flags & 0x80U)) && world.scenario.year_increment < 0;
    select_classic_extension(scenario_, mode, files_.extension);
    auto suggestion = text(services_.ruler_name(civ));
    suggestion.resize(std::min<std::size_t>(2, suggestion.size()));
    suggestion += '_';
    for (auto& c : suggestion)
        if (static_cast<unsigned char>(c) >= 32 && static_cast<unsigned char>(c) <= 64)
            c = '_';
    suggestion += year < 0 ? 'b' : 'a';
    suggestion += std::to_string(std::abs(static_cast<int>(year)));
    if (mode == state::GameMode::email && !scenario_) {
        const auto date = services_.local_time();
        suggestion += " " + std::to_string(date.month) + "-" + std::to_string(date.day) + "-" +
                      std::to_string(date.year) + " " + std::to_string(date.hour) + "-" +
                      std::to_string(date.minute) + "-" + std::to_string(date.second);
    }
    if (monthly) {
        const auto month = year < 0 ? 11 - std::abs(static_cast<int>(year) % 12)
                                    : std::abs(static_cast<int>(year) % 12);
        suggestion = std::to_string(std::abs(static_cast<int>(year) / 12)) +
                     text(services_.label(static_cast<std::size_t>(420 + month)));
    }
    suggestion = text(services_.lowercase(suggestion + files_.extension));
    FileChoiceRequest request;
    request.save = true;
    request.title = services_.label(scenario_ ? 416 : 239);
    request.initial_path =
        files_.text.search.empty() ? suggestion : files_.text.search + "\\" + suggestion;
    request.default_extension = extension_name(files_);
    request.types = classic_file_types(mode, scenario_, false, true, services_.label);
    return request;
}
SaveMenuResult ClassicSaveOperation::accept(FileChoiceResult choice) {
    if (choice.status == FileChoiceStatus::cancelled)
        return SaveMenuResult::cancelled;
    if (choice.status == FileChoiceStatus::failed) {
        services_.notice({"SAVEERROR", {choice.error}});
        return SaveMenuResult::failed;
    }
    menu_.filename = choice.path;
    // Acceptance is a real boundary: describe the game being saved now.
    auto notice = describe_save(runtime_, scenario_, services_);
    if (notice.key == "EMAILSAVED")
        notice.arguments[0] = choice.path;
    const auto status =
        save_classic_core(runtime_, files_, choice.path, scenario_, *services_.output);
    if (status != CoreSaveStatus::success) {
        services_.notice({"SAVEERROR", {services_.error_text()}});
        return SaveMenuResult::failed;
    }
    services_.notice(notice);
    return SaveMenuResult::saved;
}
void ClassicSaveOperation::finish(SaveMenuResult result) {
    result_ = result;
    if (chooser_)
        chooser_->cancel();
    // Mark before calling: even a throwing restart is attempted only once.
    const bool restart = std::exchange(stopped_, false);
    try {
        if (restart)
            services_.countdown->start(true);
    } catch (...) {
        activity_.reset();
        throw;
    }
    activity_.reset();
}
void ClassicSaveOperation::abandon() noexcept {
    if (chooser_)
        chooser_->cancel();
    if (!result_) {
        try {
            finish(SaveMenuResult::cancelled);
        } catch (...) {
        }
    }
}
std::optional<SaveMenuResult> ClassicSaveOperation::advance() {
    if (advancing_)
        throw std::logic_error("Cannot recursively advance a save workflow");
    if (result_)
        return result_;
    advancing_ = true;
    struct Guard {
        bool& flag;
        ~Guard() { flag = false; }
    } guard{advancing_};
    try {
        if (!started_) {
            if (!menu_.session_active) {
                finish(SaveMenuResult::inactive);
                return result_;
            }
            const auto& s = services_;
            if (!chooser_ || !s.countdown || !s.local_time || !s.lowercase || !s.label ||
                !s.ruler_name || !s.ruler_title || !s.civilization_plural || !s.format_year ||
                !s.error_text || !s.notice || !s.output)
                throw std::invalid_argument("Save workflow requires timer/file/text services");
            activity_.emplace(runtime_.begin_activity());
            s.countdown->stop(true);
            stopped_ = true;
            started_ = true;
            chooser_->begin(prepare_request());
            return {};
        }
        auto choice = chooser_->poll();
        if (!choice)
            return {};
        finish(accept(validate_file_choice(std::move(*choice))));
        return result_;
    } catch (...) {
        if (!result_) {
            try {
                finish(SaveMenuResult::failed);
            } catch (...) {
            }
        }
        throw;
    }
}
SaveMenuResult ClassicSaveOperation::cancel() {
    if (advancing_)
        throw std::logic_error("Cannot cancel a save workflow during a phase");
    if (!result_)
        finish(SaveMenuResult::cancelled);
    return *result_;
}
namespace {
// Compatibility driver for immediate headless providers. Interactive adapters
// implement FileChooser and drive ClassicSaveOperation from their event loop.
class ImmediateChooser final : public FileChooser {
public:
    explicit ImmediateChooser(FileChoiceCallback services) : services_(services) {}
    void begin(FileChoiceRequest request) override {
        result_ = choose_classic_file(request, services_);
    }
    std::optional<FileChoiceResult> poll() override { return std::exchange(result_, {}); }
    void cancel() noexcept override { result_.reset(); }

private:
    FileChoiceCallback services_;
    std::optional<FileChoiceResult> result_;
};
} // namespace
SaveMenuResult save_classic_menu(SessionRuntime& runtime, ClassicFileState& files,
                                 ClassicSaveMenuState& menu, bool scenario,
                                 const SaveMenuDependencies& services, FileChoiceCallback chooser) {
    ClassicSaveOperation operation(runtime, files, menu, scenario, services,
                                   std::make_unique<ImmediateChooser>(chooser));
    if (const auto result = operation.advance())
        return *result;
    return operation.advance().value(); // This adapter always has an immediate result.
}
} // namespace openciv::session
