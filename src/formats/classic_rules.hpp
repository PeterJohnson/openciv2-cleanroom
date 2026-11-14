#pragma once
#include "classic_text_reader.hpp"
#include "state/rule_state.hpp"
#include <vector>

namespace openciv::formats::classic {
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
    // Diagnostic/cleanup hook. Returning never resumes the offending parser.
    std::function<void(const RuleLoadError&)> fatal;
};
// Semantic inputs required to install converted rules against retained state.
// A final rule snapshot has already lost prerequisite fixup inputs, movement
// scaling operands and overridden/default title distinctions.
struct RuleConversionOperands {
    struct TitleOverride {
        std::uint8_t government = 0;
        state::TitlePair titles;
    };
    std::array<std::array<state::AdvanceReference, 2>, 100> advance_prerequisites{};
    std::array<state::AdvanceReference, 67> improvement_prerequisites{};
    std::array<std::int32_t, 62> movement_points{};
    std::array<std::vector<TitleOverride>, 21> title_overrides;
};
state::AdvanceReference advance_reference(std::string_view token) noexcept;
state::TerrainResult terrain_result(std::string_view token, const TextCase& comparison);

// F03 sections 2-7,9 and Appendix A. Mutates existing definitions incrementally;
// native identity IDs are retained, never inferred from classic display names.
// Presentation decoding currently admits reversible Windows-1252 text only.
class RuleLoader {
public:
    RuleLoader(TextReader& reader, state::RuleState& retained, TextCase comparison,
               RuleLoadServices services = {}, RuleConversionOperands* conversion = nullptr);
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
    struct StoredText {
        state::RuleText value;
        state::TextId id;
    };
    StoredText store(std::string text, bool blank_as_space);
    void store_into(state::RuleText& target, state::TextId& binding, std::string text,
                    bool blank_as_space);
    void name_into(state::RuleText& target, state::TextId& binding, bool blank_as_space,
                   bool keep = false);
    void leader_name(state::RuleText& target, state::NameReference& binding, bool blank_as_space,
                     bool replace);
    void section(std::optional<std::string_view> filename, std::string_view name);
    [[noreturn]] void fail(RuleLoadFault fault, std::string_view section, std::size_t row,
                           int position, int code);
    TextReader& reader_;
    state::RuleDefinitions& rules_;
    state::RuleState& retained_;
    TextCase comparison_;
    RuleLoadServices services_;
    // Optional owned-value capture, never a callback/reentry boundary. Only a
    // successful full load with replace_leader_data=true yields complete operands.
    RuleConversionOperands* conversion_;
};
} // namespace openciv::formats::classic
