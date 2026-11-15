export module openciv.formats.classic_rules;

import std;
export import openciv.base.callback_ref;
export import openciv.formats.classic_text_reader;
export import openciv.state.rule_input;

export namespace openciv::formats::classic {
/// Categories of classic rule-file failures with parser position context.
enum class RuleLoadFault { reference, missing_section, invalid_title_index };
/// A rule-load failure retaining its section, input line, and diagnostic position.
class RuleLoadError : public std::runtime_error {
public:
    /// Capture a categorized parser failure with the offending text and location.
    RuleLoadError(RuleLoadFault fault, std::string section, std::string line, std::size_t row,
                  int position, int code);
    RuleLoadFault fault;
    std::string section, line;
    std::size_t row;
    int position, code;
    static constexpr int module = 3;
};
// Diagnostic/cleanup hook. Returning never resumes the offending parser.
using RuleFailureObserver = base::CallbackRef<void(const RuleLoadError&)>;
/// Resolve a classic advance token to an advance identity or sentinel.
state::AdvanceReference advance_reference(std::string_view token) noexcept;
/// Resolve a terrain-result token using the supplied classic comparison policy.
state::TerrainResult terrain_result(std::string_view token, const TextCase& comparison);

/// F03 sections 2-7,9 and Appendix A. Decode into unpublished common inputs.
/// Parsing never changes live state or assigns native identity IDs.
/// Presentation decoding currently admits reversible Windows-1252 text only.
/// A supplied failure observer is borrowed and must outlive the loader.
class RuleLoader {
public:
    /// Borrow the reader and unpublished output, retaining the comparison and failure hook.
    RuleLoader(TextReader& reader, state::RuleInput& output, TextCase comparison,
               RuleFailureObserver services = {});
    RuleLoader(const RuleLoader&) = delete;
    RuleLoader& operator=(const RuleLoader&) = delete;
    /// Load all required rule sections from the selected file.
    void load(std::string_view filename);
    /// Parse cosmic constants from the currently selected section.
    /// Section readers start immediately after an already selected header.
    void cosmic();
    /// Parse advances from the currently selected section.
    void advances();
    /// Parse improvements from the currently selected section.
    void improvements();
    /// Parse wonder expiry references from the currently selected section.
    void wonder_expiry();
    /// Parse unit types from the currently selected section.
    void units();
    /// Parse terrain types from the currently selected section.
    void terrain();
    /// Parse governments from the currently selected section.
    void governments();
    /// Parse leaders from the currently selected section.
    void leaders();
    /// Parse commodities from the currently selected section.
    void commodities();
    /// Parse unit orders from the currently selected section.
    void orders();
    /// Parse difficulty labels from the currently selected section.
    void difficulties();
    /// Parse attitude labels from the currently selected section.
    void attitudes();

private:
    /// Convert classic name bytes to reversible rule text, optionally preserving blanks as spaces.
    state::RuleText text(std::string bytes, bool blank_as_space);
    /// Read the next name token into a rule-text field.
    void name_into(state::RuleText& target, bool blank_as_space);
    /// Select a required section, reporting a missing-section failure.
    void section(std::optional<std::string_view> filename, std::string_view name);
    /// Notify the failure observer, then throw the categorized rule-load error.
    [[noreturn]] void fail(RuleLoadFault fault, std::string_view section, std::size_t row,
                           int position, int code);
    TextReader& reader_;
    state::RuleDefinitions& rules_;
    state::RuleInput& output_;
    TextCase comparison_;
    RuleFailureObserver on_failure_;
};
} // namespace openciv::formats::classic
