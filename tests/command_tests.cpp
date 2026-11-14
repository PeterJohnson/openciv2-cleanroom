// Architecture §§4–5 foundation scenarios. Fake operations are test-only;
// they establish continuation/authority contracts, not classic gameplay rules.
#include "session/runtime.hpp"
#include <catch2/catch_test_macros.hpp>
#include <array>

using namespace openciv;
using namespace openciv::session;
namespace {
using Handler = std::function<OperationStep(OperationContext&, OperationInput)>;
class Fake final : public Continuation {
public:
    explicit Fake(Handler handler) : handler_(std::move(handler)) {}
    OperationStep resume(OperationContext& context, OperationInput input) override {
        return handler_(context, input);
    }

private:
    Handler handler_;
};
OperationStep done(std::int32_t value) {
    return {CommandStatus::completed, {{"value", value}}, {}, {}};
}
CommandDefinition definition(std::string name, Handler handler) {
    CommandDefinition result;
    result.descriptor = {1,
                         std::move(name),
                         "Authored test operation",
                         AccessClass::player,
                         {{"amount", ValueKind::integer}, {"target", ValueKind::unit, false}},
                         {{"value", ValueKind::integer}}};
    result.admit = [](const auto&, const auto&, const auto&) { return Admission{}; };
    result.execute = [handler](const auto&) { return std::make_unique<Fake>(handler); };
    return result;
}
CommandRequest request(std::int32_t amount = 3) {
    return {"test.action", {{"amount", amount}}};
}
PlayerClient client(SessionRuntime& runtime, std::uint8_t civ = 1) {
    return runtime.bind(runtime.add_participant(state::CivilizationSlot{civ}),
                        ControllerKind::scripted);
}
CommandId id(const CommandUpdate& update) {
    return update.correlation.command;
}
InteractionId interaction(const PlayerClient& client, CommandId command) {
    const auto update = client.result(command);
    REQUIRE(update.has_value());
    REQUIRE(update->interaction.has_value());
    return update->interaction->id;
}
Handler decision(std::function<void(OperationContext&)> callback = {},
                 std::optional<ParticipantId> recipient = {}) {
    return
        [phase = 0, callback, recipient](OperationContext& context, OperationInput input) mutable {
            auto& gold = context.world.civilizations[1].gold;
            if (input.kind == OperationInputKind::cancel)
                return OperationStep{CommandStatus::cancelled, {}, "cancelled", {}};
            if (phase++ == 0) {
                gold -= 5;
                context.effect("test.debit", {{"amount", std::int32_t{5}}});
                if (callback)
                    callback(context);
                InteractionSpec spec;
                spec.prompt = "Choose an authored result";
                spec.recipient = recipient;
                spec.choices = {2, 4};
                spec.deadline = 50;
                spec.default_choice = 2;
                return OperationStep{CommandStatus::awaiting_interaction, {}, {}, spec};
            }
            if (!input.choice)
                return OperationStep{CommandStatus::failed, {}, "missing_choice", {}};
            CHECK(input.source.has_value() == (input.kind == OperationInputKind::choice));
            gold += *input.choice; // Live read after the earlier callback/yield.
            context.effect("test.choice", {{"value", gold}});
            return done(gold);
        };
}
} // namespace

TEST_CASE("Controlled retries retain resolved choices across an interaction and random restore",
          "[session][commands][choices]") {
    // F01 §§2,6; X02 §§2,4. Authored operation, not an implementation of a game
    // selection rule. Ties retain the first candidate without requesting a draw.
    const auto point = [](std::int32_t attempt) {
        return base::ChoiceKey{"test.candidate", "test.operation/one", attempt};
    };
    base::ControlledChoices tape{
        {base::make_choice(point(0), base::PrimaryModulo{3}, std::int32_t{6}),
         base::make_choice(point(1), base::PrimaryModulo{3}, std::int32_t{8}),
         base::make_choice({"test.reward", "test.operation/one", 0}, base::SecondaryRange{1, 14},
                           UINT32_MAX)}};
    const auto operation = [phase = 0, selected = -1, point](OperationContext& context,
                                                             OperationInput input) mutable {
        auto& gold = context.world.civilizations[1].gold;
        if (phase++ == 0) {
            for (std::int32_t attempt = 0; attempt < 3; ++attempt) {
                selected = context.gameplay.primary_modulo(point(attempt), 3);
                if (selected != 0)
                    break;
            }
            // A deterministic tie is owned by the operation, never the sampler.
            const std::array scores{7, 7};
            const auto tied = scores[1] > scores[0] ? 1 : 0;
            CHECK(selected == 2);
            CHECK(tied == 0);
            gold -= selected;
            context.effect("test.selected", {{"candidate", selected}});
            InteractionSpec prompt;
            prompt.prompt = "Resolve retained candidate";
            prompt.choices = {1, 2};
            return OperationStep{CommandStatus::awaiting_interaction, {}, {}, prompt};
        }
        REQUIRE(input.choice.has_value());
        gold += selected; // Reuse the retained choice and read the post-callback gold.
        return done(context.gameplay.secondary_range(
            base::ChoiceKey{"test.reward", "test.operation/one", 0}, 1, gold));
    };
    state::ProcessContext first_process, second_process;
    SessionRuntime headless(first_process, RuntimeId{1}), with_view(second_process, RuntimeId{2});
    headless.register_command(definition("test.action", operation));
    with_view.register_command(definition("test.action", operation));
    headless.begin_client_session({1, 2});
    with_view.begin_client_session({1, 2});
    base::GameplayRandom controlled(tape, "session");
    headless.gameplay_random().restore(controlled.checkpoint());
    with_view.gameplay_random().restore(controlled.checkpoint());
    const auto first_client = client(headless), second_client = client(with_view);
    const auto first = id(first_client.submit(request())),
               second = id(second_client.submit(request()));
    headless.world().civilizations[1].gold = 10;
    with_view.world().civilizations[1].gold = 10;
    REQUIRE(headless.advance_one());
    REQUIRE(with_view.advance_one());
    CHECK(headless.world().civilizations[1].gold == 8);
    REQUIRE(headless.gameplay_random().checkpoint().trace->events.size() == 2);
    CHECK(first_client.result(first)->committed[0].values ==
          second_client.result(second)->committed[0].values);
    const auto suspended_random = with_view.gameplay_random().checkpoint();
    base::GameplayRandom replacement(999, "session");
    replacement.restore(suspended_random);
    with_view.gameplay_random() = std::move(replacement);
    // This restores the context while the existing continuation remains alive;
    // it does not claim a serialized whole-session/pending-operation restore.
    base::CosmeticRandom cosmetic(3, "frontend");
    for (int i = 0; i < 50; ++i) {
        with_view.presentation_random().primary_modulo("test.report", 7);
        cosmetic.secondary_code("test.animation");
        second_client.own_units();
    }
    CHECK(with_view.gameplay_random().checkpoint() == suspended_random);
    headless.world().civilizations[1].gold = 12;
    with_view.world().civilizations[1].gold = 12;
    REQUIRE(first_client.reply(interaction(first_client, first), 1).accepted);
    REQUIRE(second_client.reply(interaction(second_client, second), 1).accepted);
    REQUIRE(headless.advance_one());
    REQUIRE(with_view.advance_one());
    REQUIRE(first_client.result(first)->status == CommandStatus::completed);
    REQUIRE(second_client.result(second)->status == CommandStatus::completed);
    CHECK(first_client.result(first)->result == CommandValues{{"value", std::int32_t{14}}});
    CHECK(first_client.result(first)->result == second_client.result(second)->result);
    CHECK(headless.world().civilizations[1].gold == with_view.world().civilizations[1].gold);
    CHECK(headless.gameplay_random().checkpoint() == with_view.gameplay_random().checkpoint());
    CHECK_NOTHROW(headless.gameplay_random().require_choices_consumed());
    CHECK_NOTHROW(with_view.gameplay_random().require_choices_consumed());
}

TEST_CASE("A command choice mismatch retains committed effects and reports its category",
          "[session][commands][choices]") {
    state::ProcessContext process;
    SessionRuntime runtime(process, RuntimeId{1});
    const base::ChoiceKey point{"test.choice", "test.operation/one", 0};
    runtime.register_command(definition("test.action", [point](auto& context, auto) {
        auto& gold = context.world.civilizations[1].gold;
        gold += context.gameplay.primary_modulo(point, 3);
        context.effect("test.credit", {{"amount", gold}});
        return done(context.gameplay.primary_code(
            base::ChoiceKey{"test.missing", "test.operation/one", 0}));
    }));
    runtime.begin_client_session({1, 2});
    base::GameplayRandom controlled(base::ControlledChoices{{base::make_choice(
                                        point, base::PrimaryModulo{3}, std::int32_t{8})}},
                                    "session");
    runtime.gameplay_random().restore(controlled.checkpoint());
    const auto player = client(runtime);
    const auto command = id(player.submit(request()));
    runtime.world().civilizations[1].gold = 10;
    REQUIRE(runtime.advance_one());
    const auto result = player.result(command);
    REQUIRE(result.has_value());
    CHECK(result->status == CommandStatus::failed);
    CHECK(result->code == "random_choice_missing");
    REQUIRE(result->committed.size() == 1);
    CHECK(runtime.world().civilizations[1].gold == 12);
    const auto random = runtime.gameplay_random().checkpoint();
    CHECK(random.draws == 0);
    REQUIRE(random.trace->events.size() == 1);
    CHECK(random.trace->events[0].key == point);
    CHECK_FALSE(runtime.advance_one());
}

TEST_CASE("Command descriptors validate shape without performing execution",
          "[session][commands]") {
    state::ProcessContext process;
    SessionRuntime runtime(process, RuntimeId{1});
    int executions = 0;
    auto command = definition("test.action", [&](auto&, auto) {
        ++executions;
        return done(3);
    });
    auto invalid = command;
    invalid.descriptor.version = 2;
    CHECK_THROWS_AS(runtime.register_command(invalid), std::invalid_argument);
    invalid = command;
    invalid.descriptor.arguments.push_back(invalid.descriptor.arguments.front());
    CHECK_THROWS_AS(runtime.register_command(invalid), std::invalid_argument);
    runtime.register_command(command);
    CHECK_THROWS_AS(runtime.register_command(command), std::invalid_argument);
    command.descriptor.name = "test.editor";
    command.descriptor.access = AccessClass::developer;
    command.execute = [](const auto&) { return std::make_unique<Fake>(decision()); };
    runtime.register_command(command);
    runtime.begin_client_session({1, 2});
    const auto player = client(runtime);
    REQUIRE(player.commands().size() == 1);
    CHECK(player.commands()[0].name == "test.action");
    CHECK(player.submit({"test.editor", {{"amount", std::int32_t{1}}}}).code ==
          "command_not_available");
    CHECK(player.submit({"test.action", {}}).code == "missing_value:amount");
    CHECK(player.submit({"test.action", {{"amount", true}}}).code == "wrong_type:amount");
    CHECK(player.submit({"test.action", {{"amount", 1}, {"amount", 2}}}).code ==
          "duplicate_value:amount");
    CHECK(player.submit({"test.action", {{"amount", 1}, {"unknown", 2}}}).code ==
          "unknown_value:unknown");
    CHECK(player
              .submit({"test.action",
                       {{"amount", 1},
                        {"target", UnitTarget{state::UnitSlot{2048}, state::UnitToken{1}}}}})
              .status == CommandStatus::rejected);
    CHECK(executions == 0);
    CHECK_FALSE(runtime.advance_one());
    const auto developer = runtime.bind(player.identity().participant, ControllerKind::scripted,
                                        AccessClass::developer);
    CHECK(developer.commands().size() == 2);
    const auto private_command = developer.submit({"test.editor", {{"amount", 1}}});
    CHECK(runtime.advance_one());
    const auto prompt = interaction(developer, id(private_command));
    CHECK_FALSE(player.result(id(private_command)).has_value());
    CHECK(player.updates().empty());
    CHECK_FALSE(player.reply(prompt, 2).accepted);
    CHECK_FALSE(player.cancel(id(private_command)).accepted);
    CHECK(developer.cancel(id(private_command)).accepted);
    CHECK(runtime.advance_one());
    CHECK_THROWS_AS(runtime.register_command(command), std::logic_error);
}

TEST_CASE("Local submissions own their inputs and run in explicit FIFO order",
          "[session][commands]") {
    state::ProcessContext process;
    SessionRuntime runtime(process, RuntimeId{1});
    std::vector<std::int32_t> observed;
    auto command = definition("test.action", {});
    command.execute = [&](const CommandRequest& request) {
        const auto amount = std::get<std::int32_t>(command_value(request.arguments, "amount"));
        return std::make_unique<Fake>([&, amount](auto& context, auto) {
            observed.push_back(amount);
            context.world.civilizations[1].gold += amount;
            context.effect("test.credit", {{"amount", amount}});
            return done(context.world.civilizations[1].gold);
        });
    };
    runtime.register_command(command);
    runtime.begin_client_session({1, 2});
    const auto first = client(runtime);
    const auto second = runtime.bind(first.identity().participant, ControllerKind::automation);
    auto input = request(5);
    const auto a = first.submit(input);
    input.arguments[0].value = std::int32_t{99};
    const auto b = second.submit(request(7));
    CHECK(a.status == CommandStatus::pending);
    CHECK(id(a) != id(b));
    CHECK(runtime.world().civilizations[1].gold == 0);
    CHECK(observed.empty());
    CHECK_THROWS_AS(runtime.end_client_session(), std::logic_error);
    CHECK(runtime.advance_one());
    CHECK(first.result(id(a))->status == CommandStatus::completed);
    CHECK(first.result(id(b))->status == CommandStatus::pending);
    CHECK(runtime.advance_one());
    CHECK(observed == std::vector<std::int32_t>{5, 7});
    CHECK(runtime.world().civilizations[1].gold == 12);
    CHECK_FALSE(runtime.has_pending_commands());
    const auto updates = first.updates();
    REQUIRE(updates.size() >= 4);
    CHECK(updates.back().correlation == b.correlation);
    CHECK(updates.back().committed[0].index == 1);
    CHECK(first.updates().empty());
    CHECK(runtime.take_requests().empty());
    runtime.end_client_session();
}

TEST_CASE("Decisions preserve callbacks and distinguish completion cancellation and timeout",
          "[session][commands]") {
    state::ProcessContext process;
    SessionRuntime runtime(process, RuntimeId{1});
    runtime.register_command(definition("test.action", decision([](auto& context) {
                                            context.world.civilizations[1].gold = 37;
                                            context.effect("test.callback", {});
                                        })));
    runtime.begin_client_session({1, 2});
    runtime.world().civilizations[1].gold = 20;
    const auto player = client(runtime);
    const auto command = player.submit(request());
    CHECK(runtime.advance_one());
    CHECK(player.result(id(command))->status == CommandStatus::awaiting_interaction);
    CHECK(runtime.world().civilizations[1].gold == 37);
    const auto choice = interaction(player, id(command));
    SECTION("headless reply uses the live value") {
        CHECK_FALSE(player.reply(choice, 99).accepted);
        CHECK(player.reply(choice, 4).accepted);
        CHECK_FALSE(player.reply(choice, 4).accepted);
        CHECK(player.result(id(command))->status == CommandStatus::pending);
        CHECK(runtime.advance_one());
        CHECK(player.result(id(command))->status == CommandStatus::completed);
        CHECK(runtime.world().civilizations[1].gold == 41);
        CHECK(player.result(id(command))->committed.size() == 3);
    }
    SECTION("cancel keeps already committed changes") {
        CHECK(player.cancel(id(command)).accepted);
        CHECK(runtime.advance_one());
        CHECK(player.result(id(command))->status == CommandStatus::cancelled);
        CHECK(runtime.world().civilizations[1].gold == 37);
        CHECK(player.result(id(command))->committed.size() == 2);
    }
    SECTION("deadline supplies a distinct timeout input and its default") {
        CHECK(runtime.expire_interactions(49) == 0);
        CHECK(runtime.expire_interactions(50) == 1);
        CHECK(runtime.expire_interactions(51) == 0);
        CHECK_FALSE(player.reply(choice, 4).accepted);
        CHECK(runtime.advance_one());
        CHECK(runtime.world().civilizations[1].gold == 39);
    }
}

TEST_CASE("Revocation preserves admitted continuations and a replacement can answer",
          "[session][commands]") {
    state::ProcessContext process;
    SessionRuntime runtime(process, RuntimeId{1});
    runtime.register_command(definition("test.action", decision()));
    runtime.begin_client_session({1, 2});
    const auto old = client(runtime);
    const auto identity = old.identity();
    const auto command = old.submit(request());
    runtime.revoke(identity.controller);
    CHECK_THROWS_AS(old.submit(request()), AccessError);
    CHECK(runtime.advance_one());
    CHECK(runtime.world().civilizations[1].gold == -5);
    const auto replacement = runtime.bind(identity.participant, ControllerKind::automation);
    const auto updates = replacement.updates();
    REQUIRE(updates.size() == 1);
    CHECK(updates[0].status == CommandStatus::awaiting_interaction);
    CHECK(updates[0].committed.size() == 1);
    CHECK(replacement.reply(interaction(replacement, id(command)), 2).accepted);
    CHECK(runtime.advance_one());
    CHECK(replacement.result(id(command))->status == CommandStatus::completed);
    CHECK_THROWS_AS(old.updates(), AccessError);
}

TEST_CASE("Interaction recipients see their prompt without the actor's private effects",
          "[session][commands]") {
    state::ProcessContext process;
    SessionRuntime runtime(process, RuntimeId{1});
    // Participant IDs are runtime-assigned; the operation captures the explicit recipient after
    // setup.
    std::optional<ParticipantId> recipient;
    auto command = definition("test.action", {});
    command.execute = [&](const auto&) { return std::make_unique<Fake>(decision({}, recipient)); };
    runtime.register_command(command);
    runtime.begin_client_session({1, 2});
    const auto actor = client(runtime);
    const auto other = client(runtime, 2);
    const auto outsider = client(runtime, 3);
    recipient = other.identity().participant;
    const auto submitted = actor.submit(request());
    CHECK(runtime.advance_one());
    CHECK_FALSE(actor.result(id(submitted))->interaction.has_value());
    CHECK_FALSE(outsider.result(id(submitted)).has_value());
    const auto prompt = other.result(id(submitted));
    REQUIRE(prompt.has_value());
    REQUIRE(prompt->interaction.has_value());
    CHECK(prompt->committed.empty());
    CHECK(prompt->result.empty());
    CHECK_FALSE(actor.reply(prompt->interaction->id, 2).accepted);
    CHECK_FALSE(outsider.reply(prompt->interaction->id, 2).accepted);
    other.updates();
    SECTION("timeout withdraws the prompt") {
        CHECK(runtime.expire_interactions(50) == 1);
    }
    SECTION("the decision recipient can cancel its prompt") {
        CHECK(other.cancel(id(submitted)).accepted);
    }
    const auto withdrawal = other.updates();
    REQUIRE(withdrawal.size() == 1);
    CHECK_FALSE(withdrawal[0].interaction.has_value());
    CHECK(withdrawal[0].committed.empty());
    CHECK(runtime.advance_one());
    CHECK_FALSE(other.result(id(submitted)).has_value());
    CHECK(outsider.updates().empty());
}

TEST_CASE("Owning continuations revalidate targets and retain effects on failure",
          "[session][commands]") {
    state::ProcessContext process;
    SessionRuntime runtime(process, RuntimeId{1});
    auto command = definition("test.action", {});
    command.execute = [](const auto& request) {
        const auto target = std::get<UnitTarget>(command_value(request.arguments, "target"));
        return std::make_unique<Fake>([target, phase = 0](auto& context, auto) mutable {
            if (!context.world.entities.units.live(target.slot) ||
                context.world.entities.units.at(target.slot).token != target.token)
                return OperationStep{CommandStatus::failed, {}, "stale_target", {}};
            if (phase++ == 0) {
                context.world.civilizations[1].gold = 9;
                context.effect("test.prefix", {});
                return OperationStep{CommandStatus::pending, {}, {}, {}};
            }
            return done(9);
        });
    };
    runtime.register_command(command);
    runtime.begin_client_session({1, 2});
    auto& units = runtime.world().entities.units;
    units.set_end(1);
    units.at(state::UnitSlot{0}).token = state::UnitToken{7};
    const auto player = client(runtime);
    auto input = request();
    input.arguments.push_back({"target", UnitTarget{state::UnitSlot{0}, state::UnitToken{7}}});
    const auto submitted = player.submit(input);
    CHECK(runtime.advance_one());
    CHECK_FALSE(runtime.advance_one());
    units.at(state::UnitSlot{0}).token = state::UnitToken{8};
    CHECK(runtime.continue_command(id(submitted)));
    CHECK_FALSE(runtime.continue_command(id(submitted)));
    CHECK(runtime.advance_one());
    CHECK(player.result(id(submitted))->code == "stale_target");
    CHECK(player.result(id(submitted))->committed.size() == 1);
    CHECK(runtime.world().civilizations[1].gold == 9);
}

TEST_CASE("Fake peers execute requests once and apply resolved values without rerolling",
          "[session][commands]") {
    state::ProcessContext local_process, host_process;
    SessionRuntime local(local_process, RuntimeId{1}), host(host_process, RuntimeId{2});
    int executed = 0, applied = 0;
    auto command = definition("test.action", [&](auto& context, auto) {
        ++executed;
        const auto value = context.gameplay.primary_modulo("test.resolution", 100);
        context.world.civilizations[1].gold = value;
        context.effect("test.executed", {{"value", value}});
        return done(value);
    });
    host.register_command(command);
    command.admit = [](const auto&, const auto&, const auto&) {
        return Admission{{}, CommandRoute::await_peer, ConnectionId{7}};
    };
    command.apply_result = [&](const auto&, const auto& result) {
        const auto value = std::get<std::int32_t>(command_value(result, "value"));
        return std::make_unique<Fake>([&, value](auto& context, auto) {
            ++applied;
            context.world.civilizations[1].gold = value;
            context.effect("test.applied", {{"value", value}});
            return done(value);
        });
    };
    local.register_command(command);
    local.begin_client_session({11, 12});
    host.begin_client_session({21, 22});
    const auto player = client(local);
    const auto seat = host.add_participant(state::CivilizationSlot{1}, ConnectionId{9});
    const auto random_before = local.gameplay_random().checkpoint();
    const auto submitted = player.submit(request());
    CHECK_FALSE(local.advance_one());
    auto outgoing = local.take_requests();
    REQUIRE(outgoing.size() == 1);
    CHECK(outgoing[0].destination == ConnectionId{7});
    const auto accepted =
        host.receive_request(ConnectionId{9}, seat, outgoing[0].correlation, outgoing[0].request);
    CHECK(host.receive_request(ConnectionId{9}, seat, outgoing[0].correlation, outgoing[0].request)
              .correlation == accepted.correlation);
    CHECK(host.advance_one());
    CHECK_FALSE(host.advance_one());
    auto replies = host.take_replies();
    REQUIRE(replies.size() == 1);
    CHECK_FALSE(local.receive_result(ConnectionId{8}, replies[0]).accepted);
    auto bad = replies[0];
    bad.result[0].value = true;
    CHECK_FALSE(local.receive_result(ConnectionId{7}, bad).accepted);
    CHECK(local.receive_result(ConnectionId{7}, replies[0]).accepted);
    CHECK_FALSE(local.receive_result(ConnectionId{7}, replies[0]).accepted);
    CHECK(local.advance_one());
    CHECK(executed == 1);
    CHECK(applied == 1);
    CHECK(local.world().civilizations[1].gold == host.world().civilizations[1].gold);
    CHECK(local.gameplay_random().checkpoint() == random_before);
    CHECK(player.result(id(submitted))->status == CommandStatus::completed);
    // Duplicate delivery reissues the cached reply, never reruns execution.
    host.receive_request(ConnectionId{9}, seat, outgoing[0].correlation, outgoing[0].request);
    CHECK(host.take_replies().size() == 1);
    auto changed = outgoing[0].request;
    changed.arguments[0].value = std::int32_t{99};
    CHECK(host.receive_request(ConnectionId{9}, seat, outgoing[0].correlation, changed).status ==
          CommandStatus::rejected);
    CHECK(host.take_replies().size() == 1);
    CHECK(executed == 1);
}

TEST_CASE("Disconnect cannot discard a pending command or its required peer reply",
          "[session][commands]") {
    state::ProcessContext process;
    SessionRuntime host(process, RuntimeId{2});
    host.register_command(definition("test.action", decision()));
    host.begin_client_session({1, 2});
    const auto seat = host.add_participant(state::CivilizationSlot{1}, ConnectionId{9});
    const auto old = host.bind(seat, ControllerKind::scripted);
    const CommandCorrelation origin{RuntimeId{1}, SessionId{1}, CommandId{17}};
    const auto submitted = host.receive_request(ConnectionId{9}, seat, origin, request());
    CHECK(host.advance_one());
    host.disconnect(ConnectionId{9});
    CHECK_THROWS_AS(old.reply(InteractionId{1}, 2), AccessError);
    CHECK_THROWS_AS(host.begin_client_session({3, 4}), std::logic_error);
    host.remove_participant(seat);
    CHECK(host.cancel_command(id(submitted)).accepted);
    CHECK(host.advance_one());
    CHECK(host.world().civilizations[1].gold == -5);
    CHECK_FALSE(host.has_pending_commands());
    CHECK_THROWS_AS(host.end_client_session(), std::logic_error);
    const auto replies = host.take_replies();
    REQUIRE(replies.size() == 1);
    CHECK(replies[0].destination == ConnectionId{9});
    CHECK(replies[0].correlation == origin);
    CHECK(replies[0].status == CommandStatus::cancelled);
    host.end_client_session();
}

TEST_CASE("Admission revocation and execution exceptions preserve explicit outcomes",
          "[session][commands]") {
    state::ProcessContext process;
    SessionRuntime runtime(process, RuntimeId{1});
    std::optional<ControllerId> revoke;
    auto command = definition("test.action", [](auto& context, auto) -> OperationStep {
        context.world.civilizations[1].gold = 31;
        context.effect("test.before_exception", {});
        throw std::runtime_error("Authored callback failure");
    });
    command.admit = [&](const auto&, const auto&, const auto&) {
        if (revoke)
            runtime.revoke(*revoke);
        return Admission{};
    };
    runtime.register_command(command);
    runtime.begin_client_session({1, 2});
    const auto old = client(runtime);
    const auto seat = old.identity().participant;
    revoke = old.identity().controller;
    CHECK(old.submit(request()).code == "binding_changed_during_admission");
    CHECK_FALSE(runtime.advance_one());
    revoke.reset();
    const auto replacement = runtime.bind(seat, ControllerKind::scripted);
    const auto submitted = replacement.submit(request());
    CHECK(runtime.advance_one());
    const auto failed = replacement.result(id(submitted));
    REQUIRE(failed.has_value());
    CHECK(failed->status == CommandStatus::failed);
    CHECK(failed->code == "operation_exception");
    CHECK(failed->committed.size() == 1);
    CHECK(runtime.world().civilizations[1].gold == 31);
}

TEST_CASE("Authority changes are checked at admission and by the owning execution",
          "[session][commands]") {
    state::ProcessContext process;
    SessionRuntime host(process, RuntimeId{2});
    bool authority = true;
    int executions = 0;
    auto command = definition("test.action", [&](auto&, auto) {
        if (!authority)
            return OperationStep{CommandStatus::failed, {}, "authority_changed", {}};
        ++executions;
        return done(3);
    });
    command.admit = [&](const auto&, const auto&, const auto&) {
        return authority ? Admission{} : Admission{std::string{"not_executor"}};
    };
    host.register_command(command);
    host.begin_client_session({1, 2});
    const auto seat = host.add_participant(state::CivilizationSlot{1}, ConnectionId{9});
    const CommandCorrelation origin{RuntimeId{1}, SessionId{1}, CommandId{1}};
    SECTION("admission denies a nonexecutor") {
        authority = false;
        CHECK(host.receive_request(ConnectionId{9}, seat, origin, request()).status ==
              CommandStatus::rejected);
        CHECK_FALSE(host.advance_one());
        const auto replies = host.take_replies();
        REQUIRE(replies.size() == 1);
        CHECK(replies[0].code == "not_executor");
    }
    SECTION("an accepted request revalidates when execution starts") {
        CHECK(host.receive_request(ConnectionId{9}, seat, origin, request()).status ==
              CommandStatus::pending);
        authority = false;
        CHECK(host.advance_one());
        const auto replies = host.take_replies();
        REQUIRE(replies.size() == 1);
        CHECK(replies[0].status == CommandStatus::failed);
        CHECK(replies[0].code == "authority_changed");
    }
    CHECK(executions == 0);
    CHECK_FALSE(host.has_pending_commands());
}
