export module openciv.tools.content_command;

import std;

export extern "C++" {
    int run_content_command(std::span<const std::string> arguments);
    int run_content_convert(std::span<const std::string> arguments, bool city_names);
    int run_rules_convert(std::span<const std::string> arguments);
    enum class ContentValidation { selection, rules, city_preferences, city_names };
    int run_content_resolve(std::span<const std::string> arguments,
                            ContentValidation validation = ContentValidation::selection);
}
