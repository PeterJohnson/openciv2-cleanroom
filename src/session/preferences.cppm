export module openciv.session.preferences;

import std;
export import openciv.base.callback_ref;
export import openciv.state.preferences;
export import openciv.state.multiplayer;
export namespace openciv::session {
/// Owned ordinary and multiplayer preferences captured for persistence.
struct PreferenceSnapshot {
    state::OrdinaryPreferences ordinary;
    state::MultiplayerPreferences multiplayer;
};
/// Abstract persistence service for preference snapshots at explicit I/O boundaries.
class PreferenceWriter {
public:
    PreferenceWriter() = default;
    virtual ~PreferenceWriter() = default;
    PreferenceWriter(const PreferenceWriter&) = delete;
    PreferenceWriter& operator=(const PreferenceWriter&) = delete;
    /// Invoke before_io before each external I/O boundary. Returning false reports
    /// persistence failure without undoing already accepted preference changes.
    virtual bool write(PreferenceSnapshot values,
                       base::CallbackRef<void()> before_io = {}) const = 0;
};
} // namespace openciv::session
