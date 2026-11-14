#pragma once

#include <cstdint>
#include <functional>

namespace openciv::unit {
// U01 section 9 common external boundaries. Each primitive owns distinct
// activity/reply state and its own request payload; sharing services does not
// combine operations or add a synchronization argument to the host request.
struct UnitNetworkServices {
    std::function<void()> state_difference;
    std::function<void(std::int32_t)> flush;
    std::function<std::uint32_t()> ticks;
    std::function<void(std::int32_t, std::int32_t)> process_messages;
    std::function<bool()> priority_messages, normal_messages;
    std::function<void()> log_timeout, show_timeout;
};
// Caller has already passed its guard and selected the nonhost path. Missing
// services stop at invocation. Returns with the live reply/activity untouched
// after the final queue drain, even on timeout or after a late reply.
void request_unit_operation(bool& activity, std::int32_t& reply, bool& turn_loop_active,
                            const std::function<void()>& send, const UnitNetworkServices& services);
} // namespace openciv::unit
