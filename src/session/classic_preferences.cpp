#include "session/classic_preferences.hpp"
#include "session/runtime.hpp"
#include "formats/preference_io.hpp"

namespace openciv::session {
namespace {
void publish(SessionRuntime& runtime) {
    if (runtime.active())
        runtime.publish();
}
// Synchronous publication decorator; runtime outlives this owned file.
class PublishedOutput final : public CoreOutput {
public:
    PublishedOutput(SessionRuntime& runtime, std::unique_ptr<CoreOutput> output)
        : runtime_(runtime), output_(std::move(output)) {}
    ~PublishedOutput() override { close_on_destruction(); }

private:
    bool write_item(std::span<const formats::Byte> bytes) override {
        publish(runtime_);
        return output_->item(bytes);
    }
    bool finish() override {
        publish(runtime_);
        return output_->close();
    }
    SessionRuntime& runtime_;
    std::unique_ptr<CoreOutput> output_;
};
} // namespace
void PreferenceFileServices::validate_output() const {
    if (!open_output || !remove_file)
        throw std::invalid_argument("Preference saving requires open and removal services");
}
void load_classic_preferences(state::ProcessContext& process,
                              const state::PreferenceDefaults& defaults,
                              const PreferenceFileServices& services) {
    if (!services.open_input)
        throw std::invalid_argument("Preference loading requires an input service");
    auto input = services.open_input("CIV2.DAT");
    if (!input) {
        state::default_ordinary_preferences(process.ordinary_preferences, defaults);
        state::default_multiplayer_preferences(process.multiplayer_preferences);
        return;
    }
    const formats::ReadItem read = [&](std::size_t size) { return input->item(size); };
    if (!formats::read_ordinary_preferences(read, process.ordinary_preferences))
        state::default_ordinary_preferences(process.ordinary_preferences, defaults);
    if (!formats::read_multiplayer_preferences(read, process.multiplayer_preferences))
        state::default_multiplayer_preferences(process.multiplayer_preferences);
    (void)input->close();
}
PreferenceSaveStatus save_classic_preferences(const state::ProcessContext& process,
                                              const PreferenceFileServices& services) {
    services.validate_output();
    // Snapshot configuration before external I/O; write callbacks are not edits.
    const auto ordinary = formats::encode_ordinary_preferences(process.ordinary_preferences);
    const auto multiplayer =
        formats::encode_multiplayer_preferences(process.multiplayer_preferences);
    auto output = services.open_output("CIV2.DAT");
    if (!output)
        return PreferenceSaveStatus::output;
    const bool written = output->item(ordinary) && output->item(multiplayer);
    const bool finalized = output->close();
    if (!written || !finalized) {
        (void)services.remove_file("CIV2.DAT");
        return PreferenceSaveStatus::output;
    }
    return PreferenceSaveStatus::success;
}
PreferenceSaveStatus save_classic_preferences(SessionRuntime& runtime,
                                              const PreferenceFileServices& services) {
    services.validate_output();
    auto files = services;
    files.open_output = [&](std::string_view path) -> std::unique_ptr<CoreOutput> {
        publish(runtime);
        auto output = services.open_output(path);
        if (!output)
            return {};
        return std::make_unique<PublishedOutput>(runtime, std::move(output));
    };
    files.remove_file = [&](std::string_view path) {
        publish(runtime);
        return services.remove_file(path);
    };
    return save_classic_preferences(runtime.process(), files);
}
} // namespace openciv::session
