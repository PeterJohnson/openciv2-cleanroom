export module openciv.base.cancellation;

import std;

export extern "C++" {
    namespace openciv::base {
    // Poll-only cancellation, available on every pinned native standard library.
    // The flag communicates cancellation only, not publication of other state.
    class CancellationSource;
    class CancellationToken {
    public:
        CancellationToken() = default;
        bool stop_requested() const noexcept {
            return requested_ && requested_->load(std::memory_order_relaxed);
        }

    private:
        friend class CancellationSource;
        explicit CancellationToken(std::shared_ptr<std::atomic<bool>> requested)
            : requested_(std::move(requested)) {}
        std::shared_ptr<std::atomic<bool>> requested_;
    };
    class CancellationSource {
    public:
        CancellationToken get_token() const noexcept { return CancellationToken(requested_); }
        bool request_stop() noexcept {
            return requested_ && !requested_->exchange(true, std::memory_order_relaxed);
        }

    private:
        std::shared_ptr<std::atomic<bool>> requested_ = std::make_shared<std::atomic<bool>>(false);
    };

    // Host analysis/I/O cancellation. This does not imply gameplay rollback.
    class Cancelled : public std::runtime_error {
    public:
        Cancelled() : std::runtime_error("Operation cancelled before publication") {}
    };
    inline void check_stop(CancellationToken stop) {
        if (stop.stop_requested())
            throw Cancelled{};
    }
    } // namespace openciv::base
}
