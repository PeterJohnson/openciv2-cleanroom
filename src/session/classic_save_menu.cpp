#include "classic_save_menu.hpp"
#include "session/runtime.hpp"
#include <cstdlib>

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
SaveNotice describe_save(SessionRuntime& runtime, bool scenario, const SaveMenuServices& services) {
    const auto& world = runtime.world();
    const auto mode = world.globals.multiplayer;
    const auto civ = world.multiplayer.local_human;
    const auto year = world.globals.year;
    const auto difficulty = world.globals.difficulty;
    if (scenario) {
        if (!world.scenario.name.terminated())
            throw std::invalid_argument("Unterminated scenario name");
        const auto bytes = world.scenario.name.text();
        return {"SCENOK", {std::string(bytes.begin(), bytes.end())}};
    }
    if (mode == 2)
        return {"EMAILSAVED", {}};
    const auto& rules = *runtime.process().rules;
    const std::string difficulty_name(rules.text.text(rules.names.difficulties.at(difficulty)));
    return {"SAVEOK",
            {text(services.ruler_title(civ)), text(services.ruler_name(civ)),
             text(services.civilization_plural(civ)), text(services.format_year(year)),
             difficulty_name}};
}
} // namespace
SaveMenuResult save_classic_menu(SessionRuntime& runtime, ClassicFileState& files,
                                 ClassicSaveMenuState& menu, bool scenario,
                                 const SaveMenuServices& services) {
    if (!menu.session_active)
        return SaveMenuResult::inactive;
    if (!services.stop_countdown || !services.start_countdown || !services.local_time ||
        !services.lowercase || !services.label || !services.ruler_name || !services.ruler_title ||
        !services.civilization_plural || !services.format_year || !services.error_text ||
        !services.notice || !services.chooser.choose || !services.output.open)
        throw std::invalid_argument("Save menu requires timer/file/text services");
    services.stop_countdown(true);
    bool restarted = false;
    const auto restart = [&] {
        restarted = true;
        services.start_countdown(true);
    };
    try {
        const auto result = [&] {
            // Values captured before formatting: presentation is not a game event.
            const auto& world = runtime.world();
            const auto mode = world.globals.multiplayer;
            const auto year = world.globals.year;
            const auto civ = world.multiplayer.local_human;
            const bool monthly = (scenario || (world.globals.game_flags & 0x80U)) &&
                                 world.scenario.year_increment < 0;
            select_classic_extension(scenario, mode, files.extension);
            auto suggestion = text(services.ruler_name(civ));
            suggestion.resize(std::min<std::size_t>(2, suggestion.size()));
            suggestion += '_';
            for (auto& c : suggestion)
                if (static_cast<unsigned char>(c) >= 32 && static_cast<unsigned char>(c) <= 64)
                    c = '_';
            suggestion += year < 0 ? 'b' : 'a';
            suggestion += std::to_string(std::abs(static_cast<int>(year)));
            if (mode == 2 && !scenario) {
                const auto date = services.local_time();
                suggestion += " " + std::to_string(date.month) + "-" + std::to_string(date.day) +
                              "-" + std::to_string(date.year) + " " + std::to_string(date.hour) +
                              "-" + std::to_string(date.minute) + "-" + std::to_string(date.second);
            }
            if (monthly) {
                const auto month = year < 0 ? 11 - std::abs(static_cast<int>(year) % 12)
                                            : std::abs(static_cast<int>(year) % 12);
                suggestion = std::to_string(std::abs(static_cast<int>(year) / 12)) +
                             text(services.label(static_cast<std::size_t>(420 + month)));
            }
            suggestion = text(services.lowercase(suggestion + files.extension));
            FileChoiceRequest request;
            request.save = true;
            request.title = services.label(scenario ? 416 : 239);
            request.initial_path =
                files.text.search.empty() ? suggestion : files.text.search + "\\" + suggestion;
            request.default_extension = extension_name(files);
            request.types = classic_file_types(mode, scenario, false, true, services.label);
            const auto choice = choose_classic_file(request, services.chooser);
            if (choice.status == FileChoiceStatus::cancelled)
                return SaveMenuResult::cancelled;
            if (choice.status == FileChoiceStatus::failed) {
                services.notice({"SAVEERROR", {choice.error}});
                return SaveMenuResult::failed;
            }
            menu.filename = choice.path;
            // Acceptance is a real boundary: describe the game being saved now.
            auto notice = describe_save(runtime, scenario, services);
            if (notice.key == "EMAILSAVED")
                notice.arguments[0] = choice.path;
            const auto status =
                save_classic_core(runtime, files, choice.path, scenario, services.output);
            if (status != CoreSaveStatus::success) {
                services.notice({"SAVEERROR", {services.error_text()}});
                return SaveMenuResult::failed;
            }
            services.notice(notice);
            return SaveMenuResult::saved;
        }();
        restart();
        return result;
    } catch (...) {
        if (!restarted) {
            try {
                restart();
            } catch (...) {
            }
        }
        throw;
    }
}
} // namespace openciv::session
