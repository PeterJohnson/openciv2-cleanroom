#!/usr/bin/env python3
"""Check the implemented managed foundation's project/package boundary and locks."""
import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]

# Direct production references, reviewed against the architecture.
GUI_PROJECTS = {"OpenCiv2.App", "OpenCiv2.Desktop", "OpenCiv2.Browser"}
RENDERING_PROJECTS = {"OpenCiv2.MapRendering"}
RENDERING_PACKAGES = {"SkiaSharp", *{f"SkiaSharp.NativeAssets.{platform}"
    for platform in ("Linux", "WebAssembly", "Win32", "macOS")}}
GUI_PACKAGES = {
    "Avalonia", "Avalonia.Angle.Windows.Natives", "Avalonia.Browser", "Avalonia.BuildServices",
    "Avalonia.Desktop", "Avalonia.Fonts.Inter", "Avalonia.FreeDesktop", "Avalonia.FreeDesktop.AtSpi",
    "Avalonia.HarfBuzz", "Avalonia.Native", "Avalonia.Remote.Protocol", "Avalonia.Skia", "Avalonia.Themes.Fluent",
    "Avalonia.Win32", "Avalonia.X11", "MicroCom.Runtime", "Tmds.DBus.Protocol", "HarfBuzzSharp", "SkiaSharp",
    "Microsoft.NET.ILLink.Tasks", "Microsoft.NET.Sdk.WebAssembly.Pack",
    *{f"{library}.NativeAssets.{platform}" for library in ("HarfBuzzSharp", "SkiaSharp")
      for platform in ("Linux", "WebAssembly", "Win32", "macOS")},
}
EDGES = {
    "OpenCiv2.Automation": {"OpenCiv2.Console"},
    "OpenCiv2.App": {"OpenCiv2.Session", "OpenCiv2.ViewModels", "OpenCiv2.Console", "OpenCiv2.MapRendering"},
    "OpenCiv2.MapRendering": {"OpenCiv2.Base", "OpenCiv2.Contracts", "OpenCiv2.Formats"},
    "OpenCiv2.Desktop": {"OpenCiv2.App", "OpenCiv2.IO.Native"},
    "OpenCiv2.Browser": {"OpenCiv2.App"},
    "OpenCiv2.ViewModels": {"OpenCiv2.Contracts"},
    "OpenCiv2.Console": {"OpenCiv2.Contracts"},
    "OpenCiv2.Base": set(),
    "OpenCiv2.IO": {"OpenCiv2.Base"},
    "OpenCiv2.Contracts": {"OpenCiv2.Base"},
    "OpenCiv2.Core": {"OpenCiv2.Base", "OpenCiv2.Contracts"},
    "OpenCiv2.Formats": {"OpenCiv2.Core", "OpenCiv2.IO"},
    "OpenCiv2.Session": {"OpenCiv2.Core", "OpenCiv2.Formats", "OpenCiv2.Query", "OpenCiv2.Network"},
    "OpenCiv2.Network": {"OpenCiv2.Core", "OpenCiv2.IO"},
    "OpenCiv2.Query": {"OpenCiv2.Core", "OpenCiv2.Formats"},
    "OpenCiv2.IO.Native": {"OpenCiv2.IO"},
    "OpenCiv2.Platform.Native": {"OpenCiv2.Formats", "OpenCiv2.IO.Native"},
    "OpenCiv2.Cli": {"OpenCiv2.Query", "OpenCiv2.IO.Native"},
    "OpenCiv2.Export.Sqlite": {"OpenCiv2.Query"},
    "OpenCiv2.Cli.Sqlite": {"OpenCiv2.Cli", "OpenCiv2.Export.Sqlite"},
}


def main():
    projects = sorted(p for folder in (ROOT / "src", ROOT / "tests") for p in folder.glob("*/*.csproj") if not {"obj", "bin"} & set(p.parts))
    failures = []
    # NuGet project keys follow PackageId/AssemblyName, including the oc2 CLI.
    project_ids = {
        (ET.parse(p).findtext("PropertyGroup/PackageId") or ET.parse(p).findtext("PropertyGroup/AssemblyName") or p.stem).lower()
        for p in projects if p.stem in EDGES
    }
    for project in projects:
        lock_path = project.with_name("packages.lock.json")
        if not lock_path.exists():
            failures.append(f"Missing lock: {lock_path.relative_to(ROOT)}")
            continue
        lock = json.loads(lock_path.read_text())
        if "src" in project.relative_to(ROOT).parts:
            if project.stem not in EDGES:
                failures.append(f"Unregistered production project: {project.stem}")
            # GUI/native/optional dependencies cannot arrive silently.
            allowed = {"RandN.Pcg32", "RandN.Core"} | (GUI_PACKAGES if project.stem in GUI_PROJECTS else set())
            if project.stem == "OpenCiv2.Automation":
                allowed |= {"LuaCSharp", "LuaCSharp.Annotations", "LuaCSharp.SourceGenerator"}
            if project.stem in RENDERING_PROJECTS:
                allowed |= RENDERING_PACKAGES
            for dependencies in lock["dependencies"].values():
                for name, entry in dependencies.items():
                    if entry["type"] == "Project":
                        if name not in project_ids:
                            failures.append(f"Unexpected production project dependency: {name}")
                    elif name not in allowed:
                        failures.append(f"Unexpected production dependency: {name}")
        for reference in ET.parse(project).iter("ProjectReference"):
            target = (project.parent / reference.attrib["Include"]).resolve()
            if not target.is_relative_to(ROOT) or not target.is_file():
                failures.append(f"Invalid project reference: {project}: {target}")
            elif "src" in project.relative_to(ROOT).parts:
                if target.stem not in EDGES.get(project.stem, set()):
                    failures.append(f"Forbidden project reference: {project.stem} -> {target.stem}")
    by_name = {project.stem: project for project in projects}
    def dependencies(name):
        seen = set()
        pending = [name]
        while pending:
            current = pending.pop()
            if current in seen:
                continue
            seen.add(current)
            pending.extend(Path(r.attrib["Include"]).stem for r in ET.parse(by_name[current]).iter("ProjectReference"))
        return seen
    browser_forbidden = {"OpenCiv2.IO.Native", "OpenCiv2.Platform.Native", "OpenCiv2.Cli", "OpenCiv2.Cli.Sqlite", "OpenCiv2.Export.Sqlite"}
    for browser in ("OpenCiv2.Browser.Tests", "OpenCiv2.ConformanceBrowser", "OpenCiv2.Browser", "OpenCiv2.MapBrowser", "OpenCiv2.AutomationBrowser", "OpenCiv2.PerformanceBrowser"):
        if dependencies(browser) & browser_forbidden:
            failures.append(f"{browser} graph includes a native filesystem/CLI/export dependency")
    for portable in ("OpenCiv2.Cli", "OpenCiv2.Session", "OpenCiv2.ViewModels", "OpenCiv2.Console", "OpenCiv2.Browser.Tests"):
        if dependencies(portable) & (GUI_PROJECTS | RENDERING_PROJECTS):
            failures.append(f"{portable} unexpectedly depends on a GUI host")
    if {"OpenCiv2.Automation", "OpenCiv2.Export.Sqlite"} & dependencies("OpenCiv2.Cli"):
        failures.append("Default CLI graph includes optional Lua/SQLite")
    for project in ET.parse(ROOT / "OpenCiv2.slnx").iter("Project"):
        if Path(project.attrib["Path"]).stem in GUI_PROJECTS | RENDERING_PROJECTS | {"OpenCiv2.Automation", "OpenCiv2.AutomationCases", "OpenCiv2.Automation.Tests", "OpenCiv2.AutomationProbe", "OpenCiv2.AutomationBrowser", "OpenCiv2.Export.Sqlite", "OpenCiv2.Cli.Sqlite", "OpenCiv2.Sqlite.Tests"}:
            failures.append("Default solution includes optional GUI/Lua/SQLite")
    if failures:
        raise SystemExit("\n".join(failures))
    print(f"Managed project boundaries and package locks checked ({len(projects)} projects).")


if __name__ == "__main__":
    main()
