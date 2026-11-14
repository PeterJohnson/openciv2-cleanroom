#pragma once
#include <functional>
#include <stdexcept>

namespace openciv::test {
// An explicitly supplied child failure, distinct from incomplete service wiring.
template <class R, class... Args> void fail_service(std::function<R(Args...)>& service) {
    service = [](Args...) -> R { throw std::runtime_error("Supplied child unavailable"); };
}
} // namespace openciv::test
