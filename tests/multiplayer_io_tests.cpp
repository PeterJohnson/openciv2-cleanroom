#include "formats/state_conversion.hpp"
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
    result.saved = formats::to_live(decode_multiplayer(Bytes(multiplayer_record_size, 0xa5)));
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
TEST_CASE("F06 multiplayer writes encode resume without live substitution",
          "[formats][multiplayer-io]") {
    for (const bool complete : {false, true}) {
        auto runtime = incoming();
        runtime.saved.resume_civ = 2;
        const auto before = runtime;
        REQUIRE(write_multiplayer_item(
                    [&](std::span<const Byte> bytes) {
                        REQUIRE(bytes.size() == multiplayer_record_size);
                        REQUIRE(bytes[0x464] == 5);
                        REQUIRE(runtime.saved.resume_civ == 2);
                        return complete;
                    },
                    runtime, 261) == complete);
        auto expected = before;
        REQUIRE(runtime == expected);
    }
    auto runtime = incoming();
    const auto before_write = runtime;
    REQUIRE_THROWS_AS(
        write_multiplayer_item(
            [](std::span<const Byte>) -> bool { throw std::runtime_error("Write stopped"); },
            runtime, -1),
        std::runtime_error);
    REQUIRE(runtime == before_write);
}
TEST_CASE("F06 successful-load mode adaptation keeps entry-zero asymmetry and unrelated state",
          "[state][multiplayer-io]") {
    for (const Byte entry : {Byte{0}, Byte{1}, Byte{3}})
        for (const Byte loaded : {Byte{0}, Byte{1}, Byte{3}}) {
            auto runtime = incoming();
            const auto before = runtime;
            runtime.resume_civ = 3;
            state::GlobalValues globals;
            globals.multiplayer = static_cast<state::GameMode>(loaded);
            globals.civs_alive = 0x0b;
            globals.human_players = 0;
            globals.player_civ = 5;
            state::MultiplayerPreferences preferences{true, true, true, true};
            state::adapt_loaded_mode(static_cast<state::GameMode>(entry), globals, runtime,
                                     preferences);
            auto expected = before;
            expected.resume_civ = 3;
            if (entry != loaded) {
                expected.saved.open_game = 1;
                if (entry == 0)
                    expected.saved.double_production = expected.saved.double_movement = 0;
            }
            REQUIRE(runtime == expected);
            REQUIRE(static_cast<unsigned>(globals.multiplayer) == (entry == 0 ? loaded : entry));
            REQUIRE(globals.human_players == (entry == loaded ? 0 : 8));
            REQUIRE(globals.player_civ == 5);
            REQUIRE(preferences.simultaneous == (entry == loaded));
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
        globals.multiplayer = state::GameMode::tcp;
        globals.human_players = test.humans;
        globals.civs_alive = test.alive;
        state::MultiplayerPreferences preferences;
        state::adapt_loaded_mode(state::GameMode::single_player, globals, runtime, preferences);
        REQUIRE(globals.human_players == test.expected);
    }
}
