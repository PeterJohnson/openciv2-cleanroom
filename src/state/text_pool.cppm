export module openciv.state.text_pool;

import std;

export namespace openciv::state {
using TextId = std::uint32_t;
/// Owned text and stable implementation-local references. IDs are not classic
/// ordinals, file fields, allocation sizes or gameplay object identities.
class TextPool {
public:
    /// Replace the pool with its reserved `<nil>` entry, invalidating old views and IDs.
    void initialize();
    /// Store text through its first NUL; return zero for empty text and preserve existing views.
    TextId store(std::string_view text);
    /// Borrow the text for a checked local ID until the pool is rebuilt.
    std::string_view text(TextId id) const;
    /// Return the number of retained entries, including the reserved nil entry.
    std::size_t size() const noexcept { return entries_.size(); }

private:
    // Appending preserves views; rebuilding invalidates all references.
    std::deque<std::string> entries_;
};
} // namespace openciv::state

module :private;
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
