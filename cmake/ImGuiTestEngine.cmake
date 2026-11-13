include(cmake/Dependencies.cmake)
oc2_fetch_dependency(imgui_test_engine)
set(engine_dir "${imgui_test_engine_SOURCE_DIR}/imgui_test_engine")

# Hooks change ImGui's build configuration. Compile a separate, consistently
# instrumented copy, including the same dimming patch and SDL backends as the
# game. Never link this library into openciv2 or oc2_frontend.
get_target_property(imgui_sources oc2_imgui SOURCES)
add_library(oc2_imgui_testing STATIC ${imgui_sources}
  ${engine_dir}/imgui_capture_tool.cpp
  ${engine_dir}/imgui_te_context.cpp
  ${engine_dir}/imgui_te_coroutine.cpp
  ${engine_dir}/imgui_te_engine.cpp
  ${engine_dir}/imgui_te_exporters.cpp
  ${engine_dir}/imgui_te_perftool.cpp
  ${engine_dir}/imgui_te_ui.cpp
  ${engine_dir}/imgui_te_utils.cpp)
target_include_directories(oc2_imgui_testing SYSTEM PUBLIC
  ${imgui_SOURCE_DIR} ${imgui_SOURCE_DIR}/backends ${engine_dir})
target_compile_definitions(oc2_imgui_testing PUBLIC
  IMGUI_ENABLE_TEST_ENGINE IMGUI_TEST_ENGINE_ENABLE_COROUTINE_STDTHREAD_IMPL=1
  IMGUI_TEST_ENGINE_ENABLE_CAPTURE=1)
target_link_libraries(oc2_imgui_testing PUBLIC SDL3::SDL3 Threads::Threads)
# Assertions must remain active in Release GUI tests, too.
if(MSVC)
  target_compile_options(oc2_imgui_testing PUBLIC /UNDEBUG)
  target_compile_definitions(oc2_imgui_testing PRIVATE _CRT_SECURE_NO_WARNINGS)
else()
  target_compile_options(oc2_imgui_testing PUBLIC -UNDEBUG)
  if(OC2_SANITIZERS)
    target_compile_options(oc2_imgui_testing PRIVATE -fsanitize=address,undefined -fno-omit-frame-pointer)
    target_link_options(oc2_imgui_testing PUBLIC -fsanitize=address,undefined)
  endif()
endif()

add_executable(oc2_ui_tests tests/gui_tests.cpp tests/gui_test_cases.cpp src/ui/interface.cpp)
target_link_libraries(oc2_ui_tests PRIVATE oc2_viewmodel oc2_options oc2_imgui_testing)
configure_file(${engine_dir}/LICENSE.txt
  ${CMAKE_CURRENT_BINARY_DIR}/IMGUI_TEST_ENGINE_LICENSE.txt COPYONLY)
if(WIN32)
  get_target_property(sdl_type SDL3::SDL3 TYPE)
  if(sdl_type STREQUAL "SHARED_LIBRARY")
    add_custom_command(TARGET oc2_ui_tests POST_BUILD COMMAND ${CMAKE_COMMAND} -E copy_if_different
      $<TARGET_FILE:SDL3::SDL3> $<TARGET_FILE_DIR:oc2_ui_tests> VERBATIM)
  endif()
endif()
foreach(density IN ITEMS 1 1.65 2)
  add_test(NAME gui_${density} COMMAND oc2_ui_tests --density ${density}
    --output ${CMAKE_CURRENT_BINARY_DIR}/gui-results/${density})
  set_tests_properties(gui_${density} PROPERTIES TIMEOUT 120 LABELS gui)
endforeach()
