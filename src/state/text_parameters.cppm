export module openciv.state.text_parameters;

import std;

export namespace openciv::state {
/// P04 section 2 / Appendix B: process-shared substitution values, initially
/// zero/empty. STRING11's separate profile backing is not an ordinary slot.
/// Dynamic strings avoid excluded fixed-buffer overwrites, not semantic limits.
struct TextParameters {
    std::array<std::int32_t, 6> numbers{};
    std::array<std::string, 11> strings;
    std::string line;
};
} // namespace openciv::state
