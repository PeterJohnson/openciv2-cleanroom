export module openciv.tools.conformance_script;

import std;

export {
    namespace openciv::conformance {
    bool run_script(std::string_view operation, std::span<char*> arguments);
    }
}
