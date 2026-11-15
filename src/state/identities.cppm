export module openciv.state.identities;

import std;

export namespace openciv::state {
// F02 §3: slots and lifetime tokens are separate identity domains.
enum class CivilizationSlot : std::uint8_t {};
enum class UnitSlot : std::uint16_t {};
enum class CitySlot : std::uint16_t {};
enum class UnitToken : std::uint32_t {};
enum class CityToken : std::uint32_t {};

constexpr bool occupied(UnitToken token) noexcept {
    return token != UnitToken{0};
}
constexpr bool occupied(CityToken token) noexcept {
    return token != CityToken{0};
}

inline constexpr std::uint32_t unit_capacity = 2048;
inline constexpr std::size_t city_backing_capacity = 256;

} // namespace openciv::state
