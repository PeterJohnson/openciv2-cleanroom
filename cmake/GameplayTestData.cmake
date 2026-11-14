# Embed owned JSON inputs so the exact same tests run without filesystem access
# in native and WASM builds. Expectations are authored in the tests and ledger.
set(oc2_gameplay_test_header "#pragma once\n#include <array>\n#include <string_view>\n#include <utility>\ninline constexpr std::array<std::pair<std::string_view, std::string_view>, 7> gameplay_test_files{{\n")
foreach(name gameplay calendar spaceship governments difficulty combat scoring)
  set(path "${PROJECT_SOURCE_DIR}/data/gameplay/classic-mge-5.4.0f/${name}.json")
  set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${path}")
  file(READ "${path}" contents)
  string(APPEND oc2_gameplay_test_header "{\"${name}.json\", R\"oc2fixture(${contents})oc2fixture\"},\n")
endforeach()
string(APPEND oc2_gameplay_test_header "}};\n")
file(CONFIGURE OUTPUT "${PROJECT_BINARY_DIR}/generated/gameplay_test_data.hpp"
  CONTENT "${oc2_gameplay_test_header}" @ONLY NEWLINE_STYLE UNIX)
target_include_directories(oc2_tests PRIVATE "${PROJECT_BINARY_DIR}/generated")
