#pragma once

#include <array>
#include <cstdint>
#include <functional>
#include <memory>
#include <optional>
#include <span>
#include <string>
#include <string_view>
#include <vector>

namespace openciv::state {
// F02/S02 semantic payload. File presence words and opaque bytes live in codecs.
struct EventValues {
    std::uint32_t triggers = 0, actions = 0;
    std::int32_t unit_type = 0;
    std::int32_t who_civ = 0, talker_type = 0;
    std::int32_t whom_civ = 0, listener_type = 0, turn = 0, denominator = 0, technology = 0;
    std::int32_t move_owner_civ = 0;
    std::int32_t move_unit_type = 0, move_count = 0;
    std::array<std::int32_t, 8> move_rectangle{};
    std::int32_t move_x = 0, move_y = 0;
    std::int32_t aggression_whom_civ = 0;
    std::int32_t aggression_who_civ = 0;
    std::int32_t create_owner_civ = 0;
    std::int32_t create_unit_type = 0;
    std::array<std::int32_t, 20> create_locations{};
    std::int32_t create_location_count = 0, create_veteran = 0;
    std::int32_t auxiliary_scalar = 0;
    std::int32_t money_receiver_civ = 0, money_amount = 0, negotiation_flag = 0;
    std::int32_t cd_track = 0, terrain_type = 0;
    std::array<std::int32_t, 8> terrain_rectangle{};
    std::int32_t destroy_civ = 0, give_tech = 0, give_tech_civ = 0;
    bool operator==(const EventValues&) const = default;
};
// An absent string and a present empty string are distinct.
// Copies of a backed reference share its logical identity, including edits.
class EventString {
public:
    EventString() = default;
    explicit EventString(std::string_view text);
    bool present() const noexcept { return text_ != nullptr; }
    bool backed() const noexcept { return text_ != nullptr; }
    std::string& value();
    const std::string& value() const;
    bool operator==(const EventString&) const = default;

private:
    std::shared_ptr<std::string> text_;
};
inline constexpr std::size_t event_string_count = 35, event_text_begin = 4, event_other_begin = 24;
struct EventInput {
    EventValues values;
    std::array<std::optional<std::string>, event_string_count> strings;
};
using EventCatalog = std::vector<EventInput>;
// Field order: unit/city/who/whom; 20 text slots; move owner/unit, aggression
// whom/who, create owner/unit/home, ancillary civ/unit, money receiver, wave.
enum class EventId : std::uint64_t {};
class Event {
public:
    Event() = default;
    Event(const Event&) = delete;
    Event& operator=(const Event&) = delete;
    EventId identity() const noexcept { return identity_; }
    EventValues values;
    std::array<EventString, event_string_count> strings;
    void set_string(std::size_t field, EventString text);

private:
    friend class EventCollection;
    EventId identity_{0};
};
enum class EventAllocationKind { event, string };
struct EventAllocation {
    EventAllocationKind kind;
    std::size_t bytes; // String backing including NUL; zero for a logical event.
};
struct EventServices {
    // Optional deterministic resource outcomes. Real allocation failure also
    // fails safely. Resource callbacks must not mutate the source collection or
    // act as gameplay reentry points; original allocation traces are not an API.
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
    std::shared_ptr<Event> at(std::size_t index) { return entries_.at(index); }
    std::shared_ptr<const Event> at(std::size_t index) const { return entries_.at(index); }
    bool contains(const std::shared_ptr<const Event>& event) const noexcept;
    std::shared_ptr<Event> find(EventId identity) noexcept;
    std::shared_ptr<const Event> find(EventId identity) const noexcept;
    bool compact(const EventServices& services);
    std::shared_ptr<Event> append(const EventServices& services = {});
    void reset(); // Does not change the separate events-loaded indicator.
private:
    friend void install_events(const EventCatalog&, EventCollection&, const EventServices&);
    std::vector<std::shared_ptr<Event>> entries_;
    std::uint64_t next_identity_ = 1, revision_ = 0;
};
// Prepare a replacement before publishing. Source formats supply owned data;
// fresh event identities remain monotonic within the destination collection.
void install_events(const EventCatalog& input, EventCollection& collection,
                    const EventServices& services = {});
struct EventState {
    EventCollection collection;
    bool loaded = false;
};
// S02 section 2: append payloads first, then duplicate strings from the two
// heads. Nonempty destinations and text holes retain the specified aliasing.
// Self-copy rejects before edits; a callback-invalidated node stops safely.
bool copy_events(const EventCollection& source, EventCollection& destination,
                 const EventServices& services = {});
// Detach copied string identities once, preserving text-hole aliases and event IDs.
// Replacement is prepared before publication; failure retains the collection.
bool compact_events(EventCollection& collection, const EventServices& services = {});
// Fresh string identity, through first NUL. Failure reports F06 -9/category 3
// and leaves the target alone. minimum is capacity, never a truncation length.
bool create_event_string(EventString& target, std::string_view text,
                         const EventServices& services = {}, std::size_t minimum = 1);
} // namespace openciv::state
