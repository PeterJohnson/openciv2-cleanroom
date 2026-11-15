export module openciv.state.choices;

import std;

export namespace openciv::state {
/// Established F03/F05 choice meanings. Classic encodings belong to adapters.
enum class GameMode { single_player, hotseat, email, tcp, ipx, modem, serial };
/// Select map viewing or unit movement as the current interaction mode.
enum class MovementMode { view, move_units };
/// F03 section 7 fixes the seven government slots.
enum class Government {
    anarchy,
    despotism,
    monarchy,
    communism,
    fundamentalism,
    republic,
    democracy
};
/// The six supported difficulty selections in classic order.
enum class Difficulty { chieftain, warlord, prince, king, emperor, deity };
/// The leader gender selected during player setup.
enum class Gender { male, female };
/// The remembered world-size preset.
enum class WorldSize { small, normal, large };
/// Select standard rules or the custom-rule setup path.
enum class RuleSelection { standard, advanced };
/// N05 protocol identifiers. Optional legacy transports need no native provider.
enum class NetworkProtocol { tcp, ipx, modem, serial };
/// Identify one world-generation parameter selection.
enum class WorldParameter { land_mass, land_form, temperature, climate, age };
} // namespace openciv::state
