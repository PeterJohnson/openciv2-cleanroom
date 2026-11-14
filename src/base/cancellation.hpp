#pragma once

#include <stdexcept>
#include <stop_token>

namespace openciv::base {
// Host analysis/I/O cancellation. This does not imply gameplay rollback.
class Cancelled : public std::runtime_error {
public:
    Cancelled() : std::runtime_error("Operation cancelled before publication") {}
};
inline void check_stop(std::stop_token stop) {
    if (stop.stop_requested())
        throw Cancelled{};
}
} // namespace openciv::base
