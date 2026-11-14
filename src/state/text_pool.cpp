#include "text_pool.hpp"
#include <limits>
#include <stdexcept>

namespace openciv::state {
void TextPool::initialize() {
    std::deque<std::string> fresh{"<nil>"};
    entries_.swap(fresh);
}
TextId TextPool::store(std::string_view text) {
    text = text.substr(0, text.find('\0'));
    if (text.empty())
        return 0;
    if (entries_.size() > std::numeric_limits<TextId>::max())
        throw std::length_error("Text reference capacity exceeded");
    const auto id = static_cast<TextId>(entries_.size());
    entries_.emplace_back(text);
    return id;
}
std::string_view TextPool::text(TextId id) const {
    if (id >= entries_.size())
        throw std::out_of_range("Text reference is not present");
    return entries_[id];
}
} // namespace openciv::state
