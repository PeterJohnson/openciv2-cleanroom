export module openciv.base.cancellation;

import std;

export namespace openciv::base {
/// Poll-only cancellation, available on every pinned native standard library.
/// The flag communicates cancellation only, not publication of other state.
class CancellationSource;
/// A copyable view of a shared cancellation flag; a default token never stops.
class CancellationToken {
public:
    CancellationToken() = default;
    /// Poll the flag without synchronizing unrelated state.
    bool stop_requested() const noexcept {
        return requested_ && requested_->load(std::memory_order_relaxed);
    }

private:
    friend class CancellationSource;
    /// Attach the token to the source-owned shared cancellation flag.
    explicit CancellationToken(std::shared_ptr<std::atomic<bool>> requested)
        : requested_(std::move(requested)) {}
    std::shared_ptr<std::atomic<bool>> requested_;
};
/// Own the shared flag used to request cooperative cancellation.
class CancellationSource {
public:
    /// Return a token observing this source, including after the source is destroyed.
    CancellationToken get_token() const noexcept { return CancellationToken(requested_); }
    /// Request cancellation and return true only on the first transition to stopped.
    bool request_stop() noexcept {
        return requested_ && !requested_->exchange(true, std::memory_order_relaxed);
    }

private:
    std::shared_ptr<std::atomic<bool>> requested_ = std::make_shared<std::atomic<bool>>(false);
};

/// Host analysis/I/O cancellation. This does not imply gameplay rollback.
class Cancelled : public std::runtime_error {
public:
    /// Describe an operation cancelled before its output was published.
    Cancelled() : std::runtime_error("Operation cancelled before publication") {}
};
/// Throw Cancelled if the supplied token has been stopped.
inline void check_stop(CancellationToken stop) {
    if (stop.stop_requested())
        throw Cancelled{};
}
} // namespace openciv::base
