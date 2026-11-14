#include "session/commands.hpp"
#include <set>

namespace openciv::session {
namespace {
bool name_valid(std::string_view name) {
    if (name.empty())
        return false;
    return std::all_of(name.begin(), name.end(), [](char c) {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
               c == '.' || c == '_' || c == '-';
    });
}
void validate_schema(std::span<const ValueSchema> schema) {
    std::set<std::string> names;
    for (const auto& field : schema) {
        if (!name_valid(field.name) || !names.insert(field.name).second ||
            field.kind < ValueKind::integer || field.kind > ValueKind::city)
            throw std::invalid_argument("Invalid command value schema");
    }
}
} // namespace
void validate_descriptor(const CommandDescriptor& descriptor) {
    if (descriptor.version != 1 || !name_valid(descriptor.name) || descriptor.help.empty() ||
        (descriptor.access != AccessClass::player && descriptor.access != AccessClass::developer))
        throw std::invalid_argument("Invalid command descriptor");
    validate_schema(descriptor.arguments);
    validate_schema(descriptor.result);
}
std::optional<std::string> validate_values(std::span<const ValueSchema> schema,
                                           std::span<const NamedValue> values) {
    std::set<std::string> found;
    for (const auto& value : values) {
        if (!found.insert(value.name).second)
            return "duplicate_value:" + value.name;
        const auto field = std::find_if(schema.begin(), schema.end(),
                                        [&](const auto& s) { return s.name == value.name; });
        if (field == schema.end())
            return "unknown_value:" + value.name;
        if (value.value.index() != static_cast<std::size_t>(field->kind))
            return "wrong_type:" + value.name;
        if (const auto* civ = std::get_if<state::CivilizationSlot>(&value.value);
            civ && static_cast<unsigned>(*civ) >= 8)
            return "invalid_civilization:" + value.name;
        if (const auto* unit = std::get_if<UnitTarget>(&value.value);
            unit && static_cast<unsigned>(unit->slot) >= state::unit_capacity)
            return "invalid_unit_slot:" + value.name;
        if (const auto* city = std::get_if<CityTarget>(&value.value);
            city && static_cast<unsigned>(city->slot) >= state::city_backing_capacity)
            return "invalid_city_slot:" + value.name;
    }
    for (const auto& field : schema)
        if (field.required && !found.contains(field.name))
            return "missing_value:" + field.name;
    return {};
}
const CommandValue& command_value(std::span<const NamedValue> values, std::string_view name) {
    const auto found =
        std::find_if(values.begin(), values.end(), [&](const auto& v) { return v.name == name; });
    if (found == values.end())
        throw std::out_of_range("Missing command value");
    return found->value;
}
bool terminal(CommandStatus status) noexcept {
    return status == CommandStatus::rejected || status == CommandStatus::completed ||
           status == CommandStatus::cancelled || status == CommandStatus::failed;
}
} // namespace openciv::session
