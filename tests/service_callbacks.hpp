#pragma once
// Test-owned callable storage; production dependencies borrow these bindings.
#include "ai/goals.hpp"
#include "formats/classic_labels.hpp"
#include "formats/classic_rules.hpp"
#include "network/stream.hpp"
#include "session/classic_file_choice.hpp"
#include "session/classic_load.hpp"
#include "session/classic_load_dialog.hpp"
#include "session/classic_preferences.hpp"
#include "session/classic_rules.hpp"
#include "session/classic_save.hpp"
#include "session/classic_save_menu.hpp"
#include "session/configuration.hpp"
#include "session/new_game.hpp"
#include "session/player_setup.hpp"
#include "session/premade_world.hpp"
#include "session/unit_operations.hpp"
#include "session/world_entry.hpp"
#include "session/world_setup.hpp"
#include "unit/deletion.hpp"
#include "unit/stack_operations.hpp"
#include "unit/stacks.hpp"
namespace openciv::formats::classic {
struct RuleLoadCallbacks {
    // Diagnostic/cleanup hook. Returning never resumes the offending parser.
    std::function<void(const RuleLoadError&)> fatal;

    operator RuleFailureObserver() const& { return fatal; }
    operator RuleFailureObserver() const&& = delete;
};
} // namespace openciv::formats::classic
namespace openciv::formats::classic {
struct LabelCallbacks {
    std::function<void()> missing_labels;

    operator MissingLabels() const& { return missing_labels; }
    operator MissingLabels() const&& = delete;
};
} // namespace openciv::formats::classic
namespace openciv::ai {
struct GoalCallbacks final : UnitMovementQueries {
    // U04 owning queries, required only at the actual redirection boundary.
    // Read-only queries on stable state. They must not mutate records/rules,
    // dispatch events or reenter goals. Missing input may report an error.
    std::function<bool(state::UnitSlot)> awake_with_moves;
    std::function<std::int32_t(state::UnitSlot)> moves_left;

    GoalCallbacks() = default;
    GoalCallbacks(const GoalCallbacks& other) { *this = other; }
    GoalCallbacks& operator=(const GoalCallbacks& other) {
        awake_with_moves = other.awake_with_moves;
        moves_left = other.moves_left;
        return *this;
    }
    bool can_act(state::UnitSlot unit) const override { return awake_with_moves(unit); }
    std::int32_t remaining_moves(state::UnitSlot unit) const override { return moves_left(unit); }
};
} // namespace openciv::ai
namespace openciv::unit {
struct DeletionCallbacks final : UnitDeletionEffects {
    std::function<void(world::Position)> removed_unit;
    std::function<void(std::int32_t)> refresh_home;
    std::function<void(std::int32_t owner, std::int32_t killer)> check_destroyed;
    std::function<void(world::Position, std::optional<state::CitySlot>)> synchronize;

    DeletionCallbacks() = default;
    DeletionCallbacks(const DeletionCallbacks& other) { *this = other; }
    DeletionCallbacks& operator=(const DeletionCallbacks& other) {
        removed_unit = other.removed_unit;
        refresh_home = other.refresh_home;
        check_destroyed = other.check_destroyed;
        synchronize = other.synchronize;
        return *this;
    }
    void unit_removed(world::Position position) const override { return removed_unit(position); }
    void refresh_home_city(std::int32_t city) const override { return refresh_home(city); }
    void check_civilization(std::int32_t owner, std::int32_t killer) const override {
        return check_destroyed(owner, killer);
    }
    void synchronize_deletion(world::Position position,
                              std::optional<state::CitySlot> home) const override {
        return synchronize(position, home);
    }
};
} // namespace openciv::unit
namespace openciv::unit {
struct StackOperationCallbacks {
    // Semantic completion notification. Transport scheduling belongs to the session.
    std::function<void()> synchronize;

    operator StackSynchronization() const& { return synchronize; }
    operator StackSynchronization() const&& = delete;
};
} // namespace openciv::unit
namespace openciv::session {
struct CoreLoadCallbacks final : FileStorage {
    std::function<std::unique_ptr<CoreInput>(std::string_view)> open;
    std::function<void()> flush;
    std::function<void(std::string_view)> change_directory;
    std::function<bool(std::string_view directory, std::string_view name)> exists;
    formats::classic::TextCase comparison;
    // One factory returns owned, format-independent inputs; no open/reload/close protocol.
    std::function<state::LoadedContent()> load_content;
    std::function<void()> content_installed;

    std::unique_ptr<CoreInput> read(std::string_view path) const override { return open(path); }
    std::unique_ptr<CoreOutput> replace(std::string_view) const override {
        throw std::logic_error("Unexpected output");
    }
    bool remove(std::string_view) const override { throw std::logic_error("Unexpected removal"); }
    operator CoreLoadDependencies() const& {
        return {this,        flush,        change_directory, exists,
                &comparison, load_content, content_installed};
    }
    operator CoreLoadDependencies() const&& = delete;
};
} // namespace openciv::session
namespace openciv::session {
struct FileChoiceCallbacks {
    std::function<FileChoiceResult(const FileChoiceRequest&)> choose;

    operator FileChoiceCallback() const& { return choose; }
    operator FileChoiceCallback() const&& = delete;
};
} // namespace openciv::session
namespace openciv::session {
struct PreferenceFileCallbacks final : FileStorage {
    std::function<std::unique_ptr<CoreInput>(std::string_view)> open_input;
    std::function<std::unique_ptr<CoreOutput>(std::string_view)> open_output;
    std::function<bool(std::string_view)> remove_file;

    PreferenceFileCallbacks() = default;
    PreferenceFileCallbacks(std::function<std::unique_ptr<CoreInput>(std::string_view)> input,
                            std::function<std::unique_ptr<CoreOutput>(std::string_view)> output,
                            std::function<bool(std::string_view)> remove)
        : open_input(std::move(input)), open_output(std::move(output)),
          remove_file(std::move(remove)) {}

    PreferenceFileCallbacks(const PreferenceFileCallbacks& other) { *this = other; }
    PreferenceFileCallbacks& operator=(const PreferenceFileCallbacks& other) {
        open_input = other.open_input;
        open_output = other.open_output;
        remove_file = other.remove_file;
        return *this;
    }
    std::unique_ptr<CoreInput> read(std::string_view path) const override {
        return open_input(path);
    }
    std::unique_ptr<CoreOutput> replace(std::string_view path) const override {
        return open_output(path);
    }
    bool remove(std::string_view path) const override { return remove_file(path); }
};
} // namespace openciv::session
namespace openciv::session {
struct PlayerSetupCallbacks final : PlayerSetupInteraction {
    // Ordinary rows or a negative cancellation. Nominal positive domains:
    // difficulty 0..5, opponents 0..4 (6 - row opponents), barbarians 0..3,
    // rules 0=standard/1=custom, gender 0..1, acceleration 0..2, style 0..3.
    std::function<std::int32_t(state::Difficulty remembered)> difficulty;
    std::function<std::int32_t(state::RuleSelection remembered)> rules;
    std::function<std::int32_t(state::Gender remembered)> gender;
    std::function<SetupNumericChoice(std::int16_t remembered)> opponents, barbarians;
    std::function<SetupAdvancedResponse(SetupAdvancedChoices defaults)> advanced;
    std::function<std::int32_t(std::array<std::uint32_t, 3> years, std::int16_t remembered)>
        acceleration;
    std::function<SetupTribeChoice(std::span<const SetupTribeOption>, std::int16_t remembered)>
        tribe;
    std::function<SetupNameResponse(bool custom, std::array<std::string, 3> current)> names;
    std::function<SetupTitleResponse(std::array<std::string, 7> current)> titles;
    // Required P03/P04 child, reached only by the corresponding custom-name
    // action. Its accepted portrait effects survive returning to the name loop.
    std::function<void(state::CivilizationSlot)> portrait;
    std::function<std::int32_t(std::int16_t tribe_style, std::int16_t current_override)> city_style;
    // Random is a distinct choice; an ordinary nonnegative result is one of
    // the offered tribe slots. Slots are visited ascending, after roster draws.
    std::function<SetupNumericChoice(state::CivilizationSlot, std::span<const SetupTribeOption>)>
        opponent_tribe;

    std::int32_t choose_difficulty(state::Difficulty remembered) const override {
        return difficulty(remembered);
    }
    std::int32_t choose_rules(state::RuleSelection remembered) const override {
        return rules(remembered);
    }
    std::int32_t choose_gender(state::Gender remembered) const override {
        return gender(remembered);
    }
    SetupNumericChoice choose_opponents(std::int16_t remembered) const override {
        return opponents(remembered);
    }
    SetupNumericChoice choose_barbarians(std::int16_t remembered) const override {
        return barbarians(remembered);
    }
    SetupAdvancedResponse choose_advanced(SetupAdvancedChoices defaults) const override {
        return advanced(defaults);
    }
    std::int32_t choose_acceleration(std::array<std::uint32_t, 3> years,
                                     std::int16_t remembered) const override {
        return acceleration(years, remembered);
    }
    SetupTribeChoice choose_tribe(std::span<const SetupTribeOption> arg0,
                                  std::int16_t remembered) const override {
        return tribe(arg0, remembered);
    }
    SetupNameResponse choose_names(bool custom, std::array<std::string, 3> current) const override {
        return names(custom, current);
    }
    SetupTitleResponse choose_titles(std::array<std::string, 7> current) const override {
        return titles(current);
    }
    void choose_portrait(state::CivilizationSlot arg0) const override { return portrait(arg0); }
    std::int32_t choose_city_style(std::int16_t tribe_style,
                                   std::int16_t current_override) const override {
        return city_style(tribe_style, current_override);
    }
    SetupNumericChoice
    choose_opponent_tribe(state::CivilizationSlot arg0,
                          std::span<const SetupTribeOption> arg1) const override {
        return opponent_tribe(arg0, arg1);
    }
};
} // namespace openciv::session
namespace openciv::session {
struct RuleContextCallbacks {
    std::function<bool(std::string_view, std::string_view)> exists;
    formats::classic::TextCase comparison;
    formats::classic::LabelCallbacks labels;
    formats::classic::RuleLoadCallbacks rules;

    operator RuleContextDependencies() const& { return {exists, &comparison, labels, rules}; }
    operator RuleContextDependencies() const&& = delete;
};
} // namespace openciv::session
namespace openciv::session {
struct CoreSaveCallbacks final : FileStorage {
    // The provider owns replacement/commit policy. A false write or close is
    // failure; providers must not reenter or mutate game state.
    std::function<std::unique_ptr<CoreOutput>(std::string_view)> open;

    CoreSaveCallbacks() = default;
    CoreSaveCallbacks(std::function<std::unique_ptr<CoreOutput>(std::string_view)> callback)
        : open(std::move(callback)) {}
    CoreSaveCallbacks(const CoreSaveCallbacks& other) : open(other.open) {}
    CoreSaveCallbacks& operator=(const CoreSaveCallbacks& other) {
        open = other.open;
        return *this;
    }
    std::unique_ptr<CoreInput> read(std::string_view) const override {
        throw std::logic_error("Unexpected input");
    }
    std::unique_ptr<CoreOutput> replace(std::string_view path) const override { return open(path); }
    bool remove(std::string_view) const override { throw std::logic_error("Unexpected removal"); }
};
} // namespace openciv::session
namespace openciv::session {
struct WorldSizeCallbacks final : WorldSizeInteraction {
    // Ordinary 0=Small, 2=Large, other nonnegative=Normal; negative=cancel.
    std::function<WorldSizeChoice(state::WorldSize remembered_preset)> preset;
    std::function<CustomSizeResponse(std::string_view width_default,
                                     std::string_view height_default)>
        custom;

    WorldSizeChoice choose_preset(state::WorldSize remembered_preset) const override {
        return preset(remembered_preset);
    }
    CustomSizeResponse choose_custom(std::string_view width_default,
                                     std::string_view height_default) const override {
        return custom(width_default, height_default);
    }
};
} // namespace openciv::session
namespace openciv::session {
struct WorldParameterCallbacks {
    // Remembered value is normalized (-1..1 nominally), not a menu index.
    // Ordinary results are 0..2 or negative cancellation. An unsupported
    // positive row stops at consumption with all earlier effects retained.
    std::function<WorldParameterChoice(state::WorldParameter, std::int16_t remembered)> choose;
    PreferenceFileCallbacks preferences;

    operator WorldParameterDependencies() const& { return {choose, &preferences}; }
    operator WorldParameterDependencies() const&& = delete;
};
} // namespace openciv::session
namespace openciv::session {
struct UnitOperationCallbacks {
    unit::StackDiagnostics stacks = nullptr;
    unit::StackOperationCallbacks placement;
    unit::DeletionCallbacks deletion;
    std::function<std::uint32_t()> ticks;
    std::function<void()> timed_out;

    operator UnitOperationDependencies() const& {
        return {stacks, placement, &deletion, ticks, timed_out};
    }
    operator UnitOperationDependencies() const&& = delete;
};
} // namespace openciv::session
namespace openciv::session {
struct NewGameCallbacks {
    // V01 shared creation, with the live turn set to zero by the parent. Invoke
    // for slots 0..7, including absent slots; returned failures are ignored.
    std::function<std::int32_t(std::int32_t)> create_civilization;
    // F05 section 8, after all creation attempts and the at-start mask copy.
    std::function<void()> balance_starts;

    operator NewGameDependencies() const& { return {create_civilization, balance_starts}; }
    operator NewGameDependencies() const&& = delete;
};
} // namespace openciv::session
namespace openciv::session {
struct PremadeChoiceCallbacks {
    std::function<std::int32_t()> use_seed, use_start_locations;
    std::function<std::uint32_t()> ticks;
    operator PremadeWorldChoices() const& { return {use_seed, use_start_locations, ticks}; }
    operator PremadeWorldChoices() const&& = delete;
};
} // namespace openciv::session
namespace openciv::session {
struct PremadeWorldCallbacks final : FileStorage {
    std::function<std::unique_ptr<CoreInput>(std::string_view)> open;
    std::function<void()> load_failure;
    PremadeChoiceCallbacks choices;

    std::unique_ptr<CoreInput> read(std::string_view path) const override { return open(path); }
    std::unique_ptr<CoreOutput> replace(std::string_view) const override {
        throw std::logic_error("Unexpected output");
    }
    bool remove(std::string_view) const override { throw std::logic_error("Unexpected removal"); }
    operator PremadeWorldDependencies() const& { return {this, load_failure, choices}; }
    operator PremadeWorldDependencies() const&& = delete;
};
} // namespace openciv::session
namespace openciv::session {
struct LoadDialogCallbacks {
    CoreLoadCallbacks input;
    std::function<void(std::int32_t index)> close_map_window;
    // Return one owned listing of matching, unqualified filenames in service order.
    std::function<std::vector<std::string>(std::string_view directory, std::string_view pattern)>
        enumerate;
    // Source-specific selection/parsing returns owned data; nullopt means failure.
    // The source never receives or modifies the installed event collection.
    std::function<std::optional<state::EventCatalog>()> load_events;
    std::function<void()> scenario_loaded, synchronize_music, present_title;
    std::function<void(bool enabled)> configure_sound;
    std::function<void(CoreLoadStatus)> load_notice;
    unit::StackDiagnostics stacks = nullptr;
    unit::StackOperationCallbacks placement;
    unit::DeletionCallbacks deletion;

    operator LoadDialogDependencies() const& {
        return {input,         close_map_window, enumerate,
                load_events,   scenario_loaded,  synchronize_music,
                present_title, configure_sound,  load_notice,
                stacks,        placement,        &deletion};
    }
    operator LoadDialogDependencies() const&& = delete;
};
} // namespace openciv::session
namespace openciv::network {
struct SendCallbacks final : SocketTransport, SendLimits {
    // Live library-wide capacity, not a value captured by each queued record.
    // Required. Exceptions mean no bytes were accepted; after accepting bytes,
    // return a SocketResult. A throwing call leaves queued bytes/offset intact.
    std::function<SocketResult(std::span<const Byte>)> send;
    std::function<void()> post_write;
    std::function<void(std::size_t)> backlog;

    SocketResult transmit(std::span<const Byte> bytes) const override { return send(bytes); }
    operator SendDependencies() const& { return {*this, *this, post_write, backlog}; }
    operator SendDependencies() const&& = delete;
};
} // namespace openciv::network
namespace openciv::session {
struct GameOptionCallbacks final : MusicControl {
    PreferenceFileCallbacks preferences;
    // Required P10 owning operations. Reinitialization includes its specified
    // stop/close/open/layout sequence; synchronization is a separate child.
    std::function<void()> stop_music, reinitialize_music, synchronize_music;

    operator GameOptionDependencies() const& { return {&preferences, this}; }
    operator GameOptionDependencies() const&& = delete;
    void stop() const override { return stop_music(); }
    void reinitialize() const override { return reinitialize_music(); }
    void synchronize() const override { return synchronize_music(); }
};
} // namespace openciv::session
namespace openciv::session {
struct GraphicsOptionCallbacks {
    PreferenceFileCallbacks preferences;
    std::function<bool()> heralds_available;
    std::function<void()> low_memory_notice;

    operator GraphicsOptionDependencies() const& {
        return {&preferences, heralds_available, low_memory_notice};
    }
    operator GraphicsOptionDependencies() const&& = delete;
};
} // namespace openciv::session
namespace openciv::session {
struct MultiplayerOptionCallbacks final : OptionBroadcaster {
    PreferenceFileCallbacks preferences;
    std::function<std::uint32_t()> clock;
    // N01 owns local player/name mapping; a civilization is not a player slot.
    std::function<std::string()> local_player_name, separator;
    std::function<std::string(bool)> production_label, movement_label;
    std::function<void()> broadcast_rejection, broadcast_acceptance;
    std::function<void()> send_units;
    std::function<void(bool production, bool movement, bool simultaneous)> send_options;
    std::function<void(std::string_view)> notice;
    std::function<void(std::int32_t)> redraw_status;

    operator MultiplayerOptionDependencies() const& {
        return {
            &preferences, clock,  local_player_name, separator, production_label, movement_label,
            this,         notice, redraw_status};
    }
    operator MultiplayerOptionDependencies() const&& = delete;
    void rejected() const override { return broadcast_rejection(); }
    void accepted() const override { return broadcast_acceptance(); }
    void units() const override { return send_units(); }
    void options(bool production, bool movement, bool simultaneous) const override {
        return send_options(production, movement, simultaneous);
    }
};
} // namespace openciv::session
namespace openciv::session {
struct SaveMenuCallbacks final : CountdownControl {
    FileChoiceCallbacks chooser;
    CoreSaveCallbacks output;
    // N03 start is distinct from resume. These are real timer boundaries.
    std::function<void(bool notify)> stop_countdown, start_countdown;
    // Pure presentation queries. They do not mutate game state or admit reentry.
    std::function<LocalCalendar()> local_time;
    std::function<std::string(std::string_view)> lowercase;
    std::function<std::string(std::size_t)> label;
    std::function<std::string(std::int32_t)> ruler_name, ruler_title, civilization_plural;
    std::function<std::string(std::int32_t)> format_year;
    std::function<std::string()> error_text;
    std::function<void(const SaveNotice&)> notice;

    operator SaveMenuDependencies() const& {
        return {&output,     this,       local_time,  lowercase,
                label,       ruler_name, ruler_title, civilization_plural,
                format_year, error_text, notice};
    }
    operator SaveMenuDependencies() const&& = delete;
    void stop(bool notify) const override { return stop_countdown(notify); }
    void start(bool notify) const override { return start_countdown(notify); }
};
} // namespace openciv::session
namespace openciv::session {
struct WorldEntryCallbacks {
    // Selected source closes its readers and returns the same owned inputs used
    // for load/resume. Installation and final name-preserving updates live here.
    std::function<state::LoadedContent()> load_content;
    WorldSizeCallbacks size;
    std::function<std::optional<std::string>()> choose_map;
    PremadeWorldCallbacks premade;
    // parameters.preferences serves both parameter-completion and final saves.
    WorldParameterCallbacks parameters;
    PlayerSetupCallbacks players;
    std::function<void()> stop_menu_sound;
    // Required W02: false generates terrain; true retains/finalizes it. Reads
    // current world_setup, map, rules and starts; no allocated-map substitute.
    std::function<void(bool keep)> generate_world;
    NewGameCallbacks initialize;
    // Required F05 sections 9-10, including any retained negative selection.
    std::function<void(std::int32_t selection)> accelerate;
    // Required P03/P04 presentation resolves current leader/civilization names.
    // Advance text is owned classic bytes with ", " after every listed name.
    std::function<void(state::CivilizationSlot, std::string advances)> introduce;

    operator WorldEntryDependencies() const& {
        return {load_content,    &size,          choose_map, premade,    parameters, &players,
                stop_menu_sound, generate_world, initialize, accelerate, introduce};
    }
    operator WorldEntryDependencies() const&& = delete;
};
} // namespace openciv::session
