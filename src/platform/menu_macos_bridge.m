#include "platform/menu_macos_bridge.h"
#import <AppKit/AppKit.h>

/// Own an AppKit menu bar and route item activation to the borrowed C callback.
@interface OpenCivMenu : NSObject {
@public
    void* context;
    Oc2MacMenuCommand post;
    NSMenu* bar;
    NSMenu* previous;
    NSMenu* previousServices;
    NSMenu* current;
    NSMutableArray<NSMenuItem*>* items;
}
/// Queue the semantic command stored in the activated item's tag.
- (void)invoke:(NSMenuItem*)sender;
/// Create and select the next top-level menu group.
- (void)beginGroup:(NSString*)title;
/// Add a UTF-8 command label and optional keyboard shortcut to the current group.
- (void)addCommand:(int)command label:(const char*)label key:(char)key;
/// Detach callbacks and restore the application menus that preceded this adapter.
- (void)close;
@end

@implementation OpenCivMenu
- (void)invoke:(NSMenuItem*)sender {
    post(context, (int)sender.tag);
}
- (void)beginGroup:(NSString*)title {
    NSMenuItem* root = [[NSMenuItem alloc] initWithTitle:title action:nil keyEquivalent:@""];
    current = [[NSMenu alloc] initWithTitle:title];
    current.autoenablesItems = NO;
    root.submenu = current;
    [bar addItem:root];
}
- (void)addCommand:(int)command label:(const char*)label key:(char)key {
    NSString* shortcut =
        key ? [[NSString alloc] initWithBytes:&key length:1 encoding:NSUTF8StringEncoding] : @"";
    NSMenuItem* item = [[NSMenuItem alloc] initWithTitle:[NSString stringWithUTF8String:label]
                                                  action:@selector(invoke:)
                                           keyEquivalent:shortcut];
    item.keyEquivalentModifierMask = NSEventModifierFlagCommand;
    item.tag = command;
    item.target = self;
    [items addObject:item];
    [current addItem:item];
}
- (void)close {
    for (NSMenuItem* item in items)
        item.target = nil;
    if (NSApp.mainMenu == bar)
        NSApp.mainMenu = previous;
    NSApp.servicesMenu = previousServices;
}
@end

Oc2MacMenu* oc2_mac_menu_create(void* context, Oc2MacMenuCommand post, int about_command,
                                const char* about_label, char about_key, int quit_command,
                                const char* quit_label, char quit_key) {
    OpenCivMenu* menu = [OpenCivMenu new];
    menu->context = context;
    menu->post = post;
    menu->previous = NSApp.mainMenu;
    menu->previousServices = NSApp.servicesMenu;
    menu->bar = [NSMenu new];
    menu->items = [NSMutableArray new];
    [menu beginGroup:@"OpenCiv2"];
    [menu addCommand:about_command label:about_label key:about_key];
    NSMenu* app = menu->current;
    [app addItem:NSMenuItem.separatorItem];
    NSMenuItem* services = [[NSMenuItem alloc] initWithTitle:@"Services"
                                                      action:nil
                                               keyEquivalent:@""];
    services.submenu = [[NSMenu alloc] initWithTitle:@"Services"];
    [app addItem:services];
    NSApp.servicesMenu = services.submenu;
    [app addItem:NSMenuItem.separatorItem];
    NSMenuItem* hide = [[NSMenuItem alloc] initWithTitle:@"Hide OpenCiv2"
                                                  action:@selector(hide:)
                                           keyEquivalent:@"h"];
    hide.target = NSApp;
    [app addItem:hide];
    NSMenuItem* hideOthers = [[NSMenuItem alloc] initWithTitle:@"Hide Others"
                                                        action:@selector(hideOtherApplications:)
                                                 keyEquivalent:@"h"];
    hideOthers.keyEquivalentModifierMask = NSEventModifierFlagCommand | NSEventModifierFlagOption;
    hideOthers.target = NSApp;
    [app addItem:hideOthers];
    NSMenuItem* showAll = [[NSMenuItem alloc] initWithTitle:@"Show All"
                                                     action:@selector(unhideAllApplications:)
                                              keyEquivalent:@""];
    showAll.target = NSApp;
    [app addItem:showAll];
    [app addItem:NSMenuItem.separatorItem];
    [menu addCommand:quit_command label:quit_label key:quit_key];
    NSApp.mainMenu = menu->bar;
    return (__bridge_retained Oc2MacMenu*)menu;
}
void oc2_mac_menu_destroy(Oc2MacMenu* value) {
    OpenCivMenu* menu = (__bridge_transfer OpenCivMenu*)value;
    [menu close];
}
void oc2_mac_menu_begin_group(Oc2MacMenu* value, const char* label) {
    OpenCivMenu* menu = (__bridge OpenCivMenu*)value;
    [menu beginGroup:[NSString stringWithUTF8String:label]];
}
void oc2_mac_menu_add_item(Oc2MacMenu* value, int command, const char* label, char key) {
    OpenCivMenu* menu = (__bridge OpenCivMenu*)value;
    [menu addCommand:command label:label key:key];
}
void oc2_mac_menu_sync_item(Oc2MacMenu* value, int command, int enabled, int checked) {
    OpenCivMenu* menu = (__bridge OpenCivMenu*)value;
    for (NSMenuItem* item in menu->items) {
        if (item.tag == command) {
            item.enabled = enabled != 0;
            item.state = checked ? NSControlStateValueOn : NSControlStateValueOff;
        }
    }
}
