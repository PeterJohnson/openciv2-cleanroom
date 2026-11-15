# Experimental branch: expose the existing foundation through a named module.
# Headers and their implementations retain their global-module ABI.
add_library(oc2_base_module)
target_sources(oc2_base_module PUBLIC FILE_SET CXX_MODULES FILES
  "${PROJECT_SOURCE_DIR}/src/base/arithmetic.cppm")
target_link_libraries(oc2_base_module PUBLIC oc2_base PRIVATE oc2_options)
set_target_properties(oc2_base_module PROPERTIES CXX_SCAN_FOR_MODULES ON)

add_executable(oc2_module_tests tests/module_arithmetic_tests.cpp
  tests/module_arithmetic_header.cpp)
target_link_libraries(oc2_module_tests PRIVATE oc2_base_module oc2_options)
set_target_properties(oc2_module_tests PROPERTIES CXX_SCAN_FOR_MODULES ON)
add_test(NAME module_arithmetic COMMAND oc2_module_tests)
set_tests_properties(module_arithmetic PROPERTIES LABELS "modules;headless")

option(OC2_TEST_IMPORT_STD "Probe the toolchain's standard library module" OFF)
if(OC2_TEST_IMPORT_STD)
  message(STATUS "import std metadata: ${CMAKE_CXX_STDLIB_MODULES_JSON}")
  message(STATUS "import std detection: ${CMAKE_CXX_COMPILER_IMPORT_STD_ERROR_MESSAGE}")
  add_executable(oc2_module_std_tests tests/module_std_tests.cpp)
  target_link_libraries(oc2_module_std_tests PRIVATE oc2_base_module oc2_options)
  set_target_properties(oc2_module_std_tests PROPERTIES CXX_SCAN_FOR_MODULES ON)
  add_test(NAME module_std COMMAND oc2_module_std_tests)
  set_tests_properties(module_std PROPERTIES LABELS "modules;headless")
endif()
