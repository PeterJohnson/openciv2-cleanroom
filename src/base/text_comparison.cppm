export module openciv.base.text_comparison;

import std;

export extern "C++" {
    namespace openciv::base {
    using NameComparison = std::function<bool(std::string_view, std::string_view)>;
    class ComparisonUnavailable : public std::runtime_error {
    public:
        ComparisonUnavailable()
            : std::runtime_error("Non-ASCII name comparison requires an established profile") {}
    };
    // Exact bytes always compare; unequal high bytes require an established profile.
    inline bool ascii_name_equal(std::string_view a, std::string_view b) {
        if (a.size() != b.size())
            return false;
        const auto upper = [](char c) {
            if (static_cast<unsigned char>(c) >= 128)
                throw ComparisonUnavailable();
            return c >= 'a' && c <= 'z' ? static_cast<char>(c - 'a' + 'A') : c;
        };
        for (std::size_t i = 0; i < a.size(); ++i)
            if (a[i] != b[i] && upper(a[i]) != upper(b[i]))
                return false;
        return true;
    }
    } // namespace openciv::base
}
