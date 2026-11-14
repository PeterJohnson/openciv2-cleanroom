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
template <typename R, typename... Args>
std::function<R(Args...)> published(SessionRuntime& runtime, std::function<R(Args...)> service) {
    if (!service)
        return {};
    return [&runtime, service = std::move(service)](Args... args) -> R {
        publish(runtime);
        return service(std::forward<Args>(args)...);
    };
}
void save(SessionRuntime& runtime, const PreferenceFileServices& services) {
    auto files = services;
    files.open_output = [&](std::string_view path) {
        publish(runtime);
        auto output = services.open_output(path);
        if (output) {
            output->item = published(runtime, output->item);
            output->close = published(runtime, output->close);
        }
        return output;
    };
    files.remove_file = published(runtime, files.remove_file);
    (void)save_classic_preferences(runtime.process(), files);
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
                            const GameOptionServices& services) {
    if (!choices)
        return false;
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
                services.reinitialize_music();
            }
            publish(runtime);
            services.synchronize_music();
        } else {
            publish(runtime);
            services.stop_music();
        }
        constexpr std::array<std::uint32_t, 9> bits{0x4000, 0x2000, 0x1000, 0x800, 0x400,
                                                    0x200,  0x100,  0x80,   0x40};
        for (std::size_t i = 0; i < bits.size(); ++i)
            if ((*choices)[i + 2])
                options |= bits[i];
        runtime.process().ordinary_preferences.game_options = options;
        save(runtime, services.preferences);
    });
    return true;
}
bool configure_graphics_options(SessionRuntime& runtime,
                                std::optional<GraphicsOptionChoices> choices,
                                const GraphicsOptionServices& services) {
    if (!choices)
        return false;
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
        save(runtime, services.preferences);
    });
    return true;
}
bool configure_message_options(SessionRuntime& runtime, std::optional<MessageOptionChoices> choices,
                               const PreferenceFileServices& services) {
    if (!choices)
        return false;
    complete(runtime, [&] {
        auto& messages = runtime.world().globals.message_options;
        messages = 0;
        for (std::size_t i = 0; i < choices->size(); ++i)
            if (!(*choices)[i])
                messages |= static_cast<std::uint16_t>(1U << i);
        runtime.process().ordinary_preferences.disabled_messages = messages;
        save(runtime, services);
    });
    return true;
}
void poll_option_vote(SessionRuntime& runtime, const OptionVoteServices& services) {
    complete(runtime, [&] {
        auto& multiplayer = runtime.world().multiplayer;
        auto& vote = multiplayer.vote;
        publish(runtime);
        services.poll(1, 0);
        if (multiplayer.handoff == 0) {
            publish(runtime);
            const auto now = services.clock();
            if (base::signed32(now - vote.popup_tick) < 2401 && vote.pending != 0 &&
                vote.status != -1)
                return;
        }
        publish(runtime);
        services.finish_modal();
        publish(runtime);
        vote.popup_tick = services.clock();
    });
}
MultiplayerOptionResult configure_multiplayer_options(SessionRuntime& runtime,
                                                      std::int32_t dialog_result,
                                                      MultiplayerOptionChoices choices,
                                                      const MultiplayerOptionServices& services) {
    if (dialog_result != 0)
        return MultiplayerOptionResult::cancelled;
    auto result = MultiplayerOptionResult::unchanged;
    complete(runtime, [&] {
        auto& process = runtime.process();
        auto& preferences = process.multiplayer_preferences;
        auto& world = runtime.world();
        auto& multiplayer = world.multiplayer;
        auto& live = multiplayer.saved;
        auto& vote = multiplayer.vote;
        preferences.clear_chat_new_game = choices[0] ? 1 : 0;
        preferences.clear_chat_boot = choices[1] ? 1 : 0;
        save(runtime, services.preferences);
        if (world.globals.multiplayer >= 3 && !multiplayer.network_host) {
            result = MultiplayerOptionResult::client_only;
            return;
        }
        preferences.double_production = choices[2] ? 1 : 0;
        preferences.double_movement = choices[3] ? 1 : 0;
        if (preferences.double_production == live.double_production &&
            preferences.double_movement == live.double_movement)
            return;
        if (world.globals.multiplayer >= 3 && multiplayer.player_count > 1) {
            auto& parameters = process.text_parameters.strings;
            publish(runtime);
            parameters[0] = text(services.local_player_name());
            publish(runtime);
            parameters[1] = text(services.production_label(preferences.double_production != 0));
            publish(runtime);
            parameters[1] += text(services.separator());
            publish(runtime);
            parameters[1] += text(services.movement_label(preferences.double_movement != 0));
            vote.pending = base::subtract32(multiplayer.player_count, 1);
            vote.status = 0;
            publish(runtime);
            services.broadcast_proposal();
            publish(runtime);
            services.install_vote_callback(
                [&runtime, services = services.vote] { poll_option_vote(runtime, services); });
            publish(runtime);
            vote.popup_tick = services.vote.clock();
            publish(runtime);
            const auto modal_result = services.show_vote_dialog();
            if (modal_result < 0 || vote.status == -1 || vote.pending != 0) {
                preferences.double_production = live.double_production;
                preferences.double_movement = live.double_movement;
                publish(runtime);
                services.broadcast_rejection();
                publish(runtime);
                services.notice("PMCHANGENO");
                result = MultiplayerOptionResult::rejected;
                return;
            }
        }
        live.double_production = preferences.double_production;
        live.double_movement = preferences.double_movement;
        rules::apply_double_movement(world.globals.multiplayer, live.double_movement,
                                     *process.rules, published(runtime, services.redraw_status));
        if (world.globals.multiplayer >= 3 && multiplayer.player_count > 1) {
            publish(runtime);
            services.send_units();
            publish(runtime);
            services.send_options(live.double_production, live.double_movement, live.simultaneous);
            publish(runtime);
            services.broadcast_acceptance();
            publish(runtime);
            services.notice("PMCHANGEYES");
        }
        result = MultiplayerOptionResult::applied;
    });
    return result;
}
} // namespace openciv::session
