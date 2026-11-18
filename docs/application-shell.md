# Application host

The Avalonia desktop and browser hosts share `OpenCiv2.App` and portable
view models over scoped session clients. See [current behavior and tests](application.md),
[build/run commands](BUILDING.md#avalonia-desktop) and the
[M2 platform/rendering plan](implementation-plan.md#m2--prove-the-graphical-and-platform-boundaries).

The current authored world demonstrates a production unit-stack command,
asynchronous confirmation/cancellation and console input. Map rendering, full
menus, file/storage adapters and a playable game remain separate delivery gates.
