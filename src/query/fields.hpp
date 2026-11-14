#pragma once

#include "query/stored.hpp"
#include "formats/record_fields.hpp"
#include <type_traits>

namespace openciv::query::detail {
template <class T> std::string integer_type() {
    return std::string{std::is_signed_v<T> ? "i" : "u"} + std::to_string(sizeof(T) * 8);
}
class Fields {
public:
    explicit Fields(std::vector<Field>& target, std::string prefix = {})
        : target_(target), prefix_(std::move(prefix)) {}
    template <class T> void operator()(std::string_view name, const T& value) {
        const auto path = prefix_ + std::string{name};
        if constexpr (std::is_enum_v<T>) {
            (*this)(name, static_cast<std::underlying_type_t<T>>(value));
        } else if constexpr (std::integral<T>) {
            if constexpr (std::is_signed_v<T>)
                target_.push_back({path, integer_type<T>(), static_cast<std::int64_t>(value)});
            else
                target_.push_back({path, integer_type<T>(), static_cast<std::uint64_t>(value)});
        } else if constexpr (requires { value.has_value(); }) {
            if (value) {
                (*this)(name, *value);
            } else {
                using Item = typename T::value_type;
                if constexpr (std::is_enum_v<Item>)
                    target_.push_back({path, integer_type<std::underlying_type_t<Item>>(), {}});
                else
                    target_.push_back({path, integer_type<Item>(), {}});
            }
        } else if constexpr (requires { value.bytes; }) {
            target_.push_back(
                {path, "classic-text", formats::Bytes{value.bytes.begin(), value.bytes.end()}});
        } else if constexpr (requires { value.value; }) {
            (*this)(name, value.value);
        } else if constexpr (requires { formats::visit_fields(*this, value); }) {
            Fields nested{target_, path + "."};
            formats::visit_fields(nested, value);
        } else {
            for (std::size_t i = 0; i < value.size(); ++i)
                (*this)(std::string{name} + "[" + std::to_string(i) + "]", value[i]);
        }
    }
    void bytes(std::string_view name, std::span<const formats::Byte> value) {
        target_.push_back(
            {prefix_ + std::string{name}, "bytes", formats::Bytes{value.begin(), value.end()}});
    }
    void text(std::string_view name, const std::optional<formats::Bytes>& value) {
        target_.push_back({prefix_ + std::string{name}, "classic-text",
                           value ? Value{*value} : Value{std::monostate{}}});
    }

private:
    std::vector<Field>& target_;
    std::string prefix_;
};
} // namespace openciv::query::detail
