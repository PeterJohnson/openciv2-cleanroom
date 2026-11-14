#pragma once
#include <span>
#include <string_view>

namespace openciv::conformance {
bool run_script(std::string_view operation, std::span<char*> arguments);
}
