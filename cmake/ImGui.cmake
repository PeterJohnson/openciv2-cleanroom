include(cmake/Dependencies.cmake)
# Compile the upstream core and official SDL3 backends; no upstream CMake target.
oc2_fetch_dependency(imgui)
# 1.93.0's dimming code requires six indices, but AddRectFilled adds an AA
# fringe at fractional framebuffer scales (e.g. Wayland at 1.65x). Generate a
# narrowly patched translation unit: the multicolor rectangle always emits
# exactly six indices and four identical colors preserve the intended fill.
# Keep fetched/offline source trees untouched and fail on an unexpected source.
file(READ "${imgui_SOURCE_DIR}/imgui.cpp" imgui_source)
set(imgui_dimming_call "draw_list->AddRectFilled(viewport_rect.Min, viewport_rect.Max, col);")
string(FIND "${imgui_source}" "${imgui_dimming_call}" imgui_dimming_offset)
if(imgui_dimming_offset EQUAL -1)
  message(FATAL_ERROR "Re-evaluate the ImGui dimming patch for this source version")
endif()
string(REPLACE "${imgui_dimming_call}"
  "draw_list->AddRectFilledMultiColor(viewport_rect.Min, viewport_rect.Max, col, col, col, col);"
  imgui_patched_source "${imgui_source}")
file(CONFIGURE OUTPUT "${CMAKE_CURRENT_BINARY_DIR}/imgui_patched.cpp"
  CONTENT "${imgui_patched_source}" @ONLY)
configure_file(${CMAKE_CURRENT_SOURCE_DIR}/THIRD_PARTY_NOTICES.txt
  ${CMAKE_CURRENT_BINARY_DIR}/THIRD_PARTY_NOTICES.txt COPYONLY)
add_library(oc2_imgui STATIC
  ${CMAKE_CURRENT_BINARY_DIR}/imgui_patched.cpp
  ${imgui_SOURCE_DIR}/imgui_draw.cpp
  ${imgui_SOURCE_DIR}/imgui_tables.cpp
  ${imgui_SOURCE_DIR}/imgui_widgets.cpp
  ${imgui_SOURCE_DIR}/backends/imgui_impl_sdl3.cpp
  ${imgui_SOURCE_DIR}/backends/imgui_impl_sdlrenderer3.cpp)
target_include_directories(oc2_imgui SYSTEM PUBLIC ${imgui_SOURCE_DIR} ${imgui_SOURCE_DIR}/backends)
target_link_libraries(oc2_imgui PUBLIC SDL3::SDL3)
if(OC2_SANITIZERS AND NOT MSVC AND NOT EMSCRIPTEN)
  target_compile_options(oc2_imgui PRIVATE -fsanitize=address,undefined -fno-omit-frame-pointer)
  target_link_options(oc2_imgui PRIVATE -fsanitize=address,undefined)
endif()
if(EMSCRIPTEN)
  target_compile_options(oc2_imgui PRIVATE -fexceptions)
endif()
