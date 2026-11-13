# An explicit developer input, never sibling discovery or configure-time downloads.
set(OC2_ASSETS_DIR "" CACHE PATH "Approved asset source root or extracted runtime package (inspection only until M2)")
if(OC2_BUILD_GUI AND OC2_ASSETS_DIR)
  if(NOT IS_DIRECTORY "${OC2_ASSETS_DIR}" OR
     (NOT EXISTS "${OC2_ASSETS_DIR}/runtime-inputs.json" AND NOT EXISTS "${OC2_ASSETS_DIR}/package.json"))
    message(FATAL_ERROR "OC2_ASSETS_DIR must name an asset source root or extracted runtime package")
  endif()
  find_package(Python3 3.9 REQUIRED COMPONENTS Interpreter)
  add_custom_target(asset-check
    COMMAND "${Python3_EXECUTABLE}" "${PROJECT_SOURCE_DIR}/tools/check_assets.py" "${OC2_ASSETS_DIR}"
    WORKING_DIRECTORY "${PROJECT_SOURCE_DIR}"
    COMMENT "Checking explicitly selected asset data (runtime loading is M2)" VERBATIM)
endif()
