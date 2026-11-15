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
void check_member(const EventCollection& collection, const std::shared_ptr<const Event>& event) {
    if (!collection.contains(event))
        throw std::out_of_range("Event callback invalidated the selected event");
}
bool duplicate(EventString& target, const EventString& source, const EventServices& services) {
    return create_event_string(target, source.value(), services);
}
bool copy(const EventCollection& source, EventCollection& destination,
          const EventServices& services) {
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
            if (!from->strings[field].present())
                continue;
            // Keep the supplied identity alive through resource callbacks.
            const auto reference = from->strings[field];
            EventString replacement;
            if (!duplicate(replacement, reference, services))
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
        event->identity_ = static_cast<EventId>(next_identity_);
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
    return copy(source, destination, services);
}
std::shared_ptr<Event> EventCollection::find(EventId identity) noexcept {
    for (const auto& event : entries_)
        if (event->identity() == identity)
            return event;
    return {};
}
bool EventCollection::compact(const EventServices& services) {
    const auto revision = revision_;
    EventCollection replacement;
    auto admission = services;
    admission.allocate = [&](EventAllocation request) {
        const auto result = allowed(services, request.kind, request.bytes);
        if (revision_ != revision)
            throw std::logic_error("Event resource admission changed the source collection");
        return result;
    };
    if (!copy(*this, replacement, admission))
        return false;
    for (std::size_t i = 0; i < entries_.size(); ++i)
        entries_[i]->strings = std::move(replacement.entries_[i]->strings);
    ++revision_;
    return true;
}
bool compact_events(EventCollection& collection, const EventServices& services) {
    return collection.compact(services);
}
} // namespace openciv::state
