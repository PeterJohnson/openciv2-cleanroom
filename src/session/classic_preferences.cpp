import std;
import openciv.session.classic_preferences;
import openciv.formats.state_conversion;
import openciv.session.runtime;
import openciv.formats.preference_io;

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
PreferenceLoadStatus load_classic_preferences(state::ProcessContext& process,
                                              const state::PreferenceDefaults& supplied_defaults,
                                              const FileStorage& supplied) {
    const auto& services = supplied;
    const auto defaults = supplied_defaults;
    auto ordinary = process.ordinary_preferences;
    auto multiplayer = process.multiplayer_preferences;
    auto status = PreferenceLoadStatus::defaults;
    auto input = services.read("CIV2.DAT");
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
                                              const FileStorage& supplied) {
    const auto& services = supplied;
    // Snapshot configuration before external I/O; write callbacks are not edits.
    const auto ordinary = formats::encode_ordinary_preferences(process.ordinary_preferences);
    const auto multiplayer =
        formats::encode_multiplayer_preferences(process.multiplayer_preferences);
    auto output = services.replace("CIV2.DAT");
    if (!output)
        return PreferenceSaveStatus::output;
    const bool written = output->item(ordinary) && output->item(multiplayer);
    const bool finalized = output->close();
    if (!written || !finalized) {
        (void)services.remove("CIV2.DAT");
        return PreferenceSaveStatus::output;
    }
    return PreferenceSaveStatus::success;
}
PreferenceSaveStatus save_classic_preferences(SessionRuntime& runtime, const FileStorage& storage) {
    class PublishedStorage final : public FileStorage {
    public:
        PublishedStorage(SessionRuntime& runtime, const FileStorage& storage)
            : runtime_(runtime), storage_(storage) {}
        std::unique_ptr<CoreInput> read(std::string_view path) const override {
            return storage_.read(path);
        }
        std::unique_ptr<CoreOutput> replace(std::string_view path) const override {
            publish(runtime_);
            auto output = storage_.replace(path);
            if (!output)
                return {};
            return std::make_unique<PublishedOutput>(runtime_, std::move(output));
        }
        bool remove(std::string_view path) const override {
            publish(runtime_);
            return storage_.remove(path);
        }

    private:
        SessionRuntime& runtime_;
        const FileStorage& storage_;
    } published(runtime, storage);
    return save_classic_preferences(runtime.process(), published);
}
} // namespace openciv::session
