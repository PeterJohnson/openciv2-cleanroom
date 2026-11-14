// X02 sections 3/6, F02 section 2 and X01 fresh-game isolation. Test-only
// projection over the real runtime boundary. Expectations never enter here.
#include "conformance_session.hpp"
#include "conformance_io.hpp"
#include "session/runtime.hpp"
#include <bit>
#include <optional>

namespace openciv::conformance {
namespace {
using namespace io;
bool boolean() {
    const auto value = input<unsigned>();
    if (value > 1)
        throw std::invalid_argument("Noncanonical fresh-game boolean");
    return value != 0;
}
std::string bytes(std::size_t size) {
    const auto hex = word();
    if (hex.size() != size * 2 || hex.find_first_not_of("0123456789abcdef") != std::string::npos)
        throw std::invalid_argument("Invalid fresh-game byte array");
    return unhex(hex);
}
std::string boolean_json(bool value) {
    return std::string{"{\"$type\":\"bool\",\"value\":"} + (value ? "true}" : "false}");
}
std::string bytes_json(std::string_view value) {
    constexpr char digits[] = "0123456789abcdef";
    std::string hex;
    for (const auto character : value) {
        const auto byte = static_cast<unsigned char>(character);
        hex += digits[byte >> 4];
        hex += digits[byte & 15];
    }
    return "{\"$type\":\"bytes\",\"hex\":" + quote(hex) + "}";
}
bool valid(const std::optional<session::PlayerClient>& client) {
    if (!client)
        return false;
    try {
        (void)client->identity();
        return true;
    } catch (const session::AccessError&) {
        return false;
    }
}
std::string snapshot(session::SessionRuntime& runtime,
                     const std::optional<session::PlayerClient>& client) {
    const auto& world = runtime.world();
    const auto& process = runtime.process();
    std::string result = "{";
    auto field = [&](std::string_view key, const std::string& value) {
        if (result.size() > 1)
            result += ',';
        result += quote(key) + ':' + value;
    };
    field("F02.nextUnitToken", scalar("u32", process.tokens.next_unit));
    field("F02.nextCityToken", scalar("u32", process.tokens.next_city));
    field("F02.unitEnd", scalar("u32", world.entities.units.end()));
    field("F02.cityEnd", scalar("u32", world.entities.cities.end()));
    std::string damage, goals, future, cursors, passwords;
    for (const auto& unit : world.entities.units.storage())
        damage += static_cast<char>(unit.damage);
    field("F02.unitDamage", bytes_json(damage));
    std::string shields = "[";
    for (const auto& city : world.entities.cities.storage()) {
        if (shields.size() > 1)
            shields += ',';
        shields += scalar("i16", city.shield_box);
    }
    field("F02.cityShields", shields + ']');
    for (const auto& civilization : world.civilizations) {
        future += static_cast<char>(civilization.future_techs);
        for (const auto& goal : civilization.goals)
            goals += static_cast<char>(goal.role);
        for (const auto& goal : civilization.standing_goals)
            goals += static_cast<char>(goal.role);
    }
    for (const auto& leader : process.rules->leaders)
        cursors += static_cast<char>(leader.city_name_index);
    for (const auto byte : world.multiplayer.saved.encoded_passwords)
        passwords += static_cast<char>(byte);
    field("F02.futureTechnologies", bytes_json(future));
    field("F02.peaceTurns", scalar("i16", world.globals.peace_turns));
    field("F02.goalRoles", bytes_json(goals));
    field("F02.leaderCityNameCursors", bytes_json(cursors));
    field("F02.resumeEvent", scalar("i16", world.multiplayer.saved.resume_event));
    field("F02.passwordBytes", bytes_json(passwords));
    field("F02.mapAllocated", boolean_json(world.map.allocated()));
    field("F02.fallbackRegion", scalar("u8", process.fallback.region));
    field("F02.textLine", text(process.text_parameters.line));
    field("F02.preferenceGameOptions", scalar("u32", process.ordinary_preferences.game_options));
    field("F02.preferenceTimer", scalar("i16", process.multiplayer_preferences.timer));
    field("F02.doubleMovementApplied", boolean_json(process.rules->double_movement_applied));
    field("F02.previousClientValid", boolean_json(valid(client)));
    return result + '}';
}
} // namespace
bool run_session(std::string_view operation, std::span<char*> arguments) {
    if (operation != "F02.prepareNewGame")
        return false;
    if (!arguments.empty())
        throw std::invalid_argument("Fresh-game boundary has no arguments");
    state::ProcessContext process;
    session::SessionRuntime runtime{process, base::RuntimeId{1}};
    process.tokens.next_unit = input<std::uint32_t>();
    process.tokens.next_city = input<std::uint32_t>();
    auto& world = runtime.world();
    world.entities.units.set_end(input<std::uint32_t>());
    world.entities.cities.set_end(input<std::uint32_t>());
    world.globals.unit_end = static_cast<std::int16_t>(world.entities.units.end());
    world.globals.city_end = static_cast<std::int16_t>(world.entities.cities.end());
    const auto damage = bytes(2048);
    for (std::size_t i = 0; i < damage.size(); ++i)
        world.entities.units.storage()[i].damage = static_cast<std::uint8_t>(damage[i]);
    for (auto& city : world.entities.cities.storage())
        city.shield_box = input<std::int16_t>();
    const auto future = bytes(8);
    for (std::size_t i = 0; i < future.size(); ++i)
        world.civilizations[i].future_techs = static_cast<std::uint8_t>(future[i]);
    world.globals.peace_turns = input<std::int16_t>();
    const auto goals = bytes(512);
    std::size_t cursor = 0;
    for (auto& civilization : world.civilizations) {
        for (auto& goal : civilization.goals)
            goal.role = std::bit_cast<std::int8_t>(static_cast<std::uint8_t>(goals[cursor++]));
        for (auto& goal : civilization.standing_goals)
            goal.role = std::bit_cast<std::int8_t>(static_cast<std::uint8_t>(goals[cursor++]));
    }
    const auto cursors = bytes(21);
    for (std::size_t i = 0; i < cursors.size(); ++i)
        process.rules->leaders[i].city_name_index = static_cast<std::uint8_t>(cursors[i]);
    world.multiplayer.saved.resume_event = input<std::int16_t>();
    const auto passwords = bytes(256);
    for (std::size_t i = 0; i < passwords.size(); ++i)
        world.multiplayer.saved.encoded_passwords[i] = static_cast<std::uint8_t>(passwords[i]);
    if (boolean())
        world.map.allocate(world::Geometry{8, 6});
    process.fallback.region = input<std::uint8_t>();
    const auto line = word();
    if (line.size() > 131072)
        throw std::invalid_argument("Fresh-game text exceeds harness bound");
    process.text_parameters.line = line == "-" ? "" : unhex(line);
    process.ordinary_preferences.game_options = input<std::uint32_t>();
    process.multiplayer_preferences.timer = input<std::int16_t>();
    process.rules->double_movement_applied = boolean();
    std::optional<session::PlayerClient> client;
    if (boolean()) {
        runtime.begin_client_session({11, 22});
        client = runtime.bind(runtime.add_participant(state::CivilizationSlot{1}),
                              session::ControllerKind::scripted);
    }
    std::string extra;
    if (std::cin >> extra)
        throw std::invalid_argument("Trailing fresh-game input");
    runtime.prepare_new_game();
    const auto state = snapshot(runtime, client);
    // Three real boundary observations; no artificial publication callbacks.
    std::cout
        << "{\"outcome\":\"return\",\"result\":{\"$type\":\"null\"},\"state\":" << state
        << ",\"trace\":[{\"seq\":0,\"id\":\"root.enter\",\"kind\":\"enter\",\"call\":\"root\","
           "\"payload\":{\"operation\":\"F02.prepareNewGame\",\"parent\":null,\"arguments\":{}}},"
           "{\"seq\":1,\"id\":\"root.return\",\"kind\":\"return\",\"call\":\"root\","
           "\"payload\":{\"result\":{\"$type\":\"null\"}}},"
           "{\"seq\":2,\"id\":\"root.exit\",\"kind\":\"snapshot\",\"call\":\"root\","
           "\"payload\":{\"label\":\"exit\",\"state\":"
        << state << "}}]}\n";
    return true;
}
} // namespace openciv::conformance
