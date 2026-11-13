# Optional developer targets; normal builds do not require Python or clang-format.
find_package(Python3 3.9 QUIET COMPONENTS Interpreter)
if(Python3_Interpreter_FOUND)
  add_custom_target(format
    COMMAND "${Python3_EXECUTABLE}" "${PROJECT_SOURCE_DIR}/tools/format.py"
    WORKING_DIRECTORY "${PROJECT_SOURCE_DIR}"
    COMMENT "Formatting owned C/C++/Objective-C sources" VERBATIM)
  add_custom_target(format-check
    COMMAND "${Python3_EXECUTABLE}" "${PROJECT_SOURCE_DIR}/tools/format.py" --check
    WORKING_DIRECTORY "${PROJECT_SOURCE_DIR}"
    COMMENT "Checking source formatting" VERBATIM)
endif()
