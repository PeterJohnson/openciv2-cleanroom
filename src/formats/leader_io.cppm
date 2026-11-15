export module openciv.formats.leader_io;

import std;
export import openciv.formats.classic_io;
export import openciv.formats.save_records;
export import openciv.state.rule_state;

export namespace openciv::formats {
/// Encode live leader names as canonical custom/noncustom markers for F06 storage.
StoredLeader stored_leader(const state::Leader& leader);
/// The saved map cursor coordinates written alongside leader state.
struct SavedCursor {
    std::int16_t x = 0, y = 0;
};
/// Write leader and naming state in classic save order; report write failure.
bool write_leader_items(const WriteItem& write, const state::GlobalValues& globals,
                        std::span<const state::Civilization> civilizations,
                        std::span<const state::Leader> leaders, const SavedCursor& cursor);
} // namespace openciv::formats

module :private;
namespace openciv::formats {
namespace {
Bytes word(std::int16_t value) {
    ByteWriter bytes;
    bytes.i16(value);
    return std::move(bytes.bytes);
}
std::size_t tribe(std::int32_t civ, std::span<const state::Civilization> civilizations,
                  std::size_t leader_count) {
    if (civ < 0 || static_cast<std::size_t>(civ) >= civilizations.size())
        throw std::out_of_range("Leader selection has no civilization backing");
    const auto selected = civilizations[static_cast<std::size_t>(civ)].tribe;
    if (selected < 0 || static_cast<std::size_t>(selected) >= leader_count || selected >= 21)
        throw std::out_of_range("Leader selection has no tribe backing");
    return static_cast<std::size_t>(selected);
}
template <class Record> bool selected_records(const state::GlobalValues& globals, Record record) {
    if (globals.multiplayer == state::GameMode::single_player)
        return record(globals.player_civ);
    for (unsigned civ = 0; civ < 8; ++civ)
        if ((globals.human_players & (1U << civ)) && !record(static_cast<std::int32_t>(civ)))
            return false;
    return true;
}
} // namespace
StoredLeader stored_leader(const state::Leader& live) {
    StoredLeader result;
    result.attack = live.attack;
    result.expand = live.expand;
    result.civilize = live.civilize;
    result.times_used = live.times_used;
    result.female = live.female;
    result.city_name_index = live.city_name_index;
    result.color = live.color;
    result.city_style = live.city_style;
    // F06 consumes signs after rebuilding names from the selected rules. A
    // canonical nonzero magnitude avoids coupling export to runtime text IDs.
    const auto marker = [](state::NameReference name) -> std::int16_t {
        return name.custom ? -1 : 1;
    };
    result.leader_name = marker(live.leader_name);
    result.plural = marker(live.plural);
    result.adjective = marker(live.adjective);
    for (std::size_t sex = 0; sex < 2; ++sex)
        result.names[sex] = marker(live.names[sex]);
    for (std::size_t g = 0; g < 7; ++g)
        for (std::size_t sex = 0; sex < 2; ++sex)
            result.titles[g][sex] = marker(live.titles[g][sex]);
    return result;
}
bool write_leader_items(const WriteItem& write, const state::GlobalValues& globals,
                        std::span<const state::Civilization> civilizations,
                        std::span<const state::Leader> leaders, const SavedCursor& cursor) {
    for (std::size_t i = 0; i < 21; ++i) {
        if (i >= leaders.size())
            throw std::out_of_range("Tribe triplet has no leader backing");
        if (!write(Bytes{leaders[i].times_used}) || !write(Bytes{leaders[i].city_name_index}) ||
            !write(Bytes{leaders[i].female}))
            return false;
    }
    if (!write(word(cursor.x)) || !write(word(cursor.y)))
        return false;
    return selected_records(globals, [&](std::int32_t civ) {
        return write(
            encode_leader(stored_leader(leaders[tribe(civ, civilizations, leaders.size())])));
    });
}
} // namespace openciv::formats
