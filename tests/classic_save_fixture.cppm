export module openciv.tests.classic_save_fixture;

import std;
export import openciv.tests.service_callbacks;
export import openciv.tests.classic_stream_fixture;
export import openciv.session.classic_save;
export import openciv.session.runtime;
export import openciv.formats.classic_save;

export namespace openciv::test {
struct ClassicSaveFixture {
    state::ProcessContext process;
    session::SessionRuntime runtime{process, base::RuntimeId{72}};
    session::ClassicFileState files;
    session::CoreSaveCallbacks services;
    formats::Bytes bytes;
    std::string path;
    std::size_t opens = 0, writes = 0, closes = 0;
    bool fail_open = false, fail_write = false, fail_close = false;
    bool throw_write = false, throw_close = false;
    std::size_t write_limit = std::numeric_limits<std::size_t>::max();
    std::function<void()> observe;
    ClassicSaveFixture() {
        runtime.begin_client_session({10, 20});
        process.tokens = {100, 200};
        process.rules->text.initialize();
        const auto label = state::NameReference{process.rules->text.store("Label"), false};
        for (auto& leader : process.rules->leaders) {
            leader.leader_name = leader.plural = leader.adjective = label;
        }
        auto& w = runtime.world();
        w.globals.player_civ = 1;
        w.globals.saved_current_civ = -7;
        w.globals.turn_civ = 3;
        w.globals.turn = 31;
        w.globals.unit_end = w.globals.city_end = 1;
        w.globals.num_enemies = 99;
        w.globals.game_flags = 0x40;
        w.civilizations[1].units_active.fill(55);
        w.civilizations[1].units_in_production.fill(66);
        w.entities.units.set_end(1);
        w.entities.cities.set_end(1);
        auto& unit = w.entities.units.at(state::UnitSlot{0});
        unit.token = state::UnitToken{7};
        unit.owner = state::CivilizationSlot{1};
        auto& city = w.entities.cities.at(state::CitySlot{0});
        city.token = state::CityToken{8};
        city.owner = state::CivilizationSlot{1};
        w.map.allocate(world::Geometry{4, 2});
        w.map.pattern_seed = 19;
        for (unsigned civ = 1; civ < 8; ++civ)
            std::ranges::fill(w.map.remembered_layer(civ), 9);
        w.map.coarse_grid(0)[0] = 3;
        w.map.coarse_grid(1)[0] = 4;
        w.map.regions[0].land = 91; // Deliberately stale: saving must not rebuild it.
        w.power_history[149][7] = 81;
        w.views[7].zoom = -7;
        w.eliminations.names[11] = "Last leader";
        w.multiplayer.local_human = 2;
        w.multiplayer.network_turn_civ = 5;
        w.multiplayer.saved.resume_civ = 6;
        w.multiplayer.hotseat_participants = 6;
        files.cursor = {-2, 7};
        files.current_screen = {10, 20, 650, 500};
        services.open = [&](std::string_view selected) -> std::unique_ptr<session::CoreOutput> {
            ++opens;
            path = selected;
            if (observe)
                observe();
            if (fail_open)
                return {};
            return test::output_file({[&](std::span<const formats::Byte> value) {
                                          ++writes;
                                          if (observe)
                                              observe();
                                          if (throw_write)
                                              throw std::runtime_error("write failure");
                                          const auto count = std::min(value.size(), write_limit);
                                          bytes.insert(bytes.end(), value.begin(),
                                                       value.begin() + count);
                                          return !fail_write && count == value.size();
                                      },
                                      [&] {
                                          ++closes;
                                          if (observe)
                                              observe();
                                          if (throw_close)
                                              throw std::runtime_error("close failure");
                                          return !fail_close;
                                      }});
        };
    }
    session::CoreSaveStatus save(bool scenario = false,
                                 std::string_view requested = "C:\\saves\\Name") {
        return session::save_classic_core(runtime, files, requested, scenario, services);
    }
    void events() {
        auto& events = runtime.world().events;
        events.loaded = true;
        auto event = events.collection.append();
        event->values.turn = 17;
        event->set_string(0, state::EventString{"Settlers"});
        event->set_string(4, state::EventString{""});
        event->set_string(34, state::EventString{"sound.wav"});
    }
};
} // namespace openciv::test
