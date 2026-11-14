#include "state/events.hpp"
#include <algorithm>
#include <limits>
#include <stdexcept>

namespace openciv::state {
namespace {
std::string_view terminated(std::string_view text) {
    return text.substr(0, text.find('\0'));
}
bool allowed(const EventServices& services, EventAllocationKind kind, std::size_t bytes) {
    return !services.allocate || services.allocate({kind, bytes});
}
void check_member(const EventCollection& collection, const std::shared_ptr<Event>& event) {
    if (!collection.contains(event))
        throw std::out_of_range("Event callback invalidated the selected event");
}
std::size_t minimum(std::size_t field) {
    if (field == 0 || field == 25 || field == 29 || field == 32)
        return 15;
    return (field >= 4 && field < 24) || field == 34 ? 1 : 24;
}
bool duplicate(EventString& target, const EventString& source, const EventServices& services,
               std::size_t capacity) {
    capacity = std::max(capacity, terminated(source.value()).size() + 1);
    if (allowed(services, EventAllocationKind::string, capacity)) {
        // The reference argument is captured; its contents are live after the
        // resource call. A grown string cannot overrun the admitted capacity.
        const auto text = terminated(source.value());
        if (text.size() + 1 > capacity)
            throw std::out_of_range("Event copy source grew beyond supplied string backing");
        try {
            target = EventString(text);
            return true;
        } catch (const std::bad_alloc&) {
        }
    }
    if (services.report)
        services.report(-9, 3);
    return false;
}
bool copy(const EventCollection& source, EventCollection& destination,
          const EventServices& services, bool preserve_identity) {
    if (&source == &destination)
        throw std::invalid_argument("Event self-copy has no admitted finite traversal");
    for (std::size_t i = 0; i < source.size(); ++i) {
        const auto from = source.at(i);
        auto to = destination.append(services);
        if (!to)
            return false;
        check_member(source, from);
        to->values = from->values;
        to->strings = from->strings;
        if (preserve_identity)
            to->identity = from->identity;
    }
    for (std::size_t i = 0; i < source.size(); ++i) {
        const auto from = source.at(i), to = destination.at(i);
        for (std::size_t field = 0; field < event_string_count; ++field) {
            if (field >= event_text_begin && field < event_other_begin &&
                !from->strings[field].present()) {
                field = event_other_begin - 1;
                continue;
            }
            if (!from->strings[field].present())
                continue;
            // Keep the supplied identity alive through resource callbacks.
            const auto reference = from->strings[field];
            EventString replacement;
            if (!duplicate(replacement, reference, services, minimum(field)))
                return false;
            check_member(source, from);
            check_member(destination, to);
            to->set_string(field, std::move(replacement));
        }
    }
    return true;
}
} // namespace
EventString::EventString(std::string_view text)
    : text_(std::make_shared<std::string>(terminated(text))) {}
EventString EventString::unresolved() {
    EventString result;
    result.unresolved_ = true;
    return result;
}
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
std::array<std::uint32_t*, event_string_count> event_markers(EventValues& v) {
    std::array<std::uint32_t*, event_string_count> result{};
    result[0] = &v.unit_name;
    result[1] = &v.city_name;
    result[2] = &v.who_name;
    result[3] = &v.whom_name;
    for (std::size_t i = 0; i < 20; ++i)
        result[4 + i] = &v.text[i];
    const std::array other{&v.move_owner_name,     &v.move_unit_name,     &v.aggression_whom_name,
                           &v.aggression_who_name, &v.create_owner_name,  &v.create_unit_name,
                           &v.create_homecity,     &v.auxiliary_civ_name, &v.auxiliary_unit_name,
                           &v.money_receiver_name, &v.wave_file};
    std::copy(other.begin(), other.end(), result.begin() + event_other_begin);
    return result;
}
void Event::set_string(std::size_t field, EventString text) {
    auto& target = strings.at(field);
    *event_markers(values)[field] = text.present() ? 1 : 0;
    target = std::move(text);
}
bool EventCollection::contains(const std::shared_ptr<Event>& event) const noexcept {
    return std::find(entries_.begin(), entries_.end(), event) != entries_.end();
}
std::shared_ptr<Event> EventCollection::find(EventId identity) const noexcept {
    for (const auto& event : entries_)
        if (event->identity == identity)
            return event;
    return {};
}
std::shared_ptr<Event> EventCollection::append(const EventServices& services) {
    const auto before = revision_;
    if (!allowed(services, EventAllocationKind::event, 0))
        return {};
    if (before != revision_)
        throw std::out_of_range("Event allocation callback changed the captured list tail");
    if (next_identity_ == std::numeric_limits<std::uint64_t>::max())
        throw std::overflow_error("Event logical identities exhausted");
    try {
        auto event = std::make_shared<Event>();
        event->identity = static_cast<EventId>(next_identity_);
        entries_.push_back(event);
        ++next_identity_;
        ++revision_;
        return event;
    } catch (const std::bad_alloc&) {
        return {};
    }
}
void EventCollection::reset() {
    entries_.clear();
    ++revision_;
}
bool create_event_string(EventString& target, std::string_view text, const EventServices& services,
                         std::size_t minimum) {
    const std::string input(terminated(text));
    const auto length = std::max(minimum, input.size() + 1);
    if (allowed(services, EventAllocationKind::string, length)) {
        try {
            target = EventString(input);
            return true;
        } catch (const std::bad_alloc&) {
        }
    }
    if (services.report)
        services.report(-9, 3);
    return false;
}
bool copy_events(const EventCollection& source, EventCollection& destination,
                 const EventServices& services) {
    return copy(source, destination, services, false);
}
bool compact_events(EventCollection& collection, const EventServices& services) {
    EventCollection temporary;
    if (!copy(collection, temporary, services, true))
        return false;
    collection.reset();
    return copy(temporary, collection, services, true);
}
} // namespace openciv::state
