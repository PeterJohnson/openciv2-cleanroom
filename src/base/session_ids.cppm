export module openciv.base.session_ids;

import std;

export namespace openciv::base {
/// Identity of an engine runtime, independent of addresses and classic wire fields.
/// Architecture §4: runtime identity domains, not classic file/wire fields.
enum class RuntimeId : std::uint64_t {};
/// Identity of one client-admission lifetime within a runtime.
enum class SessionId : std::uint64_t {};
/// Identity of a participant seat bound to a civilization.
enum class ParticipantId : std::uint64_t {};
/// Identity of a revocable controller binding.
enum class ControllerId : std::uint64_t {};
/// Identity of an admitted command and its retained result.
enum class CommandId : std::uint64_t {};
/// Identity of a pending user interaction.
enum class InteractionId : std::uint64_t {};
/// Identity assigned to a transport connection by the session adapter.
enum class ConnectionId : std::uint16_t {};
} // namespace openciv::base
