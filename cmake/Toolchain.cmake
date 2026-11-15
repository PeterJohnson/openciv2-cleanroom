# CI enforces one baseline; developer builds may use other C++23 toolchains.
option(OC2_VERIFY_TOOLCHAIN "Require the dependency manifest's CI toolchain versions" OFF)
file(READ "${PROJECT_SOURCE_DIR}/dependencies.json" oc2_toolchain_lock)
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${PROJECT_SOURCE_DIR}/dependencies.json")
if(EMSCRIPTEN)
  set(oc2_platform web)
elseif(APPLE)
  set(oc2_platform macos)
elseif(WIN32)
  set(oc2_platform windows)
else()
  set(oc2_platform linux)
endif()

if(OC2_VERIFY_TOOLCHAIN)
  string(JSON oc2_cmake_pin GET "${oc2_toolchain_lock}" tools cmake)
  string(JSON oc2_compiler_id GET "${oc2_toolchain_lock}" toolchains ${oc2_platform} compilerId)
  string(JSON oc2_compiler_version GET "${oc2_toolchain_lock}" toolchains ${oc2_platform} compilerVersion)
  if(NOT CMAKE_VERSION VERSION_EQUAL oc2_cmake_pin)
    message(FATAL_ERROR "Pinned CI CMake is ${oc2_cmake_pin}; found ${CMAKE_VERSION}")
  endif()
  foreach(language C CXX OBJCXX)
    if(CMAKE_${language}_COMPILER_LOADED)
      if(NOT CMAKE_${language}_COMPILER_ID STREQUAL oc2_compiler_id OR
         NOT CMAKE_${language}_COMPILER_VERSION VERSION_EQUAL oc2_compiler_version)
        message(FATAL_ERROR "Pinned ${oc2_platform} ${language} compiler is ${oc2_compiler_id} ${oc2_compiler_version}; found ${CMAKE_${language}_COMPILER_ID} ${CMAKE_${language}_COMPILER_VERSION}")
      endif()
    endif()
  endforeach()
  if(EMSCRIPTEN)
    string(JSON oc2_emsdk_pin GET "${oc2_toolchain_lock}" tools emsdk)
    execute_process(COMMAND "${CMAKE_C_COMPILER}" --version OUTPUT_VARIABLE oc2_emcc_version
      RESULT_VARIABLE oc2_emcc_result)
    string(FIND "${oc2_emcc_version}" " ${oc2_emsdk_pin} (" oc2_emsdk_match)
    if(NOT oc2_emcc_result EQUAL 0 OR oc2_emsdk_match EQUAL -1)
      message(FATAL_ERROR "Pinned emsdk is ${oc2_emsdk_pin}; found ${oc2_emcc_version}")
    endif()
  endif()
endif()

# Plain text avoids JSON escaping problems with user-provided Windows paths.
# This records configuration only, not successful compilation or test execution.
file(SHA256 "${PROJECT_SOURCE_DIR}/dependencies.json" oc2_lock_hash)
set(oc2_identity "status=configured-only\nplatform=${oc2_platform}\ncmake=${CMAKE_VERSION}\ngenerator=${CMAKE_GENERATOR}\nsystem=${CMAKE_SYSTEM_NAME} ${CMAKE_SYSTEM_VERSION}\nprocessor=${CMAKE_SYSTEM_PROCESSOR}\nc=${CMAKE_C_COMPILER_ID} ${CMAKE_C_COMPILER_VERSION}\ncxx=${CMAKE_CXX_COMPILER_ID} ${CMAKE_CXX_COMPILER_VERSION}\ncxx_path=${CMAKE_CXX_COMPILER}\ndependency_lock_sha256=${oc2_lock_hash}\n")
foreach(variable OC2_VERIFY_TOOLCHAIN OC2_BUILD_GUI OC2_BUILD_TOOLS OC2_ENABLE_SQLITE_EXPORT BUILD_TESTING OC2_GUI_TESTS OC2_SANITIZERS
    CMAKE_CXX_STANDARD CMAKE_CXX_STANDARD_REQUIRED CMAKE_CXX_EXTENSIONS CMAKE_CXX_SCAN_FOR_MODULES
    CMAKE_OBJCXX_STANDARD CMAKE_OBJCXX_STANDARD_REQUIRED CMAKE_OBJCXX_EXTENSIONS
    OC2_FETCH_SDL OC2_FETCH_DEPENDENCIES CMAKE_BUILD_TYPE CMAKE_OSX_SYSROOT CMAKE_OSX_DEPLOYMENT_TARGET
    CMAKE_OBJCXX_COMPILER_ID CMAKE_OBJCXX_COMPILER_VERSION
    CMAKE_CXX_COMPILER_VERSION_INTERNAL SDL3_DIR SDL3_VERSION
    CMAKE_VS_WINDOWS_TARGET_PLATFORM_VERSION CMAKE_VS_PLATFORM_TOOLSET_VERSION
    FETCHCONTENT_SOURCE_DIR_CATCH2 FETCHCONTENT_SOURCE_DIR_SDL3 FETCHCONTENT_SOURCE_DIR_IMGUI
    FETCHCONTENT_SOURCE_DIR_IMGUI_TEST_ENGINE FETCHCONTENT_SOURCE_DIR_DEJAVU FETCHCONTENT_SOURCE_DIR_SQLITE)
  string(APPEND oc2_identity "${variable}=${${variable}}\n")
endforeach()
foreach(variable DEVELOPER_DIR VCToolsVersion WindowsSDKVersion)
  string(APPEND oc2_identity "${variable}=$ENV{${variable}}\n")
endforeach()
file(WRITE "${PROJECT_BINARY_DIR}/build-identity.txt" "${oc2_identity}")
