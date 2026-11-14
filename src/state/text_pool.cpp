#include "text_pool.hpp"
#include <algorithm>
#include <limits>
#include <stdexcept>

namespace openciv::state {
void TextPool::initialize(const TextAllocation& allocate) {
    entries_.clear();
    last_.reset();
    store("<nil>", 0, allocate);
}
TextId TextPool::store(std::string_view text, std::size_t minimum, const TextAllocation& allocate) {
    text = text.substr(0, text.find('\0'));
    if (minimum && text.empty())
        text = " ";
    if (!minimum && text.empty())
        return 0;
    // Capture borrowed pool/input text before an allocation callback can reset
    // its owner. The ordinal is taken after that callback, from the live pool.
    std::string bytes(text);
    const auto extent = std::max(minimum, bytes.size() + 1);
    if (allocate)
        allocate(bytes, extent);
    if (entries_.size() > std::numeric_limits<TextId>::max())
        throw std::length_error("Text pool ordinal exceeds its 32-bit domain");
    bytes.resize(extent, '\0');
    const auto id = static_cast<TextId>(entries_.size());
    entries_.push_back(std::move(bytes));
    last_ = id;
    return id;
}
std::string_view TextPool::allocation(TextId id) const {
    if (id >= entries_.size())
        throw std::out_of_range("Text label has no pool backing");
    return entries_[id];
}
std::string_view TextPool::text(TextId id) const {
    const auto bytes = allocation(id);
    return bytes.substr(0, bytes.find('\0'));
}
} // namespace openciv::state
