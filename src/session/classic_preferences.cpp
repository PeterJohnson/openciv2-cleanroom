#include "session/classic_preferences.hpp"
#include "formats/state_conversion.hpp"
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
PreferenceLoadStatus load_classic_preferences(state::ProcessContext& process,
                                              const state::PreferenceDefaults& supplied_defaults,
                                              const PreferenceFileServices& supplied) {
    const auto services = supplied;
    const auto defaults = supplied_defaults;
    if (!services.open_input)
        throw std::invalid_argument("Preference loading requires an input service");
    auto ordinary = process.ordinary_preferences;
    auto multiplayer = process.multiplayer_preferences;
    auto status = PreferenceLoadStatus::defaults;
    auto input = services.open_input("CIV2.DAT");
    if (!input) {
        state::default_ordinary_preferences(ordinary, defaults);
        state::default_multiplayer_preferences(multiplayer);
    } else {
        const auto first = input->item(formats::ordinary_preferences_size);
        const auto second = input->item(formats::multiplayer_preferences_size);
        if (!input->close())
            return PreferenceLoadStatus::input;
        if (first.prefix.size() > formats::ordinary_preferences_size ||
            second.prefix.size() > formats::multiplayer_preferences_size)
            throw std::invalid_argument("Preference item exceeds its stored field size");
        if (first.complete)
            ordinary = formats::to_live(formats::decode_ordinary_preferences(first.prefix));
        else
            state::default_ordinary_preferences(ordinary, defaults);
        if (second.complete)
            multiplayer = formats::to_live(formats::decode_multiplayer_preferences(second.prefix));
        else
            state::default_multiplayer_preferences(multiplayer);
        if (first.complete && second.complete)
            status = PreferenceLoadStatus::success;
    }
    process.ordinary_preferences = std::move(ordinary);
    process.multiplayer_preferences = std::move(multiplayer);
    return status;
}
PreferenceSaveStatus save_classic_preferences(const state::ProcessContext& process,
                                              const PreferenceFileServices& supplied) {
    const auto services = supplied;
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
                                              const PreferenceFileServices& supplied) {
    const auto services = supplied;
    services.validate_output();
    auto files = services;
    files.open_output = [&runtime, open = services.open_output](
                            std::string_view path) -> std::unique_ptr<CoreOutput> {
        publish(runtime);
        auto output = open(path);
        if (!output)
            return {};
        return std::make_unique<PublishedOutput>(runtime, std::move(output));
    };
    files.remove_file = [&runtime, remove = services.remove_file](std::string_view path) {
        publish(runtime);
        return remove(path);
    };
    return save_classic_preferences(runtime.process(), files);
}
} // namespace openciv::session
