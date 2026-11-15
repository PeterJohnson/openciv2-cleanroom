# Unmodified, hash-pinned jart/json.cpp and its bundled double-conversion.
# Vendored so formats and tools-only builds never need a network or package manager.
file(READ "${PROJECT_SOURCE_DIR}/dependencies.json" oc2_json_lock)
string(JSON oc2_json_count LENGTH "${oc2_json_lock}" libraries jart-json vendoredFiles)
math(EXPR oc2_json_last "${oc2_json_count} - 1")
foreach(index RANGE ${oc2_json_last})
  string(JSON source MEMBER "${oc2_json_lock}" libraries jart-json vendoredFiles ${index})
  string(JSON expected GET "${oc2_json_lock}" libraries jart-json vendoredFiles "${source}")
  file(SHA256 "${PROJECT_SOURCE_DIR}/${source}" actual)
  if(NOT actual STREQUAL expected)
    message(FATAL_ERROR "Vendored JSON dependency hash changed: ${source}")
  endif()
  set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${PROJECT_SOURCE_DIR}/${source}")
endforeach()
set(oc2_json_dir "${PROJECT_SOURCE_DIR}/third_party/jart-json")
add_library(oc2_json STATIC "${oc2_json_dir}/json.cpp"
  "${oc2_json_dir}/double-conversion/bignum.cc"
  "${oc2_json_dir}/double-conversion/bignum-dtoa.cc"
  "${oc2_json_dir}/double-conversion/cached-powers.cc"
  "${oc2_json_dir}/double-conversion/double-to-string.cc"
  "${oc2_json_dir}/double-conversion/fast-dtoa.cc"
  "${oc2_json_dir}/double-conversion/fixed-dtoa.cc"
  "${oc2_json_dir}/double-conversion/string-to-double.cc"
  "${oc2_json_dir}/double-conversion/strtod.cc")
target_include_directories(oc2_json SYSTEM PUBLIC "${oc2_json_dir}")
target_compile_features(oc2_json PUBLIC cxx_std_23)
if(MSVC)
  # jtckdint selects long long via __cplusplus. MSVC's legacy macro otherwise
  # chooses 32-bit long and corrupts multi-digit negative JSON integers on LLP64.
  target_compile_options(oc2_json PRIVATE /Zc:__cplusplus)
endif()
if(EMSCRIPTEN)
  target_compile_options(oc2_json PRIVATE -fexceptions)
endif()
if(OC2_SANITIZERS AND NOT MSVC AND NOT EMSCRIPTEN)
  target_compile_options(oc2_json PRIVATE -fsanitize=address,undefined -fno-omit-frame-pointer)
endif()
