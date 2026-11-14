#pragma once

#include "query/stored.hpp"

namespace openciv::query::detail {
void validate(const formats::StoredSave& save, const std::vector<Record>& records,
              std::vector<Diagnostic>& diagnostics, std::vector<std::string>& checks);
void validate(const formats::RawMapFile& map, const std::vector<Record>& records,
              std::vector<Diagnostic>& diagnostics, std::vector<std::string>& checks);
} // namespace openciv::query::detail
