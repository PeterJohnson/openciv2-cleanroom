#pragma once
#include <span>
#include <string>
int run_content_command(std::span<const std::string> arguments);
enum class ContentValidation { selection, rules, city_preferences, city_names };
int run_content_resolve(std::span<const std::string> arguments,
                        ContentValidation validation = ContentValidation::selection);
