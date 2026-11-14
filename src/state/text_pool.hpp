#pragma once
#include <cstdint>
#include <deque>
#include <string>
#include <string_view>

namespace openciv::state {
using TextId = std::uint32_t;
// Owned text and stable implementation-local references. IDs are not classic
// ordinals, file fields, allocation sizes or gameplay object identities.
class TextPool {
public:
    void initialize();
    TextId store(std::string_view text);
    std::string_view text(TextId id) const;
    std::size_t size() const noexcept { return entries_.size(); }

private:
    // Appending preserves views; rebuilding invalidates all references.
    std::deque<std::string> entries_;
};
} // namespace openciv::state
