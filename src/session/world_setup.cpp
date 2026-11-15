#include "session/world_setup.hpp"
#include <bit>

namespace openciv::session {
void WorldParameterDependencies::validate() const {
    if (!choose)
        throw std::invalid_argument("World parameters require a choice service");
    if (!preferences)
        throw std::invalid_argument("Preference storage is required");
}
namespace {
using Parameter = state::WorldParameter;
void publish(SessionRuntime& runtime) {
    if (runtime.active())
        runtime.publish();
}
template <class Operation> auto complete(SessionRuntime& runtime, Operation operation) {
    try {
        const auto result = operation();
        publish(runtime);
        return result;
    } catch (...) {
        publish(runtime);
        throw;
    }
}
std::int16_t narrow(std::int32_t value) {
    return std::bit_cast<std::int16_t>(static_cast<std::uint16_t>(value));
}
// Decimal prefix conversion followed by s16. Reducing the accumulator to
// sixteen bits on each digit is equivalent to the specified final reduction;
// F03's radix-prefixed rule-token parser is not this dialog's decimal parser.
std::int16_t dimension(std::string_view text) {
    while (!text.empty() && (text.front() == ' ' || (text.front() >= '\t' && text.front() <= '\r')))
        text.remove_prefix(1);
    bool negative = false;
    if (!text.empty() && (text.front() == '+' || text.front() == '-')) {
        negative = text.front() == '-';
        text.remove_prefix(1);
    }
    std::uint16_t value = 0;
    for (const auto c : text) {
        if (c < '0' || c > '9')
            break;
        value = static_cast<std::uint16_t>(value * 10U + static_cast<unsigned>(c - '0'));
    }
    return std::bit_cast<std::int16_t>(negative ? static_cast<std::uint16_t>(0U - value) : value);
}
void require_call(std::string_view call) {
    if (call.empty())
        throw std::invalid_argument("World parameters require a semantic call identity");
}
void sample(state::WorldSetup& setup, Parameter parameter, base::GameplayRandom& random,
            std::string_view call) {
    constexpr std::array<std::string_view, 5> sites{"F05.world.land-mass", "F05.world.land-form",
                                                    "F05.world.temperature", "F05.world.climate",
                                                    "F05.world.age"};
    const auto index = static_cast<std::size_t>(parameter);
    auto& value = setup.parameters[index];
    if (parameter == Parameter::land_form) {
        value = 0;
        if (random.primary_modulo({std::string(sites[index]), std::string(call), 0}, 3) == 0)
            value = setup.parameters[0];
    } else {
        value = random.primary_modulo({std::string(sites[index]), std::string(call), 0}, 3) - 1;
    }
}
} // namespace

bool select_world_size(SessionRuntime& runtime, const WorldSizeInteraction& supplied) {
    const auto& services = supplied;
    auto pin = runtime.pin_world();
    return complete(runtime, [&] {
        auto& setup = runtime.world().world_setup;
        auto& flags = runtime.world().globals.game_flags;
        auto& remembered = runtime.process().ordinary_preferences.map_size;
        flags = 0;
        bool success = false;
        bool done = false;
        while (!done) {
            publish(runtime);
            const auto choice = services.choose_preset(remembered);
            if (const auto* preset = std::get_if<std::int32_t>(&choice)) {
                if (*preset >= 0) {
                    remembered = static_cast<state::WorldSize>(narrow(*preset));
                    setup.width = *preset == 0 ? 40 : (*preset == 2 ? 75 : 50);
                    setup.height = *preset == 0 ? 50 : (*preset == 2 ? 120 : 80);
                    success = true;
                }
                done = true;
                continue;
            }
            for (;;) {
                publish(runtime);
                const auto custom = services.choose_custom("50", "80");
                if (custom.status == CustomSizeStatus::unavailable) {
                    done = true;
                    break;
                }
                if (custom.status == CustomSizeStatus::cancelled)
                    break;
                setup.width = dimension(custom.width);
                setup.height = dimension(custom.height);
                const auto area = setup.width * setup.height;
                if (setup.width >= 20 && setup.width <= 250 && setup.height >= 20 &&
                    setup.height <= 250 && area >= 1000 && area <= 10000) {
                    success = done = true;
                    break;
                }
            }
        }
        const auto area = setup.width * setup.height;
        if (area > 5999)
            flags |= 4U;
        if (area < 3000)
            flags |= 8U;
        setup.width = narrow(2 * setup.width);
        return success;
    });
}
void randomize_world_parameters(SessionRuntime& runtime, base::GameplayRandom& random,
                                std::string_view call) {
    require_call(call);
    auto pin = runtime.pin_world();
    complete(runtime, [&] {
        for (const auto parameter : {Parameter::land_mass, Parameter::land_form,
                                     Parameter::temperature, Parameter::climate, Parameter::age})
            sample(runtime.world().world_setup, parameter, random, call);
        return true;
    });
}
bool customize_world_parameters(SessionRuntime& runtime, base::GameplayRandom& random,
                                std::string_view call, const WorldParameterDependencies& supplied) {
    const auto& services = supplied;
    require_call(call);
    services.validate();
    auto pin = runtime.pin_world();
    return complete(runtime, [&] {
        auto& setup = runtime.world().world_setup;
        auto& remembered = runtime.process().ordinary_preferences.world_parameters;
        for (const auto parameter : {Parameter::land_mass, Parameter::land_form, Parameter::climate,
                                     Parameter::temperature, Parameter::age}) {
            const auto index = static_cast<std::size_t>(parameter);
            publish(runtime);
            const auto choice = services.choose(parameter, remembered[index]);
            if (const auto* ordinary = std::get_if<std::int32_t>(&choice)) {
                if (*ordinary > 2)
                    throw std::invalid_argument(
                        "World parameter row is outside the specified choices");
                setup.parameters[index] = base::subtract32(*ordinary, 1);
                if (*ordinary < 0)
                    return false;
                remembered[index] = narrow(setup.parameters[index]);
            } else {
                sample(setup, parameter, random, call);
            }
        }
        (void)save_classic_preferences(runtime, *services.preferences);
        return true;
    });
}
bool select_world_parameters(SessionRuntime& runtime, base::GameplayRandom& random,
                             std::string_view call, bool customize, bool world_selected,
                             const WorldParameterDependencies& services) {
    if (!world_selected)
        return false;
    if (customize)
        return customize_world_parameters(runtime, random, call, services);
    randomize_world_parameters(runtime, random, call);
    return world_selected;
}
} // namespace openciv::session
