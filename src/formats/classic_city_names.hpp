#pragma once
#include "classic_text_reader.hpp"
#include "city/name_catalog.hpp"
namespace openciv::formats::classic {
// Decode the currently open CITY source. Preserves first-match selectors and
// incomplete prefixes; the latter fail if selection reaches their unknown end.
state::CityNameLists read_city_names(TextReader& reader, const TextCase& comparison);
// Snapshot scenario/home sources as ordered layers; each owns its fallback.
city::NameCatalog load_city_names(TextReader& reader, TextDirectories& directories,
                                  const TextCase& comparison);
} // namespace openciv::formats::classic
