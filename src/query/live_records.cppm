export module openciv.query.live_records;

import std;
export import openciv.base.session_ids;
export import openciv.state.identities;
export import openciv.state.unit_types;

export namespace openciv::query {
/// Describe how a player-visible record was obtained.
enum class LiveProvenance { owned_current };
/// Identify the runtime, session revision, and observer behind a live query.
struct LiveObservation {
    base::RuntimeId runtime{0};
    base::SessionId session{0};
    std::uint64_t revision = 0;
    state::CivilizationSlot observer{0};
    LiveProvenance provenance = LiveProvenance::owned_current;
    bool operator==(const LiveObservation&) const = default;
};
/// Owned snapshot of a player-visible unit and its current identity token.
struct OwnUnit {
    state::UnitSlot slot{0};
    state::UnitToken token{0};
    std::int16_t x = 0, y = 0;
    state::UnitType type{0};
    std::uint8_t moves_used = 0, damage = 0;
    state::UnitOrder orders = state::UnitOrder::none;
    std::optional<state::CitySlot> home;
    bool operator==(const OwnUnit&) const = default;
};
/// Owned snapshot of a player-visible city without retained name padding.
struct OwnCity {
    state::CitySlot slot{0};
    state::CityToken token{0};
    std::int16_t x = 0, y = 0;
    std::vector<std::uint8_t> name; // Classic bytes before NUL; never retained tail bytes.
    std::int8_t size = 0, production = 0;
    std::int16_t food_box = 0, shield_box = 0;
    bool operator==(const OwnCity&) const = default;
};
/// Owned query records paired with the observation that produced them.
template <class Record> struct LiveRecords {
    LiveObservation observation;
    std::vector<Record> records;
    bool operator==(const LiveRecords&) const = default;
};
} // namespace openciv::query
