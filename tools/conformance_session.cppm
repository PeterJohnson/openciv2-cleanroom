export module openciv.tools.conformance_session;

import std;

export extern "C++" {
    namespace openciv::conformance {
    bool run_session(std::string_view operation, std::span<char*> arguments);
    }
}
