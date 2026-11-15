export module openciv.state.identities;

import std;

export namespace openciv::state {
/// F02 §3: slots and lifetime tokens are separate identity domains.
enum class CivilizationSlot : std::uint8_t {};
/// A unit slot index, distinct from the lifetime occupying it.
enum class UnitSlot : std::uint16_t {};
/// A city slot index, distinct from the lifetime occupying it.
enum class CitySlot : std::uint16_t {};
/// A unit lifetime token; zero marks an unused slot.
enum class UnitToken : std::uint32_t {};
/// A city lifetime token; zero marks an unused slot.
enum class CityToken : std::uint32_t {};

/// Report whether a unit token denotes an occupied slot.
constexpr bool occupied(UnitToken token) noexcept {
    return token != UnitToken{0};
}
/// Report whether a city token denotes an occupied slot.
constexpr bool occupied(CityToken token) noexcept {
    return token != CityToken{0};
}

inline constexpr std::uint32_t unit_capacity = 2048;
inline constexpr std::size_t city_backing_capacity = 256;

} // namespace openciv::state
