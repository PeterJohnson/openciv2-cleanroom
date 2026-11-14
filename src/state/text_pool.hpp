#pragma once
#include <cstdint>
#include <deque>
#include <functional>
#include <optional>
#include <string>
#include <string_view>

namespace openciv::state {
using TextId = std::uint32_t;
using TextAllocation = std::function<void(std::string_view, std::size_t)>;
// F02 section 8, F03 Appendix A, P04 section 2: append-only ordinal identities.
// Byte strings, independent of presentation decoding and gameplay object IDs.
class TextPool {
public:
    // Explicit pool initialization/rebuild, not an implicit new-game reset.
    // Replaces old storage, then appends <nil> at ordinal zero. Failure retains
    // the reached reset prefix; old numeric bindings are not silently repaired.
    void initialize(const TextAllocation& allocate = {});
    TextId store(std::string_view text, std::size_t minimum = 0,
                 const TextAllocation& allocate = {});
    std::string_view text(TextId id) const;
    std::string_view allocation(TextId id) const;
    std::size_t size() const noexcept { return entries_.size(); }
    std::optional<TextId> last() const noexcept { return last_; }

private:
    // Appending does not invalidate existing strings/views. Reinitializing does.
    std::deque<std::string> entries_;
    std::optional<TextId> last_;
};
constexpr std::uint32_t label_magnitude(std::int16_t label) {
    const std::int32_t value = label;
    return static_cast<std::uint32_t>(value < 0 ? -value : value);
}
} // namespace openciv::state
