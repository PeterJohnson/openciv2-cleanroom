#pragma once

namespace openciv::state {
// Established F03/F05 choice meanings. Classic encodings belong to adapters.
enum class GameMode { single_player, hotseat, email, tcp, ipx, modem, serial };
enum class MovementMode { view, move_units };
// F03 section 7 fixes the seven government slots.
enum class Government {
    anarchy,
    despotism,
    monarchy,
    communism,
    fundamentalism,
    republic,
    democracy
};
enum class Difficulty { chieftain, warlord, prince, king, emperor, deity };
enum class Gender { male, female };
enum class WorldSize { small, normal, large };
enum class RuleSelection { standard, advanced };
// N05 protocol identifiers. Optional legacy transports need no native provider.
enum class NetworkProtocol { tcp, ipx, modem, serial };
} // namespace openciv::state
