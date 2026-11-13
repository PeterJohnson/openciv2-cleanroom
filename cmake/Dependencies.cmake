include_guard(GLOBAL)
include(FetchContent)
file(READ "${PROJECT_SOURCE_DIR}/dependencies.json" oc2_dependency_lock)
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${PROJECT_SOURCE_DIR}/dependencies.json")

# No configure-time network access unless explicitly enabled. Existing populated
# trees and explicit source overrides work offline; absent inputs fail early.
macro(oc2_fetch_dependency name)
  string(TOUPPER "${name}" oc2_dependency_upper)
  string(TOLOWER "${name}" oc2_dependency_lower)
  string(JSON oc2_dependency_url GET "${oc2_dependency_lock}" libraries ${name} url)
  string(JSON oc2_dependency_hash GET "${oc2_dependency_lock}" libraries ${name} sha256)
  set(oc2_source_override "${FETCHCONTENT_SOURCE_DIR_${oc2_dependency_upper}}")
  set(oc2_saved_disconnected "${FETCHCONTENT_FULLY_DISCONNECTED}")
  if(oc2_source_override)
    if(NOT IS_DIRECTORY "${oc2_source_override}")
      message(FATAL_ERROR "Invalid FETCHCONTENT_SOURCE_DIR_${oc2_dependency_upper}: ${oc2_source_override}")
    endif()
  elseif(NOT OC2_FETCH_DEPENDENCIES)
    if(NOT IS_DIRECTORY "${FETCHCONTENT_BASE_DIR}/${oc2_dependency_lower}-src")
      message(FATAL_ERROR "${name} is not available offline. Supply FETCHCONTENT_SOURCE_DIR_${oc2_dependency_upper}, or explicitly enable OC2_FETCH_DEPENDENCIES.")
    endif()
    set(FETCHCONTENT_FULLY_DISCONNECTED ON)
  endif()
  FetchContent_Declare(${name}
    URL "${oc2_dependency_url}"
    URL_HASH "SHA256=${oc2_dependency_hash}"
    DOWNLOAD_EXTRACT_TIMESTAMP TRUE)
  FetchContent_MakeAvailable(${name})
  set(FETCHCONTENT_FULLY_DISCONNECTED "${oc2_saved_disconnected}")
endmacro()
