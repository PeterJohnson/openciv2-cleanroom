#pragma once

// AppKit is compiled as Objective-C. Only this C ABI crosses into C++ modules.
#ifdef __cplusplus
extern "C" {
#endif

/// Opaque owned AppKit menu handle passed across the C/Objective-C boundary.
typedef struct Oc2MacMenu Oc2MacMenu;
/// Queue a semantic command using a context that outlives the native menu.
typedef void (*Oc2MacMenuCommand)(void* context, int command);

/// Install an AppKit menu bar with application commands and a borrowed callback.
/// @return An owned handle to release with oc2_mac_menu_destroy().
Oc2MacMenu* oc2_mac_menu_create(void* context, Oc2MacMenuCommand post, int about_command,
                                const char* about_label, char about_key, int quit_command,
                                const char* quit_label, char quit_key);
/// Restore prior application menu state and release this handle.
void oc2_mac_menu_destroy(Oc2MacMenu* menu);
/// Begin a top-level menu group using a UTF-8 label.
void oc2_mac_menu_begin_group(Oc2MacMenu* menu, const char* label);
/// Add a command to the current group with an optional Command-key shortcut.
void oc2_mac_menu_add_item(Oc2MacMenu* menu, int command, const char* label, char key);
/// Update the native item matching the semantic command identifier.
void oc2_mac_menu_sync_item(Oc2MacMenu* menu, int command, int enabled, int checked);

#ifdef __cplusplus
}
#endif
