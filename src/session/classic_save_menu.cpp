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
        throw std::out_of_range("Save extension has no leading-period backing");
    return files.extension.substr(1);
}
} // namespace
SaveMenuResult save_classic_menu(SessionRuntime& runtime, ClassicFileState& files,
                                 ClassicSaveMenuState& menu, bool scenario,
                                 const SaveMenuServices& services,
                                 std::optional<std::uint8_t> incoming_cleanup_mask) {
    if (!menu.session_active)
        return SaveMenuResult::inactive;
    if (!services.stop_countdown || !services.start_countdown || !services.local_time ||
        !services.lowercase || !services.change_directory || !services.label ||
        !services.ruler_name || !services.ruler_title || !services.civilization_plural ||
        !services.format_year || !services.error_text || !services.notice ||
        !services.chooser.choose || !services.chooser.extended_error || !services.output.open)
        throw std::invalid_argument("Save menu requires complete timer/file/text service bindings");
    auto& world = runtime.world();
    auto& globals = world.globals;
    auto& shared = runtime.process().text_parameters;
    const auto publish = [&] {
        if (runtime.active())
            runtime.publish();
    };
    publish();
    services.stop_countdown(true);
    auto result = SaveMenuResult::cancelled;
    for (;;) {
        select_classic_extension(scenario, globals.multiplayer, files.extension);
        menu.private_filter = text(services.label(scenario ? 415 : 238));
        const auto pattern = "*." + extension_name(files);
        menu.private_filter += " (" + pattern + ")";
        menu.private_filter.push_back('\0');
        menu.private_filter += pattern;
        menu.private_filter.append(2, '\0');
        menu.filename = text(services.ruler_name(world.multiplayer.local_human));
        menu.filename.resize(std::min<std::size_t>(2, menu.filename.size()));
        menu.filename += '_';
        for (auto& byte : menu.filename)
            if (static_cast<unsigned char>(byte) >= 32 && static_cast<unsigned char>(byte) <= 64)
                byte = '_';
        shared.line.clear();
        shared.line += globals.year < 0 ? 'b' : 'a';
        menu.filename += ' ';
        menu.filename.back() = shared.line.front();
        menu.filename += std::to_string(std::abs(static_cast<int>(globals.year)));
        if (globals.multiplayer == 2 && !scenario) {
            const auto calendar = services.local_time();
            menu.filename +=
                " " + std::to_string(calendar.month) + "-" + std::to_string(calendar.day) + "-" +
                std::to_string(calendar.year) + " " + std::to_string(calendar.hour) + "-" +
                std::to_string(calendar.minute) + "-" + std::to_string(calendar.second);
        }
        menu.filename += '.';
        menu.filename += extension_name(files);
        menu.filename = text(services.lowercase(menu.filename));
        if ((scenario || (globals.game_flags & 0x80U)) && world.scenario.year_increment < 0) {
            menu.filename = std::to_string(std::abs(static_cast<int>(globals.year) / 12));
            const auto month = globals.year < 0 ? 11 - std::abs(static_cast<int>(globals.year) % 12)
                                                : std::abs(static_cast<int>(globals.year) % 12);
            menu.filename += text(services.label(static_cast<std::size_t>(420 + month)));
            menu.filename += files.extension;
            menu.filename = text(services.lowercase(menu.filename));
        }
        files.text.working = files.text.search;
        services.change_directory(files.text.working);
        (void)build_classic_filter(menu.shared_filter, globals, scenario, false, true,
                                   services.label);
        FileChoiceRequest choice;
        choice.save = true;
        choice.owner = menu.main_window;
        choice.title = text(services.label(scenario ? 416 : 239));
        choice.filter = menu.shared_filter;
        choice.default_extension = extension_name(files);
        publish();
        const bool accepted =
            choose_classic_file(std::move(choice), menu.filename, services.chooser);
        files.text.working = files.text.home;
        services.change_directory(files.text.working);
        if (accepted) {
            const auto status = save_classic_core(runtime, files, menu.filename, scenario,
                                                  services.output, incoming_cleanup_mask);
            if (status != CoreSaveStatus::success) {
                shared.strings[0] = text(services.error_text());
                publish();
                services.notice("SAVEERROR");
                result = SaveMenuResult::failed;
            } else {
                std::string_view notice;
                if (scenario) {
                    if (!world.scenario.name.terminated())
                        throw std::out_of_range("Scenario name has no terminator backing");
                    const auto& bytes = world.scenario.name.bytes;
                    shared.strings[0] = text(std::string(bytes.begin(), bytes.end()));
                    notice = "SCENOK";
                } else if (globals.multiplayer == 2) {
                    shared.strings[0] = menu.filename;
                    notice = "EMAILSAVED";
                } else {
                    shared.strings[0] = text(services.ruler_title(world.multiplayer.local_human));
                    shared.strings[1] = text(services.ruler_name(world.multiplayer.local_human));
                    shared.strings[2] =
                        text(services.civilization_plural(world.multiplayer.local_human));
                    shared.strings[3] = text(services.format_year(globals.year));
                    const auto& rules = *runtime.process().rules;
                    shared.strings[4] =
                        rules.text.text(rules.names.difficulties.at(globals.difficulty));
                    notice = "SAVEOK";
                }
                publish();
                services.notice(notice);
                result = SaveMenuResult::saved;
            }
            break;
        }
        if (globals.multiplayer != 2)
            break;
    }
    publish();
    services.start_countdown(true);
    return result;
}
} // namespace openciv::session
