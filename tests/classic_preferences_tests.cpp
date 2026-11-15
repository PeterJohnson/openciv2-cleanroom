#include "formats/state_conversion.hpp"
// F03 section 8/Appendix A and F06 CIV2.DAT. Independently authored bytes and
// service outcomes, not reconstruction harnesses or original-game captures.
#include "classic_stream_fixture.hpp"
#include "formats/preference_io.hpp"
#include "session/classic_preferences.hpp"
#include "session/runtime.hpp"
#include <catch2/catch_test_macros.hpp>
#include <algorithm>

using namespace openciv;
using namespace openciv::formats;
using namespace openciv::session;
namespace {
state::OrdinaryPreferences ordinary(const Bytes& bytes) {
    return to_live(decode_ordinary_preferences(bytes));
}
state::MultiplayerPreferences multiplayer(const Bytes& bytes) {
    return to_live(decode_multiplayer_preferences(bytes));
}

void word(Bytes& bytes, std::size_t at, std::uint16_t value) {
    bytes.at(at) = static_cast<Byte>(value);
    bytes.at(at + 1) = static_cast<Byte>(value >> 8);
}
void dword(Bytes& bytes, std::size_t at, std::uint32_t value) {
    word(bytes, at, static_cast<std::uint16_t>(value));
    word(bytes, at + 2, static_cast<std::uint16_t>(value >> 16));
}
Bytes ordinary_defaults(bool large, bool animation, const Bytes&) {
    Bytes expected(72, 0);
    word(expected, 0, 0x3f);
    word(expected, 2, large ? 2 : 0);
    word(expected, 10, 6);
    word(expected, 12, 4);
    dword(expected, 14, animation ? 0x3f3258U : 0x1f3258U);
    word(expected, 22, 1);
    word(expected, 24, 2);
    return expected;
}
Bytes multiplayer_defaults(const Bytes&) {
    Bytes expected(444, 0);
    expected[0x9a] = '6';
    expected[0x9b] = '0';
    return expected;
}
struct Fixture {
    state::ProcessContext process;
    state::PreferenceDefaults defaults{1000, true};
    PreferenceFileServices services;
    std::vector<std::string> trace;
    Bytes input = Bytes(516, 0x88), output;
    std::size_t position = 0, reads = 0, writes = 0, closes = 0, removals = 0;
    bool input_open = true, output_open = true, close_ok = true;
    std::optional<std::size_t> short_item, fail_write;
    std::size_t prefix = 0;
    std::function<void(std::size_t)> before_read, after_write;
    Fixture() {
        process.ordinary_preferences = ordinary(Bytes(72, 0x11));
        process.multiplayer_preferences = multiplayer(Bytes(444, 0x22));
        services.open_input = [&](std::string_view path) -> std::unique_ptr<CoreInput> {
            CHECK(path == "CIV2.DAT");
            trace.push_back("open-input");
            if (!input_open)
                return {};
            return test::input_file(
                {[&](std::size_t size) {
                     const auto index = reads++;
                     trace.push_back("read:" + std::to_string(size));
                     if (before_read)
                         before_read(index);
                     auto count = std::min(size, input.size() - position);
                     if (short_item == index)
                         count = std::min(count, prefix);
                     const auto start = input.begin() + static_cast<std::ptrdiff_t>(position);
                     position += count;
                     return ReadResult{{start, start + static_cast<std::ptrdiff_t>(count)},
                                       short_item != index && count == size};
                 },
                 {}, // Preference input has no byte-at-a-time operation.
                 [&] {
                     ++closes;
                     trace.push_back("close");
                     return close_ok;
                 }});
        };
        services.open_output = [&](std::string_view path) -> std::unique_ptr<CoreOutput> {
            CHECK(path == "CIV2.DAT");
            trace.push_back("open-output");
            if (!output_open)
                return {};
            output.clear();
            return test::output_file({[&](std::span<const Byte> bytes) {
                                          const auto index = writes++;
                                          trace.push_back("write:" + std::to_string(bytes.size()));
                                          output.insert(output.end(), bytes.begin(), bytes.end());
                                          if (after_write)
                                              after_write(index);
                                          return fail_write != index;
                                      },
                                      [&] {
                                          ++closes;
                                          trace.push_back("close");
                                          return close_ok;
                                      }});
        };
        services.remove_file = [&](std::string_view path) {
            CHECK(path == "CIV2.DAT");
            ++removals;
            trace.push_back("delete");
            return false; // Delete failure does not change the write status.
        };
    }
    PreferenceLoadStatus load() { return load_classic_preferences(process, defaults, services); }
    PreferenceSaveStatus save() { return save_classic_preferences(process, services); }
};
} // namespace

TEST_CASE("F06 ordinary preference codec binds every fixed offset without scalar validation",
          "[preferences][formats]") {
    Bytes bytes(72, 0xa5);
    word(bytes, 0, 0x813f);
    for (std::size_t i = 1; i <= 6; ++i)
        word(bytes, i * 2, static_cast<std::uint16_t>(0xfff0U + i));
    dword(bytes, 14, 0xfedcba98U);
    word(bytes, 18, 0x8001);
    for (std::size_t i = 0; i < 8; ++i)
        word(bytes, 20 + 2 * i, static_cast<std::uint16_t>(101 + i));
    word(bytes, 68, 0x8000);
    word(bytes, 70, 0x7fff);
    const auto p = decode_ordinary_preferences(bytes);
    CHECK(p.rules == 0x813f);
    CHECK(p.default_level == -15);
    CHECK(p.difficulty == -14);
    CHECK(p.tribe == -13);
    CHECK(p.gender == -12);
    CHECK(p.science == -11);
    CHECK(p.tax == -10);
    CHECK(p.game_options == 0xfedcba98U);
    CHECK(p.disabled_messages == 0x8001);
    CHECK(p.opponents == 101);
    CHECK(p.map_size == 102);
    CHECK(p.barbarians == 103);
    CHECK(p.world_parameters == std::array<std::int16_t, 5>{104, 105, 106, 107, 108});
    CHECK_FALSE(p.text.terminated());
    CHECK(p.rules_choice == -32768);
    CHECK(p.accelerated_start == 32767);
    CHECK(encode_ordinary_preferences(p) == bytes);
}

TEST_CASE("F06 multiplayer preference codec binds scalars rectangles and retained arrays",
          "[preferences][formats]") {
    Bytes bytes(444, 0);
    for (std::size_t i = 0; i < 12; ++i)
        word(bytes, i * 2, static_cast<std::uint16_t>(0xff00U + i));
    for (std::size_t i = 0; i < 4; ++i)
        std::fill_n(bytes.begin() + 0x18 + static_cast<std::ptrdiff_t>(i * 32), 32,
                    static_cast<Byte>(0xa1 + i));
    word(bytes, 0x98, 0x8000);
    std::fill_n(bytes.begin() + 0x9a, 6, 0xb5);
    word(bytes, 0xa0, 0x7fff);
    for (std::size_t i = 0; i < 8; ++i)
        dword(bytes, 0xa2 + 4 * i, 0x80000000U + static_cast<std::uint32_t>(i));
    word(bytes, 0xc2, 11);
    word(bytes, 0xc4, 12);
    word(bytes, 0xc6, 13);
    word(bytes, 0xc8, 14);
    for (std::size_t i = 0; i < 10; ++i)
        std::fill_n(bytes.begin() + 0xca + static_cast<std::ptrdiff_t>(i * 24), 24,
                    static_cast<Byte>(0xc0 + i));
    word(bytes, 0x1ba, 0x1234);
    const auto p = decode_multiplayer_preferences(bytes);
    CHECK(p.main_menu == -256);
    CHECK(p.type == -255);
    CHECK(p.hotseat_menu == -254);
    CHECK(p.lan_menu == -253);
    CHECK(p.legacy_setting_1 == -252);
    CHECK(p.email_menu == -251);
    CHECK(p.protocol == 0xff06);
    CHECK(p.open_game == -249);
    CHECK(p.kill_on_retirement == -248);
    CHECK(p.show_human_starts == -247);
    CHECK(p.chat_from_start == -246);
    CHECK(p.simultaneous == -245);
    CHECK(p.game_name.bytes[31] == 0xa1);
    CHECK(p.player_name.bytes[31] == 0xa2);
    CHECK(p.email_address.bytes[31] == 0xa3);
    CHECK(p.ip_address.bytes[31] == 0xa4);
    CHECK_FALSE(p.game_name.terminated());
    CHECK(p.timer == -32768);
    CHECK(p.custom_timer.bytes[5] == 0xb5);
    CHECK_FALSE(p.custom_timer.terminated());
    CHECK(p.hotseat_players == 32767);
    CHECK(p.parley == state::Rectangle{-2147483647 - 1, -2147483647, -2147483646, -2147483645});
    CHECK(p.chat == state::Rectangle{-2147483644, -2147483643, -2147483642, -2147483641});
    CHECK(p.clear_chat_new_game == 11);
    CHECK(p.clear_chat_boot == 12);
    CHECK(p.double_production == 13);
    CHECK(p.double_movement == 14);
    for (std::size_t i = 0; i < 3; ++i)
        CHECK(p.legacy_settings[i][23] == 0xc0 + i);
    for (std::size_t i = 0; i < 7; ++i)
        CHECK(p.settings[i][23] == 0xc3 + i);
    CHECK(p.legacy_setting_5 == 0x1234);
    CHECK(encode_multiplayer_preferences(p) == bytes);
}

TEST_CASE("F03 live preference defaults clear text and obey startup gates",
          "[preferences][state]") {
    for (const auto width : {-1, 999, 1000})
        for (const auto animation : {false, true}) {
            const Bytes prior(72, 0xa5);
            auto p = ordinary(prior);
            state::default_ordinary_preferences(p, {width, animation});
            CHECK(encode_ordinary_preferences(p) ==
                  ordinary_defaults(width > 999, animation, prior));
        }
    const Bytes prior(444, 0xa5);
    auto p = multiplayer(prior);
    state::default_multiplayer_preferences(p);
    CHECK(encode_multiplayer_preferences(p) == multiplayer_defaults(prior));
}

TEST_CASE("F06 preference item decoding uses owned prefixes and explicit prior values",
          "[preferences][formats]") {
    for (std::size_t n = 0; n <= 72; ++n) {
        const auto prior = decode_ordinary_preferences(Bytes(72, 0xcc));
        Bytes expected(72, 0xcc);
        std::fill_n(expected.begin(), n, 0x77);
        CHECK(encode_ordinary_preferences(
                  decode_ordinary_preference_item({Bytes(n, 0x77), false}, prior)) == expected);
        CHECK(encode_ordinary_preferences(prior) == Bytes(72, 0xcc));
    }
    for (std::size_t n = 0; n <= 444; ++n) {
        const auto prior = decode_multiplayer_preferences(Bytes(444, 0xcc));
        Bytes expected(444, 0xcc);
        std::fill_n(expected.begin(), n, 0x77);
        CHECK(encode_multiplayer_preferences(
                  decode_multiplayer_preference_item({Bytes(n, 0x77), false}, prior)) == expected);
        CHECK(encode_multiplayer_preferences(prior) == Bytes(444, 0xcc));
    }
}
TEST_CASE("F06 preference codecs reject incorrect lengths and inconsistent items",
          "[preferences][formats]") {
    for (const std::size_t size : {0U, 71U, 73U})
        CHECK_THROWS_AS(decode_ordinary_preferences(Bytes(size)), DecodeError);
    for (const std::size_t size : {0U, 443U, 445U})
        CHECK_THROWS_AS(decode_multiplayer_preferences(Bytes(size)), DecodeError);
    for (const bool complete : {false, true})
        CHECK_THROWS_AS(decode_ordinary_preference_item({Bytes(complete ? 71 : 73), complete}, {}),
                        std::invalid_argument);
}

TEST_CASE("F03 opening failure defaults both blocks without reading or closing",
          "[preferences][session]") {
    Fixture f;
    f.input_open = false;
    const auto ordinary = encode_ordinary_preferences(f.process.ordinary_preferences);
    const auto multiplayer = encode_multiplayer_preferences(f.process.multiplayer_preferences);
    f.load();
    CHECK(f.trace == std::vector<std::string>{"open-input"});
    CHECK(encode_ordinary_preferences(f.process.ordinary_preferences) ==
          ordinary_defaults(true, true, ordinary));
    CHECK(encode_multiplayer_preferences(f.process.multiplayer_preferences) ==
          multiplayer_defaults(multiplayer));
}

TEST_CASE("F03 preference fallback stages both items against entry snapshots",
          "[preferences][session]") {
    Fixture f;
    f.input.assign(40, 0x33);
    f.input.insert(f.input.end(), 444, 0x44);
    f.input.insert(f.input.end(), 17, 0x55);
    f.short_item = 0;
    f.prefix = 40;
    f.before_read = [&](std::size_t index) {
        if (index == 0) {
            f.defaults.screen_width = 999;
            f.defaults.animations_available = false;
        } else {
            CHECK(f.position == 40);
            CHECK(f.process.ordinary_preferences == ordinary(Bytes(72, 0x11)));
            CHECK(f.process.ordinary_preferences.text[1] == 0x11);
            CHECK(f.process.ordinary_preferences.text[4] == 0x11);
        }
    };
    f.load();
    Bytes partial(72, 0x11);
    std::fill_n(partial.begin(), 40, 0x33);
    CHECK(encode_ordinary_preferences(f.process.ordinary_preferences) ==
          ordinary_defaults(true, true, partial));
    CHECK(f.process.multiplayer_preferences == multiplayer(Bytes(444, 0x44)));
    CHECK(f.position == 484);
    CHECK(f.trace == std::vector<std::string>{"open-input", "read:72", "read:444", "close"});
}

TEST_CASE("F03 multiplayer fallback preserves failed bytes in unassigned tails",
          "[preferences][session]") {
    Fixture f;
    f.short_item = 1;
    f.prefix = 208;
    f.load();
    CHECK(f.process.ordinary_preferences == ordinary(Bytes(72, 0x88)));
    Bytes partial(444, 0x22);
    std::fill_n(partial.begin(), 208, 0x88);
    CHECK(encode_multiplayer_preferences(f.process.multiplayer_preferences) ==
          multiplayer_defaults(partial));
    CHECK(f.position == 280);
    CHECK(f.closes == 1);
}

TEST_CASE("F03 successful preference input keeps arbitrary values and ignores trailing bytes",
          "[preferences][session]") {
    Fixture f;
    f.input.insert(f.input.end(), 23, 0x33);
    f.load();
    CHECK(f.process.ordinary_preferences == ordinary(Bytes(72, 0x88)));
    CHECK(f.process.multiplayer_preferences == multiplayer(Bytes(444, 0x88)));
    CHECK(f.process.ordinary_preferences.text.size() == 32);
    CHECK(f.process.multiplayer_preferences.custom_timer.size() == 6);
    CHECK(f.position == 516);
    CHECK(f.closes == 1);
}

TEST_CASE("F03 preference saving encodes both blocks and reports close failure",
          "[preferences][session]") {
    for (bool close_ok : {false, true}) {
        Fixture f;
        f.close_ok = close_ok;
        auto expected = encode_ordinary_preferences(f.process.ordinary_preferences);
        const auto second = encode_multiplayer_preferences(f.process.multiplayer_preferences);
        expected.insert(expected.end(), second.begin(), second.end());
        CHECK(f.save() ==
              (close_ok ? PreferenceSaveStatus::success : PreferenceSaveStatus::output));
        CHECK(f.output == expected);
        CHECK(f.closes == 1);
        CHECK(f.removals == (close_ok ? 0 : 1));
    }
}

TEST_CASE("F03 failed preference writes close before deleting without rolling back state",
          "[preferences][session]") {
    for (const std::size_t item : {0U, 1U}) {
        Fixture f;
        f.fail_write = item;
        f.process.ordinary_preferences.game_options = 0x1234;
        CHECK(f.save() == PreferenceSaveStatus::output);
        CHECK(f.writes == item + 1);
        CHECK(f.closes == 1);
        CHECK(f.removals == 1);
        REQUIRE(f.trace.size() == item + 4);
        CHECK(f.trace[f.trace.size() - 2] == "close");
        CHECK(f.trace.back() == "delete");
        CHECK(f.process.ordinary_preferences.game_options == 0x1234);
    }
    Fixture f;
    f.output_open = false;
    CHECK(f.save() == PreferenceSaveStatus::output);
    CHECK(f.trace == std::vector<std::string>{"open-output"});
    CHECK(f.removals == 0);
}

TEST_CASE("F03 exceptional preference services close once without installing a prefix",
          "[preferences][session]") {
    SECTION("Second read failure leaves both installed blocks untouched") {
        Fixture f;
        f.before_read = [](std::size_t index) {
            if (index == 1)
                throw std::runtime_error("supplied read stop");
        };
        CHECK_THROWS_AS(f.load(), std::runtime_error);
        CHECK(f.process.ordinary_preferences == ordinary(Bytes(72, 0x11)));
        CHECK(f.process.multiplayer_preferences == multiplayer(Bytes(444, 0x22)));
        CHECK(f.closes == 1);
    }
    SECTION("Write exception is not an ordinary failed-item return") {
        Fixture f;
        f.after_write = [](std::size_t) { throw std::runtime_error("supplied write stop"); };
        CHECK_THROWS_AS(f.save(), std::runtime_error);
        CHECK(f.output.size() == 72);
        CHECK(f.closes == 1);
        CHECK(f.removals == 0);
    }
}

TEST_CASE("F03 process preferences do not mutate a running world or its random context",
          "[preferences][session]") {
    Fixture f;
    SessionRuntime runtime(f.process, base::RuntimeId{78});
    runtime.begin_client_session({10, 20});
    runtime.world().globals.game_options = 0x1234;
    runtime.world().multiplayer.saved.simultaneous = true;
    const auto random = runtime.gameplay_random().checkpoint();
    f.load();
    CHECK(runtime.world().globals.game_options == 0x1234);
    CHECK(runtime.world().multiplayer.saved.simultaneous);
    CHECK(runtime.gameplay_random().checkpoint() == random);
    const auto ordinary = f.process.ordinary_preferences;
    const auto multiplayer = f.process.multiplayer_preferences;
    runtime.end_client_session();
    runtime.begin_client_session({30, 40});
    CHECK(f.process.ordinary_preferences == ordinary);
    CHECK(f.process.multiplayer_preferences == multiplayer);
}

TEST_CASE("Classic owned streams finalize once and preserve an earlier I/O exception",
          "[preferences][classic-files]") {
    static_assert(!std::is_copy_constructible_v<test::InputFile>);
    static_assert(!std::is_copy_constructible_v<test::OutputFile>);
    unsigned closes = 0;
    {
        auto output = test::output_file({[](auto) { return true; },
                                         [&] {
                                             ++closes;
                                             return false;
                                         }});
        CHECK_FALSE(output->close());
        CHECK_FALSE(output->close());
        CHECK_THROWS_AS(output->item(Bytes{1}), std::logic_error);
    }
    CHECK(closes == 1);
    closes = 0;
    try {
        auto input = test::input_file({[](auto) -> ReadResult { throw std::runtime_error("read"); },
                                       {},
                                       [&]() -> bool {
                                           ++closes;
                                           throw std::runtime_error("close");
                                       }});
        (void)input->item(72);
        FAIL("Expected read failure");
    } catch (const std::runtime_error& error) {
        CHECK(std::string(error.what()) == "read");
    }
    CHECK(closes == 1);
    closes = 0;
    {
        auto output = test::output_file({[](auto) { return true; },
                                         [&]() -> bool {
                                             ++closes;
                                             throw std::runtime_error("close");
                                         }});
        CHECK_THROWS_AS(output->close(), std::runtime_error);
        CHECK_FALSE(output->close());
    }
    CHECK(closes == 1);
}

TEST_CASE("F03 preference finalization failure leaves installed values unchanged",
          "[preferences][session]") {
    Fixture f;
    f.close_ok = false;
    const auto ordinary = f.process.ordinary_preferences;
    const auto multiplayer = f.process.multiplayer_preferences;
    CHECK(f.load() == PreferenceLoadStatus::input);
    CHECK(f.process.ordinary_preferences == ordinary);
    CHECK(f.process.multiplayer_preferences == multiplayer);
    CHECK(f.closes == 1);
}

TEST_CASE("Preference loading does not require the prior live text to fit a classic export",
          "[preferences][session]") {
    for (const bool short_input : {false, true}) {
        Fixture f;
        f.process.ordinary_preferences.text = std::string(100, 'P');
        f.process.multiplayer_preferences.game_name = std::string(100, 'G');
        if (short_input)
            f.input.clear();
        CHECK(f.load() ==
              (short_input ? PreferenceLoadStatus::defaults : PreferenceLoadStatus::success));
        CHECK(f.process.ordinary_preferences.text.size() == (short_input ? 0 : 32));
        CHECK(f.process.multiplayer_preferences.game_name.size() == (short_input ? 0 : 32));
        CHECK(f.closes == 1);
    }
}
