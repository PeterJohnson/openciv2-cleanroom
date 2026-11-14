#pragma once
#include <span>
#include <string_view>

namespace openciv::conformance {
// Returns false only when the operation is not one of the registered samplers.
bool run_random(std::string_view operation, std::span<char*> arguments);
} // namespace openciv::conformance
