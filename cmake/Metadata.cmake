# Optional stdlib-only developer checks; no Python dependency for normal builds.
if(Python3_Interpreter_FOUND)
  add_custom_target(inventory-check
    COMMAND "${Python3_EXECUTABLE}" "${PROJECT_SOURCE_DIR}/tools/check_conformance.py"
    COMMAND "${Python3_EXECUTABLE}" "${PROJECT_SOURCE_DIR}/tools/check_assets.py"
    WORKING_DIRECTORY "${PROJECT_SOURCE_DIR}"
    COMMENT "Checking specification registry and coverage metadata" VERBATIM)
  if(BUILD_TESTING)
    add_test(NAME inventory COMMAND "${Python3_EXECUTABLE}" "${PROJECT_SOURCE_DIR}/tools/check_conformance.py")
    set_tests_properties(inventory PROPERTIES LABELS "metadata" TIMEOUT 30)
    add_test(NAME content_inventory COMMAND "${Python3_EXECUTABLE}" "${PROJECT_SOURCE_DIR}/tools/check_assets.py")
    set_tests_properties(content_inventory PROPERTIES LABELS "metadata" TIMEOUT 30)
  endif()
endif()
