#include "unit/stacks.hpp"
#include <bit>
#include <string>
#include <utility>

namespace openciv::unit {
namespace {
std::int32_t signed_owner(const state::Unit& unit) {
    return std::bit_cast<std::int8_t>(static_cast<std::uint8_t>(unit.owner));
}
std::int16_t short_value(std::int32_t value) {
    return std::bit_cast<std::int16_t>(static_cast<std::uint16_t>(value));
}
bool at(const state::Unit& unit, world::Position position) {
    return unit.x == position.x && unit.y == position.y;
}
struct Depth {
    std::size_t& current;
    explicit Depth(std::size_t& value) : current(value) { ++current; }
    ~Depth() { --current; }
};
} // namespace

StackResourceStop::StackResourceStop(StackStop reason)
    : std::runtime_error(reason == StackStop::steps ? "Unit stack execution budget exhausted"
                                                    : "Unit stack validation depth exhausted"),
      reason_(reason) {}

LocalStacks::LocalStacks(state::UnitPool& units, world::MapStorage& map,
                         state::UnitStackState& retained, StackBudget& budget,
                         StackServices services, RepairPlacement placement)
    : units_(units), map_(map), retained_(retained), budget_(budget),
      services_(std::move(services)), placement_(std::move(placement)) {
    if (static_cast<bool>(placement_.detach) != static_cast<bool>(placement_.attach))
        throw std::invalid_argument("Unit stack repair requires both placement children");
}
state::Unit& LocalStacks::record(std::int32_t slot) {
    if (slot >= 0 && static_cast<std::size_t>(slot) < units_.storage().size())
        return units_.storage()[static_cast<std::size_t>(slot)];
    throw std::out_of_range("Unit stack index is outside the owned pool: " + std::to_string(slot));
}
void LocalStacks::diagnose(StackDiagnostic diagnostic) {
    if (services_.diagnostics)
        services_.diagnostics->push_back(diagnostic);
}
void LocalStacks::step() {
    if (budget_.steps == 0)
        throw StackResourceStop(StackStop::steps);
    --budget_.steps;
}
std::int32_t LocalStacks::raw_forward_count(std::int32_t slot) {
    std::int32_t count = 0;
    while (slot >= 0 && count < 2048) {
        step();
        ++count;
        slot = record(slot).next.value;
    }
    return count;
}
void LocalStacks::cut(std::int32_t slot, std::int32_t neighbor, bool backward) {
    if (backward) {
        record(slot).previous.value = -1;
        if (record(neighbor).next.value == slot)
            record(neighbor).next.value = -1;
    } else {
        record(slot).next.value = -1;
        if (record(neighbor).previous.value == slot)
            record(neighbor).previous.value = -1;
    }
}
std::int32_t LocalStacks::validate(std::int32_t slot) {
    step();
    if (depth_ >= budget_.validation_depth)
        throw StackResourceStop(StackStop::validation_depth);
    Depth depth(depth_);
    if (slot < 0 || static_cast<std::size_t>(slot) >= units_.storage().size()) {
        diagnose({StackProblem::entry_range, slot});
        throw std::out_of_range("Stack validation index is outside the owned pool");
    }
    if (retained_.suppression != 0)
        return 1;
    std::int32_t result = 1;
    if (raw_forward_count(slot) >= 2048) {
        diagnose({StackProblem::infinite, slot});
        result = 0;
        auto current = slot;
        auto saved_next = record(current).next.value;
        do {
            if (current < 0)
                break;
            step();
            const world::Position position{record(current).x, record(current).y};
            if (placement_.detach) {
                placement_.detach(current);
                placement_.attach(current, position);
            } else {
                detach(current);
                attach_at_front(current, position);
            }
            current = saved_next;
            if (current < 0)
                break;
            saved_next = record(current).next.value;
        } while (current != slot);
    }
    for (unsigned pass = 0; pass < 4; ++pass) {
        const bool backward = (pass % 2) == 0;
        auto current = slot;
        for (;;) {
            step();
            const auto neighbor =
                backward ? record(current).previous.value : record(current).next.value;
            if (neighbor < 0)
                break;
            std::optional<StackProblem> problem;
            if (pass == 0) {
                if (!state::occupied(record(neighbor).token))
                    problem = StackProblem::backward_dead;
            } else if (pass == 1) {
                if (!state::occupied(record(current).token))
                    problem = StackProblem::forward_dead;
            } else if (record(neighbor).x != record(current).x) {
                problem = backward ? StackProblem::backward_x : StackProblem::forward_x;
            } else if (record(neighbor).y != record(current).y) {
                problem = backward ? StackProblem::backward_y : StackProblem::forward_y;
            }
            if (problem) {
                diagnose({*problem, current, neighbor});
                result = 0;
                cut(current, neighbor, backward);
            }
            current = neighbor;
        }
    }
    return result;
}
std::int32_t LocalStacks::next(std::int32_t slot) {
    if (slot < 0)
        return slot;
    validate(slot);
    return record(slot).next.value;
}
std::int32_t LocalStacks::end_of_chain(std::int32_t slot, bool backward) {
    if (slot < 0)
        return slot;
    validate(slot);
    for (;;) {
        step();
        const auto neighbor = backward ? record(slot).previous.value : record(slot).next.value;
        if (neighbor < 0 || neighbor == slot)
            return slot;
        slot = neighbor;
    }
}
std::int32_t LocalStacks::first(std::int32_t slot) {
    return end_of_chain(slot, true);
}
std::int32_t LocalStacks::last(std::int32_t slot) {
    return end_of_chain(slot, false);
}
std::int32_t LocalStacks::first_for_owner_at(std::int32_t owner, world::Position position) {
    for (std::size_t i = 0; i < units_.end(); ++i) {
        step();
        const auto& unit = units_.storage()[i];
        if (state::occupied(unit.token) && signed_owner(unit) == owner && at(unit, position))
            return first(static_cast<std::int32_t>(i));
    }
    return -1;
}
std::int32_t LocalStacks::first_at(world::Position position) {
    if (retained_.animation_active && !map_.unit_owner(position))
        return -1;
    for (std::size_t i = 0; i < units_.end(); ++i) {
        step();
        const auto& unit = units_.storage()[i];
        if (state::occupied(unit.token) && at(unit, position)) {
            const auto slot = static_cast<std::int32_t>(i);
            validate(slot);
            return first(slot);
        }
    }
    return -1;
}
void LocalStacks::detach(std::int32_t slot) {
    step();
    retained_.detach_active = true;
    detach_body(slot);
    retained_.detach_active = false;
}
void LocalStacks::detach_body(std::int32_t slot) {
    const bool previous = record(slot).previous.value >= 0;
    if (previous)
        record(record(slot).previous.value).next = record(slot).next;
    const bool next = record(slot).next.value >= 0;
    if (next)
        record(record(slot).next.value).previous = record(slot).previous;
    record(slot).previous.value = record(slot).next.value = -1;
    const world::Position position{record(slot).x, record(slot).y};
    if (!previous && !next && map_.geometry().in_bounds(position))
        map_.mutate({world::TileField::features, position, 1, 0});
    const auto staging = short_value(-100 * (signed_owner(record(slot)) + 1));
    record(slot).x = record(slot).y = staging;
}
void LocalStacks::attach_at_front(std::int32_t slot, world::Position position) {
    step();
    retained_.attach_active = true;
    attach_body(slot, position);
    retained_.attach_active = false;
}
void LocalStacks::attach_body(std::int32_t slot, world::Position position) {
    const auto head = first_for_owner_at(signed_owner(record(slot)), position);
    record(slot).x = short_value(position.x);
    record(slot).y = short_value(position.y);
    record(slot).previous.value = -1;
    record(slot).next.value = short_value(head);
    if (head >= 0) {
        record(head).previous.value = short_value(slot);
    } else if (map_.geometry().in_bounds(position)) {
        map_.mutate({world::TileField::features, position, 1});
        map_.mutate({world::TileField::owner, position, signed_owner(record(slot))});
    }
}
} // namespace openciv::unit
