export module openciv.analysis.sqlite_export;

import std;
export import openciv.base.callback_ref;
export import openciv.query.stored;
export import openciv.base.cancellation;

export namespace openciv::analysis {
/// Schema 1, application_id 0x4f433253. Returns a complete, owned SQLite image.
/// No filesystem or live-state access. Progress runs between completed records;
/// cancellation or a throwing callback discards the whole in-memory transaction.
formats::Bytes sqlite_snapshot(const query::StoredSnapshot& snapshot,
                               base::CancellationToken stop = {},
                               const base::CallbackRef<void(std::size_t)>& progress = {});
} // namespace openciv::analysis
