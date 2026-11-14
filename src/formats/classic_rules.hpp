#pragma once
#include "classic_text_reader.hpp"
#include "state/rules.hpp"
#include <vector>

namespace openciv::formats::classic {
// Logical append-only storage, not original string IDs or signed-16 leader
// bindings. Allocation ordinals remain stable across vector reallocations.
// Construction defaults do not prescribe an original process/new-game reset.
struct RuleLoadState {
    std::vector<std::string> text_allocations; // Includes terminator and zero padding.
    std::array<std::optional<std::size_t>, 62> interface_unit_names{};
    std::array<state::RuleText, 21> current_leader_names;
    bool double_movement_applied = false;
};
enum class RuleLoadFault { reference, missing_section, invalid_title_index };
class RuleLoadError : public std::runtime_error {
public:
    RuleLoadError(RuleLoadFault fault, std::string section, std::string line, std::size_t row,
                  int position, int code);
    RuleLoadFault fault;
    std::string section, line;
    std::size_t row;
    int position, code;
    static constexpr int module = 3;
};
struct RuleLoadServices {
    // Called before publishing each nonempty/padded allocation. Throwing stops
    // at that boundary. Ordinary empty text makes no allocation call.
    std::function<void(std::string_view text, std::size_t bytes)> allocate;
    // Diagnostic/cleanup hook. Returning never resumes the offending parser.
    std::function<void(const RuleLoadError&)> fatal;
};
state::AdvanceReference advance_reference(std::string_view token) noexcept;
state::TerrainResult terrain_result(std::string_view token, const TextCase& comparison);

// F03 sections 2-7,9 and Appendix A. Mutates existing definitions incrementally;
// native identity IDs are retained, never inferred from classic display names.
// Presentation decoding currently admits reversible Windows-1252 text only.
class RuleLoader {
public:
    RuleLoader(TextReader& reader, state::RuleDefinitions& definitions, RuleLoadState& retained,
               TextCase comparison, RuleLoadServices services = {});
    void load(std::string_view filename, bool replace_leader_data);
    // Section readers start immediately after an already selected header.
    void cosmic();
    void advances(bool keep_names = false);
    void improvements(bool keep_names = false);
    void wonder_expiry();
    void units(bool keep_names = false);
    void terrain();
    void governments();
    void leaders(bool replace_leader_data);
    void commodities();
    void orders();
    void difficulties();
    void attitudes();

private:
    state::RuleText store(std::string text, std::size_t minimum);
    state::RuleText name(std::size_t minimum);
    void name_into(state::RuleText& target, std::size_t minimum, bool keep);
    void section(std::optional<std::string_view> filename, std::string_view name);
    [[noreturn]] void fail(RuleLoadFault fault, std::string_view section, std::size_t row,
                           int position, int code);
    TextReader& reader_;
    state::RuleDefinitions& rules_;
    RuleLoadState& retained_;
    TextCase comparison_;
    RuleLoadServices services_;
};
} // namespace openciv::formats::classic
