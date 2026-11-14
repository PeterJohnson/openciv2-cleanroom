#pragma once

#include <cstdint>

namespace openciv::base {
// Architecture §4: runtime identity domains, not classic file/wire fields.
enum class RuntimeId : std::uint64_t {};
enum class SessionId : std::uint64_t {};
enum class ParticipantId : std::uint64_t {};
enum class ControllerId : std::uint64_t {};
enum class ConnectionId : std::uint16_t {};
} // namespace openciv::base
