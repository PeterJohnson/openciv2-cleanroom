export module openciv.state.events;

import std;

export namespace openciv::state {
/// F02/S02 semantic payload. File presence words and opaque bytes live in codecs.
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
    std::int32_t create_location_count = 0;
    bool create_veteran = false;
    std::int32_t auxiliary_scalar = 0;
    std::int32_t money_receiver_civ = 0, money_amount = 0;
    bool negotiation_flag = false;
    std::int32_t cd_track = 0, terrain_type = 0;
    std::array<std::int32_t, 8> terrain_rectangle{};
    std::int32_t destroy_civ = 0, give_tech = 0, give_tech_civ = 0;
    bool operator==(const EventValues&) const = default;
};
/// An absent string and a present empty string are distinct.
/// Copies of a backed reference share its logical identity, including edits.
class EventString {
public:
    EventString() = default;
    /// Create a present string with a fresh shared identity, stopping at the first NUL.
    explicit EventString(std::string_view text);
    /// Report whether this field has a string identity, even if the text is empty.
    bool present() const noexcept { return text_ != nullptr; }
    /// Report whether a shared text backing is available.
    bool backed() const noexcept { return text_ != nullptr; }
    /// Borrow mutable text; throw std::out_of_range for an absent string.
    std::string& value();
    /// Borrow immutable text; throw std::out_of_range for an absent string.
    const std::string& value() const;
    bool operator==(const EventString&) const = default;

private:
    std::shared_ptr<std::string> text_;
};
inline constexpr std::size_t event_string_count = 35, event_text_begin = 4, event_other_begin = 24;
/// Owned event operands and optional strings prepared for installation.
struct EventInput {
    EventValues values;
    std::array<std::optional<std::string>, event_string_count> strings;
};
using EventCatalog = std::vector<EventInput>;
/// Field order: unit/city/who/whom; 20 text slots; move owner/unit, aggression
/// whom/who, create owner/unit/home, ancillary civ/unit, money receiver, wave.
enum class EventId : std::uint64_t {};
/// A live event with stable logical identity and shared string references.
class Event {
public:
    Event() = default;
    Event(const Event&) = delete;
    Event& operator=(const Event&) = delete;
    /// Return this event identity within its owning collection lifetime.
    EventId identity() const noexcept { return identity_; }
    EventValues values;
    std::array<EventString, event_string_count> strings;
    /// Replace one string reference after validating the field index.
    void set_string(std::size_t field, EventString text);

private:
    friend class EventCollection;
    EventId identity_{0};
};
/// Own ordered events and allocate monotonically increasing event identities.
class EventCollection {
public:
    EventCollection() = default;
    EventCollection(const EventCollection&) = delete;
    EventCollection& operator=(const EventCollection&) = delete;
    /// Return the number of retained events.
    std::size_t size() const noexcept { return entries_.size(); }
    /// Report whether the collection is empty and has never issued an identity.
    bool pristine() const noexcept { return entries_.empty() && next_identity_ == 1; }
    /// Return shared ownership of the event at a checked index.
    std::shared_ptr<Event> at(std::size_t index) { return entries_.at(index); }
    /// Return shared immutable access to the event at a checked index.
    std::shared_ptr<const Event> at(std::size_t index) const { return entries_.at(index); }
    /// Test whether this exact event object belongs to the collection.
    bool contains(const std::shared_ptr<const Event>& event) const noexcept;
    /// Find a mutable event by identity, returning null when absent.
    std::shared_ptr<Event> find(EventId identity) noexcept;
    /// Find an immutable event by identity, returning null when absent.
    std::shared_ptr<const Event> find(EventId identity) const noexcept;
    /// Detach copied string identities while preserving event identities and required aliases.
    void compact();
    /// Append a fresh event and assign its next collection identity.
    std::shared_ptr<Event> append();
    /// Remove all events while preserving the identity counter and external loaded flag.
    void reset(); // Does not change the separate events-loaded indicator.
private:
    friend void install_events(const EventCatalog&, EventCollection&);
    std::vector<std::shared_ptr<Event>> entries_;
    std::uint64_t next_identity_ = 1;
};
/// Prepare a replacement before publishing. Source formats supply owned data;
/// fresh event identities remain monotonic within the destination collection.
void install_events(const EventCatalog& input, EventCollection& collection);
/// The event collection and its independently retained loaded indicator.
struct EventState {
    EventCollection collection;
    bool loaded = false;
};
/// S02 section 2: append payloads first, then duplicate strings from the two
/// heads. Nonempty destinations and text holes retain the specified aliasing.
/// Self-copy rejects before edits.
void copy_events(const EventCollection& source, EventCollection& destination);
/// Detach copied string identities once, preserving text-hole aliases and event IDs.
/// Replacement is prepared before publication. Allocation failure propagates.
void compact_events(EventCollection& collection);
/// Fresh string identity, through first NUL. Allocation failure propagates.
void create_event_string(EventString& target, std::string_view text);
} // namespace openciv::state

module :private;
namespace openciv::state {
namespace {
std::string_view terminated(std::string_view text) {
    return text.substr(0, text.find('\0'));
}
void copy(const EventCollection& source, EventCollection& destination) {
    if (&source == &destination)
        throw std::invalid_argument("Event self-copy has no admitted finite traversal");
    for (std::size_t i = 0; i < source.size(); ++i) {
        const auto from = source.at(i);
        const auto to = destination.append();
        to->values = from->values;
        to->strings = from->strings;
    }
    for (std::size_t i = 0; i < source.size(); ++i) {
        const auto from = source.at(i);
        const auto to = destination.at(i);
        for (std::size_t field = 0; field < event_string_count; ++field) {
            if (field >= event_text_begin && field < event_other_begin &&
                !from->strings[field].present()) {
                field = event_other_begin - 1;
                continue;
            }
            if (from->strings[field].present())
                to->set_string(field, EventString(from->strings[field].value()));
        }
    }
}
} // namespace
EventString::EventString(std::string_view text)
    : text_(std::make_shared<std::string>(terminated(text))) {}
std::string& EventString::value() {
    if (!text_)
        throw std::out_of_range("Event string has no established text backing");
    return *text_;
}
const std::string& EventString::value() const {
    if (!text_)
        throw std::out_of_range("Event string has no established text backing");
    return *text_;
}
void Event::set_string(std::size_t field, EventString text) {
    auto& target = strings.at(field);
    target = std::move(text);
}
bool EventCollection::contains(const std::shared_ptr<const Event>& event) const noexcept {
    return std::find(entries_.begin(), entries_.end(), event) != entries_.end();
}
std::shared_ptr<const Event> EventCollection::find(EventId identity) const noexcept {
    for (const auto& event : entries_)
        if (event->identity() == identity)
            return event;
    return {};
}
std::shared_ptr<Event> EventCollection::append() {
    if (next_identity_ == std::numeric_limits<std::uint64_t>::max())
        throw std::overflow_error("Event logical identities exhausted");
    auto event = std::make_shared<Event>();
    event->identity_ = static_cast<EventId>(next_identity_);
    entries_.push_back(event);
    ++next_identity_;
    return event;
}
void EventCollection::reset() {
    entries_.clear();
}
void create_event_string(EventString& target, std::string_view text) {
    target = EventString(text);
}
void copy_events(const EventCollection& source, EventCollection& destination) {
    copy(source, destination);
}
std::shared_ptr<Event> EventCollection::find(EventId identity) noexcept {
    for (const auto& event : entries_)
        if (event->identity() == identity)
            return event;
    return {};
}
void EventCollection::compact() {
    EventCollection replacement;
    copy(*this, replacement);
    for (std::size_t i = 0; i < entries_.size(); ++i)
        entries_[i]->strings = std::move(replacement.entries_[i]->strings);
}
void install_events(const EventCatalog& input, EventCollection& collection) {
    EventCollection replacement;
    replacement.next_identity_ = collection.next_identity_;
    for (const auto& source : input) {
        const auto event = replacement.append();
        event->values = source.values;
        for (std::size_t field = 0; field < source.strings.size(); ++field)
            if (source.strings[field])
                event->strings[field] = EventString(*source.strings[field]);
    }
    collection.entries_ = std::move(replacement.entries_);
    collection.next_identity_ = replacement.next_identity_;
}
void compact_events(EventCollection& collection) {
    collection.compact();
}
} // namespace openciv::state
