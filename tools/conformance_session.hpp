#pragma once
#include <span>
#include <string_view>

namespace openciv::conformance {
bool run_session(std::string_view operation, std::span<char*> arguments);
}
