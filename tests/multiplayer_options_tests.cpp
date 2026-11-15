// F03 section 8, N03 section 11 and F01 wrapping ticks. Independently authored
// channel outcomes exercise the session operation, not original-peer transport.
#include "configuration_fixture.hpp"
#include <limits>
using namespace openciv;
using namespace openciv::session;
namespace {
struct ChannelState {
    unsigned begins = 0, polls = 0, cancellations = 0;
    std::optional<OptionProposal> proposal;
    OptionVoteProgress progress{0, 0};
    std::function<void()> on_begin, on_poll;
};
class Channel final : public OptionVoteChannel {
public:
    explicit Channel(ChannelState& state) : state_(state) {}
    void begin(OptionProposal proposal) override {
        ++state_.begins;
        state_.proposal = std::move(proposal);
        if (state_.on_begin)
            state_.on_begin();
    }
    OptionVoteProgress poll() override {
        ++state_.polls;
        if (state_.on_poll)
            state_.on_poll();
        return state_.progress;
    }
    void cancel() noexcept override { ++state_.cancellations; }

private:
    ChannelState& state_;
};
struct Fixture : test::ConfigurationFixture {
    MultiplayerOptionServices services;
    ChannelState channel;
    std::vector<std::array<std::int32_t, 3>> sent;
    std::uint32_t tick = 100;
    Fixture() {
        auto& multiplayer = runtime.world().multiplayer;
        runtime.world().globals.multiplayer = 3;
        multiplayer.network_host = true;
        multiplayer.player_count = 3;
        multiplayer.saved.simultaneous = -32768;
        process.multiplayer_preferences.double_production = 17;
        process.multiplayer_preferences.double_movement = -19;
        process.rules->definitions.units[0].movement = 64;
        process.rules->definitions.units[1].movement = 3;
        process.rules->definitions.units[1].domain = state::RuleDomain::air;
        process.rules->definitions.units[61].movement = 127;
        services.preferences = preferences;
        services.local_player_name = [] { return "Local player"; };
        services.production_label = [](bool on) { return on ? "Production on" : "Production off"; };
        services.movement_label = [](bool on) { return on ? "Movement on" : "Movement off"; };
        services.separator = [] { return " / "; };
        services.clock = [&] { return tick; };
        services.broadcast_rejection = [&] { trace.push_back("rejected"); };
        services.broadcast_acceptance = [&] { trace.push_back("accepted"); };
        services.redraw_status = [&](std::int32_t argument) {
            CHECK(argument == 1);
            trace.push_back("redraw");
        };
        services.send_units = [&] { trace.push_back("units"); };
        services.send_options = [&](std::int32_t p, std::int32_t m, std::int32_t s) {
            trace.push_back("options");
            sent.push_back({p, m, s});
        };
        services.notice = [&](std::string_view notice) { trace.emplace_back(notice); };
    }
    std::unique_ptr<MultiplayerOptionOperation>
    operation(MultiplayerOptionChoices choices = {true, false, true, true},
              std::int32_t result = 0) {
        return std::make_unique<MultiplayerOptionOperation>(runtime, result, choices, services,
                                                            std::make_unique<Channel>(channel));
    }
    MultiplayerOptionResult apply(MultiplayerOptionChoices choices = {true, false, true, true}) {
        auto op = operation(choices);
        auto result = op->poll();
        if (!result)
            result = op->poll();
        REQUIRE(result);
        return *result;
    }
};
} // namespace
TEST_CASE("N03 cancelled option input has no configuration effects",
          "[configuration][multiplayer]") {
    for (const auto status : {-1, 1, 256}) {
        Fixture f;
        const auto preferences = f.process.multiplayer_preferences;
        auto op = f.operation({}, status);
        CHECK(op->poll() == MultiplayerOptionResult::cancelled);
        CHECK(f.process.multiplayer_preferences == preferences);
        CHECK(f.trace.empty());
        CHECK(f.channel.begins == 0);
    }
}
TEST_CASE("N03 a proposal yields and remains unapplied while replies are pending",
          "[configuration][multiplayer]") {
    Fixture f;
    f.channel.progress = {2, 0};
    const auto random = f.runtime.gameplay_random().checkpoint();
    auto op = f.operation();
    CHECK_FALSE(op->poll());
    REQUIRE(op->proposal());
    CHECK(op->proposal()->sender == "Local player");
    CHECK(op->proposal()->description == "Production on / Movement on");
    CHECK(op->proposal()->expected_votes == 2);
    CHECK(f.channel.begins == 1);
    CHECK(f.channel.polls == 0);
    CHECK_FALSE(op->poll());
    CHECK(f.runtime.world().multiplayer.saved.double_movement == 0);
    CHECK(f.process.rules->definitions.units[0].movement == 64);
    REQUIRE(f.written.size() == 2);
    const auto saved = formats::decode_multiplayer_preferences(f.written[1]);
    CHECK(saved.clear_chat_new_game == 1);
    CHECK(saved.double_production == 17);
    CHECK(saved.double_movement == -19);
    CHECK_THROWS_AS(f.runtime.prepare_new_game(), std::logic_error);
    f.services.send_units = {}; // The operation retains its validated bindings.
    f.channel.progress = {0, 0};
    CHECK(op->poll() == MultiplayerOptionResult::applied);
    CHECK(f.runtime.world().multiplayer.saved.double_production == 1);
    CHECK(f.runtime.world().multiplayer.saved.double_movement == 1);
    CHECK(f.process.rules->definitions.units[0].movement == 128);
    CHECK(f.process.rules->definitions.units[1].movement == 3);
    CHECK(f.process.rules->definitions.units[61].movement == 254);
    CHECK(f.sent == std::vector<std::array<std::int32_t, 3>>{{1, 1, -32768}});
    CHECK(f.trace.back() == "PMCHANGEYES");
    CHECK(f.runtime.gameplay_random().checkpoint() == random);
    CHECK(op->poll() == MultiplayerOptionResult::applied);
    CHECK(f.channel.begins == 1);
    CHECK(f.channel.cancellations == 1);
    CHECK_NOTHROW(f.runtime.prepare_new_game());
}
TEST_CASE("N03 network clients save chat without changing host options",
          "[configuration][multiplayer]") {
    Fixture f;
    f.runtime.world().multiplayer.network_host = false;
    CHECK(f.apply() == MultiplayerOptionResult::client_only);
    CHECK(f.process.multiplayer_preferences.double_production == 17);
    CHECK(f.process.multiplayer_preferences.double_movement == -19);
    CHECK(f.written.size() == 2);
    CHECK(f.channel.begins == 0);
    CHECK_FALSE(f.process.rules->double_movement_applied);
}
TEST_CASE("N03 unchanged values and local modes skip the vote", "[configuration][multiplayer]") {
    SECTION("Unchanged") {
        Fixture f;
        CHECK(f.apply({true, true, false, false}) == MultiplayerOptionResult::unchanged);
        CHECK(f.process.multiplayer_preferences.double_movement == 0);
        CHECK(f.process.rules->definitions.units[0].movement == 64);
        CHECK(f.channel.begins == 0);
    }
    SECTION("Local or lone host") {
        for (const auto mode : {0, 1, 2, 3}) {
            Fixture f;
            f.runtime.world().globals.multiplayer = static_cast<std::uint8_t>(mode);
            f.runtime.world().multiplayer.player_count = 1;
            MultiplayerOptionOperation op(f.runtime, 0, {true, false, true, true}, f.services);
            CHECK(op.poll() == MultiplayerOptionResult::applied);
            CHECK(f.process.rules->double_movement_applied == (mode != 0));
            CHECK(f.sent.empty());
        }
    }
}
TEST_CASE("N03 preference save failure does not veto an accepted proposal",
          "[configuration][multiplayer]") {
    Fixture f;
    f.fail_item = 0;
    CHECK(f.apply() == MultiplayerOptionResult::applied);
    CHECK(f.written.size() == 1);
    CHECK(f.runtime.world().multiplayer.saved.double_movement == 1);
    CHECK(std::find(f.trace.begin(), f.trace.end(), "delete") != f.trace.end());
}
TEST_CASE("N03 rejected or timed out proposals restore the current authoritative options",
          "[configuration][multiplayer]") {
    for (const auto reply : {OptionVoteProgress{0, -1}, {1, 0}, {-1, 0}}) {
        Fixture f;
        auto op = f.operation();
        REQUIRE_FALSE(op->poll());
        f.channel.progress = reply;
        f.tick += 2401;
        f.runtime.world().multiplayer.saved.double_production = -7;
        f.runtime.world().multiplayer.saved.double_movement = 2;
        CHECK(op->poll() == MultiplayerOptionResult::rejected);
        CHECK(f.process.multiplayer_preferences.double_production == -7);
        CHECK(f.process.multiplayer_preferences.double_movement == 2);
        CHECK_FALSE(f.process.rules->double_movement_applied);
        CHECK(f.trace.back() == "PMCHANGENO");
        CHECK(f.sent.empty());
    }
    for (const auto status : {-2, 0, 9}) {
        Fixture f;
        f.channel.progress = {0, status};
        CHECK(f.apply() == MultiplayerOptionResult::applied);
    }
}
TEST_CASE("N03 vote deadlines preserve signed wrapping and the 2401 tick boundary",
          "[configuration][multiplayer][vote]") {
    struct Time {
        std::uint32_t start, now;
        bool waits;
    };
    for (const auto value : {Time{10, 2410, true},
                             {10, 2411, false},
                             {0xfffffff0U, 0x10U, true},
                             {0, 0x80000000U, true}}) {
        Fixture f;
        f.channel.progress = {-2, -2};
        f.tick = value.start;
        auto op = f.operation();
        REQUIRE_FALSE(op->poll());
        f.tick = value.now;
        const auto result = op->poll();
        CHECK(result.has_value() != value.waits);
        if (result)
            CHECK(*result == MultiplayerOptionResult::rejected);
        else
            CHECK(f.runtime.world().multiplayer.vote.popup_tick == value.start);
    }
}
TEST_CASE("N03 handoff skips the deadline clock and completion stores a fresh tick",
          "[configuration][multiplayer][vote]") {
    for (const bool handoff : {false, true}) {
        Fixture f;
        unsigned clocks = 0;
        f.services.clock = [&] { return ++clocks == 1 ? 100U : clocks == 2 ? 200U : 300U; };
        f.channel.on_poll = [&] { f.runtime.world().multiplayer.handoff = handoff ? -1 : 0; };
        auto op = f.operation();
        REQUIRE_FALSE(op->poll());
        CHECK(op->poll() == MultiplayerOptionResult::applied);
        CHECK(clocks == (handoff ? 2U : 3U));
        CHECK(f.runtime.world().multiplayer.vote.popup_tick == (handoff ? 200U : 300U));
    }
}
TEST_CASE("N03 proposal text and publication tails observe specified live child effects",
          "[configuration][multiplayer]") {
    SECTION("Text and count") {
        Fixture f;
        f.services.local_player_name = [] { return std::string("Local\0ignored", 13); };
        f.services.production_label = [&](bool enabled) {
            CHECK(enabled);
            f.process.multiplayer_preferences.double_movement = 0;
            return "Production on";
        };
        f.services.separator = [&] {
            f.runtime.world().multiplayer.player_count = std::numeric_limits<std::int32_t>::min();
            return " / ";
        };
        CHECK(f.apply() == MultiplayerOptionResult::applied);
        REQUIRE(f.channel.proposal);
        CHECK(f.channel.proposal->sender == "Local");
        CHECK(f.channel.proposal->description == "Production on / Movement off");
        CHECK(f.channel.proposal->expected_votes == std::numeric_limits<std::int32_t>::max());
        CHECK(f.runtime.world().multiplayer.saved.double_movement == 0);
        CHECK(f.sent.empty());
    }
    SECTION("Selected tail finishes after a mode change") {
        Fixture f;
        f.runtime.world().globals.multiplayer = 1;
        f.runtime.world().multiplayer.player_count = 1;
        f.services.redraw_status = [&](auto) {
            f.runtime.world().globals.multiplayer = 3;
            f.runtime.world().multiplayer.player_count = 2;
        };
        f.services.send_units = [&] {
            f.runtime.world().globals.multiplayer = 0;
            f.runtime.world().multiplayer.saved.double_movement = -4;
        };
        CHECK(f.apply() == MultiplayerOptionResult::applied);
        CHECK(f.sent[0][1] == -4);
        CHECK(f.trace.back() == "PMCHANGEYES");
    }
    SECTION("Redraw suppresses publication") {
        Fixture f;
        f.services.redraw_status = [&](auto) { f.runtime.world().multiplayer.player_count = 1; };
        test::fail_service(f.services.send_units);
        CHECK(f.apply() == MultiplayerOptionResult::applied);
        CHECK(f.sent.empty());
    }
}
TEST_CASE("N03 cancellation and destruction release pending votes and world lifetime",
          "[configuration][multiplayer][vote]") {
    for (const bool abandon : {false, true}) {
        Fixture f;
        f.channel.progress = {2, 0};
        auto op = f.operation();
        REQUIRE_FALSE(op->poll());
        if (abandon)
            op.reset();
        else {
            op->cancel();
            CHECK(op->poll() == MultiplayerOptionResult::rejected);
        }
        CHECK(f.process.multiplayer_preferences.double_movement == 0);
        CHECK(f.channel.cancellations == 1);
        CHECK(f.trace.back() == "PMCHANGENO");
        CHECK_NOTHROW(f.runtime.prepare_new_game());
    }
}
TEST_CASE("N03 cancellation during channel begin waits for the callback to return",
          "[configuration][multiplayer][vote]") {
    Fixture f;
    auto op = f.operation();
    f.channel.on_begin = [&] {
        CHECK_THROWS_AS(op->poll(), std::logic_error);
        op->cancel();
        CHECK(f.channel.cancellations == 0);
        CHECK_THROWS_AS(f.runtime.prepare_new_game(), std::logic_error);
    };
    CHECK(op->poll() == MultiplayerOptionResult::rejected);
    CHECK(f.channel.cancellations == 1);
    CHECK_NOTHROW(f.runtime.prepare_new_game());
}
TEST_CASE("N03 failing vote or publication children retain completed effects and clean up",
          "[configuration][multiplayer]") {
    for (const auto stage : {0, 1, 2}) {
        Fixture f;
        const auto fail = [] { throw std::runtime_error("authored child failure"); };
        if (stage == 0)
            f.channel.on_begin = fail;
        if (stage == 1)
            f.channel.on_poll = fail;
        if (stage == 2)
            f.services.send_units = fail;
        auto op = f.operation();
        if (stage != 0)
            REQUIRE_FALSE(op->poll());
        CHECK_THROWS_AS(op->poll(), std::runtime_error);
        CHECK(op->poll() == MultiplayerOptionResult::failed);
        CHECK(f.written.size() == 2);
        CHECK(f.runtime.world().multiplayer.saved.double_movement == (stage == 2 ? 1 : 0));
        CHECK(f.channel.cancellations == 1);
        CHECK_NOTHROW(f.runtime.prepare_new_game());
    }
}
TEST_CASE("N03 missing vote wiring rejects before persistence or proposal",
          "[configuration][multiplayer]") {
    Fixture f;
    CHECK_THROWS_AS(MultiplayerOptionOperation(f.runtime, 0, {true, false, true, true}, f.services),
                    std::invalid_argument);
    f.services.clock = {};
    CHECK_THROWS_AS(f.operation(), std::invalid_argument);
    CHECK(f.trace.empty());
    CHECK(f.channel.begins == 0);
}
