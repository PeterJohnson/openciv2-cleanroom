# Named interfaces mirror the architecture boundaries; CMake orders imports.
function(oc2_modules target visibility)
  string(TOLOWER "${visibility}" scope)
  target_sources(${target} ${visibility} FILE_SET ${scope}_modules TYPE CXX_MODULES FILES ${ARGN})
  set_target_properties(${target} PROPERTIES CXX_SCAN_FOR_MODULES ON)
endfunction()

if(TARGET oc2_base)
  oc2_modules(oc2_base PUBLIC
    src/base/arithmetic.cppm
    src/base/callback_ref.cppm
    src/base/cancellation.cppm
    src/base/choices.cppm
    src/base/hash.cppm
    src/base/random_policy.cppm
    src/base/random.cppm
    src/base/random_source.cppm
    src/base/session_ids.cppm
    src/base/text_comparison.cppm
  )
endif()

if(TARGET oc2_contracts)
  oc2_modules(oc2_contracts PUBLIC
    src/state/identities.cppm
    src/state/choices.cppm
    src/state/unit_types.cppm
    src/session/player_setup_types.cppm
    src/session/world_setup_types.cppm
    src/session/configuration_types.cppm
  )
endif()

if(TARGET oc2_game)
  oc2_modules(oc2_game PUBLIC
    src/ai/goals.cppm
    src/city/name_catalog.cppm
    src/city/names.cppm
    src/civilization/knowledge.cppm
    src/rules/city_preferences.cppm
    src/rules/double_movement.cppm
    src/rules/normalization.cppm
    src/rules/rules_install.cppm
    src/state/city_names.cppm
    src/state/city_preferences.cppm
    src/state/civilization.cppm
    src/state/events.cppm
    src/state/game.cppm
    src/state/game_input.cppm
    src/state/gameplay.cppm
    src/state/history.cppm
    src/state/entity_allocation.cppm
    src/state/label_catalog.cppm
    src/state/labels.cppm
    src/state/layout.cppm
    src/state/multiplayer.cppm
    src/state/preferences.cppm
    src/state/records.cppm
    src/state/rule_input.cppm
    src/state/rule_state.cppm
    src/state/rules.cppm
    src/state/scenario.cppm
    src/state/text_parameters.cppm
    src/state/text_pool.cppm
    src/state/world_lifetime.cppm
    src/state/starting_positions.cppm
    src/state/world_setup.cppm
    src/turn/calendar.cppm
    src/unit/deletion.cppm
    src/unit/stacks.cppm
    src/world/geometry.cppm
    src/world/map.cppm
    src/world/map_input.cppm
  )
endif()

if(TARGET oc2_io)
  oc2_modules(oc2_io PUBLIC src/formats/bytes.cppm src/io/stream.cppm src/io/storage.cppm)
endif()

if(TARGET oc2_formats)
  oc2_modules(oc2_formats PUBLIC
    src/formats/builtin_gameplay_data.cppm
    src/formats/city_names.cppm
    src/formats/city_preferences.cppm
    src/formats/classic_city_names.cppm
    src/formats/classic_city_preferences.cppm
    src/formats/classic_io.cppm
    src/formats/classic_labels.cppm
    src/formats/classic_records.cppm
    src/formats/classic_rules.cppm
    src/formats/classic_save.cppm
    src/formats/classic_text.cppm
    src/formats/classic_text_reader.cppm
    src/formats/content_conversion.cppm
    src/formats/content_json.cppm
    src/formats/content_pack.cppm
    src/formats/content_reader.cppm
    src/formats/content_source.cppm
    src/formats/error.cppm
    src/formats/event_io.cppm
    src/formats/fixed_text.cppm
    src/formats/game_input.cppm
    src/formats/gameplay.cppm
    src/formats/leader_io.cppm
    src/formats/multiplayer_io.cppm
    src/formats/preference_io.cppm
    src/formats/random_checkpoint.cppm
    src/formats/record_compatibility.cppm
    src/formats/record_fields.cppm
    src/formats/rules.cppm
    src/formats/save_aux_io.cppm
    src/formats/save_records.cppm
    src/formats/state_conversion.cppm
    src/formats/stored_records.cppm
    src/formats/world_record_io.cppm
  )
endif()

if(TARGET oc2_network)
  oc2_modules(oc2_network PUBLIC
    src/network/compression.cppm
    src/network/messages.cppm
    src/network/sections.cppm
    src/network/stream.cppm
  )
endif()

if(TARGET oc2_query)
  oc2_modules(oc2_query PUBLIC
    src/query/fields.cppm
    src/query/live.cppm
    src/query/live_records.cppm
    src/query/output.cppm
    src/query/stored.cppm
    src/query/stored_validation.cppm
  )
endif()

if(TARGET oc2_session)
  oc2_modules(oc2_session PUBLIC
    src/session/classic_file_choice.cppm
    src/session/classic_load.cppm
    src/session/classic_load_dialog.cppm
    src/session/classic_loaded_units.cppm
    src/session/classic_preferences.cppm
    src/session/classic_rules.cppm
    src/session/classic_save.cppm
    src/session/classic_save_menu.cppm
    src/session/client_types.cppm
    src/session/client_connection.cppm
    src/session/client.cppm
    src/session/command_types.cppm
    src/session/commands.cppm
    src/session/configuration.cppm
    src/session/game_install.cppm
    src/session/new_game.cppm
    src/session/player_setup.cppm
    src/session/preferences.cppm
    src/session/classic_premade_world.cppm
    src/session/classic_world_entry.cppm
    src/session/premade_world.cppm
    src/session/runtime.cppm
    src/session/setup_calendar.cppm
    src/session/text_configuration.cppm
    src/session/unit_operations.cppm
    src/session/world_entry.cppm
    src/session/world_setup.cppm
  )
  oc2_modules(oc2_session PRIVATE src/session/runtime_state.cppm)
endif()

if(TARGET oc2_viewmodel)
  oc2_modules(oc2_viewmodel PUBLIC
    src/viewmodel/menu.cppm
    src/viewmodel/shell.cppm
  )
endif()

if(TARGET oc2_export_sqlite)
  oc2_modules(oc2_export_sqlite PUBLIC
    src/analysis/sqlite_export.cppm
  )
endif()

if(TARGET oc2_io_native)
  oc2_modules(oc2_io_native PUBLIC
    src/platform/native_files.cppm
    src/platform/content_files.cppm
    src/platform/classic_files.cppm
  )
endif()

if(TARGET oc2_frontend)
  oc2_modules(oc2_frontend PUBLIC
    src/application.cppm
    src/platform/menu.cppm
    src/ui/interface.cppm
    src/ui/fonts.cppm
    src/ui/imgui_backends.cppm
  )
endif()

if(TARGET oc2_font_data)
  oc2_modules(oc2_font_data PUBLIC
    src/ui/font_data.cppm
  )
endif()

if(TARGET oc2_ui_tests)
  oc2_modules(oc2_ui_tests PRIVATE
    src/ui/interface.cppm
    src/ui/fonts.cppm
    src/ui/imgui_backends.cppm
    tests/gui_fixture.cppm
  )
endif()

if(TARGET oc2_cli)
  oc2_modules(oc2_cli PRIVATE
    tools/content_command.cppm
  )
endif()

if(TARGET oc2_conformance_adapter)
  oc2_modules(oc2_conformance_adapter PRIVATE
    tools/conformance_io.cppm
    tools/conformance_random.cppm
    tools/conformance_script.cppm
    tools/conformance_session.cppm
  )
endif()

if(TARGET oc2_tests)
  oc2_modules(oc2_tests PRIVATE
    tests/classic_load_fixture.cppm
    tests/classic_rule_fixture.cppm
    tests/classic_save_fixture.cppm
    tests/classic_stream_fixture.cppm
    tests/configuration_fixture.cppm
    tests/rule_fixture.cppm
    tests/service_callbacks.cppm
    tests/service_fixture.cppm
  )
endif()

# Dependencies keep their upstream header builds; every owned C++ consumer scans.
get_property(oc2_targets DIRECTORY PROPERTY BUILDSYSTEM_TARGETS)
foreach(target IN LISTS oc2_targets)
  if(target MATCHES "^(oc2_|openciv2$)" AND NOT target MATCHES "^oc2_(options|json|imgui.*|sqlite)$")
    set_target_properties(${target} PROPERTIES CXX_SCAN_FOR_MODULES ON)
  endif()
endforeach()
