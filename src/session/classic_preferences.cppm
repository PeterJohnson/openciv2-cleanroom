export module openciv.session.classic_preferences;
import openciv.formats.state_conversion;
import openciv.formats.preference_io;

import std;
export import openciv.session.preferences;
import openciv.session.runtime;
export import openciv.io.storage;
export import openciv.state.game;

export namespace openciv::session {

class ClassicPreferenceWriter final : public PreferenceWriter {
public:
    explicit ClassicPreferenceWriter(const io::FileStorage& storage) : storage_(storage) {}
    bool write(PreferenceSnapshot values, base::CallbackRef<void()> before_io = {}) const override;

private:
    const io::FileStorage& storage_;
};
enum class PreferenceLoadStatus { success, defaults, input };
// Stage both independent items against data snapshots and close before install.
// The storage provider is borrowed for this synchronous call.
// Short items receive their specified fallback defaults; close failure and
// exceptions leave installed preferences unchanged. Missing files default both.
PreferenceLoadStatus load_classic_preferences(state::ProcessContext& process,
                                              const state::PreferenceDefaults& defaults,
                                              const io::FileStorage& services);
enum class PreferenceSaveStatus { success = 0, output = 1 };
// F03 production preference contract: coherent configuration, truthful write
// and close results, exactly-once close. Failed output is removed when possible;
// accepted preferences remain. Providers do not admit configuration mutations.
PreferenceSaveStatus save_classic_preferences(const state::ProcessContext& process,
                                              const io::FileStorage& services);
// Synchronous owner adapter: publish before open/write/close/remove.
PreferenceSaveStatus save_classic_preferences(SessionRuntime& runtime,
                                              const io::FileStorage& services);
} // namespace openciv::session

module :private;
namespace openciv::session {
namespace {
class NotifiedOutput final : public io::CoreOutput {
public:
    NotifiedOutput(std::unique_ptr<io::CoreOutput> output, base::CallbackRef<void()> before_io)
        : output_(std::move(output)), before_io_(before_io) {}
    ~NotifiedOutput() override { close_on_destruction(); }

private:
    bool write_item(std::span<const formats::Byte> bytes) override {
        notify();
        return output_->item(bytes);
    }
    bool finish() override {
        notify();
        return output_->close();
    }
    void notify() const {
        if (before_io_)
            before_io_();
    }
    std::unique_ptr<io::CoreOutput> output_;
    base::CallbackRef<void()> before_io_;
};
} // namespace
PreferenceLoadStatus load_classic_preferences(state::ProcessContext& process,
                                              const state::PreferenceDefaults& supplied_defaults,
                                              const io::FileStorage& supplied) {
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
bool ClassicPreferenceWriter::write(PreferenceSnapshot values,
                                    base::CallbackRef<void()> before_io) const {
    const auto ordinary = formats::encode_ordinary_preferences(values.ordinary);
    const auto multiplayer = formats::encode_multiplayer_preferences(values.multiplayer);
    const auto notify = [&] {
        if (before_io)
            before_io();
    };
    notify();
    auto file = storage_.replace("CIV2.DAT");
    if (!file)
        return false;
    NotifiedOutput output(std::move(file), before_io);
    const bool written = output.item(ordinary) && output.item(multiplayer);
    const bool finalized = output.close();
    if (!written || !finalized) {
        notify();
        (void)storage_.remove("CIV2.DAT");
        return false;
    }
    return true;
}
PreferenceSaveStatus save_classic_preferences(const state::ProcessContext& process,
                                              const io::FileStorage& storage) {
    return ClassicPreferenceWriter(storage).write(
               {process.ordinary_preferences, process.multiplayer_preferences})
               ? PreferenceSaveStatus::success
               : PreferenceSaveStatus::output;
}
PreferenceSaveStatus save_classic_preferences(SessionRuntime& runtime,
                                              const io::FileStorage& storage) {
    const auto before_io = [&] {
        if (runtime.active())
            runtime.publish();
    };
    return ClassicPreferenceWriter(storage).write(
               {runtime.process().ordinary_preferences, runtime.process().multiplayer_preferences},
               before_io)
               ? PreferenceSaveStatus::success
               : PreferenceSaveStatus::output;
}
} // namespace openciv::session
