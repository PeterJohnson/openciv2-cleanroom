import std;
import openciv.state.events;

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
