module openciv.session.classic_load;

import std;
import openciv.session.classic_file_choice;
import openciv.session.game_install;
import openciv.formats.game_input;
import openciv.rules.rules_install;

namespace openciv::session {
namespace {
using namespace formats;
void paths(ClassicFileState& retained, const std::string& request,
           const CoreLoadDependencies& services) {
    const auto separator = request.rfind('\\');
    if (separator == std::string::npos)
        throw std::out_of_range("Core load requires an established final backslash");
    retained.scenario_directory = request.substr(0, separator);
    retained.text.working = retained.scenario_directory;
    services.change_directory(retained.text.working);
    const auto resource = "GAME." + retained.text.extension;
    const bool custom = services.exists(retained.text.working, resource);
    retained.game_stem =
        custom ? retained.scenario_directory + "\\GAME" : retained.default_game_stem;
    retained.loaded_filename = request.substr(separator + 1);
    const auto home = retained.text.home;
    if (request.size() >= home.size() &&
        services.comparison->equal(request.substr(0, home.size()), home) &&
        separator > home.size()) {
        auto relative = request.substr(home.size(), separator - home.size());
        if (!relative.empty() && relative.front() == '\\')
            relative.erase(0, 1);
        if (!relative.empty())
            retained.remembered_subdirectory = std::move(relative);
    }
    retained.text.working = retained.text.home;
    services.change_directory(retained.text.working);
}
} // namespace
void CoreLoadDependencies::validate() const {
    if (!storage || !flush || !change_directory || !exists || !comparison || !comparison->equal ||
        !load_content)
        throw std::invalid_argument("Core load requires complete file/path/content bindings");
}
ClassicLoadOperation::ClassicLoadOperation(SessionRuntime& runtime, ClassicFileState& retained,
                                           CoreLoadDependencies services)
    : runtime_(runtime), retained_(retained), services_(services), activity_([&] {
          services_.validate();
          (void)runtime_.gameplay_random();
          return runtime_.begin_activity();
      }()) {}
CoreLoadStatus ClassicLoadOperation::load(std::string_view path,
                                          std::string_view password_reset_call) {
    if (started_)
        throw std::logic_error("Load operation already started");
    const std::string request(path), reset_call(password_reset_call);
    if (request.find('\0') != std::string::npos || request.rfind('\\') == std::string::npos)
        throw std::invalid_argument("Core load requires a qualified path without NUL");
    started_ = true;
    auto& retained = retained_;
    auto& runtime = runtime_;
    const auto& services = services_;
    select_classic_extension(false, runtime.world().globals.multiplayer, retained.extension);
    retained.opened_path = request;
    if (request.find('.') == std::string::npos)
        retained.opened_path += retained.extension;
    auto input = services.storage->read(retained.opened_path);
    if (!input)
        return CoreLoadStatus::input;
    try {
        // Header and bounded file bytes are local to this operation. I/O callbacks
        // cannot expose a partially restored world or select simulation work.
        Bytes bytes;
        for (unsigned i = 0; i < 12; ++i) {
            const auto value = input->byte();
            if (!value) {
                input->close();
                return CoreLoadStatus::input;
            }
            bytes.push_back(*value);
        }
        GameHeader header;
        try {
            header = decode_game_header(bytes);
        } catch (const DecodeError&) {
            input->close();
            return CoreLoadStatus::input;
        }
        switch (game_version_status(header.version)) {
        case VersionStatus::newer:
            input->close();
            return CoreLoadStatus::newer;
        case VersionStatus::unsupported_old:
            input->close();
            return CoreLoadStatus::unsupported;
        case VersionStatus::supported:
            break;
        }
        constexpr std::size_t maximum = 256 * 1024 * 1024;
        for (;;) {
            auto part = input->item(65536);
            if (part.prefix.size() > 65536 || (part.complete && part.prefix.size() != 65536))
                throw std::invalid_argument("Inconsistent input transfer");
            if (part.prefix.size() > maximum - bytes.size())
                throw std::length_error("Save exceeds admission limit");
            bytes.insert(bytes.end(), part.prefix.begin(), part.prefix.end());
            if (!part.complete)
                break;
        }
        if (!input->close())
            return CoreLoadStatus::input;
        std::unique_ptr<state::GameInput> loaded;
        std::vector<std::array<Byte, 40>> event_bytes;
        std::unique_ptr<StoredSave> imported;
        try {
            imported = std::make_unique<StoredSave>(decode_stored_save(bytes));
            const auto& stored = *imported;
            if (stored.events)
                for (const auto& event : stored.events->entries)
                    event_bytes.push_back(event.record.retained);
            loaded = std::make_unique<state::GameInput>(game_input(stored));
            validate_game_input(*loaded);
        } catch (const DecodeError&) {
            return CoreLoadStatus::data;
        } catch (const std::invalid_argument&) {
            return CoreLoadStatus::data;
        } catch (const std::out_of_range&) {
            return CoreLoadStatus::data;
        }
        paths(retained, request, services);
        auto content = services.load_content();
        if (!content.rules)
            throw std::invalid_argument("Content source returned no rules");
        // Installation alone owns live transforms and random choices. A bound
        // semantic child may fail after earlier installation effects.
        auto& rules = *runtime.process().rules;
        if (content.labels)
            state::install_labels(*content.labels, rules.text, rules.labels);
        rules::RuleInstaller(*content.rules, rules).load(true);
        if (services.content_installed)
            services.content_installed();
        install_game(*loaded, runtime, *content.rules, retained.current_screen, reset_call);
        auto& opaque = retained.record_bytes;
        opaque = {};
        opaque.lifetime = runtime.world().lifetime;
        opaque.globals = imported->globals;
        std::copy(imported->civilizations.begin(), imported->civilizations.end(),
                  opaque.civilizations.begin());
        opaque.multiplayer = imported->multiplayer;
        for (std::size_t i = 0; i < imported->units.size(); ++i) {
            const auto token = runtime.world().entities.units.storage()[i].token;
            if (state::occupied(token))
                opaque.units.emplace(token, imported->units[i]);
        }
        for (std::size_t i = 0; i < imported->cities.size(); ++i) {
            const auto token = runtime.world().entities.cities.storage()[i].token;
            if (state::occupied(token))
                opaque.cities.emplace(token, imported->cities[i]);
        }
        retained.event_bytes.clear();
        for (std::size_t i = 0; i < event_bytes.size(); ++i) {
            const auto event = runtime.world().events.collection.at(i);
            retained.event_bytes.emplace(event->identity(),
                                         ImportedEventBytes{event, event_bytes[i]});
        }
        retained.starts = {};
        retained.cursor = {loaded->cursor_x, loaded->cursor_y};
        pack_layout(loaded->layout, retained.packed_layout);
        runtime.publish();
        services.flush();
        return CoreLoadStatus::success;
    } catch (...) {
        runtime.publish();
        throw;
    }
}
CoreLoadStatus load_classic_core(SessionRuntime& runtime, ClassicFileState& retained,
                                 std::string_view path, const CoreLoadDependencies& services,
                                 std::string_view password_reset_call) {
    ClassicLoadOperation operation(runtime, retained, services);
    return operation.load(path, password_reset_call);
}
} // namespace openciv::session
