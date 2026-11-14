#include "unit/request.hpp"
#include "base/arithmetic.hpp"

namespace openciv::unit {
void request_unit_operation(bool& activity, std::int32_t& reply, bool& turn_loop_active,
                            const std::function<void()>& send,
                            const UnitNetworkServices& services) {
    activity = false;
    reply = -2;
    send();
    const auto start = services.ticks(); // Required even after a synchronous reply.
    while (reply == -2) {
        const auto now = services.ticks();
        if (base::signed32(now - start) >= 3600)
            break;
        services.process_messages(1, 1);
    }
    if (reply == -2) {
        services.log_timeout();
        services.show_timeout();
        turn_loop_active = false;
    }
    while (services.priority_messages() || services.normal_messages())
        services.process_messages(1, 0);
}
} // namespace openciv::unit
