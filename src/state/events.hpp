#pragma once

#include <array>
#include <cstdint>
#include <functional>
#include <memory>
#include <span>
#include <string>
#include <string_view>
#include <vector>

namespace openciv::state {
// F02/S02 event payload and F06 retained record values. Presence words are
// uninterpreted serialized bits, never live references. Event::strings owns
// logical identities; file codecs alone use the retained presence values.
struct EventValues {
    std::uint32_t triggers = 0, actions = 0, unit_name = 0;
    std::int32_t unit_type = 0;
    std::uint32_t city_name = 0, who_name = 0;
    std::int32_t who_civ = 0, talker_type = 0;
    std::uint32_t whom_name = 0;
    std::int32_t whom_civ = 0, listener_type = 0, turn = 0, denominator = 0, technology = 0;
    std::array<std::uint32_t, 20> text{};
    std::uint32_t move_owner_name = 0;
    std::int32_t move_owner_civ = 0;
    std::uint32_t move_unit_name = 0;
    std::int32_t move_unit_type = 0, move_count = 0;
    std::array<std::int32_t, 8> move_rectangle{};
    std::int32_t move_x = 0, move_y = 0;
    std::uint32_t aggression_whom_name = 0;
    std::int32_t aggression_whom_civ = 0;
    std::uint32_t aggression_who_name = 0;
    std::int32_t aggression_who_civ = 0;
    std::uint32_t create_owner_name = 0;
    std::int32_t create_owner_civ = 0;
    std::uint32_t create_unit_name = 0;
    std::int32_t create_unit_type = 0;
    std::array<std::int32_t, 20> create_locations{};
    std::int32_t create_location_count = 0, create_veteran = 0;
    std::uint32_t create_homecity = 0, auxiliary_civ_name = 0;
    std::int32_t auxiliary_scalar = 0;
    std::uint32_t auxiliary_unit_name = 0;
    std::array<std::uint8_t, 40> retained{};
    std::uint32_t money_receiver_name = 0;
    std::int32_t money_receiver_civ = 0, money_amount = 0, negotiation_flag = 0;
    std::uint32_t wave_file = 0;
    std::int32_t cd_track = 0, terrain_type = 0;
    std::array<std::int32_t, 8> terrain_rectangle{};
    std::int32_t destroy_civ = 0, give_tech = 0, give_tech_civ = 0;
    bool operator==(const EventValues&) const = default;
};
// Absent, unresolved saved presence, and backed text are different states.
// Copies of a backed reference share its logical identity, including edits.
class EventString {
public:
    EventString() = default;
    explicit EventString(std::string_view text);
    static EventString unresolved();
    bool present() const noexcept { return unresolved_ || text_ != nullptr; }
    bool backed() const noexcept { return text_ != nullptr; }
    std::string& value();
    const std::string& value() const;
    bool operator==(const EventString&) const = default;

private:
    bool unresolved_ = false;
    std::shared_ptr<std::string> text_;
};
inline constexpr std::size_t event_string_count = 35, event_text_begin = 4, event_other_begin = 24;
// Field order: unit/city/who/whom; 20 text slots; move owner/unit, aggression
// whom/who, create owner/unit/home, ancillary civ/unit, money receiver, wave.
std::array<std::uint32_t*, event_string_count> event_markers(EventValues& values);
enum class EventId : std::uint64_t {};
struct Event {
    EventId identity{0};
    EventValues values;
    std::array<EventString, event_string_count> strings;
    void set_string(std::size_t field, EventString text);
};
enum class EventAllocationKind { event, string };
struct EventAllocation {
    EventAllocationKind kind;
    std::size_t bytes; // String backing including NUL; zero for a logical event.
};
struct EventServices {
    // Optional deterministic resource outcomes. Real allocation failure also
    // fails safely. These are not original address/pool-block trace emulators.
    std::function<bool(EventAllocation)> allocate;
    std::function<void(std::int32_t code, std::int32_t category)> report;
};
class EventCollection {
public:
    EventCollection() = default;
    EventCollection(const EventCollection&) = delete;
    EventCollection& operator=(const EventCollection&) = delete;
    std::size_t size() const noexcept { return entries_.size(); }
    bool pristine() const noexcept { return entries_.empty() && next_identity_ == 1; }
    std::shared_ptr<Event> at(std::size_t index) const { return entries_.at(index); }
    bool contains(const std::shared_ptr<Event>& event) const noexcept;
    std::shared_ptr<Event> find(EventId identity) const noexcept;
    std::shared_ptr<Event> append(const EventServices& services = {});
    void reset(); // Does not change the separate events-loaded indicator.
private:
    std::vector<std::shared_ptr<Event>> entries_;
    std::uint64_t next_identity_ = 1, revision_ = 0;
};
struct EventState {
    EventCollection collection;
    bool loaded = false;
};
// S02 section 2: append payloads first, then duplicate strings from the two
// heads. Nonempty destinations and text holes retain the specified aliasing.
// Self-copy rejects before edits; a callback-invalidated node stops safely.
bool copy_events(const EventCollection& source, EventCollection& destination,
                 const EventServices& services = {});
// Two successive copies preserve payload/order and event identity. Failure in
// the first pass retains the source; the second retains its published prefix.
bool compact_events(EventCollection& collection, const EventServices& services = {});
// Fresh string identity, through first NUL. Failure reports F06 -9/category 3
// and leaves the target alone. minimum is capacity, never a truncation length.
bool create_event_string(EventString& target, std::string_view text,
                         const EventServices& services = {}, std::size_t minimum = 1);
} // namespace openciv::state
