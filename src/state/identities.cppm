export module openciv.state.identities;

import std;

export extern "C++" {
    namespace openciv::state {
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

    // F02 §§2–3: owned by a process context, shared across its successive games.
    // This is only the token portion of that context, not the complete process state.
    struct ProcessTokens {
        std::uint32_t next_unit = 1;
        std::uint32_t next_city = 1;

        UnitToken assign_unit() noexcept;
        CityToken assign_city() noexcept;
        bool operator==(const ProcessTokens&) const = default;
    };

    inline constexpr std::uint32_t unit_capacity = 2048;

    // F02 §3 slot-selection prefix only: no record initialization/token assignment,
    // count update or limit notice. The caller supplies exactly the [0, unit_end)
    // token view at entry and owns backing for all 2048 slots. No callback may run
    // during reservation. On extension the entry view stays unchanged; rebuild it
    // from the owning pool before a subsequent call. unit_end must not alias tokens.
    std::optional<UnitSlot> reserve_unit_slot(std::span<const UnitToken> scanned_tokens,
                                              std::uint32_t& unit_end, CivilizationSlot owner,
                                              std::int16_t military_count,
                                              std::uint8_t effective_humans);
    } // namespace openciv::state
}
