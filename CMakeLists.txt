cmake_minimum_required(VERSION 3.24)
project(OpenCiv2 VERSION 0.1.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
include(CTest)
option(OC2_BUILD_GUI "Build the SDL application" ON)
option(OC2_FETCH_SDL "Fetch pinned SDL instead of using an installed package" OFF)
option(OC2_SANITIZERS "Enable address and undefined behavior sanitizers" OFF)
option(OC2_GUI_TESTS "Build Dear ImGui Test Engine GUI tests (desktop testing only)" OFF)
option(OC2_FETCH_DEPENDENCIES "Allow downloading hash-pinned dependencies" OFF)
include(cmake/Formatting.cmake)
include(cmake/Assets.cmake)

add_library(oc2_options INTERFACE)
target_compile_features(oc2_options INTERFACE cxx_std_20)
if(MSVC)
  target_compile_options(oc2_options INTERFACE /W4 /permissive- /utf-8)
else()
  target_compile_options(oc2_options INTERFACE -Wall -Wextra -Wpedantic -Wconversion -Wshadow)
  if(OC2_SANITIZERS AND NOT EMSCRIPTEN)
    target_compile_options(oc2_options INTERFACE -fsanitize=address,undefined -fno-omit-frame-pointer)
    target_link_options(oc2_options INTERFACE -fsanitize=address,undefined)
  endif()
endif()

if(EMSCRIPTEN)
  target_compile_options(oc2_options INTERFACE -fexceptions)
  target_link_options(oc2_options INTERFACE -fexceptions -sALLOW_MEMORY_GROWTH=1 -sEXIT_RUNTIME=1)
endif()

# F01 portable arithmetic and versioned random contexts: standard library only.
add_library(oc2_base src/base/arithmetic.cpp src/base/random.cpp)
target_compile_features(oc2_base PUBLIC cxx_std_20)
target_include_directories(oc2_base PUBLIC src)
target_link_libraries(oc2_base PRIVATE oc2_options)
if(MSVC)
  target_compile_options(oc2_base PRIVATE /fp:strict)
else()
  target_compile_options(oc2_base PRIVATE -fno-fast-math -ffp-contract=off)
endif()

# F02 state primitives: no presentation, database, network or asset dependency.
add_library(oc2_game src/state/identities.cpp)
target_compile_features(oc2_game PUBLIC cxx_std_20)
target_include_directories(oc2_game PUBLIC src)
target_link_libraries(oc2_game PUBLIC oc2_base PRIVATE oc2_options)

# Application presentation state, not game mechanics. No SDL or OS dependencies.
add_library(oc2_viewmodel src/viewmodel/shell.cpp src/viewmodel/menu.cpp)
target_compile_features(oc2_viewmodel PUBLIC cxx_std_20)
target_include_directories(oc2_viewmodel PUBLIC src)
target_link_libraries(oc2_viewmodel PRIVATE oc2_options)

if(OC2_BUILD_GUI)
  if(APPLE AND NOT EMSCRIPTEN)
    # Initialize language rules before dependency directories inherit this scope.
    enable_language(OBJC OBJCXX)
    set(CMAKE_OBJCXX_STANDARD 20)
    set(CMAKE_OBJCXX_STANDARD_REQUIRED ON)
    set(CMAKE_OBJCXX_EXTENSIONS OFF)
  endif()
  include(cmake/SDL.cmake)
  include(cmake/ImGui.cmake)
  include(cmake/Fonts.cmake)
  add_library(oc2_frontend src/application.cpp src/ui/interface.cpp src/ui/fonts.cpp src/platform/menu_fallback.cpp)
  target_include_directories(oc2_frontend PUBLIC src)
  target_link_libraries(oc2_frontend PUBLIC oc2_viewmodel SDL3::SDL3 PRIVATE oc2_options oc2_imgui oc2_font_data)
  if(NOT EMSCRIPTEN)
    find_package(Threads REQUIRED)
    target_link_libraries(oc2_frontend PUBLIC Threads::Threads)
  endif()
  if(WIN32 AND NOT EMSCRIPTEN)
    target_sources(oc2_frontend PRIVATE src/platform/menu_windows.cpp)
    target_link_libraries(oc2_frontend PRIVATE comctl32 user32)
    target_compile_definitions(oc2_frontend PRIVATE NOMINMAX WIN32_LEAN_AND_MEAN)
  elseif(APPLE AND NOT EMSCRIPTEN)
    set_target_properties(oc2_frontend PROPERTIES OBJCXX_STANDARD 20 OBJCXX_STANDARD_REQUIRED ON OBJCXX_EXTENSIONS OFF)
    target_sources(oc2_frontend PRIVATE src/platform/menu_macos.mm)
    set_source_files_properties(src/platform/menu_macos.mm PROPERTIES COMPILE_OPTIONS "-fobjc-arc")
    target_link_libraries(oc2_frontend PRIVATE "-framework AppKit")
  endif()

  add_executable(openciv2 WIN32 MACOSX_BUNDLE src/main.cpp)
  target_link_libraries(openciv2 PRIVATE oc2_frontend oc2_options)
  set_target_properties(openciv2 PROPERTIES
    MACOSX_BUNDLE_BUNDLE_NAME OpenCiv2
    MACOSX_BUNDLE_GUI_IDENTIFIER org.openciv2.cleanroom
    MACOSX_BUNDLE_SHORT_VERSION_STRING ${PROJECT_VERSION})
  if(EMSCRIPTEN)
    set_target_properties(openciv2 PROPERTIES SUFFIX ".html")
    target_link_options(openciv2 PRIVATE
      "--shell-file=${CMAKE_CURRENT_SOURCE_DIR}/web/shell.html")
    set_property(TARGET openciv2 APPEND PROPERTY LINK_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/web/shell.html")
  elseif(WIN32)
    # Accommodate both the default static fetch and installed shared SDL packages.
    get_target_property(sdl_type SDL3::SDL3 TYPE)
    if(sdl_type STREQUAL "SHARED_LIBRARY")
      add_custom_command(TARGET openciv2 POST_BUILD COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:SDL3::SDL3> $<TARGET_FILE_DIR:openciv2> VERBATIM)
    endif()
  endif()
endif()

if(BUILD_TESTING)
  include(cmake/Catch2.cmake)
  add_executable(oc2_tests tests/viewmodel_tests.cpp tests/base_tests.cpp tests/wrapping_i32_tests.cpp tests/state_tests.cpp)
  target_link_libraries(oc2_tests PRIVATE oc2_game oc2_viewmodel oc2_options Catch2::Catch2WithMain)
  add_executable(oc2_conformance_adapter tools/conformance_adapter.cpp)
  target_link_libraries(oc2_conformance_adapter PRIVATE oc2_game oc2_options)
  include(cmake/ConformanceIdentity.cmake)
  if(EMSCRIPTEN)
    # Activate emsdk first so its Node is on PATH (or provide an emulator).
    # A toolchain file alone, unlike emcmake, does not always select the runner.
    if(NOT CMAKE_CROSSCOMPILING_EMULATOR)
      find_program(OC2_NODE_EXECUTABLE NAMES node nodejs REQUIRED)
      set(CMAKE_CROSSCOMPILING_EMULATOR "${OC2_NODE_EXECUTABLE}")
    endif()
    add_test(NAME viewmodel COMMAND ${CMAKE_CROSSCOMPILING_EMULATOR} $<TARGET_FILE:oc2_tests>)
    set_tests_properties(viewmodel PROPERTIES LABELS "unit;headless")
  else()
    include(Catch)
    catch_discover_tests(oc2_tests DISCOVERY_MODE PRE_TEST PROPERTIES LABELS unit)
    if(OC2_BUILD_GUI)
      add_executable(oc2_runtime_tests tests/runtime_tests.cpp tests/runtime_main.cpp)
      target_link_libraries(oc2_runtime_tests PRIVATE oc2_frontend oc2_options oc2_imgui Catch2::Catch2)
      add_test(NAME runtime COMMAND oc2_runtime_tests)
      set_tests_properties(runtime PROPERTIES
        ENVIRONMENT "SDL_VIDEO_DRIVER=dummy;SDL_RENDER_DRIVER=software" LABELS "runtime" TIMEOUT 30)
      add_test(NAME startup COMMAND openciv2 --smoke-test)
      set_tests_properties(startup PROPERTIES ENVIRONMENT "SDL_VIDEO_DRIVER=dummy;SDL_RENDER_DRIVER=software" TIMEOUT 15)
      add_test(NAME startup_failure COMMAND openciv2 --smoke-test)
      set_tests_properties(startup_failure PROPERTIES
        ENVIRONMENT "SDL_VIDEO_DRIVER=openciv_missing_driver" WILL_FAIL TRUE TIMEOUT 15)
      if(OC2_GUI_TESTS)
        include(cmake/ImGuiTestEngine.cmake)
      endif()
    endif()
  endif()
endif()

include(cmake/Metadata.cmake)
# Record identities after targets, platform languages and dependencies are selected.
include(cmake/Toolchain.cmake)
if(BUILD_TESTING AND Python3_Interpreter_FOUND)
  add_test(NAME conformance_foundations
    COMMAND "${Python3_EXECUTABLE}" "${PROJECT_SOURCE_DIR}/tools/run_conformance.py"
      --root "${PROJECT_SOURCE_DIR}"
      --report "${PROJECT_BINARY_DIR}/conformance-results.json"
      --build-identity "${PROJECT_BINARY_DIR}/build-identity.txt"
      --adapter ${CMAKE_CROSSCOMPILING_EMULATOR} $<TARGET_FILE:oc2_conformance_adapter>)
  set_tests_properties(conformance_foundations PROPERTIES LABELS "conformance;headless" TIMEOUT 30)
endif()
