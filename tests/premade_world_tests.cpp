// F05 section 3, F06 section 4, F01 clock mapping, W01 retained map fields.
// Independently authored binary inputs and specification expectations. No
// original executable, capture payload or historical harness is a test input.
#include "session/premade_world.hpp"
#include "session/runtime.hpp"
#include "session/world_setup.hpp"
#include <catch2/catch_test_macros.hpp>
#include <algorithm>

using namespace openciv;
using namespace openciv::session;
namespace {
formats::Bytes raw_map(std::uint16_t seed = 24, std::uint16_t topology = 0xfffe,
                       bool hints = true) {
    formats::ByteWriter out;
    out.append(formats::encode_map_header({4, 2, 99, topology, seed, 77, 88}));
    for (unsigned i = 0; i < 21; ++i)
        out.i16(hints && i == 0 ? 2 : (hints && i == 20 ? 300 : -1));
    for (unsigned i = 0; i < 21; ++i)
        out.i16(hints && i == 0 ? 1 : (hints && i == 20 ? -7 : -1));
    out.append(formats::Bytes{0xc2, 0xff, 0xff, 61, 0x55, 0x73, 0x8a, 0x42, 0x45, 62, 0xaa, 0xf7,
                              0x19, 0x81, 0x20, 63, 0xff, 0xe9, 0xf0, 0x55, 0x9b, 3,  0x01, 0x21});
    return std::move(out.bytes);
}
formats::Bytes tiles(const world::MapStorage& map) {
    formats::Bytes bytes;
    for (const auto& tile : map.tiles()) {
        const auto record = formats::encode_tile(tile);
        bytes.insert(bytes.end(), record.begin(), record.end());
    }
    return bytes;
}
struct Fixture {
    state::ProcessContext process;
    SessionRuntime runtime{process, base::RuntimeId{93}};
    formats::StartingPositions starts;
    PremadeWorldServices services;
    formats::Bytes input = raw_map();
    std::size_t position = 0;
    bool open_failure = false;
    std::int32_t seed_response = 1, hint_response = 1;
    std::uint32_t tick = 0x12345678U;
    std::vector<std::string> trace;
    std::vector<std::size_t> reads;
    std::vector<world::StorageRequest> allocations;
    std::function<void(std::string_view)> observe;
    Fixture() {
        auto& map = runtime.world().map;
        map.allocate({8, 4});
        map.pattern_seed = 55;
        map.stored_topology = 0;
        map.regions[3] = {10, 20, 30, 0x12345678, {42, 43}};
        map.remembered_barbarian = {7, 8, 9};
        map.tile({0, 0}).region = 17;
        map.tile({0, 0}).features = 0x42;
        for (unsigned civ = 1; civ < 8; ++civ)
            std::fill(map.remembered_layer(civ).begin(), map.remembered_layer(civ).end(), 77);
        for (unsigned grid = 0; grid < 4; ++grid)
            std::fill(map.coarse_grid(grid).begin(), map.coarse_grid(grid).end(), 42);
        process.fallback = formats::decode_tile(formats::Bytes{0x82, 0xff, 0xe1, 17, 15, 0x32});
        starts.x.fill(0x1234);
        starts.y.fill(0x5678);
        runtime.world().world_setup = {99, 88, {1, 2, 3, 4, 5}};
        runtime.world().globals.game_flags = 0xffff;
        runtime.world().globals.rules = 0x1555;
        services.open = [&](std::string_view path) -> std::unique_ptr<CoreInput> {
            CHECK(path == "authored.mp");
            event("open");
            if (open_failure)
                return {};
            position = 0;
            return std::make_unique<CoreInput>(
                CoreInput{[&](std::size_t count) {
                              reads.push_back(count);
                              event("read");
                              const auto size = std::min(count, input.size() - position);
                              const auto bytes = std::span{input}.subspan(position, size);
                              formats::Bytes prefix(bytes.begin(), bytes.end());
                              position += size;
                              return formats::ReadResult{std::move(prefix), size == count};
                          },
                          {},
                          [&] {
                              event("close");
                              return false;
                          }});
        };
        services.allocate = [&](world::StorageRequest request) {
            allocations.push_back(request);
            event("allocate");
        };
        services.load_failure = [&] { event("failure"); };
        services.choices.use_seed = [&] {
            event("seed");
            return seed_response;
        };
        services.choices.use_start_locations = [&] {
            event("hints");
            return hint_response;
        };
        services.choices.ticks = [&] {
            event("ticks");
            return tick;
        };
    }
    void event(std::string_view name) {
        trace.emplace_back(name);
        if (observe)
            observe(name);
    }
    auto load() { return load_premade_world(runtime, starts, "authored.mp", services); }
    void read_without_preparation() {
        formats::MemoryItems source{input};
        REQUIRE(formats::read_map_items([&](auto count) { return source.read(count); },
                                        formats::MapStream::raw, runtime.world().map, starts));
    }
};
} // namespace

TEST_CASE("F05 premade loading composes F06 normalization and retained rule topology",
          "[premade-world][session]") {
    Fixture f;
    const auto fallback = f.process.fallback;
    const auto regions = f.runtime.world().map.regions;
    const auto preferences = f.process.ordinary_preferences;
    f.input.insert(f.input.end(), {0xde, 0xad});
    const auto original = f.input;
    REQUIRE(f.load() == WorldSource::premade_flat);
    auto& map = f.runtime.world().map;
    CHECK(f.position == 122); // No exact-EOF read or trailing-byte consumption.
    CHECK(f.reads == std::vector<std::size_t>{14, 42, 42, 24});
    REQUIRE(f.allocations.size() == 12);
    CHECK(f.allocations.front() == world::StorageRequest{world::StorageKind::tiles, 0, 4});
    CHECK(f.allocations.back() == world::StorageRequest{world::StorageKind::coarse, 3, 1});
    CHECK(f.trace[f.trace.size() - 3] == "close");
    CHECK(f.trace[f.trace.size() - 2] == "seed");
    CHECK(f.trace.back() == "hints");
    CHECK(map.geometry().width() == 4);
    CHECK(map.geometry().height() == 2);
    CHECK(map.geometry().area() == 4);
    CHECK(map.geometry().coarse_width() == 1);
    CHECK(map.pattern_seed == 24);
    CHECK(map.stored_topology == 0xfffe);
    CHECK(tiles(map) == formats::Bytes{0xc2, 0, 0, 0, 0, 0xf0, 0x8a, 0, 0, 0, 0, 0xf0,
                                       0x19, 0, 0, 0, 0, 0xf0, 0xf0, 0, 0, 0, 0, 0xf0});
    CHECK(map.regions == regions);
    CHECK(f.process.fallback == fallback);
    CHECK(map.remembered_barbarian == formats::Bytes{7, 8, 9});
    for (unsigned civ = 1; civ < 8; ++civ)
        CHECK(std::all_of(map.remembered_layer(civ).begin(), map.remembered_layer(civ).end(),
                          [](auto byte) { return byte == 0; }));
    for (unsigned grid = 0; grid < 4; ++grid)
        CHECK(std::all_of(map.coarse_grid(grid).begin(), map.coarse_grid(grid).end(),
                          [](auto cell) { return !cell; }));
    CHECK(f.starts.x[0] == 2);
    CHECK(f.starts.y[0] == 1);
    CHECK(f.starts.x[20] == 300);
    CHECK(f.starts.y[20] == -7); // No invented hint validation.
    CHECK(f.runtime.world().globals.rules == 0x9555);
    CHECK(f.runtime.world().globals.game_flags == 0xffff);
    CHECK(f.runtime.world().world_setup == state::WorldSetup{4, 2, {1, 2, 3, 4, 5}});
    CHECK(f.process.ordinary_preferences == preferences);
    CHECK(f.input == original);
    CHECK_FALSE(f.runtime.active());
}

TEST_CASE("F05 preparation clears seven remembered layers while retaining other map storage",
          "[premade-world][session]") {
    Fixture f;
    f.read_without_preparation();
    auto& map = f.runtime.world().map;
    for (unsigned civ = 1; civ < 8; ++civ)
        std::fill(map.remembered_layer(civ).begin(), map.remembered_layer(civ).end(),
                  static_cast<std::uint8_t>(civ));
    for (unsigned grid = 0; grid < 4; ++grid)
        map.coarse_grid(grid)[0] = static_cast<std::uint8_t>(grid + 10);
    const auto revision = map.storage_revision();
    const auto setup = f.runtime.world().world_setup;
    CHECK(prepare_premade_world(f.runtime, f.starts, f.services.choices) ==
          WorldSource::premade_flat);
    for (unsigned civ = 1; civ < 8; ++civ)
        CHECK(std::all_of(map.remembered_layer(civ).begin(), map.remembered_layer(civ).end(),
                          [](auto byte) { return byte == 0; }));
    for (unsigned grid = 0; grid < 4; ++grid)
        CHECK(map.coarse_grid(grid)[0] == grid + 10);
    CHECK(map.storage_revision() == revision);
    CHECK(map.remembered_barbarian == formats::Bytes{7, 8, 9});
    CHECK(f.runtime.world().world_setup == setup);
}

TEST_CASE("F05 low pattern seeds use one clock sample without asking or touching gameplay RNG",
          "[premade-world][session]") {
    for (const auto seed : {std::uint16_t{0}, std::uint16_t{1}})
        for (const auto tick : {0U, 32768U, 32767U, 0xffffffffU}) {
            Fixture f;
            f.runtime.begin_client_session({17, 29});
            const auto random = f.runtime.gameplay_random().checkpoint();
            f.input = raw_map(seed, 0, false);
            f.tick = tick;
            f.services.choices.use_seed = {};
            f.services.choices.use_start_locations = {};
            REQUIRE(f.load() == WorldSource::premade_cylindrical);
            CHECK(f.runtime.world().map.pattern_seed == (tick == 0 || tick == 32768 ? 1 : 32767));
            CHECK(std::count(f.trace.begin(), f.trace.end(), "ticks") == 1);
            CHECK(f.runtime.world().globals.rules == 0x1555);
            CHECK(f.runtime.gameplay_random().checkpoint() == random);
        }
}

TEST_CASE("F05 seed and hint responses treat only zero as discard", "[premade-world][session]") {
    for (const std::int32_t response : {-9, -1, 0, 1, 2}) {
        Fixture f;
        f.seed_response = f.hint_response = response;
        f.tick = 0x8002;
        REQUIRE(f.load());
        CHECK(f.runtime.world().map.pattern_seed == (response == 0 ? 2 : 24));
        if (response == 0)
            CHECK(f.starts == formats::StartingPositions{});
        else {
            CHECK(f.starts.x[20] == 300);
            CHECK(f.starts.y[20] == -7);
        }
        CHECK(std::count(f.trace.begin(), f.trace.end(), "ticks") == (response == 0 ? 1 : 0));
    }
}

TEST_CASE("F05 hint prompt scans all x values and ignores y validity", "[premade-world][session]") {
    for (const std::size_t slot : {std::size_t{0}, std::size_t{20}}) {
        Fixture f;
        f.read_without_preparation();
        f.starts.x.fill(-2);
        f.starts.y.fill(30000);
        f.starts.x[slot] = 0;
        f.hint_response = 0;
        CHECK(prepare_premade_world(f.runtime, f.starts, f.services.choices) ==
              WorldSource::premade_flat);
        CHECK(f.trace == std::vector<std::string>{"seed", "hints"});
        CHECK(f.starts == formats::StartingPositions{});
    }
    Fixture f;
    f.read_without_preparation();
    f.starts.x.fill(-2);
    f.starts.y.fill(1);
    const auto starts = f.starts;
    f.services.choices.use_start_locations = {};
    CHECK(prepare_premade_world(f.runtime, f.starts, f.services.choices) ==
          WorldSource::premade_flat);
    CHECK(f.starts == starts);
    CHECK(f.trace == std::vector<std::string>{"seed"});
}

TEST_CASE("F05 choices observe normalized state and later gates consume live mutations",
          "[premade-world][session]") {
    Fixture f;
    f.input = raw_map(24, 0, false);
    f.services.choices.use_seed = [&] {
        CHECK(f.runtime.world().map.tile({0, 0}).features == 0);
        CHECK_FALSE(f.runtime.world().map.tile({0, 0}).owner);
        CHECK(f.runtime.world().globals.rules == 0x1555);
        f.runtime.world().map.pattern_seed = 99;
        f.starts.x[20] = 123;
        f.runtime.world().globals.rules = 0x0042;
        return -1;
    };
    f.services.choices.use_start_locations = [&] {
        CHECK(f.runtime.world().map.pattern_seed == 99);
        CHECK(f.starts.x[20] == 123);
        f.runtime.world().map.stored_topology = 7;
        f.runtime.world().globals.rules |= 0x0100;
        return 1;
    };
    REQUIRE(f.load() == WorldSource::premade_flat);
    CHECK(f.runtime.world().globals.rules == 0x8142);
    CHECK(f.runtime.world().map.pattern_seed == 99);
}

TEST_CASE("F05 cancellation and open failure leave map starts dimensions and flags unchanged",
          "[premade-world][session]") {
    Fixture f;
    const auto starts = f.starts;
    const auto setup = f.runtime.world().world_setup;
    const auto globals = f.runtime.world().globals;
    const auto old_tiles = tiles(f.runtime.world().map);
    CHECK_FALSE(load_premade_world(f.runtime, f.starts, {}, {}));
    CHECK(f.trace.empty());
    f.open_failure = true;
    CHECK_FALSE(f.load());
    CHECK(f.trace == std::vector<std::string>{"open", "failure"});
    CHECK(f.starts == starts);
    CHECK(f.runtime.world().world_setup == setup);
    CHECK(f.runtime.world().globals == globals);
    CHECK(tiles(f.runtime.world().map) == old_tiles);
    CHECK_THROWS_AS(load_premade_world(f.runtime, f.starts, std::string("x\0y", 3), {}),
                    std::invalid_argument);
}

TEST_CASE("F05 short raw-map items close then report failure without normalizing their prefix",
          "[premade-world][session]") {
    for (const std::size_t available :
         {0U, 13U, 14U, 15U, 55U, 56U, 57U, 97U, 98U, 99U, 100U, 101U, 121U}) {
        Fixture f;
        f.input.resize(available);
        const auto setup = f.runtime.world().world_setup;
        const auto globals = f.runtime.world().globals;
        CHECK_FALSE(f.load());
        REQUIRE(f.trace.size() >= 3);
        CHECK(f.trace[f.trace.size() - 2] == "close");
        CHECK(f.trace.back() == "failure");
        CHECK(f.runtime.world().globals == globals);
        auto& map = f.runtime.world().map;
        if (available < 98) {
            CHECK(f.runtime.world().world_setup == setup);
            CHECK(map.geometry().width() == 8);
            CHECK(map.pattern_seed == 55);
            CHECK(map.tile({0, 0}).region == 17);
        } else {
            CHECK(f.runtime.world().world_setup.width == 4);
            CHECK(f.runtime.world().world_setup.height == 2);
            CHECK(map.pattern_seed == 24);
            CHECK(map.stored_topology == 0xfffe);
            CHECK(map.tile({2, 1}).owner.has_value()); // Never F05-normalized to Unowned.
            if (available >= 100)
                CHECK(map.tile({0, 0}).features == 0xff);
        }
        if (available == 15)
            CHECK(f.starts.x[0] == 0x1202);
        if (available == 57) {
            CHECK(f.starts.x[0] == 2);
            CHECK(f.starts.y[0] == 0x5601);
        }
        if (available < 14) {
            CHECK(f.starts.x[0] == 0x1234);
            CHECK(f.starts.y[0] == 0x5678);
        }
        CHECK(std::count(f.trace.begin(), f.trace.end(), "close") == 1);
    }
}

TEST_CASE("F05 allocation failures publish loaded dimensions retain buffers and close once",
          "[premade-world][session]") {
    for (std::size_t stop = 0; stop < 12; ++stop) {
        Fixture f;
        std::size_t calls = 0;
        f.services.allocate = [&](auto) {
            CHECK(f.runtime.world().world_setup.width == 4);
            CHECK(f.runtime.world().world_setup.height == 2);
            if (calls++ == stop)
                throw std::bad_alloc{};
        };
        CHECK_THROWS_AS(f.load(), std::bad_alloc);
        CHECK(calls == stop + 1);
        CHECK(f.reads == std::vector<std::size_t>{14, 42, 42});
        CHECK(f.trace.back() == "close");
        CHECK_FALSE(f.runtime.world().map.allocated());
        CHECK(f.runtime.world().map.pattern_seed == 24);
        CHECK(f.runtime.world().globals.rules == 0x1555);
        CHECK(f.starts.x[20] == 300);
        if (stop > 0)
            CHECK(f.runtime.world().map.tile({0, 0}) == world::Tile{});
        else
            CHECK_THROWS_AS(f.runtime.world().map.tiles(), std::logic_error);
    }
}

TEST_CASE("F05 returned close failure is ignored but close exceptions do not normalize or retry",
          "[premade-world][session]") {
    Fixture f;
    f.observe = [&](auto event) {
        if (event == "close")
            throw std::runtime_error("close");
    };
    CHECK_THROWS_AS(f.load(), std::runtime_error);
    CHECK(std::count(f.trace.begin(), f.trace.end(), "close") == 1);
    CHECK(f.runtime.world().map.tile({0, 0}).features == 0xff);
    CHECK(f.runtime.world().globals.rules == 0x1555);
    CHECK(f.trace.back() == "close");
}

TEST_CASE("F05 missing choice services retain normalization and earlier clock or hint effects",
          "[premade-world][session]") {
    for (int stop = 0; stop < 3; ++stop) {
        Fixture f;
        if (stop == 0)
            f.services.choices.use_seed = {};
        if (stop == 1) {
            f.seed_response = 0;
            f.services.choices.ticks = {};
        }
        if (stop == 2) {
            f.seed_response = 0;
            f.services.choices.use_start_locations = {};
        }
        CHECK_THROWS_AS(f.load(), std::bad_function_call);
        CHECK(f.runtime.world().map.tile({0, 0}).features == 0);
        CHECK_FALSE(f.runtime.world().map.tile({0, 0}).owner);
        CHECK(f.runtime.world().globals.rules == 0x1555);
        CHECK(f.runtime.world().map.pattern_seed == (stop == 2 ? 0x5678 : 24));
        CHECK(f.starts.x[20] == 300);
        CHECK(std::count(f.trace.begin(), f.trace.end(), "close") == 1);
    }
}

TEST_CASE("F05 second premade game replaces flat rule but retains classifications and parameters",
          "[premade-world][session]") {
    Fixture f;
    REQUIRE(f.load() == WorldSource::premade_flat);
    f.runtime.world().globals.game_flags = 6;
    f.input = raw_map(99, 0, false);
    f.services.choices.use_start_locations = {};
    REQUIRE(f.load() == WorldSource::premade_cylindrical);
    CHECK(f.runtime.world().globals.rules == 0x1555);
    CHECK(f.runtime.world().globals.game_flags == 6);
    CHECK(f.runtime.world().world_setup.parameters == std::array<std::int32_t, 5>{1, 2, 3, 4, 5});
    CHECK(f.runtime.world().map.regions[3].fertile == 20);
    CHECK(f.starts == formats::StartingPositions{});
}

TEST_CASE("F05 map I/O allocation choices and failure notices publish before external callbacks",
          "[premade-world][session]") {
    Fixture f;
    f.runtime.begin_client_session({1, 2});
    auto& unit = f.runtime.world().entities.units.storage()[0];
    unit.token = state::UnitToken{1};
    unit.owner = state::CivilizationSlot{1};
    f.runtime.world().entities.units.set_end(1);
    const auto client = f.runtime.bind(f.runtime.add_participant(state::CivilizationSlot{1}),
                                       ControllerKind::frontend);
    std::int16_t next = 7;
    unit.x = next;
    f.observe = [&](auto) {
        REQUIRE(client.own_units().records.size() == 1);
        CHECK(client.own_units().records[0].x == next);
        unit.x = ++next;
    };
    f.seed_response = 0;
    REQUIRE(f.load());
    CHECK(client.own_units().records[0].x == next);
    f.input.resize(13);
    CHECK_FALSE(f.load());
    CHECK(client.own_units().records[0].x == next);
    f.open_failure = true;
    CHECK_FALSE(f.load());
    CHECK(client.own_units().records[0].x == next);
}

TEST_CASE("F05 malformed item and allocation reentry stop safely with closed input",
          "[premade-world][session]") {
    Fixture f;
    f.input[0] = 3;
    CHECK_THROWS_AS(f.load(), formats::DecodeError);
    CHECK(f.trace == std::vector<std::string>{"open", "read", "close"});
    CHECK(f.runtime.world().world_setup.width == 99);
    f.input = raw_map();
    f.trace.clear();
    f.services.allocate = [&](auto) { f.runtime.world().map.allocate({8, 4}); };
    CHECK_THROWS_AS(f.load(), std::out_of_range);
    CHECK(f.trace.back() == "close");
    CHECK(f.runtime.world().world_setup.width == 4);
}

TEST_CASE("F05 premade result still uses the automatic parameter branch after success or failure",
          "[premade-world][session]") {
    for (const bool fail : {false, true}) {
        Fixture f;
        f.open_failure = fail;
        const auto source = f.load();
        std::vector<base::Choice> choices;
        for (const auto site : {"land-mass", "land-form", "temperature", "climate", "age"})
            choices.push_back(base::make_choice({std::string("F05.world.") + site, "premade", 0},
                                                base::PrimaryModulo{3}, std::int32_t{2}));
        base::GameplayRandom random{base::ControlledChoices{choices}, "setup"};
        CHECK(select_world_parameters(f.runtime, random, "premade", false, source.has_value(),
                                      {}) == !fail);
        random.require_choices_consumed();
        CHECK(f.runtime.world().world_setup.parameters ==
              std::array<std::int32_t, 5>{1, 0, 1, 1, 1});
    }
}
