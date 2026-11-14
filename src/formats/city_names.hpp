#pragma once
#include "city/names.hpp"
#include "classic_text_reader.hpp"
#include "rules.hpp"
#include "state/city_names.hpp"

namespace openciv::formats::classic {
// C01 uses the low-level F03 opener: a missing EXTRA is a retry result, not
// the wrapper's retry of an already closed stream. References must outlive
// the returned provider; comparison and captured section operands are owned.
city::NameProvider city_name_provider(TextReader& reader, TextDirectories& directories,
                                      TextCase comparison);
} // namespace openciv::formats::classic

namespace openciv::formats::content {
struct CityNameContent {
    state::CityNameLists lists;
    std::shared_ptr<const RuleContent> rules;
    std::shared_ptr<const ResolvedFile> source;
    bool starting_lists_complete = false;
};
// data-format.md section 5 / Appendix B. Checks base-pack starting lists;
// resolved overlays may omit them. Always rejects ambiguous plural selectors.
std::shared_ptr<const CityNameContent> load_city_names(const ResolvedPack& pack,
                                                       const classic::TextCase& comparison);
// Owns immutable content and comparison. Operates directly on ordered lists;
// does not manufacture a CITY file or borrow a scanner buffer.
city::NameProvider city_name_provider(std::shared_ptr<const CityNameContent> names,
                                      classic::TextCase comparison);
} // namespace openciv::formats::content
