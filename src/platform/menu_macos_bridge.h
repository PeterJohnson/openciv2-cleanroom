#pragma once

// AppKit is compiled as Objective-C. Only this C ABI crosses into C++ modules.
#ifdef __cplusplus
extern "C" {
#endif

typedef struct Oc2MacMenu Oc2MacMenu;
typedef void (*Oc2MacMenuCommand)(void* context, int command);

Oc2MacMenu* oc2_mac_menu_create(void* context, Oc2MacMenuCommand post, int about_command,
                                const char* about_label, char about_key, int quit_command,
                                const char* quit_label, char quit_key);
void oc2_mac_menu_destroy(Oc2MacMenu* menu);
void oc2_mac_menu_begin_group(Oc2MacMenu* menu, const char* label);
void oc2_mac_menu_add_item(Oc2MacMenu* menu, int command, const char* label, char key);
void oc2_mac_menu_sync_item(Oc2MacMenu* menu, int command, int enabled, int checked);

#ifdef __cplusplus
}
#endif
