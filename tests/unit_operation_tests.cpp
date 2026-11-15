#include "session/unit_operations.hpp"
#include <catch2/catch_test_macros.hpp>
#include <deque>
using namespace openciv;
using namespace openciv::session;
namespace {
struct ChannelState {
    unsigned sends = 0, polls = 0, cancellations = 0;
    std::optional<UnitRequest> request;
    std::deque<UnitReply> replies;
    bool fail_send = false;
    std::function<void()> on_send, on_poll;
    std::vector<bool> waiting;
};
class Channel final : public UnitRequestChannel {
public:
    explicit Channel(ChannelState& state) : state_(state) {}
    void send(UnitRequest request) override {
        ++state_.sends;
        state_.request = request;
        if (state_.on_send)
            state_.on_send();
        if (state_.fail_send)
            throw std::runtime_error("send failed");
    }
    UnitReply poll(bool waiting) override {
        ++state_.polls;
        state_.waiting.push_back(waiting);
        if (state_.on_poll)
            state_.on_poll();
        if (state_.replies.empty())
            return {};
        auto reply = state_.replies.front();
        state_.replies.pop_front();
        return reply;
    }
    void cancel() noexcept override { ++state_.cancellations; }

private:
    ChannelState& state_;
};
struct Fixture {
    state::ProcessContext process;
    SessionRuntime runtime{process, base::RuntimeId{199}};
    ChannelState channel;
    UnitOperationServices services;
    std::uint32_t tick = 100;
    unsigned timeouts = 0, synchronizations = 0;
    Fixture() {
        runtime.begin_client_session({10, 20});
        auto& world = runtime.world();
        world.map.allocate(world::Geometry{8, 6});
        world.globals.multiplayer = state::GameMode::tcp;
        world.turn_loop_active = true;
        world.entities.units.set_end(2);
        for (unsigned i = 0; i < 2; ++i) {
            auto& unit = world.entities.units.storage()[i];
            unit.token = state::UnitToken{i + 1};
            unit.x = unit.y = 2;
            unit.owner = state::CivilizationSlot{1};
        }
        world.entities.units.storage()[0].next.value = 1;
        world.entities.units.storage()[1].previous.value = 0;
        services.ticks = [&] { return tick; };
        services.timed_out = [&] {
            ++timeouts;
            CHECK(world.turn_loop_active);
        };
        services.placement.synchronize = [&] { ++synchronizations; };
        services.deletion.check_destroyed = [](std::int32_t, std::int32_t) {};
        services.deletion.removed_unit = [](world::Position) {};
        services.deletion.refresh_home = [](std::int32_t) {};
        services.deletion.synchronize = [&](world::Position, std::optional<state::CitySlot>) {
            ++synchronizations;
        };
    }
    std::unique_ptr<UnitOperation> operation(UnitOperationKind kind) {
        return std::make_unique<UnitOperation>(runtime, UnitRequest{kind, {0, 4, 2}}, true,
                                               unit::StackBudget{10000, 64}, services,
                                               std::make_unique<Channel>(channel));
    }
};
} // namespace
TEST_CASE("U01 remote operations send once and yield until a reply without mutating local units",
          "[session][unit-operation]") {
    for (const auto kind :
         {UnitOperationKind::detach, UnitOperationKind::attach, UnitOperationKind::relocate,
          UnitOperationKind::bottom, UnitOperationKind::erase}) {
        Fixture f;
        const auto before = f.runtime.world().entities.units.storage()[0];
        auto operation = f.operation(kind);
        REQUIRE_FALSE(operation->poll());
        CHECK(f.channel.sends == 1);
        CHECK(f.channel.polls == 0);
        CHECK_THROWS_AS(f.runtime.prepare_new_game(), std::logic_error);
        CHECK_FALSE(operation->poll());
        f.channel.replies.push_back({71, true});
        const auto result = operation->poll();
        REQUIRE(result);
        CHECK(result->reply == 71);
        CHECK_FALSE(result->timed_out);
        CHECK(f.runtime.world().entities.units.storage()[0] == before);
        CHECK(f.channel.cancellations == 1);
        CHECK(operation->poll()->reply == 71);
        CHECK(f.channel.sends == 1);
        CHECK_NOTHROW(f.runtime.prepare_new_game());
    }
}
TEST_CASE("U01 deadlines wrap safely and late queued replies drain over separate polls",
          "[session][unit-operation]") {
    Fixture f;
    f.tick = 0xfffffff0U;
    auto operation = f.operation(UnitOperationKind::erase);
    REQUIRE_FALSE(operation->poll());
    f.tick += 3599;
    CHECK_FALSE(operation->poll());
    CHECK(f.timeouts == 0);
    ++f.tick;
    f.channel.replies.push_back({{}, false});
    CHECK_FALSE(operation->poll());
    CHECK(f.timeouts == 1);
    CHECK_FALSE(f.runtime.world().turn_loop_active);
    f.channel.replies.push_back({9, true});
    const auto result = operation->poll();
    REQUIRE(result);
    CHECK(result->reply == 9);
    CHECK(result->timed_out);
    CHECK_FALSE(f.channel.waiting.back());
    CHECK(f.timeouts == 1);
}
TEST_CASE("U01 cancellation and transport failure release the guarded session and channel once",
          "[session][unit-operation]") {
    for (const bool throwing : {false, true}) {
        Fixture f;
        f.channel.fail_send = throwing;
        auto operation = f.operation(UnitOperationKind::detach);
        if (throwing)
            CHECK_THROWS_AS(operation->poll(), std::runtime_error);
        else {
            CHECK_FALSE(operation->poll());
            operation->cancel();
        }
        CHECK(operation->poll()->cancelled);
        operation.reset();
        CHECK(f.channel.cancellations == 1);
        CHECK_NOTHROW(f.runtime.prepare_new_game());
    }
}
TEST_CASE("U01 local session operations use the same mechanics without a transport request",
          "[session][unit-operation]") {
    Fixture f;
    f.runtime.world().multiplayer.network_host = true;
    auto operation = f.operation(UnitOperationKind::relocate);
    REQUIRE(operation->poll());
    CHECK(f.channel.sends == 0);
    CHECK(f.runtime.world().entities.units.storage()[0].x == 4);
    CHECK(f.synchronizations == 1);
}
TEST_CASE("U01 missing remote services and direct unauthorized mutations reject before effects",
          "[session][unit-operation]") {
    Fixture f;
    f.services.ticks = {};
    CHECK_THROWS_AS(f.operation(UnitOperationKind::detach), std::invalid_argument);
    unit::StackBudget budget{100, 10};
    unit::StackOperations local(f.runtime.world(), budget, {}, {});
    CHECK_THROWS_AS(local.detach(0, false), std::logic_error);
    CHECK(f.channel.sends == 0);
    CHECK(f.runtime.world().entities.units.storage()[0].next.value == 1);
}

TEST_CASE("U01 cancellation inside send defers channel destruction until send returns",
          "[session][unit-operation]") {
    Fixture f;
    auto operation = f.operation(UnitOperationKind::detach);
    f.channel.on_send = [&] {
        CHECK_THROWS_AS(operation->poll(), std::logic_error);
        operation->cancel();
        CHECK(f.channel.cancellations == 0);
        CHECK_THROWS_AS(f.runtime.prepare_new_game(), std::logic_error);
    };
    const auto result = operation->poll();
    REQUIRE(result);
    CHECK(result->cancelled);
    CHECK(f.channel.cancellations == 1);
    CHECK(f.channel.request->arguments == std::array<std::int32_t, 6>{0, 0, 0, 0, 0, 0});
    CHECK_NOTHROW(f.runtime.prepare_new_game());
}

TEST_CASE("U01 whole-stack requests preserve successors and recheck authority between children",
          "[session][unit-operation]") {
    for (const bool become_host : {false, true}) {
        Fixture f;
        auto operation = f.operation(UnitOperationKind::relocate_stack);
        REQUIRE_FALSE(operation->poll());
        REQUIRE(f.channel.request->operation == UnitOperationKind::relocate);
        CHECK(f.channel.request->arguments == std::array<std::int32_t, 6>{0, 4, 2, 0, 0, 0});
        f.channel.on_poll = [&] {
            auto units = f.runtime.world().entities.units.storage();
            auto& moved = units[static_cast<std::size_t>(f.channel.request->arguments[0])];
            moved.x = 4;
            moved.previous.value = moved.next.value = -1;
            units[1].previous.value = -1;
            f.runtime.world().multiplayer.network_host = become_host;
        };
        f.channel.replies.push_back({0, true});
        CHECK_FALSE(operation->poll());
        CHECK_THROWS_AS(f.runtime.prepare_new_game(), std::logic_error);
        if (become_host) {
            REQUIRE(operation->poll());
            CHECK(f.channel.sends == 1);
        } else {
            CHECK_FALSE(operation->poll());
            CHECK(f.channel.request->arguments[0] == 1);
            f.channel.replies.push_back({0, true});
            REQUIRE(operation->poll());
            CHECK(f.channel.sends == 2);
        }
        CHECK(f.runtime.world().entities.units.storage()[0].x == 4);
        CHECK(f.runtime.world().entities.units.storage()[1].x == 4);
        CHECK(f.synchronizations == 1);
        CHECK(f.channel.cancellations == 1);
        CHECK_NOTHROW(f.runtime.prepare_new_game());
    }
}
TEST_CASE("U01 remote primitive arguments reach the channel before local record access",
          "[session][unit-operation]") {
    for (const auto kind :
         {UnitOperationKind::detach, UnitOperationKind::attach, UnitOperationKind::relocate}) {
        Fixture f;
        UnitOperation operation(f.runtime, {kind, {-7, 70000, -80000}}, false, {100, 10},
                                f.services, std::make_unique<Channel>(f.channel));
        CHECK_FALSE(operation.poll());
        CHECK(f.channel.request->arguments[0] == -7);
        if (kind != UnitOperationKind::detach) {
            CHECK(f.channel.request->arguments[1] == 70000);
            CHECK(f.channel.request->arguments[2] == -80000);
        }
    }
}

namespace {
class CommandUnit final : public Continuation {
public:
    CommandUnit(UnitOperationServices services, std::unique_ptr<UnitRequestChannel> channel)
        : services_(std::move(services)), channel_(std::move(channel)) {}
    OperationStep resume(OperationContext& context, OperationInput input) override {
        if (!child_)
            child_ = std::make_unique<UnitOperation>(
                context, UnitRequest{UnitOperationKind::relocate, {0, 4, 2}}, false,
                unit::StackBudget{10000, 64}, services_, std::move(channel_));
        if (input.kind == OperationInputKind::cancel) {
            child_->cancel();
            return {CommandStatus::cancelled, {}, {}, {}};
        }
        return {child_->poll() ? CommandStatus::completed : CommandStatus::pending, {}, {}, {}};
    }

private:
    UnitOperationServices services_;
    std::unique_ptr<UnitRequestChannel> channel_;
    std::unique_ptr<UnitOperation> child_;
};
} // namespace
TEST_CASE("U01 unit operations compose inside real command continuations",
          "[session][unit-operation][commands]") {
    for (const auto route : {0, 1, 2}) {
        Fixture f;
        f.runtime.end_client_session();
        CommandDefinition command;
        command.descriptor = {1, "test.unit", "Authored unit child", AccessClass::player, {}, {}};
        command.admit = [](const auto&, const auto&, const auto&) { return Admission{}; };
        command.execute = [&](const auto&) {
            return std::make_unique<CommandUnit>(f.services, std::make_unique<Channel>(f.channel));
        };
        f.runtime.register_command(std::move(command));
        f.runtime.begin_client_session({10, 20});
        f.runtime.world().multiplayer.network_host = route == 0;
        const auto client = f.runtime.bind(f.runtime.add_participant(state::CivilizationSlot{1}),
                                           ControllerKind::scripted);
        const auto id = client.submit({"test.unit", {}}).correlation.command;
        REQUIRE(f.runtime.advance_one());
        if (route != 0) {
            REQUIRE(client.result(id)->status == CommandStatus::pending);
            CHECK_THROWS_AS(f.runtime.prepare_new_game(), std::logic_error);
            if (route == 2)
                f.runtime.cancel_command(id);
            else {
                f.channel.replies.push_back({0, true});
                REQUIRE(f.runtime.continue_command(id));
            }
            REQUIRE(f.runtime.advance_one());
        }
        CHECK(client.result(id)->status ==
              (route == 2 ? CommandStatus::cancelled : CommandStatus::completed));
        CHECK(f.channel.sends == (route == 0 ? 0 : 1));
        CHECK(f.channel.cancellations == 1);
        if (route == 0)
            CHECK(f.runtime.world().entities.units.storage()[0].x == 4);
        CHECK_NOTHROW(f.runtime.prepare_new_game());
    }
}
