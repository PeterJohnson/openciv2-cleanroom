#pragma once
#include "state/civilization.hpp"
#include <functional>
#include <optional>
#include <string>
#include <string_view>

namespace openciv::session {
using FileLabel = std::function<std::string(std::size_t)>;
// F06 sections 5/6/11. Unknown modes retain the preceding extension.
void select_classic_extension(bool scenario, std::uint8_t mode, std::string& extension);
// F06 section 11 shared filter. Contains embedded NULs and its final NUL.
// Unknown ordinary modes establish an empty filter but no next write position;
// attempting an alternative append in that state stops after the clear.
std::optional<std::size_t> build_classic_filter(std::string& filter,
                                                const state::GlobalValues& globals, bool scenario,
                                                bool raw_map, bool current_only,
                                                const FileLabel& label);
struct FileChoiceRequest {
    bool save = false, require_existing = false;
    // Opaque presentation owner token. A native binding resolves it; no native
    // window handle enters the simulation or file format.
    std::optional<std::uint64_t> owner;
    std::string title, filter, default_extension;
    // Set by choose_classic_file, not selected by callers.
    std::uint32_t flags = 0, filter_index = 0, filename_capacity = 0;
};
struct FileChoiceServices {
    std::function<std::uint32_t(const FileChoiceRequest&, std::string& filename)> choose;
    std::function<std::uint32_t()> extended_error;
};
// Native choice service boundary from F06 section 11 / Appendix A. Accepts only
// low byte 1. Every other return queries extended error and retains name writes.
bool choose_classic_file(FileChoiceRequest request, std::string& filename,
                         const FileChoiceServices& services);
} // namespace openciv::session
