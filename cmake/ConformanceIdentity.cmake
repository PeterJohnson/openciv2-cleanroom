cmake_policy(VERSION 3.24)
# Keep this compiled-input set in sync with run_conformance.ADAPTER_SOURCES.
set(oc2_conformance_sources
  CMakeLists.txt dependencies.json cmake/Toolchain.cmake
  cmake/ConformanceIdentity.cmake
  src/base/arithmetic.hpp src/base/arithmetic.cpp
  src/base/random.hpp src/base/random.cpp
  src/base/random_source.hpp src/base/random_source.cpp
  src/base/choices.hpp src/base/choices.cpp
  src/base/hash.hpp src/base/hash.cpp
  src/state/identities.hpp src/state/identities.cpp
  src/state/records.hpp src/state/records.cpp
  src/world/geometry.hpp src/world/geometry.cpp src/world/map.hpp src/world/map.cpp
  tools/conformance_adapter.cpp tools/conformance_io.hpp
  tools/conformance_random.hpp tools/conformance_random.cpp
  tools/conformance_script.hpp tools/conformance_script.cpp
  tools/conformance_session.hpp tools/conformance_session.cpp
  src/base/session_ids.hpp src/query/fields.hpp src/query/live.hpp src/query/live.cpp
  src/query/live_records.hpp src/session/client.hpp src/session/command_types.hpp
  src/session/runtime.hpp src/session/runtime.cpp src/session/runtime_internal.hpp
  src/session/command_runtime.cpp src/session/commands.hpp src/session/commands.cpp)

if(CMAKE_SCRIPT_MODE_FILE)
  set(oc2_conformance_root "${OC2_SOURCE_DIR}")
else()
  set(oc2_conformance_root "${PROJECT_SOURCE_DIR}")
endif()
file(GLOB oc2_state_sources RELATIVE "${oc2_conformance_root}"
  "${oc2_conformance_root}/src/state/*.hpp" "${oc2_conformance_root}/src/state/*.cpp")
list(SORT oc2_state_sources)
list(APPEND oc2_conformance_sources ${oc2_state_sources})
list(REMOVE_DUPLICATES oc2_conformance_sources)

if(CMAKE_SCRIPT_MODE_FILE)
  # Invoked by the build, not by the reporting process. Only replace the header
  # when its contents change, so unrelated rebuilds do not recompile the adapter.
  set(oc2_sources_json "{")
  set(oc2_separator "")
  foreach(source IN LISTS oc2_conformance_sources)
    file(SHA256 "${OC2_SOURCE_DIR}/${source}" digest)
    string(APPEND oc2_sources_json "${oc2_separator}\"${source}\":\"${digest}\"")
    set(oc2_separator ",")
  endforeach()
  string(APPEND oc2_sources_json "}")
  file(READ "${OC2_BUILD_IDENTITY}" oc2_build_identity)
  string(REPLACE "\\" "\\\\" oc2_build_identity "${oc2_build_identity}")
  string(REPLACE "\"" "\\\"" oc2_build_identity "${oc2_build_identity}")
  string(REPLACE "\n" "\\n" oc2_build_identity "${oc2_build_identity}")
  string(REPLACE "\r" "\\r" oc2_build_identity "${oc2_build_identity}")
  string(REPLACE "\t" "\\t" oc2_build_identity "${oc2_build_identity}")
  set(oc2_manifest "{\"format\":\"openciv2-conformance-adapter/1\",\"sources\":${oc2_sources_json},\"buildIdentity\":\"${oc2_build_identity}\"}")
  file(CONFIGURE OUTPUT "${OC2_OUTPUT}" CONTENT
    "#pragma once\ninline constexpr char oc2_conformance_identity[] = R\"oc2_manifest(@oc2_manifest@)oc2_manifest\";\n" @ONLY)
else()
  set(oc2_identity_header "${PROJECT_BINARY_DIR}/generated/conformance_identity.hpp")
  set(oc2_identity_inputs ${oc2_conformance_sources})
  list(TRANSFORM oc2_identity_inputs PREPEND "${PROJECT_SOURCE_DIR}/")
  add_custom_command(OUTPUT "${oc2_identity_header}"
    COMMAND "${CMAKE_COMMAND}"
      "-DOC2_SOURCE_DIR=${PROJECT_SOURCE_DIR}"
      "-DOC2_BUILD_IDENTITY=${PROJECT_BINARY_DIR}/build-identity.txt"
      "-DOC2_OUTPUT=${oc2_identity_header}"
      -P "${CMAKE_CURRENT_LIST_FILE}"
    DEPENDS ${oc2_identity_inputs} "${PROJECT_BINARY_DIR}/build-identity.txt"
    COMMENT "Recording compiled conformance adapter identity" VERBATIM)
  target_sources(oc2_conformance_adapter PRIVATE "${oc2_identity_header}")
  target_include_directories(oc2_conformance_adapter PRIVATE "${PROJECT_BINARY_DIR}/generated")
endif()
