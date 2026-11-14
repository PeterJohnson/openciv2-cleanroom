// Authored F06 sections 5, 6, 9 / F01 section 1 expectations, not original captures.
#include "formats/multiplayer_io.hpp"
#include "session/runtime.hpp"
#include <catch2/catch_test_macros.hpp>
#include <algorithm>
#include <bit>

using namespace openciv;
using namespace openciv::formats;
namespace {
base::ChoiceKey byte_key(std::string_view call, std::size_t i) {
    return {"F06.password-reset.byte",
            std::string(call) + "/row/" + std::to_string(i / 32) + "/byte/" +
                std::to_string(i % 32),
            0};
}
std::int32_t code(std::size_t i) {
    return static_cast<std::int32_t>((0x1234 + i * 97) % 32768);
}
base::ControlledChoices choices(std::string_view call, std::size_t count = 255) {
    base::ControlledChoices result;
    for (std::size_t i = 0; i < count; ++i)
        result.records.push_back(
            base::make_choice(byte_key(call, i), base::PrimaryCode{}, code(i)));
    return result;
}
state::MultiplayerState incoming() {
    state::MultiplayerState result;
    result.saved = decode_multiplayer(Bytes(multiplayer_record_size, 0xa5));
    result.passwords.pending.fill(true);
    result.passwords.present.fill(true);
    result.local_human = 300;
    result.resume_civ = 301;
    return result;
}
state::MultiplayerValues saved() {
    state::MultiplayerValues result;
    result.turn_time_limit = 123456;
    result.simultaneous = -12;
    result.double_production = 17;
    result.double_movement = -18;
    result.resume_civ = 5;
    result.encoded_passwords.fill(0x7b);
    result.encoded_passwords[0] = 0;
    result.encoded_passwords[7 * 32] = 0;
    state::encode_passwords(result.encoded_passwords);
    result.retained_472.fill(0xa6);
    return result;
}
} // namespace

TEST_CASE("F06 password encoding rotates across row boundaries and the full circular array",
          "[state][passwords]") {
    std::array<Byte, 256> zero{}, index{};
    for (std::size_t i = 0; i < index.size(); ++i)
        index[i] = static_cast<Byte>(i);
    auto encoded = zero;
    state::encode_passwords(encoded);
    REQUIRE(encoded == index);
    state::decode_passwords(encoded);
    REQUIRE(encoded == zero);
    // Each individual source bit has one specified destination in the rotated
    // circular stream. Check the encoding directly, not only inverse agreement.
    for (std::size_t i = 0; i < 256; ++i)
        for (unsigned bit = 0; bit < 8; ++bit) {
            auto plain = zero;
            plain[i] = static_cast<Byte>(1U << bit);
            auto expected = index;
            const auto destination = bit >= 3 ? i : (i + 1) % 256;
            expected[destination] ^= static_cast<Byte>(1U << (bit >= 3 ? bit - 3 : bit + 5));
            auto actual = plain;
            state::encode_passwords(actual);
            REQUIRE(actual == expected);
            state::decode_passwords(actual);
            REQUIRE(actual == plain);
        }
}
TEST_CASE("F06 password reset consumes 255 semantic primary choices and retains byte 255",
          "[state][passwords]") {
    auto runtime = incoming();
    runtime.saved.encoded_passwords[255] = 0x5a;
    const auto before = runtime.saved;
    base::GameplayRandom random(choices("load/one"), "session");
    state::reset_passwords(runtime, random, "load/one");
    random.require_choices_consumed();
    REQUIRE(random.checkpoint().draws == 0); // Controlled choices do not advance the generator.
    REQUIRE(random.checkpoint().trace->events.size() == 255);
    REQUIRE(random.checkpoint().trace->events.back().key == byte_key("load/one", 254));
    REQUIRE(runtime.saved.encoded_passwords[0] == 0x40); // Incoming final byte's low bits wrap.
    auto plaintext = runtime.saved.encoded_passwords;
    state::decode_passwords(plaintext);
    for (std::size_t i = 0; i < 255; ++i)
        REQUIRE(plaintext[i] == (i % 32 == 0 ? Byte{0} : static_cast<Byte>(code(i))));
    REQUIRE(plaintext[255] == 0x5a);
    REQUIRE(runtime.passwords == state::PasswordFlags{});
    auto expected = before;
    expected.encoded_passwords = runtime.saved.encoded_passwords;
    REQUIRE(runtime.saved == expected);
    REQUIRE(runtime.local_human == 300);
    REQUIRE(runtime.resume_civ == 301);
}
TEST_CASE("F06 missing password choices retain the sampled prefix before clearing or encoding",
          "[state][passwords]") {
    auto runtime = incoming();
    const auto before = runtime;
    base::GameplayRandom random(choices("load/short", 100), "session");
    try {
        state::reset_passwords(runtime, random, "load/short");
        FAIL("Missing choice must stop password reset");
    } catch (const base::ChoiceError& error) {
        REQUIRE(error.failure() == base::ChoiceFailure::missing);
        REQUIRE(error.key() == byte_key("load/short", 100));
    }
    auto expected = before;
    for (std::size_t i = 0; i < 100; ++i)
        expected.saved.encoded_passwords[i] = static_cast<Byte>(code(i));
    REQUIRE(runtime == expected);
    REQUIRE(runtime.saved.encoded_passwords[0] == 0x34); // Row starts have not yet been cleared.
    REQUIRE(random.checkpoint().trace->events.size() == 100);
    REQUIRE_THROWS_AS(state::reset_passwords(runtime, random, ""), std::invalid_argument);
    REQUIRE(runtime == expected);
}
TEST_CASE("F06 password refresh preserves encoded bytes and derives both flags from row starts",
          "[state][passwords]") {
    auto runtime = incoming();
    runtime.saved = saved();
    runtime.passwords.pending.fill(false);
    const auto bytes = runtime.saved.encoded_passwords;
    state::refresh_password_flags(runtime);
    REQUIRE(runtime.saved.encoded_passwords == bytes);
    for (std::size_t civ = 0; civ < 8; ++civ) {
        REQUIRE(runtime.passwords.pending[civ] == (civ != 0 && civ != 7));
        REQUIRE(runtime.passwords.present[civ] == (civ != 0 && civ != 7));
    }
    // Rows may contain no terminator; refresh inspects just their first byte.
    runtime.saved.encoded_passwords.fill(0xff);
    state::encode_passwords(runtime.saved.encoded_passwords);
    state::refresh_password_flags(runtime);
    REQUIRE(std::all_of(runtime.passwords.present.begin(), runtime.passwords.present.end(),
                        [](bool value) { return value; }));
}
TEST_CASE("F06 password generation repeats from its checkpoint independently of presentation",
          "[state][passwords]") {
    base::GameplayRandom gameplay(123, "session");
    base::PresentationRandom presentation(456, "session");
    base::CosmeticRandom cosmetic(789, "frontend");
    const auto before = gameplay.checkpoint();
    auto first = incoming(), second = first;
    const auto presentation_before = presentation.checkpoint();
    const auto cosmetic_before = cosmetic.checkpoint();
    gameplay.record_choices(255);
    state::reset_passwords(first, gameplay, "load/replay");
    const auto after = gameplay.checkpoint();
    REQUIRE(after.draws == 255);
    REQUIRE(after.trace->events.size() == 255);
    REQUIRE(presentation.checkpoint() == presentation_before);
    REQUIRE(cosmetic.checkpoint() == cosmetic_before);
    gameplay.restore(before);
    gameplay.record_choices(255);
    presentation.primary_code("presentation.before-load");
    cosmetic.primary_code("cosmetic.before-load");
    state::reset_passwords(second, gameplay, "load/replay");
    REQUIRE(second == first);
    REQUIRE(gameplay.checkpoint() == after);
}
TEST_CASE("F06 old or single-player loads reset only password and explicit multiplayer defaults",
          "[formats][multiplayer-io]") {
    for (const auto version : std::array<std::uint16_t, 4>{0x26, 0x27, 0x28, 0x2c}) {
        auto runtime = incoming();
        const auto before = runtime.saved;
        state::GlobalValues globals;
        globals.multiplayer = version < 0x29 ? 3 : 0;
        globals.saved_current_civ = -128;
        globals.turn_civ = 7;
        state::MultiplayerPreferences preferences{-2, 3, -4, 5};
        base::GameplayRandom random(choices("load/defaults"), "session");
        REQUIRE(read_multiplayer_items(
            [](std::size_t) -> ReadResult { throw std::logic_error("Unexpected item read"); },
            version, globals, runtime, preferences, random, "load/defaults"));
        random.require_choices_consumed();
        REQUIRE(runtime.local_human == -128);
        REQUIRE(runtime.resume_civ == 128);
        auto expected = before;
        expected.encoded_passwords = runtime.saved.encoded_passwords;
        expected.open_game = 1;
        expected.kill_on_retirement = -2;
        expected.show_human_starts = 3;
        expected.chat_from_start = -4;
        expected.double_production = expected.double_movement = 0;
        expected.resume_civ = 128;
        REQUIRE(runtime.saved == expected);
        REQUIRE(preferences == state::MultiplayerPreferences{-2, 3, -4, 0});
        REQUIRE(runtime.passwords == state::PasswordFlags{});
        auto plaintext = runtime.saved.encoded_passwords;
        state::decode_passwords(plaintext);
        REQUIRE(plaintext[255] == 0xa5);
    }
}
TEST_CASE("F06 default-load random failure keeps local-human publication and sampled prefix",
          "[formats][multiplayer-io]") {
    auto runtime = incoming();
    const auto before = runtime;
    state::GlobalValues globals;
    globals.saved_current_civ = -1;
    state::MultiplayerPreferences preferences{-2, 3, -4, 5};
    base::GameplayRandom random(choices("load/fail", 1), "session");
    REQUIRE_THROWS_AS(
        read_multiplayer_items({}, 0x2c, globals, runtime, preferences, random, "load/fail"),
        base::ChoiceError);
    auto expected = before;
    expected.local_human = -1;
    expected.saved.encoded_passwords[0] = static_cast<Byte>(code(0));
    REQUIRE(runtime == expected);
    REQUIRE(preferences.simultaneous == 5);
}
TEST_CASE("F06 every partial multiplayer item retains its prefix and zero-cleared remainder",
          "[formats][multiplayer-io]") {
    const auto bytes = encode_multiplayer(saved());
    state::GlobalValues globals;
    globals.multiplayer = 3;
    globals.saved_current_civ = -1;
    state::MultiplayerPreferences preferences{1, 2, 3, 4};
    base::GameplayRandom random(base::ControlledChoices{}, "session");
    const auto random_before = random.checkpoint();
    for (std::size_t length = 0; length <= bytes.size(); ++length) {
        auto runtime = incoming();
        unsigned calls = 0;
        REQUIRE_FALSE(read_multiplayer_items(
            [&](std::size_t size) {
                ++calls;
                REQUIRE(size == multiplayer_record_size);
                REQUIRE(runtime.saved == state::MultiplayerValues{});
                REQUIRE(runtime.local_human == -1);
                return ReadResult{
                    {bytes.begin(), bytes.begin() + static_cast<std::ptrdiff_t>(length)}, false};
            },
            0x2c, globals, runtime, preferences, random, ""));
        REQUIRE(calls == 1);
        Bytes expected(bytes.size(), 0);
        std::copy_n(bytes.begin(), length, expected.begin());
        REQUIRE(encode_multiplayer(runtime.saved) == expected);
        REQUIRE(runtime.resume_civ == 301);
        REQUIRE(runtime.passwords.pending[0]);
        REQUIRE(runtime.passwords.present[7]);
        REQUIRE(preferences.simultaneous == 4);
    }
    REQUIRE(random.checkpoint() == random_before);
}
TEST_CASE(
    "F06 complete multiplayer reads select resume civilization and refresh flags without draws",
    "[formats][multiplayer-io]") {
    struct Case {
        Byte saved;
        std::int8_t turn;
        Byte alive;
        std::int32_t expected;
    };
    for (const auto test : {Case{5, 2, 0, 5},
                            {0, -1, 0, 255},
                            {0, 4, 0, 4},
                            {0, 0, 0xa1, 5},
                            {0, 0, 1, 0},
                            {0, 0, 0, 0}}) {
        auto value = saved();
        value.resume_civ = test.saved;
        const auto bytes = encode_multiplayer(value);
        auto runtime = incoming();
        state::GlobalValues globals;
        globals.multiplayer = 3;
        globals.saved_current_civ = -128;
        globals.turn_civ = test.turn;
        globals.civs_alive = test.alive;
        const auto globals_before = globals;
        state::MultiplayerPreferences preferences{1, 2, 3, 4};
        base::GameplayRandom random(base::ControlledChoices{}, "session");
        const auto random_before = random.checkpoint();
        MemoryItems memory(bytes);
        REQUIRE(read_multiplayer_items([&](std::size_t size) { return memory.read(size); }, 0x2c,
                                       globals, runtime, preferences, random, ""));
        REQUIRE(runtime.local_human == -128);
        REQUIRE(runtime.resume_civ == test.expected);
        value.resume_civ = static_cast<Byte>(test.expected);
        REQUIRE(runtime.saved == value);
        REQUIRE_FALSE(runtime.passwords.pending[0]);
        REQUIRE(runtime.passwords.pending[1]);
        REQUIRE_FALSE(runtime.passwords.present[7]);
        REQUIRE(preferences == state::MultiplayerPreferences{1, 2, 3, 4});
        REQUIRE(globals == globals_before);
        REQUIRE(random.checkpoint() == random_before);
    }
}
TEST_CASE("F06 multiplayer callbacks retain live tail bytes and later selector values",
          "[formats][multiplayer-io]") {
    auto runtime = incoming();
    state::GlobalValues globals;
    globals.multiplayer = 3;
    state::MultiplayerPreferences preferences;
    base::GameplayRandom random(base::ControlledChoices{}, "session");
    REQUIRE_FALSE(read_multiplayer_items(
        [&](std::size_t) {
            runtime.saved.retained_472[33] = 0x67;
            runtime.saved.center_x[0] = std::bit_cast<std::int16_t>(std::uint16_t{0xabcd});
            return ReadResult{{0x34}, false};
        },
        0x2c, globals, runtime, preferences, random, ""));
    REQUIRE(runtime.saved.retained_472[33] == 0x67);
    REQUIRE(runtime.saved.center_x[0] == std::bit_cast<std::int16_t>(std::uint16_t{0xab34}));
    REQUIRE(read_multiplayer_items(
        [&](std::size_t size) {
            globals.turn_civ = 6;
            return ReadResult{Bytes(size), true};
        },
        0x2c, globals, runtime, preferences, random, ""));
    REQUIRE(runtime.resume_civ == 6);
    const auto before = runtime;
    REQUIRE_THROWS_AS(read_multiplayer_items({}, 0x29, globals, runtime, preferences, random, ""),
                      std::invalid_argument);
    REQUIRE(runtime == before);
    REQUIRE_THROWS_AS(read_multiplayer_items([](std::size_t) { return ReadResult{{}, true}; }, 0x2c,
                                             globals, runtime, preferences, random, ""),
                      std::invalid_argument);
    REQUIRE(runtime.saved == state::MultiplayerValues{});
    REQUIRE(runtime.resume_civ == 6);
}
TEST_CASE("F06 multiplayer writes restore temporary resume substitution only after success",
          "[formats][multiplayer-io]") {
    for (const bool complete : {false, true}) {
        auto runtime = incoming();
        runtime.saved.resume_civ = 2;
        const auto before = runtime;
        REQUIRE(write_multiplayer_item(
                    [&](std::span<const Byte> bytes) {
                        REQUIRE(bytes.size() == multiplayer_record_size);
                        REQUIRE(bytes[0x464] == 5);
                        REQUIRE(runtime.saved.resume_civ == 5);
                        runtime.saved.open_game = 7;
                        return complete;
                    },
                    runtime, 261) == complete);
        auto expected = before;
        expected.saved.resume_civ = complete ? 2 : 5;
        expected.saved.open_game = 7;
        REQUIRE(runtime == expected);
    }
    auto runtime = incoming();
    REQUIRE_THROWS_AS(
        write_multiplayer_item(
            [](std::span<const Byte>) -> bool { throw std::runtime_error("Write stopped"); },
            runtime, -1),
        std::runtime_error);
    REQUIRE(runtime.saved.resume_civ == 255);
}
TEST_CASE("F06 successful-load mode adaptation keeps entry-zero asymmetry and unrelated state",
          "[state][multiplayer-io]") {
    for (const Byte entry : {Byte{0}, Byte{1}, Byte{3}})
        for (const Byte loaded : {Byte{0}, Byte{1}, Byte{3}}) {
            auto runtime = incoming();
            const auto before = runtime;
            runtime.resume_civ = 3;
            state::GlobalValues globals;
            globals.multiplayer = loaded;
            globals.civs_alive = 0x0b;
            globals.human_players = 0;
            globals.player_civ = 5;
            state::MultiplayerPreferences preferences{1, 2, 3, 4};
            state::adapt_loaded_mode(entry, globals, runtime, preferences);
            auto expected = before;
            expected.resume_civ = 3;
            if (entry != loaded) {
                expected.saved.open_game = 1;
                if (entry == 0)
                    expected.saved.double_production = expected.saved.double_movement = 0;
            }
            REQUIRE(runtime == expected);
            REQUIRE(globals.multiplayer == (entry == 0 ? loaded : entry));
            REQUIRE(globals.human_players == (entry == loaded ? 0 : 8));
            REQUIRE(globals.player_civ == 5);
            REQUIRE(preferences.simultaneous == (entry == loaded ? 4 : 0));
            REQUIRE(preferences.kill_on_retirement == 1);
        }
    struct Case {
        Byte humans, alive;
        std::int32_t resume;
        Byte expected;
    };
    for (const auto test : {Case{0x40, 0, 3, 0x40},
                            {0, 0x83, 0, 2},
                            {0, 0x83, 3, 2},
                            {0, 1, 0, 0},
                            {0, 0, 7, 0},
                            {0, 0x82, 7, 0x80},
                            {0, 0x82, 33, 2},
                            {0, 0x82, -31, 2}}) {
        auto runtime = incoming();
        runtime.resume_civ = test.resume;
        state::GlobalValues globals;
        globals.multiplayer = 3;
        globals.human_players = test.humans;
        globals.civs_alive = test.alive;
        state::MultiplayerPreferences preferences;
        state::adapt_loaded_mode(0, globals, runtime, preferences);
        REQUIRE(globals.human_players == test.expected);
    }
}
TEST_CASE("F06 multiplayer state and menu preferences belong to the runtime world and process",
          "[session][multiplayer-io]") {
    state::ProcessContext process;
    session::SessionRuntime runtime(process, session::RuntimeId{44});
    runtime.begin_client_session({1, 2});
    runtime.world().globals.saved_current_civ = 4;
    process.multiplayer_preferences = {-2, 3, -4, 5};
    const auto presentation = runtime.presentation_random().checkpoint();
    REQUIRE(read_multiplayer_items({}, 0x2c, runtime.world().globals, runtime.world().multiplayer,
                                   runtime.process().multiplayer_preferences,
                                   runtime.gameplay_random(), "session/load/one"));
    REQUIRE(runtime.gameplay_random().checkpoint().draws == 255);
    REQUIRE(runtime.presentation_random().checkpoint() == presentation);
    REQUIRE(runtime.world().multiplayer.local_human == 4);
    REQUIRE(runtime.world().multiplayer.saved.kill_on_retirement == -2);
    const auto saved_state = runtime.world().multiplayer;
    const auto preferences = process.multiplayer_preferences;
    runtime.end_client_session();
    runtime.begin_client_session({7, 8});
    REQUIRE(runtime.world().multiplayer == saved_state);
    REQUIRE(process.multiplayer_preferences == preferences);
    state::ProcessContext separate;
    session::SessionRuntime other(separate, session::RuntimeId{45});
    REQUIRE(other.world().multiplayer == state::MultiplayerState{});
    REQUIRE(separate.multiplayer_preferences == state::MultiplayerPreferences{});
}
