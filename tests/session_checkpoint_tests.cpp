// M1-01: authored projection/operation codecs, not installed game commands.
#include "session/runtime.hpp"
#include "base/hash.hpp"
#include "formats/content_json.hpp"
#include "formats/preference_io.hpp"
#include <catch2/catch_test_macros.hpp>
#include <algorithm>
#include <bit>
#include <tuple>

using namespace openciv;
using namespace openciv::session;
namespace {
const std::string content_hash = [] {
    constexpr std::string_view content = "test immutable constructor-default content, version 1";
    return base::sha256({reinterpret_cast<const std::uint8_t*>(content.data()), content.size()});
}();
void admit(bool value) {
    if (!value)
        throw std::invalid_argument("Outside authored checkpoint profile");
}
void u32(CheckpointBytes& out, std::uint32_t value) {
    for (unsigned i = 0; i < 4; ++i)
        out.push_back(static_cast<std::uint8_t>(value >> (8 * i)));
}
std::uint32_t read32(std::span<const std::uint8_t> in, std::size_t offset) {
    admit(offset <= in.size() && in.size() - offset >= 4);
    std::uint32_t value = 0;
    for (unsigned i = 0; i < 4; ++i)
        value |= static_cast<std::uint32_t>(in[offset + i]) << (8 * i);
    return value;
}
void append(CheckpointBytes& out, const CheckpointBytes& value) {
    out.insert(out.end(), value.begin(), value.end());
}
// Declared defaults are newly constructed GameState/ProcessContext values.
// Treasury, tokens and all preferences are the only mutable state in this profile.
void fresh_except_treasury(const state::GameState& game, const state::ProcessContext& process) {
    auto cold_process = std::make_unique<state::ProcessContext>();
    auto cold = std::make_unique<state::GameState>(*cold_process);
    admit(game.entities.units.end() == 0 && game.entities.cities.end() == 0);
    admit(std::equal(game.entities.units.storage().begin(), game.entities.units.storage().end(),
                     cold->entities.units.storage().begin()));
    admit(std::equal(game.entities.cities.storage().begin(), game.entities.cities.storage().end(),
                     cold->entities.cities.storage().begin()));
    const auto& s = game.unit_stacks;
    admit(s.suppression == 0 && !s.animation_active && !s.detach_active && !s.attach_active &&
          !s.relocate_active && !s.bottom_active && s.detach_reply == 0 && s.attach_reply == 0 &&
          s.relocate_reply == 0 && s.bottom_reply == 0);
    admit(!game.unit_deletion.active && game.unit_deletion.reply == 0 &&
          game.unit_totals == cold->unit_totals && !game.turn_loop_active &&
          game.movement_mode == 0 &&
          game.civilization_color_flags == cold->civilization_color_flags);
    admit(game.globals == cold->globals && game.world_setup == cold->world_setup &&
          game.names == cold->names &&
          game.pending_leader_overrides == cold->pending_leader_overrides);
    cold->civilizations[1].gold = game.civilizations[1].gold;
    admit(game.civilizations == cold->civilizations && game.multiplayer == cold->multiplayer &&
          !game.events.loaded && game.events.collection.pristine() &&
          game.power_history == cold->power_history && game.eliminations == cold->eliminations &&
          game.scenario == cold->scenario && game.layout == cold->layout &&
          game.views == cold->views && game.mood == cold->mood);
    admit(!game.map.allocated() && game.map.storage_revision() == 0 && game.map.pattern_seed == 0 &&
          game.map.stored_topology == 0 && game.map.regions == cold->map.regions &&
          game.map.remembered_barbarian.empty());
    admit(process.fallback == cold_process->fallback &&
          process.text_parameters.numbers == cold_process->text_parameters.numbers &&
          process.text_parameters.strings == cold_process->text_parameters.strings &&
          process.text_parameters.line.empty());
    admit(process.rules && process.rules->definitions == cold_process->rules->definitions &&
          process.rules->names == cold_process->rules->names &&
          process.rules->leaders == cold_process->rules->leaders &&
          !process.rules->double_movement_applied && process.rules->text.size() == 0);
    const auto& labels = process.rules->labels;
    admit(labels.status_language == 0 && labels.declared_count == 0 && labels.entries.empty() &&
          labels.popups == cold_process->rules->labels.popups);
}
CheckpointProfile profile() {
    return {"test.local-foundation/1",
            [](const state::GameState& world, const state::ProcessContext& process) {
                fresh_except_treasury(world, process);
                CheckpointBytes out{'F', 'N', 'D', 1};
                u32(out, std::bit_cast<std::uint32_t>(world.civilizations[1].gold));
                u32(out, process.tokens.next_unit);
                u32(out, process.tokens.next_city);
                append(out, formats::encode_ordinary_preferences(process.ordinary_preferences));
                append(out,
                       formats::encode_multiplayer_preferences(process.multiplayer_preferences));
                return out;
            },
            [](std::span<const std::uint8_t> data, state::GameState& world,
               state::ProcessContext& process) {
                admit(data.size() == 16 + formats::ordinary_preferences_size +
                                         formats::multiplayer_preferences_size &&
                      data[0] == 'F' && data[1] == 'N' && data[2] == 'D' && data[3] == 1);
                world.civilizations[1].gold = std::bit_cast<std::int32_t>(read32(data, 4));
                process.tokens = {read32(data, 8), read32(data, 12)};
                process.ordinary_preferences = formats::decode_ordinary_preferences(
                    data.subspan(16, formats::ordinary_preferences_size));
                process.multiplayer_preferences = formats::decode_multiplayer_preferences(
                    data.subspan(16 + formats::ordinary_preferences_size));
            }};
}
base::ChoiceKey point(std::string call) {
    return {"test.checkpoint", std::move(call), 0};
}
class Decision final : public Continuation {
public:
    std::int32_t amount = 0, selected = 0;
    unsigned phase = 0;
    explicit Decision(std::int32_t input) : amount(input) {}
    OperationStep resume(OperationContext& context, OperationInput input) override {
        auto& treasury = context.world.civilizations[1].gold;
        if (phase == 0) {
            ++phase;
            selected = context.gameplay.primary_modulo(point("prefix"), 7);
            context.presentation.secondary_unit(point("presentation-prefix"));
            treasury = base::subtract32(treasury, amount);
            context.effect("test.debit", {{"amount", amount}, {"selected", selected}});
            InteractionSpec prompt;
            prompt.prompt = "Authored checkpoint decision";
            prompt.choices = {2, 4};
            prompt.deadline = 50;
            prompt.default_choice = 2;
            prompt.target = std::string("owned target");
            return {CommandStatus::awaiting_interaction, {}, {}, prompt};
        }
        if (input.kind == OperationInputKind::cancel)
            return {CommandStatus::cancelled, {}, "cancelled", {}};
        admit(input.choice.has_value() && phase == 1);
        const auto suffix = context.gameplay.primary_modulo(point("suffix"), 5);
        context.presentation.primary_code(point("presentation-suffix"));
        treasury = base::add32(treasury, selected + *input.choice + suffix);
        context.effect("test.credit", {{"treasury", treasury}});
        phase = 2;
        return {CommandStatus::completed, {{"treasury", treasury}}, {}, {}};
    }
};
CommandDefinition definition() {
    CommandDefinition d;
    d.descriptor = {1,
                    "test.checkpoint",
                    "Authored checkpoint proof",
                    AccessClass::player,
                    {{"amount", ValueKind::integer}},
                    {{"treasury", ValueKind::integer}}};
    d.admit = [](const auto&, const auto&, const auto&) { return Admission{}; };
    d.execute = [](const CommandRequest& request) {
        return std::make_unique<Decision>(
            std::get<std::int32_t>(command_value(request.arguments, "amount")));
    };
    d.checkpoint = ContinuationCodec{
        "test.decision", 1,
        [](const Continuation& continuation) {
            const auto& decision = dynamic_cast<const Decision&>(continuation);
            admit(decision.phase == 1);
            CheckpointBytes result;
            u32(result, 1);
            u32(result, std::bit_cast<std::uint32_t>(decision.amount));
            u32(result, static_cast<std::uint32_t>(decision.selected));
            return result;
        },
        [](std::span<const std::uint8_t> data, const CommandRequest& request,
           const CommandUpdate& update) {
            admit(data.size() == 12 && read32(data, 0) == 1 && read32(data, 8) < 7 &&
                  update.committed.size() == 1);
            const auto amount = std::bit_cast<std::int32_t>(read32(data, 4));
            admit(amount == std::get<std::int32_t>(command_value(request.arguments, "amount")));
            auto result = std::make_unique<Decision>(amount);
            result->phase = 1;
            result->selected = static_cast<std::int32_t>(read32(data, 8));
            return result;
        }};
    return d;
}
struct Fixture {
    state::ProcessContext process;
    SessionRuntime runtime;
    ParticipantId participant{0};
    explicit Fixture(std::uint64_t identity = 1, bool start = true)
        : runtime(process, RuntimeId{identity}) {
        runtime.register_command(definition());
        if (start) {
            runtime.begin_client_session({11, 13});
            participant = runtime.add_participant(state::CivilizationSlot{1});
        }
    }
    PlayerClient client() { return runtime.bind(participant, ControllerKind::scripted); }
    void restore(const CheckpointBytes& data) {
        runtime.restore_checkpoint(data, profile(), content_hash);
        participant = ParticipantId{1};
    }
    void controlled() {
        runtime.gameplay_random() = base::GameplayRandom(
            base::ControlledChoices{
                {base::make_choice(point("prefix"), base::PrimaryModulo{7}, std::int32_t{9}),
                 base::make_choice(point("suffix"), base::PrimaryModulo{5}, std::int32_t{8})}},
            "session");
        runtime.presentation_random() = base::PresentationRandom(
            base::ControlledChoices{
                {base::make_choice(point("presentation-prefix"), base::SecondaryUnit{}, UINT32_MAX),
                 base::make_choice(point("presentation-suffix"), base::PrimaryCode{},
                                   std::int32_t{7})}},
            "session");
    }
};
CommandUpdate suspend(Fixture& fixture, PlayerClient& client) {
    fixture.runtime.world().civilizations[1].gold = 100;
    const auto submitted = client.submit({"test.checkpoint", {{"amount", std::int32_t{5}}}});
    REQUIRE(fixture.runtime.advance_one());
    return *client.result(submitted.correlation.command);
}
CheckpointBytes altered(const CheckpointBytes& encoded,
                        const std::function<void(jt::Json&)>& change) {
    const std::string text(encoded.begin(), encoded.end());
    auto outer = formats::content::parse_json(text, "test");
    auto inner = formats::content::parse_json(outer.value["payload"].getString(), "test");
    change(inner.value);
    const auto payload = inner.value.toString();
    outer.value["payload"] = payload;
    outer.value["sha256"] =
        base::sha256({reinterpret_cast<const std::uint8_t*>(payload.data()), payload.size()});
    const auto result = outer.value.toString();
    return {result.begin(), result.end()};
}
} // namespace

TEST_CASE("M1 checkpoint idle profile restores separate process and all preferences",
          "[session][checkpoint]") {
    Fixture source, destination(2, false);
    source.process.tokens = {UINT32_MAX, 0};
    source.process.ordinary_preferences =
        formats::decode_ordinary_preferences(formats::Bytes(72, 0xa5));
    source.process.multiplayer_preferences =
        formats::decode_multiplayer_preferences(formats::Bytes(444, 0x5a));
    source.runtime.world().civilizations[1].gold = INT32_MIN;
    source.runtime.gameplay_random() = base::GameplayRandom(11, "test/custom-gameplay");
    source.runtime.presentation_random() = base::PresentationRandom(13, "test/custom-presentation");
    source.runtime.gameplay_random().primary_code("test.advance");
    const auto saved = source.runtime.checkpoint(profile(), content_hash);
    destination.restore(saved);
    CHECK(destination.process.tokens == source.process.tokens);
    CHECK(destination.process.ordinary_preferences == source.process.ordinary_preferences);
    CHECK(destination.process.multiplayer_preferences == source.process.multiplayer_preferences);
    CHECK(destination.runtime.world().civilizations[1].gold == INT32_MIN);
    CHECK(destination.runtime.gameplay_random().checkpoint() ==
          source.runtime.gameplay_random().checkpoint());
    CHECK(destination.runtime.presentation_random().checkpoint() ==
          source.runtime.presentation_random().checkpoint());
    auto client = destination.client();
    CHECK(client.own_units().records.empty());
    CHECK_FALSE(destination.runtime.has_pending_commands());
    CHECK_FALSE(destination.runtime.advance_one());
}

TEST_CASE("M1 suspended checkpoint resumes generated and controlled commands once",
          "[session][checkpoint]") {
    for (bool controlled : {false, true}) {
        Fixture source, destination(2);
        if (controlled)
            source.controlled();
        else {
            source.runtime.gameplay_random().record_choices(2);
            source.runtime.presentation_random().record_choices(2);
        }
        auto client = source.client(), stale = destination.client();
        const auto pending = suspend(source, client);
        const auto snapshot = source.runtime.checkpoint(profile(), content_hash);
        destination.restore(snapshot);
        CHECK_THROWS_AS(stale.own_units(), AccessError);
        auto restored = destination.client();
        REQUIRE(restored.result(pending.correlation.command) == pending);
        CHECK(destination.runtime.world().civilizations[1].gold == 95);
        CHECK(destination.runtime.gameplay_random().checkpoint() ==
              source.runtime.gameplay_random().checkpoint());
        CHECK_THROWS_AS(destination.runtime.gameplay_random().primary_modulo(point("prefix"), 7),
                        base::ChoiceError);
        REQUIRE(client.reply(pending.interaction->id, 4).accepted);
        REQUIRE(restored.reply(pending.interaction->id, 4).accepted);
        CHECK_FALSE(restored.reply(pending.interaction->id, 4).accepted);
        REQUIRE(source.runtime.advance_one());
        REQUIRE(destination.runtime.advance_one());
        CHECK(restored.result(pending.correlation.command) ==
              client.result(pending.correlation.command));
        CHECK(restored.result(pending.correlation.command)->committed.size() == 2);
        CHECK(destination.runtime.world().civilizations[1].gold ==
              source.runtime.world().civilizations[1].gold);
        CHECK(destination.runtime.gameplay_random().checkpoint() ==
              source.runtime.gameplay_random().checkpoint());
        CHECK(destination.runtime.presentation_random().checkpoint() ==
              source.runtime.presentation_random().checkpoint());
        CHECK_NOTHROW(destination.runtime.gameplay_random().require_choices_consumed());
        CHECK_NOTHROW(destination.runtime.presentation_random().require_choices_consumed());
        CHECK_FALSE(restored.reply(pending.interaction->id, 4).accepted);
    }
}

TEST_CASE("M1 restored cancellation and timeout retain accepted prefix", "[session][checkpoint]") {
    for (bool cancel : {false, true}) {
        Fixture source, destination(2, false);
        source.controlled();
        auto client = source.client();
        const auto pending = suspend(source, client);
        destination.restore(source.runtime.checkpoint(profile(), content_hash));
        auto restored = destination.client();
        if (cancel) {
            REQUIRE(client.cancel(pending.correlation.command).accepted);
            REQUIRE(restored.cancel(pending.correlation.command).accepted);
        } else {
            CHECK(destination.runtime.expire_interactions(49) == 0);
            CHECK(source.runtime.expire_interactions(50) == 1);
            CHECK(destination.runtime.expire_interactions(50) == 1);
        }
        REQUIRE(source.runtime.advance_one());
        REQUIRE(destination.runtime.advance_one());
        CHECK(restored.result(pending.correlation.command) ==
              client.result(pending.correlation.command));
        CHECK(destination.runtime.world().civilizations[1].gold ==
              source.runtime.world().civilizations[1].gold);
        CHECK(destination.runtime.gameplay_random().checkpoint() ==
              source.runtime.gameplay_random().checkpoint());
        if (cancel) {
            CHECK(destination.runtime.world().civilizations[1].gold == 95);
            CHECK(restored.result(pending.correlation.command)->committed.size() == 1);
        }
    }
}

TEST_CASE("M1 checkpoint rejects corrupt admission without changing destination",
          "[session][checkpoint]") {
    Fixture source, destination(2);
    auto client = source.client(), existing = destination.client();
    const auto pending = suspend(source, client);
    (void)pending;
    const auto snapshot = source.runtime.checkpoint(profile(), content_hash);
    destination.runtime.world().civilizations[1].gold = 271;
    destination.process.tokens = {77, 88};
    destination.process.ordinary_preferences.game_options = 0x12345678;
    const auto before = destination.runtime.checkpoint(profile(), content_hash);
    const std::vector<std::function<void(jt::Json&)>> changes{
        [](auto& v) { v["content"] = std::string(64, 'b'); },
        [](auto& v) { v["profile"] = "00"; },
        [](auto& v) { v["command"]["codecVersion"] = "999"; },
        [](auto& v) { v["command"]["codec"] = "00"; },
        [](auto& v) { v["command"]["name"] = "00"; },
        [](auto& v) { v["command"]["locals"] = "00"; },
        [](auto& v) { v["world"] = "00"; },
        [](auto& v) { v["counters"]["command"] = "0"; },
        [](auto& v) { v["command"]["interaction"]["recipient"] = "2"; },
        [](auto& v) {
            auto rng = formats::content::parse_json(v["gameplay"].getString(), "test");
            rng.value["policy"]["version"] = "99";
            v["gameplay"] = rng.value.toString();
        }};
    for (const auto& change : changes) {
        CHECK_THROWS(destination.restore(altered(snapshot, change)));
        CHECK(destination.runtime.checkpoint(profile(), content_hash) == before);
        CHECK_NOTHROW(existing.own_units());
    }
    auto broken = snapshot;
    broken[broken.size() / 2] ^= 1;
    CHECK_THROWS(destination.restore(broken));
    broken.resize(12);
    CHECK_THROWS(destination.restore(broken));
    auto unknown =
        formats::content::parse_json(std::string(snapshot.begin(), snapshot.end()), "test");
    unknown.value["format"] = "openciv2-local-checkpoint/999";
    const auto unknown_text = unknown.value.toString();
    CHECK_THROWS(destination.restore(CheckpointBytes(unknown_text.begin(), unknown_text.end())));
    CHECK(destination.runtime.checkpoint(profile(), content_hash) == before);
}

TEST_CASE("M1 checkpoint rejects unsupported world categories and session work",
          "[session][checkpoint]") {
    const std::vector<std::function<void(Fixture&)>> changes{
        [](auto& f) { f.runtime.world().civilizations[2].gold = 1; },
        [](auto& f) { f.runtime.world().entities.units.storage()[2047].damage = 3; },
        [](auto& f) { f.runtime.world().entities.cities.set_end(1); },
        [](auto& f) { f.runtime.world().globals.turn = 1; },
        [](auto& f) { f.runtime.world().map.allocate(world::Geometry(8, 8)); },
        [](auto& f) { f.runtime.world().events.loaded = true; },
        [](auto& f) {
            f.runtime.world().events.collection.append();
            f.runtime.world().events.collection.reset();
        },
        [](auto& f) { f.runtime.world().unit_stacks.attach_active = true; },
        [](auto& f) { f.runtime.world().unit_deletion.reply = -2; },
        [](auto& f) { f.runtime.world().unit_totals[2] = 1; },
        [](auto& f) { f.runtime.world().turn_loop_active = true; },
        [](auto& f) { f.runtime.world().movement_mode = 1; },
        [](auto& f) { f.runtime.world().civilization_color_flags[2] = 1; },
        [](auto& f) { f.runtime.world().pending_leader_overrides[2] = 0; },
        [](auto& f) { f.runtime.world().mood.happy = 1; },
        [](auto& f) { f.runtime.world().map.pattern_seed = 1; },
        [](auto& f) { f.runtime.world().map.regions[2].land = 1; },
        [](auto& f) { f.runtime.world().map.remembered_barbarian.push_back(1); },
        [](auto& f) { f.process.rules->double_movement_applied = true; },
        [](auto& f) { f.process.rules->labels.entries.push_back({}); },
        [](auto& f) { f.process.rules->text.initialize(); },
        [](auto& f) { f.process.fallback.region = 1; },
        [](auto& f) { f.process.text_parameters.line = "retained"; },
        [](auto& f) { f.runtime.add_participant(state::CivilizationSlot{1}); },
        [](auto& f) { f.runtime.reassign_participant(f.participant, state::CivilizationSlot{2}); },
        [](auto& f) {
            f.runtime.reassign_participant(f.participant, state::CivilizationSlot{1},
                                           ConnectionId{9});
        },
        [](auto& f) {
            auto c = f.client();
            c.submit({"test.checkpoint", {{"amount", std::int32_t{1}}}});
        },
        [](auto& f) {
            auto c = f.client();
            const auto p = suspend(f, c);
            c.reply(p.interaction->id, 2);
        },
        [](auto& f) {
            auto c = f.client();
            suspend(f, c);
            c.submit({"test.checkpoint", {{"amount", std::int32_t{1}}}});
        }};
    for (const auto& change : changes) {
        Fixture fixture;
        change(fixture);
        const auto game = fixture.runtime.gameplay_random().checkpoint();
        const auto presentation = fixture.runtime.presentation_random().checkpoint();
        const auto gold = fixture.runtime.world().civilizations[1].gold;
        CHECK_THROWS(fixture.runtime.checkpoint(profile(), content_hash));
        CHECK(fixture.runtime.gameplay_random().checkpoint() == game);
        CHECK(fixture.runtime.presentation_random().checkpoint() == presentation);
        CHECK(fixture.runtime.world().civilizations[1].gold == gold);
    }
}

TEST_CASE("M1 checkpoint replacement revokes bindings and preserves identity high water marks",
          "[session][checkpoint]") {
    Fixture source, destination(2);
    auto original = source.client();
    const auto pending = suspend(source, original);
    const auto snapshot = source.runtime.checkpoint(profile(), content_hash);
    auto old = destination.client();
    for (int i = 0; i < 5; ++i)
        old = destination.client();
    const auto old_identity = old.identity();
    destination.restore(snapshot);
    auto first = destination.client();
    CHECK(first.identity().controller > old_identity.controller);
    CHECK_THROWS_AS(old.reply(pending.interaction->id, 2), AccessError);
    const auto repeated = destination.runtime.checkpoint(profile(), content_hash);
    const auto first_identity = first.identity();
    destination.restore(repeated);
    CHECK_THROWS_AS(first.cancel(pending.correlation.command), AccessError);
    auto final = destination.client();
    CHECK(final.identity().controller > first_identity.controller);
    CHECK(final.result(pending.correlation.command) == pending);
    REQUIRE(final.reply(pending.interaction->id, 2).accepted);
    REQUIRE(destination.runtime.advance_one());
    const auto next = final.submit({"test.checkpoint", {{"amount", std::int32_t{1}}}});
    CHECK(next.correlation.command > pending.correlation.command);
    REQUIRE(destination.runtime.advance_one());
    CHECK(final.result(next.correlation.command)->interaction->id > pending.interaction->id);
}

TEST_CASE("M1 checkpoint rejects peer work without losing routed output", "[session][checkpoint]") {
    Fixture idle;
    const auto snapshot = idle.runtime.checkpoint(profile(), content_hash);
    state::ProcessContext process;
    SessionRuntime remote(process, RuntimeId{9});
    auto d = definition();
    d.admit = [](const auto&, const auto&, const auto&) {
        return Admission{{}, CommandRoute::await_peer, ConnectionId{7}};
    };
    d.apply_result = [](const auto&, const auto&) -> std::unique_ptr<Continuation> {
        throw std::logic_error("This rejection test must never apply a peer result");
    };
    remote.register_command(std::move(d));
    remote.begin_client_session({1, 2});
    auto client =
        remote.bind(remote.add_participant(state::CivilizationSlot{1}), ControllerKind::scripted);
    const auto pending = client.submit({"test.checkpoint", {{"amount", std::int32_t{1}}}});
    REQUIRE(pending.status == CommandStatus::pending);
    const auto random = remote.gameplay_random().checkpoint();
    CHECK_THROWS(remote.checkpoint(profile(), content_hash));
    CHECK_THROWS(remote.restore_checkpoint(snapshot, profile(), content_hash));
    CHECK(client.result(pending.correlation.command) == pending);
    CHECK(remote.gameplay_random().checkpoint() == random);
    const auto requests = remote.take_requests();
    REQUIRE(requests.size() == 1);
    CHECK(requests[0].destination == ConnectionId{7});
    CHECK(requests[0].correlation == pending.correlation);
    CHECK_THROWS(remote.checkpoint(profile(), content_hash));
    // Missing operation codecs fail explicitly instead of serializing closures.
    state::ProcessContext local_process;
    SessionRuntime local(local_process, RuntimeId{10});
    d = definition();
    d.checkpoint.reset();
    local.register_command(std::move(d));
    local.begin_client_session({1, 2});
    auto actor =
        local.bind(local.add_participant(state::CivilizationSlot{1}), ControllerKind::scripted);
    actor.submit({"test.checkpoint", {{"amount", std::int32_t{1}}}});
    REQUIRE(local.advance_one());
    CHECK_THROWS(local.checkpoint(profile(), content_hash));
}
