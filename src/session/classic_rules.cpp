#include "session/classic_rules.hpp"
#include "rules/rules_install.hpp"

namespace openciv::session {
namespace classic = formats::classic;
ClassicRuleContext::ClassicRuleContext(state::RuleState& rules, ClassicFileState& files,
                                       classic::TextWorkspace& workspace,
                                       classic::TextFiles text_files, RuleContextServices services)
    : rules_(rules), files_(files), services_(std::move(services)),
      reader_(workspace, files.text, std::move(text_files), services_.comparison) {
    if (!services_.reload_sprites || !services_.flush || !services_.change_directory ||
        !services_.exists)
        throw std::invalid_argument("Rule context requires resource, directory and flush services");
}
void ClassicRuleContext::load(bool replace_leader_data) {
    classic::LabelLoader(reader_, files_.text, rules_.text, rules_.labels, services_.labels)
        .rebuild();
    services_.reload_sprites();
    const auto resource = "RULES." + files_.text.extension;
    const auto scenario = files_.scenario_directory;
    const bool custom = services_.exists(scenario, resource);
    files_.text.search = custom ? scenario : files_.text.home;
    files_.text.working = files_.text.search;
    const auto selected = files_.text.working;
    services_.change_directory(selected);
    auto input = std::make_unique<state::RuleInput>();
    classic::RuleLoader(reader_, *input, services_.comparison, services_.rules).load("RULES");
    rules::RuleInstaller(*input, rules_).load(replace_leader_data);
    files_.text.working = files_.text.home;
    const auto home = files_.text.working;
    services_.change_directory(home);
    services_.flush();
}
void ClassicRuleContext::reload(LoadedRuleTable table, bool keep_names) {
    const std::string_view section = table == LoadedRuleTable::advances       ? "CIVILIZE"
                                     : table == LoadedRuleTable::improvements ? "IMPROVE"
                                     : table == LoadedRuleTable::units
                                         ? "UNITS"
                                         : throw std::invalid_argument("Unknown final rule table");
    if (!reader_.open("RULES", section)) {
        const classic::RuleLoadError error(classic::RuleLoadFault::missing_section,
                                           std::string(section), std::string(reader_.line()), 0, 0,
                                           0);
        if (services_.rules.fatal)
            services_.rules.fatal(error);
        throw error;
    }
    auto input = std::make_unique<state::RuleInput>();
    classic::RuleLoader loader(reader_, *input, services_.comparison, services_.rules);
    rules::RuleInstaller installer(*input, rules_);
    if (table == LoadedRuleTable::advances) {
        loader.advances();
        installer.advances(keep_names);
    } else if (table == LoadedRuleTable::improvements) {
        loader.improvements();
        installer.improvements(keep_names);
    } else {
        loader.units();
        installer.units(keep_names);
    }
}
void ClassicRuleContext::close() {
    reader_.close();
}
} // namespace openciv::session
