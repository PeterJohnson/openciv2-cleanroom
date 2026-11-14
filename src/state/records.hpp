#pragma once

#include "state/identities.hpp"
#include <algorithm>
#include <array>
#include <functional>
#include <memory>
#include <stdexcept>

namespace openciv::state {
// F02 §§3,5–6: logical runtime values, never a packed file/wire representation.
// Narrow numeric members preserve the specified cyclic publication domains.
enum class UnitType : std::uint8_t {};
enum class UnitOrder : std::int8_t {
    none = -1,
    fortifying = 1,
    fortified = 2,
    sleeping = 3,
    fortress = 4,
    road = 5,
    irrigate = 6,
    mine = 7,
    transform = 8,
    clean = 9,
    airbase = 10,
    go_to = 11,
    ai_idle = 16,
    ai_go_to = 27
};
// Every negative link is terminal. Retain its actual signed value for round trips;
// interpreting a link does not validate or repair its target record.
template <class Slot> struct SlotLink {
    std::int16_t value = -1;
    std::optional<Slot> slot() const noexcept {
        return value < 0 ? std::nullopt : std::optional{static_cast<Slot>(value)};
    }
    bool operator==(const SlotLink&) const = default;
};
// Classic byte strings are not UTF-8. Keep the terminator and all retained tail
// bytes independently of any presentation conversion performed by the client.
template <std::size_t N> struct FixedText {
    std::array<std::uint8_t, N> bytes{};
    std::span<const std::uint8_t> text() const noexcept {
        return {bytes.begin(), std::find(bytes.begin(), bytes.end(), std::uint8_t{0})};
    }
    bool terminated() const noexcept { return text().size() < N; }
    bool operator==(const FixedText&) const = default;
};

struct Unit {
    UnitToken token{0};
    std::int16_t x = 0, y = 0;
    std::uint16_t flags = 0;
    UnitType type{0};
    CivilizationSlot owner{0};
    std::uint8_t moves_used = 0, visibility = 0, damage = 0, last_direction = 0;
    std::uint8_t ai_task = 0, counter = 0, auxiliary_timer = 0;
    UnitOrder orders = UnitOrder::none;
    std::optional<CitySlot> home;
    std::int16_t destination_x = 0, destination_y = 0;
    SlotLink<UnitSlot> previous, next;
    std::uint8_t retained_byte = 0;
    std::uint16_t retained_word = 0;
    bool operator==(const Unit&) const = default;
};
struct City {
    CityToken token{0};
    std::int16_t x = 0, y = 0;
    std::uint32_t flags = 0;
    CivilizationSlot owner{0}, founder{0};
    std::int8_t size = 0;
    std::uint8_t turns_captured = 0, known_to = 0;
    std::array<std::uint8_t, 8> remembered_size{};
    std::uint32_t specialist_kinds = 0;
    std::int16_t food_box = 0, shield_box = 0, base_trade = 0;
    FixedText<16> name;
    std::uint32_t worked_tiles = 0; // Includes specialist count and retained bits.
    std::array<std::uint8_t, 5> improvements{};
    std::int8_t production = 0;
    std::uint8_t route_count = 0;
    std::array<std::int8_t, 3> supplies{}, demands{}, route_goods{};
    std::array<SlotLink<CitySlot>, 3> route_partners{};
    std::int16_t science = 0, tax = 0, trade = 0;
    std::uint8_t total_food = 0, total_shields = 0, happy = 0, unhappy = 0;
    std::uint8_t retained_byte = 0;
    bool operator==(const City&) const = default;
};

// Heap-backed fixed-capacity storage keeps slot addresses stable without large
// native/WASM stack allocations. The scan bound is independent of live count.
template <class Record, class Slot, std::size_t Capacity> class SlotPool {
public:
    SlotPool() : records_(std::make_unique<std::array<Record, Capacity>>()) {}
    std::size_t end() const noexcept { return end_; }
    void set_end(std::size_t end) {
        if (end > Capacity)
            throw std::out_of_range("Pool scan bound exceeds supplied backing");
        end_ = end;
    }
    std::span<Record> storage() noexcept { return *records_; }
    std::span<const Record> storage() const noexcept { return *records_; }
    std::span<Record> scanned() noexcept { return storage().first(end_); }
    std::span<const Record> scanned() const noexcept { return storage().first(end_); }
    Record& at(Slot slot) { return records_->at(static_cast<std::size_t>(slot)); }
    const Record& at(Slot slot) const { return records_->at(static_cast<std::size_t>(slot)); }
    bool live(Slot slot) const noexcept {
        const auto index = static_cast<std::size_t>(slot);
        return index < end_ && occupied((*records_)[index].token);
    }
    // F02 §3 deletion finalization only, after the owning lifecycle's cleanup.
    // Negative/already-free inputs are no-ops; unsupported positive slots reject.
    bool finish_deletion(std::int32_t index) {
        if (index < 0)
            return false;
        if (static_cast<std::size_t>(index) >= end_)
            throw std::out_of_range("Deletion outside current pool scan");
        auto& record = (*records_)[static_cast<std::size_t>(index)];
        if (!occupied(record.token))
            return false;
        record.token = decltype(record.token){0};
        if (static_cast<std::size_t>(index) == end_ - 1)
            --end_;
        return true;
    }

private:
    std::unique_ptr<std::array<Record, Capacity>> records_;
    std::size_t end_ = 0;
};
inline constexpr std::size_t city_creation_limit = 255;
inline constexpr std::size_t city_backing_capacity = 256;
using UnitPool = SlotPool<Unit, UnitSlot, unit_capacity>;
using CityPool = SlotPool<City, CitySlot, city_backing_capacity>;
struct EntityPools {
    UnitPool units;
    CityPool cities;
};
// U01 sections 2–4, 9: retained controls, not stored unit-record fields. Activity
// is a boolean assignment at operation boundaries, never a nesting counter.
struct UnitStackState {
    std::int32_t suppression = 0;
    bool animation_active = false;
    bool detach_active = false;
    bool attach_active = false;
    bool relocate_active = false;
    bool bottom_active = false;
    std::int32_t detach_reply = 0, attach_reply = 0, relocate_reply = 0, bottom_reply = 0;
};
// U01 sections 6, 9: deletion has its own activity and reply fields. The reply
// becomes Pending (-2) at a client request, not at construction or every call.
struct UnitDeletionState {
    bool active = false;
    std::int32_t reply = 0;
};
std::optional<UnitSlot> reserve_unit_slot(UnitPool& pool, CivilizationSlot owner,
                                          std::int16_t military_count,
                                          std::uint8_t effective_humans);
// F05 §2 entity portion of the outer new-session boundary. Does not reset tokens,
// map, rules, preferences, name cursors or the separate inner new-game boundary.
void invalidate_entities_for_new_session(EntityPools& pools);
// F02 §2 / F05 §5 entity portion of inner reset: bounds and selected slot only.
void reset_entity_scans(EntityPools& pools, std::optional<UnitSlot>& selected);
// F02 §3 / F06 §7. Caller invokes only after BOTH record input groups succeed.
void renew_loaded_lifetimes(EntityPools& pools, ProcessTokens& tokens, bool legacy_without_ids);

// F02 §9 shared retained mood workspace, separate from city-record snapshots.
struct CityMoodWorkspace {
    std::optional<CitySlot> city;
    std::int32_t happy = 0, unhappy = 0;
    bool operator==(const CityMoodWorkspace&) const = default;
};
std::int32_t city_calculation_entry(std::optional<CitySlot> city, bool full,
                                    CityMoodWorkspace& workspace,
                                    const std::function<std::int32_t()>& pipeline = {});
} // namespace openciv::state
