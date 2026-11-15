export module openciv.formats.classic_rules;

import std;
export import openciv.base.callback_ref;
export import openciv.formats.classic_text_reader;
export import openciv.state.rule_input;

export extern "C++" {
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
    // Diagnostic/cleanup hook. Returning never resumes the offending parser.
    using RuleFailureObserver = base::CallbackRef<void(const RuleLoadError&)>;
    state::AdvanceReference advance_reference(std::string_view token) noexcept;
    state::TerrainResult terrain_result(std::string_view token, const TextCase& comparison);

    // F03 sections 2-7,9 and Appendix A. Decode into unpublished common inputs.
    // Parsing never changes live state or assigns native identity IDs.
    // Presentation decoding currently admits reversible Windows-1252 text only.
    // A supplied failure observer is borrowed and must outlive the loader.
    class RuleLoader {
    public:
        RuleLoader(TextReader& reader, state::RuleInput& output, TextCase comparison,
                   RuleFailureObserver services = {});
        RuleLoader(const RuleLoader&) = delete;
        RuleLoader& operator=(const RuleLoader&) = delete;
        void load(std::string_view filename);
        // Section readers start immediately after an already selected header.
        void cosmic();
        void advances();
        void improvements();
        void wonder_expiry();
        void units();
        void terrain();
        void governments();
        void leaders();
        void commodities();
        void orders();
        void difficulties();
        void attitudes();

    private:
        state::RuleText text(std::string bytes, bool blank_as_space);
        void name_into(state::RuleText& target, bool blank_as_space);
        void section(std::optional<std::string_view> filename, std::string_view name);
        [[noreturn]] void fail(RuleLoadFault fault, std::string_view section, std::size_t row,
                               int position, int code);
        TextReader& reader_;
        state::RuleDefinitions& rules_;
        state::RuleInput& output_;
        TextCase comparison_;
        RuleFailureObserver on_failure_;
    };
    } // namespace openciv::formats::classic
}
