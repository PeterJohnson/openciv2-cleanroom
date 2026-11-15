# Portable targets may only link other portable implementation targets. Header
# access is checked separately, including transitive player-facing includes.
set(oc2_portable_targets oc2_base oc2_io oc2_contracts oc2_game oc2_formats oc2_query oc2_network oc2_session oc2_viewmodel)
set(oc2_portable_dependencies ${oc2_portable_targets} oc2_options oc2_json)
foreach(owner IN LISTS oc2_portable_targets)
  get_target_property(links ${owner} LINK_LIBRARIES)
  if(links)
    foreach(dependency IN LISTS links)
      if(NOT dependency IN_LIST oc2_portable_dependencies)
        message(FATAL_ERROR "Portable target ${owner} has forbidden dependency ${dependency}")
      endif()
    endforeach()
  endif()
endforeach()
