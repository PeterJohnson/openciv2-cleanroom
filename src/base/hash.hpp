#pragma once

#include <cstdint>
#include <span>
#include <string>

namespace openciv::base {
// Content identities only; no gameplay random context or host services.
std::string hex_bytes(std::span<const std::uint8_t> bytes);
std::string sha256(std::span<const std::uint8_t> bytes);
} // namespace openciv::base
