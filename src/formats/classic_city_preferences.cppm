export module openciv.formats.classic_city_preferences;

import std;
export import openciv.formats.classic_text_reader;
export import openciv.state.city_preferences;
export import openciv.state.rule_state;

export namespace openciv::formats::classic {
/// Decode an update against a snapshot of the supplied names. Installation is
/// shared with native content in rules::apply_city_preferences.
state::CityPreferenceUpdate load_city_preferences(TextReader& reader,
                                                  const state::RuleDefinitions& rules,
                                                  const TextCase& comparison);
/// Parse classic city build preferences using the selected rule names and case policy.
state::CityPreferenceUpdate load_city_preferences(TextReader& reader, const state::RuleState& rules,
                                                  const TextCase& comparison);
} // namespace openciv::formats::classic

module :private;
namespace openciv::formats::classic {
namespace {
state::CityPreferenceUpdate load(TextReader& reader, const std::array<std::string, 38>& names,
                                 const TextCase& comparison) {
    if (!comparison.equal)
        throw std::invalid_argument("City preferences require a comparison policy");
    state::CityPreferenceUpdate update;
    try {
        update.deprioritize_defense = reader.open("CITYPREF.TXT", "NODEFEND");
        reader.close();
        if (!reader.open("CITYPREF.TXT", "AUTOBUILD"))
            return update;
        while (update.build_order.size() < 32 && reader.next_line()) {
            const auto token = reader.next_token();
            std::size_t index = 0;
            for (; index < names.size(); ++index)
                if (comparison.equal(token, names[index]))
                    break;
            if (index == names.size())
                break;
            update.build_order.push_back(static_cast<std::uint8_t>(index + 1));
        }
        reader.close();
    } catch (...) {
        try {
            reader.close();
        } catch (...) {
        }
        throw;
    }
    return update;
}
} // namespace
state::CityPreferenceUpdate load_city_preferences(TextReader& reader,
                                                  const state::RuleDefinitions& rules,
                                                  const TextCase& comparison) {
    std::array<std::string, 38> names;
    for (std::size_t i = 0; i < names.size(); ++i)
        names[i] = rules.improvements[i + 1].identity.name.classic;
    return load(reader, names, comparison);
}
state::CityPreferenceUpdate load_city_preferences(TextReader& reader, const state::RuleState& rules,
                                                  const TextCase& comparison) {
    std::array<std::string, 38> names;
    for (std::size_t i = 0; i < names.size(); ++i)
        names[i] = rules.text.text(rules.names.improvements[i + 1]);
    return load(reader, names, comparison);
}
} // namespace openciv::formats::classic
