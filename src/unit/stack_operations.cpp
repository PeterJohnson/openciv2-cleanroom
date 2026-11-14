#include "unit/stack_operations.hpp"
#include <bit>
#include <utility>

namespace openciv::unit {
namespace {
std::int16_t link(std::int32_t slot) {
    return std::bit_cast<std::int16_t>(static_cast<std::uint16_t>(slot));
}
} // namespace
StackOperations::StackOperations(state::GameState& world, StackBudget& budget,
                                 StackServices stack_services, StackOperationServices services)
    : world_(world), services_(std::move(services)),
      local_(world.entities.units, world.map, world.unit_stacks, budget, std::move(stack_services),
             {[this](std::int32_t slot) { detach(slot, false); },
              [this](std::int32_t slot, world::Position position) {
                  attach_at_front(slot, position, false);
              }}) {}
StackOperations::OperationState StackOperations::state(PlacementOperation operation) {
    auto& state = world_.unit_stacks;
    switch (operation) {
    case PlacementOperation::detach:
        return {state.detach_active, state.detach_reply};
    case PlacementOperation::attach:
        return {state.attach_active, state.attach_reply};
    case PlacementOperation::relocate:
        return {state.relocate_active, state.relocate_reply};
    case PlacementOperation::bottom:
        return {state.bottom_active, state.bottom_reply};
    }
    throw std::invalid_argument("Unknown unit placement operation");
}
bool StackOperations::begin(PlacementRequest request) {
    auto operation = state(request.operation);
    operation.active = true;
    if (world_.globals.multiplayer < 3 || world_.multiplayer.network_host)
        return true;
    request_unit_operation(
        operation.active, operation.reply, world_.turn_loop_active,
        [&] { services_.request(request); }, services_);
    return false;
}
void StackOperations::synchronize(bool sync) {
    if (world_.globals.multiplayer >= 3 && sync) {
        services_.state_difference();
        services_.flush(5000);
    }
}
void StackOperations::finish(PlacementOperation operation, bool sync) {
    synchronize(sync);
    state(operation).active = false;
}
void StackOperations::detach(std::int32_t slot, bool sync) {
    local_.step();
    if (!begin({PlacementOperation::detach, {slot}}))
        return;
    local_.detach_body(slot);
    finish(PlacementOperation::detach, sync);
}
void StackOperations::attach_at_front(std::int32_t slot, world::Position position, bool sync) {
    local_.step();
    if (!begin({PlacementOperation::attach, {slot, position.x, position.y}}))
        return;
    local_.attach_body(slot, position);
    finish(PlacementOperation::attach, sync);
}
void StackOperations::relocate(std::int32_t slot, world::Position position, bool sync) {
    local_.step();
    if (!begin({PlacementOperation::relocate, {slot, position.x, position.y}}))
        return;
    detach(slot, false);
    attach_at_front(slot, position, false);
    finish(PlacementOperation::relocate, sync);
}
void StackOperations::relocate_in_place(std::int32_t slot, bool sync) {
    const auto& unit = local_.record(slot);
    const world::Position position{unit.x, unit.y};
    relocate(slot, position, sync);
}
void StackOperations::move_to_bottom(std::int32_t slot, bool sync) {
    if (local_.record(slot).next.value < 0)
        return;
    local_.step();
    if (!begin({PlacementOperation::bottom, {slot}}))
        return;
    if (local_.record(slot).next.value >= 0) {
        const world::Position position{local_.record(slot).x, local_.record(slot).y};
        auto anchor = first(slot);
        if (anchor == slot)
            anchor = next(slot);
        detach(slot, false);
        const auto tail = last(anchor);
        local_.record(tail).next.value = link(slot);
        local_.record(slot).previous.value = link(tail);
        local_.record(slot).next.value = -1;
        local_.record(slot).x = link(position.x);
        local_.record(slot).y = link(position.y);
    }
    finish(PlacementOperation::bottom, sync);
}
void StackOperations::relocate_stack(std::int32_t slot, world::Position position, bool sync) {
    auto current = first(slot);
    while (current >= 0) {
        local_.step();
        const auto saved_next = next(current);
        relocate(current, position, false);
        current = saved_next;
    }
    synchronize(sync);
}
} // namespace openciv::unit
