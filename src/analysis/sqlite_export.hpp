#pragma once

#include "query/stored.hpp"
#include <functional>
#include "base/cancellation.hpp"

namespace openciv::analysis {
// Schema 1, application_id 0x4f433253. Returns a complete, owned SQLite image.
// No filesystem or live-state access. Progress runs between completed records;
// cancellation or a throwing callback discards the whole in-memory transaction.
formats::Bytes sqlite_snapshot(const query::StoredSnapshot& snapshot,
                               base::CancellationToken stop = {},
                               const std::function<void(std::size_t)>& progress = {});
} // namespace openciv::analysis
