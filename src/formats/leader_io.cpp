#include "leader_io.hpp"
#include <algorithm>
#include <bit>

namespace openciv::formats {
namespace {
ReadResult read_item(const ReadItem& read, std::size_t size) {
    auto result = read(size);
    if (result.prefix.size() > size || (result.complete && result.prefix.size() != size))
        throw std::invalid_argument("Item service returned an inconsistent transferred prefix");
    return result;
}
bool read_byte(const ReadItem& read, std::uint8_t& value) {
    const auto result = read_item(read, 1);
    if (!result.prefix.empty())
        value = result.prefix[0];
    return result.complete;
}
bool read_word(const ReadItem& read, std::int16_t& value) {
    const auto result = read_item(read, 2);
    // Publish the returned prefix over the live destination after the service.
    ByteWriter bytes;
    bytes.i16(value);
    std::copy(result.prefix.begin(), result.prefix.end(), bytes.bytes.begin());
    value = ByteReader{bytes.bytes}.i16();
    return result.complete;
}
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
template <class Record> bool selected_records(state::GlobalValues& globals, Record record) {
    if (globals.multiplayer == 0)
        return record();
    struct Restore {
        std::int8_t& value;
        std::int8_t prior;
        ~Restore() { value = prior; }
    } restore{globals.player_civ, globals.player_civ};
    for (unsigned civ = 0; civ < 8; ++civ)
        if (globals.human_players & (1U << civ)) {
            globals.player_civ = static_cast<std::int8_t>(civ);
            if (!record())
                return false;
        }
    return true;
}
std::string_view fixed_text(const state::FixedText<24>& text) {
    if (!text.terminated())
        throw std::out_of_range("Civilization name has no backed terminator");
    const auto bytes = text.text();
    return {reinterpret_cast<const char*>(bytes.data()), bytes.size()};
}
void copy_label(state::FixedText<24>& target, state::NameReference name,
                const state::TextPool& pool) {
    if (name.custom)
        return;
    const auto bytes = pool.text(name.id);
    if (bytes.size() >= target.bytes.size())
        throw std::out_of_range("Rule label does not fit the saved civilization name field");
    for (std::size_t i = 0; i < bytes.size(); ++i)
        target.bytes[i] = static_cast<std::uint8_t>(bytes[i]);
    target.bytes[bytes.size()] = 0;
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
StoredLeader LeaderScratchRecord::value() const {
    if (backed != bytes.size())
        throw std::out_of_range("Saved leader scratch record lacks complete backing");
    return decode_leader(bytes);
}
void LeaderScratchRecord::assign(const StoredLeader& leader) {
    const auto encoded = encode_leader(leader);
    std::copy(encoded.begin(), encoded.end(), bytes.begin());
    backed = bytes.size();
}
bool read_leader_items(const ReadItem& read, state::GlobalValues& globals,
                       std::span<const state::Civilization> civilizations,
                       std::span<state::Leader> leaders, SavedCursor& cursor,
                       LeaderLoadScratch& scratch) {
    for (std::size_t i = 0; i < 21; ++i) {
        if (i >= leaders.size())
            throw std::out_of_range("Tribe triplet has no leader backing");
        auto& live = leaders[i];
        if (!read_byte(read, live.times_used) || !read_byte(read, live.city_name_index) ||
            !read_byte(read, live.female))
            return false;
    }
    if (!read_word(read, cursor.x) || !read_word(read, cursor.y))
        return false;
    return selected_records(globals, [&] {
        const auto selected = tribe(globals.player_civ, civilizations, leaders.size());
        auto& target = scratch.by_tribe[selected];
        const auto result = read_item(read, leader_record_size);
        if (target.backed > target.bytes.size())
            throw std::out_of_range("Saved leader scratch backing exceeds its record");
        std::copy(result.prefix.begin(), result.prefix.end(), target.bytes.begin());
        target.backed = std::max(target.backed, result.prefix.size());
        return result.complete;
    });
}
bool write_leader_items(const WriteItem& write, state::GlobalValues& globals,
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
    return selected_records(globals, [&] {
        return write(encode_leader(
            stored_leader(leaders[tribe(globals.player_civ, civilizations, leaders.size())])));
    });
}
void restore_leader_names(bool scenario, state::GlobalValues& globals,
                          std::span<state::Civilization> civilizations,
                          std::span<const state::CivilizationNames> names, state::RuleState& rules,
                          const LeaderLoadScratch& scratch) {
    for (auto& live : rules.leaders) {
        if (live.female >= live.names.size())
            throw std::out_of_range("Leader gender has no selected-name backing");
        live.leader_name = live.names[live.female];
        live.plural.custom = false;
        live.adjective.custom = false;
        for (auto& pair : live.titles)
            for (auto& id : pair)
                id.custom = false;
    }
    if (!scenario) {
        selected_records(globals, [&] {
            const auto selected = tribe(globals.player_civ, civilizations, rules.leaders.size());
            auto& live = rules.leaders[selected];
            live.leader_name.custom = !live.leader_name.custom;
            const auto saved = scratch.by_tribe[selected].value();
            if (saved.plural < 0)
                live.plural.custom = !live.plural.custom;
            if (saved.adjective < 0)
                live.adjective.custom = !live.adjective.custom;
            for (std::size_t g = 0; g < live.titles.size(); ++g)
                for (std::size_t sex = 0; sex < 2; ++sex)
                    if (saved.titles[g][sex] < 0)
                        live.titles[g][sex].custom = !live.titles[g][sex].custom;
            return true;
        });
        return;
    }
    for (std::size_t civ = 1; civ < 8; ++civ) {
        const auto selected =
            tribe(static_cast<std::int32_t>(civ), civilizations, rules.leaders.size());
        auto& live = rules.leaders[selected];
        live.female = (civilizations[civ].flags & 0x200U) ? 1 : 0;
        civilizations[civ].leader_portrait = static_cast<std::uint8_t>(selected + 21 * live.female);
        if (civ >= names.size())
            throw std::out_of_range("Scenario civilization has no name backing");
        if (fixed_text(names[civ].tribe) == rules.text.text(live.plural.id))
            for (std::size_t g = 0; g < live.titles.size(); ++g)
                for (std::size_t sex = 0; sex < 2; ++sex)
                    live.titles[g][sex] =
                        state::NameReference{rules.names.government_titles[g][sex], false};
        live.leader_name.custom = !live.leader_name.custom;
        live.plural.custom = !live.plural.custom;
        live.adjective.custom = !live.adjective.custom;
    }
}
void prepare_scenario_names(bool scenario_export, const state::GlobalValues& globals,
                            std::span<const state::Civilization> civilizations,
                            std::span<state::CivilizationNames> names,
                            const state::RuleState& rules) {
    if (!scenario_export && !(globals.game_flags & 0x80U))
        return;
    for (std::size_t civ = 1; civ < 8; ++civ) {
        const auto selected =
            tribe(static_cast<std::int32_t>(civ), civilizations, rules.leaders.size());
        if (civ >= names.size())
            throw std::out_of_range("Scenario civilization has no name backing");
        const auto& live = rules.leaders[selected];
        copy_label(names[civ].adjective, live.adjective, rules.text);
        copy_label(names[civ].tribe, live.plural, rules.text);
        copy_label(names[civ].leader, live.leader_name, rules.text);
    }
}
} // namespace openciv::formats
