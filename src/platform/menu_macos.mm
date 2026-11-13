#include "platform/menu.hpp"
#import <AppKit/AppKit.h>

// Objective-C declarations must live at global scope. ARC owns this target for
// the lifetime of the menu; NSMenuItem itself does not retain its target.
@interface OpenCivMenuTarget : NSObject {
@public
    openciv::platform::CommandSink sink;
}
- (void)invoke:(NSMenuItem*)sender;
@end

@implementation OpenCivMenuTarget
- (void)invoke:(NSMenuItem*)sender {
    sink(static_cast<openciv::Command>(sender.tag));
}
@end

namespace openciv::platform {
namespace {
NSString* string(std::string_view value) {
    return [[NSString alloc] initWithBytes:value.data() length:value.size() encoding:NSUTF8StringEncoding];
}

class MacMenu final : public Menu {
public:
    explicit MacMenu(CommandSink sink) {
        target_ = [OpenCivMenuTarget new];
        target_->sink = sink;
        previous_ = NSApp.mainMenu;
        previous_services_ = NSApp.servicesMenu;
        bar_ = [NSMenu new];
        items_ = [NSMutableArray new];

        NSMenu* app = submenu(@"OpenCiv2");
        add(app, *find_menu_item(Command::about));
        [app addItem:NSMenuItem.separatorItem];
        NSMenuItem* services = [[NSMenuItem alloc] initWithTitle:@"Services" action:nil keyEquivalent:@""];
        services.submenu = [[NSMenu alloc] initWithTitle:@"Services"];
        [app addItem:services];
        NSApp.servicesMenu = services.submenu;
        [app addItem:NSMenuItem.separatorItem];
        NSMenuItem* hide = [[NSMenuItem alloc] initWithTitle:@"Hide OpenCiv2" action:@selector(hide:) keyEquivalent:@"h"];
        hide.target = NSApp;
        [app addItem:hide];
        NSMenuItem* hide_others = [[NSMenuItem alloc] initWithTitle:@"Hide Others" action:@selector(hideOtherApplications:) keyEquivalent:@"h"];
        hide_others.keyEquivalentModifierMask = NSEventModifierFlagCommand | NSEventModifierFlagOption;
        hide_others.target = NSApp;
        [app addItem:hide_others];
        NSMenuItem* show_all = [[NSMenuItem alloc] initWithTitle:@"Show All" action:@selector(unhideAllApplications:) keyEquivalent:@""];
        show_all.target = NSApp;
        [app addItem:show_all];
        [app addItem:NSMenuItem.separatorItem];
        add(app, *find_menu_item(Command::quit));

        for (const auto& group : main_menu()) {
            // About and Quit follow macOS convention in the application menu.
            NSMenu* menu = nil;
            for (const auto& item : group.items) {
                if (item.command == Command::quit || item.command == Command::about) continue;
                if (!menu) menu = submenu(string(group.label));
                add(menu, item);
            }
        }
        NSApp.mainMenu = bar_;
    }
    ~MacMenu() override {
        for (NSMenuItem* item in items_) item.target = nil;
        if (NSApp.mainMenu == bar_) NSApp.mainMenu = previous_;
        NSApp.servicesMenu = previous_services_;
    }
    bool native() const override { return true; }
    void sync(const Shell& shell) override {
        for (NSMenuItem* item in items_) {
            const auto command = static_cast<Command>(item.tag);
            item.enabled = shell.enabled(command);
            item.state = shell.checked(command) ? NSControlStateValueOn : NSControlStateValueOff;
        }
    }

private:
    NSMenu* submenu(NSString* title) {
        NSMenuItem* root = [[NSMenuItem alloc] initWithTitle:title action:nil keyEquivalent:@""];
        NSMenu* menu = [[NSMenu alloc] initWithTitle:title];
        menu.autoenablesItems = NO;
        root.submenu = menu;
        [bar_ addItem:root];
        return menu;
    }
    void add(NSMenu* menu, const MenuItem& item) {
        NSString* key = item.key ? string(std::string_view(&item.key, 1)) : @"";
        NSMenuItem* native = [[NSMenuItem alloc] initWithTitle:string(item.label) action:@selector(invoke:) keyEquivalent:key];
        native.keyEquivalentModifierMask = NSEventModifierFlagCommand;
        native.tag = static_cast<NSInteger>(item.command);
        native.target = target_;
        [items_ addObject:native];
        [menu addItem:native];
    }
    NSMenu* __strong bar_;
    NSMenu* __strong previous_;
    NSMenu* __strong previous_services_;
    NSMutableArray<NSMenuItem*>* __strong items_;
    OpenCivMenuTarget* __strong target_;
};
}

std::unique_ptr<Menu> create_menu(SDL_Window*, CommandSink sink) {
    return std::make_unique<MacMenu>(sink);
}
} // namespace openciv::platform
