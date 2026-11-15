#include "session/configuration.hpp"
#include "session/runtime.hpp"
#include "rules/double_movement.hpp"
#include <utility>

namespace openciv::session {
namespace {
void publish(SessionRuntime& runtime) {
    if (runtime.active())
        runtime.publish();
}
template <class Callback> auto published(SessionRuntime& runtime, Callback service) {
    return [&runtime, service](auto&&... args) -> decltype(auto) {
        publish(runtime);
        return service(std::forward<decltype(args)>(args)...);
    };
}
template <class Operation> void complete(SessionRuntime& runtime, Operation operation) {
    try {
        operation();
        publish(runtime);
    } catch (...) {
        publish(runtime);
        throw;
    }
}
std::string text(std::string value) {
    value.resize(std::min(value.size(), value.find('\0')));
    return value;
}
} // namespace
bool configure_game_options(SessionRuntime& runtime, std::optional<GameOptionChoices> choices,
                            const GameOptionDependencies& supplied) {
    if (!choices)
        return false;
    const auto& services = supplied;
    if (!services.preferences)
        throw std::invalid_argument("Preference storage is required");
    if (!services.music)
        throw std::invalid_argument("Game options require complete music services");
    auto pin = runtime.pin_world();
    complete(runtime, [&] {
        auto& options = runtime.world().globals.game_options;
        const bool old_music = (options & 8U) != 0;
        options &= 0xffff8027U;
        if ((*choices)[0])
            options |= 0x10U;
        if ((*choices)[1]) {
            options |= 8U;
            if (!old_music) {
                publish(runtime);
                services.music->reinitialize();
            }
            publish(runtime);
            services.music->synchronize();
        } else {
            publish(runtime);
            services.music->stop();
        }
        constexpr std::array<std::uint32_t, 9> bits{0x4000, 0x2000, 0x1000, 0x800, 0x400,
                                                    0x200,  0x100,  0x80,   0x40};
        for (std::size_t i = 0; i < bits.size(); ++i)
            if ((*choices)[i + 2])
                options |= bits[i];
        runtime.process().ordinary_preferences.game_options = options;
        (void)save_classic_preferences(runtime, *services.preferences);
    });
    return true;
}
bool configure_graphics_options(SessionRuntime& runtime,
                                std::optional<GraphicsOptionChoices> choices,
                                const GraphicsOptionDependencies& supplied) {
    if (!choices)
        return false;
    const auto& services = supplied;
    if (!services.preferences)
        throw std::invalid_argument("Preference storage is required");
    if (!services.heralds_available || !services.low_memory_notice)
        throw std::invalid_argument("Graphics options require availability and notice services");
    auto pin = runtime.pin_world();
    complete(runtime, [&] {
        auto& options = runtime.world().globals.game_options;
        const bool old_heralds = (options & 0x200000U) != 0;
        options &= ~0x003f0000U;
        constexpr std::array<std::uint32_t, 6> bits{0x20000, 0x40000,  0x200000,
                                                    0x80000, 0x100000, 0x10000};
        for (std::size_t i = 0; i < bits.size(); ++i) {
            if (!(*choices)[i])
                continue;
            if (i == 2 && !old_heralds) {
                publish(runtime);
                if (!services.heralds_available()) {
                    publish(runtime);
                    services.low_memory_notice();
                }
            }
            options |= bits[i];
        }
        runtime.process().ordinary_preferences.game_options = options;
        (void)save_classic_preferences(runtime, *services.preferences);
    });
    return true;
}
bool configure_message_options(SessionRuntime& runtime, std::optional<MessageOptionChoices> choices,
                               const FileStorage& supplied) {
    if (!choices)
        return false;
    const auto& services = supplied;
    auto pin = runtime.pin_world();
    complete(runtime, [&] {
        auto& messages = runtime.world().globals.message_options;
        messages = 0;
        for (std::size_t i = 0; i < choices->size(); ++i)
            if (!(*choices)[i])
                messages |= static_cast<std::uint16_t>(1U << i);
        runtime.process().ordinary_preferences.disabled_messages = messages;
        (void)save_classic_preferences(runtime, services);
    });
    return true;
}
MultiplayerOptionOperation::MultiplayerOptionOperation(SessionRuntime& runtime,
                                                       std::int32_t dialog_result,
                                                       MultiplayerOptionChoices choices,
                                                       MultiplayerOptionDependencies services,
                                                       std::unique_ptr<OptionVoteChannel> channel)
    : runtime_(runtime), choices_(choices), services_(services), channel_(std::move(channel)) {
    if (dialog_result != 0) {
        finish(MultiplayerOptionResult::cancelled);
        return;
    }
    const auto& s = services_;
    if (!s.preferences)
        throw std::invalid_argument("Preference storage is required");
    if (!s.clock || !s.local_player_name || !s.separator || !s.production_label ||
        !s.movement_label || !s.broadcaster || !s.notice || !s.redraw_status)
        throw std::invalid_argument("Multiplayer options require complete service bindings");
    const auto& world = runtime_.world();
    const auto& multiplayer = world.multiplayer;
    const bool changed = multiplayer.saved.double_production != (choices[2] ? 1 : 0) ||
                         multiplayer.saved.double_movement != (choices[3] ? 1 : 0);
    if (changed && world.globals.multiplayer >= state::GameMode::tcp && multiplayer.network_host &&
        multiplayer.player_count > 1 && !channel_)
        throw std::invalid_argument("Multiplayer proposal requires a vote channel");
    activity_.emplace(runtime_.begin_activity());
}
MultiplayerOptionOperation::~MultiplayerOptionOperation() {
    try {
        cancel();
    } catch (...) {
        finish(MultiplayerOptionResult::failed);
    }
}
void MultiplayerOptionOperation::finish(MultiplayerOptionResult result) noexcept {
    result_ = result;
    auto channel = std::move(channel_);
    if (channel)
        channel->cancel();
    activity_.reset();
}
void MultiplayerOptionOperation::reject() {
    auto& preferences = runtime_.process().multiplayer_preferences;
    const auto& live = runtime_.world().multiplayer.saved;
    preferences.double_production = live.double_production;
    preferences.double_movement = live.double_movement;
    publish(runtime_);
    services_.broadcaster->rejected();
    publish(runtime_);
    services_.notice("PMCHANGENO");
    finish(MultiplayerOptionResult::rejected);
}
void MultiplayerOptionOperation::cancel_now() {
    if (proposal_)
        reject();
    else {
        if (chosen_) {
            const auto& live = runtime_.world().multiplayer.saved;
            auto& preferences = runtime_.process().multiplayer_preferences;
            preferences.double_production = live.double_production;
            preferences.double_movement = live.double_movement;
        }
        finish(MultiplayerOptionResult::cancelled);
    }
}
void MultiplayerOptionOperation::cancel() {
    if (result_)
        return;
    if (polling_) {
        cancel_requested_ = true;
        return;
    }
    polling_ = true;
    try {
        cancel_now();
    } catch (...) {
        polling_ = false;
        finish(MultiplayerOptionResult::failed);
        throw;
    }
    polling_ = false;
}
void MultiplayerOptionOperation::apply() {
    auto& world = runtime_.world();
    auto& live = world.multiplayer.saved;
    const auto& preferences = runtime_.process().multiplayer_preferences;
    live.double_production = preferences.double_production;
    live.double_movement = preferences.double_movement;
    rules::apply_double_movement(world.globals.multiplayer, live.double_movement,
                                 *runtime_.process().rules,
                                 published(runtime_, services_.redraw_status));
    if (world.globals.multiplayer >= state::GameMode::tcp && world.multiplayer.player_count > 1) {
        publish(runtime_);
        services_.broadcaster->units();
        publish(runtime_);
        services_.broadcaster->options(live.double_production, live.double_movement,
                                       live.simultaneous);
        publish(runtime_);
        services_.broadcaster->accepted();
        publish(runtime_);
        services_.notice("PMCHANGEYES");
    }
    publish(runtime_);
    finish(MultiplayerOptionResult::applied);
}
std::optional<MultiplayerOptionResult> MultiplayerOptionOperation::poll() {
    if (polling_)
        throw std::logic_error("Cannot reenter an option vote");
    if (result_)
        return result_;
    polling_ = true;
    struct Exit {
        bool& polling;
        ~Exit() { polling = false; }
    } exit{polling_};
    const auto cancelled = [&] {
        if (!cancel_requested_)
            return false;
        cancel_now();
        return true;
    };
    try {
        auto& process = runtime_.process();
        auto& preferences = process.multiplayer_preferences;
        auto& world = runtime_.world();
        auto& multiplayer = world.multiplayer;
        auto& live = multiplayer.saved;
        auto& vote = multiplayer.vote;
        if (!started_) {
            started_ = true;
            preferences.clear_chat_new_game = choices_[0] ? 1 : 0;
            preferences.clear_chat_boot = choices_[1] ? 1 : 0;
            (void)save_classic_preferences(runtime_, *services_.preferences);
            if (cancelled())
                return result_;
            if (world.globals.multiplayer >= state::GameMode::tcp && !multiplayer.network_host) {
                finish(MultiplayerOptionResult::client_only);
                return result_;
            }
            preferences.double_production = choices_[2] ? 1 : 0;
            preferences.double_movement = choices_[3] ? 1 : 0;
            chosen_ = true;
            if (preferences.double_production == live.double_production &&
                preferences.double_movement == live.double_movement) {
                finish(MultiplayerOptionResult::unchanged);
                return result_;
            }
            if (world.globals.multiplayer >= state::GameMode::tcp && multiplayer.player_count > 1) {
                if (!channel_)
                    throw std::invalid_argument("Multiplayer proposal requires a vote channel");
                auto& parameters = process.text_parameters.strings;
                publish(runtime_);
                parameters[0] = text(services_.local_player_name());
                publish(runtime_);
                parameters[1] =
                    text(services_.production_label(preferences.double_production != 0));
                publish(runtime_);
                parameters[1] += text(services_.separator());
                publish(runtime_);
                parameters[1] += text(services_.movement_label(preferences.double_movement != 0));
                if (cancelled())
                    return result_;
                vote.pending = base::subtract32(multiplayer.player_count, 1);
                vote.status = 0;
                proposal_ =
                    OptionProposal{parameters[0], parameters[1], preferences.double_production,
                                   preferences.double_movement, vote.pending};
                publish(runtime_);
                channel_->begin(*proposal_);
                if (cancelled())
                    return result_;
                publish(runtime_);
                vote.popup_tick = services_.clock();
                if (cancelled())
                    return result_;
                return {}; // Presentation and network scheduling resume on a later frame.
            }
            apply();
            return result_;
        }
        publish(runtime_);
        const auto progress = channel_->poll();
        if (cancelled())
            return result_;
        vote.pending = progress.pending;
        vote.status = progress.status;
        if (!multiplayer.handoff) {
            publish(runtime_);
            const auto now = services_.clock();
            if (cancelled())
                return result_;
            if (base::signed32(now - vote.popup_tick) < 2401 && vote.pending != 0 &&
                vote.status != -1)
                return {};
        }
        publish(runtime_);
        vote.popup_tick = services_.clock();
        if (cancelled())
            return result_;
        if (vote.status == -1 || vote.pending != 0)
            reject();
        else
            apply();
        return result_;
    } catch (...) {
        publish(runtime_);
        finish(MultiplayerOptionResult::failed);
        throw;
    }
}
} // namespace openciv::session
