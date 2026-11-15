export module openciv.tools.conformance_script;

import std;

export extern "C++" {
    namespace openciv::conformance {
    bool run_script(std::string_view operation, std::span<char*> arguments);
    }
}
