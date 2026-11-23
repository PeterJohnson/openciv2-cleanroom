#!/usr/bin/env python3
"""Validate compiled managed identities before recording foundation evidence.

This covers the portable foundation host, not X02 operation registrations. Source
hashes come from embedded build-time manifests, never from relabeling old DLLs.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import platform
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
COMMON = (
    "packaging/Notices.targets",
    "Directory.Build.props", "Directory.Build.targets",
    "Directory.Packages.props", "global.json", "NuGet.Config",
    "dependencies.json", "docs/random-policy-pcg32.md",
    "docs/algorithms/F01-determinism-and-arithmetic.md",
    "docs/algorithms/F02-game-state-and-invariants.md", "docs/algorithms/F03-rules-and-configuration.md", "docs/algorithms/F06-save-load-and-file-formats.md",
    "docs/algorithms/W01-map-geometry-and-spatial-state.md", "docs/algorithms/C01-city-lifecycle-and-ownership.md",
    "docs/algorithms/C08-buildings-and-wonders.md",
    "docs/algorithms/C09-city-capture-and-revolt.md",
    "docs/algorithms/C02-city-tiles-and-worker-assignment.md",
    "docs/algorithms/C03-city-economy-and-corruption.md",
    "docs/algorithms/C04-city-happiness-and-disorder.md",
    "docs/algorithms/C05-city-growth-and-support.md",
    "docs/algorithms/W04-terrain-work-and-improvements.md",
    "docs/algorithms/W05-pollution-and-global-warming.md",
    "docs/algorithms/W02-map-generation.md", "docs/algorithms/U03-pathfinding-and-goto.md",
    "docs/algorithms/U02-movement-and-zones-of-control.md", "docs/algorithms/U04-unit-orders-and-activation.md",
    "docs/algorithms/U05-transport-and-special-movement.md",
    "docs/algorithms/U06-combat-and-veterancy.md",
    "docs/algorithms/U07-nuclear-attacks.md",
    "docs/algorithms/F05-game-setup-and-starting-positions.md",
    "docs/algorithms/S01-scenario-loading-and-parameters.md",
    "docs/algorithms/S02-event-language-and-execution.md",
    "docs/algorithms/P01-map-projection-and-interaction.md", "docs/algorithms/P02-map-rendering-and-minimap.md",
    "docs/algorithms/P03-graphics-and-asset-processing.md", "docs/algorithms/P05-input-and-command-dispatch.md",
    "docs/algorithms/W03-visibility-and-exploration.md", "docs/algorithms/P10-audio-video-and-animation.md",
    "docs/content/asset-format.md", "docs/content/assets.json", "docs/content/fonts.json",
    "docs/content/map-assets.md", "docs/content/map-asset-bindings.json",
    "docs/testing/map-rendering.md", "docs/performance/map-rendering.md",
    "docs/algorithms/P04-text-and-dialogs.md",
    "docs/algorithms/A01-ai-strategy-and-assessment.md",
    "docs/algorithms/A08-ai-research-government-and-diplomacy.md",
    "docs/algorithms/F04-turn-sequencing.md",
    "docs/algorithms/P09-council-and-throne-room.md",
    "docs/algorithms/V03-research-and-technology.md",
    "docs/algorithms/V01-civilization-lifecycle-and-civil-war.md",
    "docs/algorithms/A02-ai-city-production.md",
    "docs/algorithms/A03-ai-settlement-and-terrain-work.md",
    "docs/algorithms/A04-ai-ground-tactics.md",
    "docs/algorithms/A05-ai-naval-and-transport.md",
    "docs/algorithms/C06-city-production-and-purchasing.md",
    "docs/algorithms/C07-trade-and-commodities.md",
    "docs/algorithms/V02-governments-and-tax-rates.md",
    "docs/algorithms/V04-diplomacy-and-treaties.md",
    "docs/algorithms/V05-diplomatic-negotiation.md",
    "docs/algorithms/V06-espionage-and-bribery.md",
    "docs/algorithms/V07-spaceship.md",
    "docs/algorithms/V08-scoring-victory-and-retirement.md",
    "docs/algorithms/W06-huts-and-barbarians.md",
    "docs/algorithms/N02-network-commands-and-state-synchronization.md",
    "docs/algorithms/N03-multiplayer-turn-sequencing.md",
    "docs/algorithms/N05-transport-and-wire-protocol.md",
    "docs/algorithms/U01-unit-lifecycle-and-stacks.md",
    "docs/algorithms/X01-classic-compatibility-contract.md", "docs/architecture.md", "docs/m1-session-foundations.md", "docs/m1-command-contract.md",
    "docs/algorithms/X02-whole-game-conformance.md", "docs/m1-checkpoint.md", "docs/m1-closeout.md", "docs/application.md",
    "docs/m1-content-conversion.md", "docs/content/data-format.md", "docs/content/gameplay-format.md",
    *["data/gameplay/classic-mge-5.4.0f/" + name + ".json" for name in
      ("gameplay", "calendar", "spaceship", "governments", "difficulty", "combat", "scoring")],
)
LIBRARIES = {"OpenCiv2.ViewModels", "OpenCiv2.Console", "OpenCiv2.Base", "OpenCiv2.IO", "OpenCiv2.Contracts", "OpenCiv2.FoundationCases", "OpenCiv2.Core", "OpenCiv2.Formats", "OpenCiv2.Query", "OpenCiv2.Network", "OpenCiv2.Session"}
HOSTS = {"OpenCiv2.FoundationProbe", "OpenCiv2.Browser.Tests"}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON key: {key}")
        result[key] = value
    return result


def expected_inputs(root, name):
    kind = "src" if name in (LIBRARIES - {"OpenCiv2.FoundationCases"}) | {"OpenCiv2.App", "OpenCiv2.MapRendering", "OpenCiv2.Desktop", "OpenCiv2.Desktop.Automation", "OpenCiv2.Browser", "OpenCiv2.Automation", "OpenCiv2.IO.Native", "OpenCiv2.Platform.Native"} else "tests"
    directory = root / kind / name
    paths = [root / p for p in COMMON]
    paths += [directory / f"{name}.csproj", directory / "packages.lock.json"]
    paths += sorted(p for p in directory.rglob("*.cs") if not {"bin", "obj"} & set(p.relative_to(directory).parts))
    paths += sorted(p for p in (directory / "wwwroot").rglob("*") if p.is_file())
    if name == "OpenCiv2.Platform.Native":
        paths += [root / "native/audio/oc2_audio.c", root / "tools/dotnet/build_audio.py"]
    if name == "OpenCiv2.MapBrowser":
        paths += sorted((root / "src/OpenCiv2.Browser").glob("*.cs"))
        paths += [root / "src/OpenCiv2.Browser/wwwroot/index.html"]
        paths += [root / "src/OpenCiv2.Browser/wwwroot/browser-lifecycle.js"]
        paths += [root / "src/OpenCiv2.Browser/wwwroot/storage.js"]
        paths += [root / "src/OpenCiv2.Browser/wwwroot/audio.js"]
        paths += sorted((root / "tests/OpenCiv2.MapProbe").glob("NativeMapMatrix*.cs"))
        paths += [root / "tests/OpenCiv2.MapProbe" / p for p in
                  ("RendererLibraryChecks.cs", "MapPresentationChecks.cs", "ScopedMapPerformanceWorld.cs", "GpuOptimizationChecks.cs", "GpuMaskChecks.cs", "GpuPixelChecks.cs", "GpuSamplingChecks.cs")]
    return {p.relative_to(root).as_posix(): sha(p) for p in paths}


def validate_report(report, root=ROOT):
    if report.get("Format") != "openciv2-managed-foundations/2" or report.get("Passed") is not True:
        raise ValueError("Missing or failed managed foundation report")
    if report.get("PageErrors"):
        raise ValueError("Browser reported execution errors")
    cases = report.get("Cases")
    if not isinstance(cases, list) or not cases or any(c.get("Passed") is not True for c in cases):
        raise ValueError("Missing or failed managed foundation cases")
    identities = report.get("Build", [])
    names = [entry["Assembly"] for entry in identities]
    hosts = set(names) & HOSTS
    if len(hosts) != 1 or len(names) != len(set(names)) or set(names) != LIBRARIES | hosts:
        raise ValueError("Incomplete or duplicate compiled managed assembly identities")
    sdk = json.loads((root / "global.json").read_text())["sdk"]["version"]
    runtime = ET.parse(root / "Directory.Build.props").findtext("PropertyGroup/RuntimeFrameworkVersion")
    sources = {}
    for entry in identities:
        name = entry["Assembly"]
        expected = expected_inputs(root, name)
        if entry["Sources"] != expected:
            raise ValueError(f"Stale or incomplete compiled sources for {name}; rebuild and rerun")
        target = "net11.0-browser" if name == "OpenCiv2.Browser.Tests" else "net11.0"
        if (entry["Sdk"], entry["Runtime"], entry["Target"], entry["Configuration"]) != (sdk, runtime, target, "Release"):
            raise ValueError(f"Unpinned or non-release managed build: {name}")
        sources.update(expected)
    return sources


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("--report", type=Path, action="append", required=True)
    args = parser.parse_args()
    compiled_sources = {}
    reports = []
    for path in args.report:
        report = json.loads(path.read_text(), object_pairs_hook=unique_object)
        compiled_sources.update(validate_report(report))
        reports.append({"path": str(path.resolve()), "sha256": sha(path), "build": report["Build"]})
    # Artifacts here are a post-run inventory. X02 must additionally bind its
    # invoked adapter artifacts before/after execution.
    artifacts = {}
    for path in sorted(p for folder in (ROOT / "src", ROOT / "tests") for p in folder.rglob("*")):
        if path.is_file() and "bin" in path.parts and path.name.startswith("OpenCiv2.") and path.suffix in (".dll", ".wasm"):
            artifacts[path.relative_to(ROOT).as_posix()] = sha(path)
    if not artifacts:
        raise ValueError("No managed artifacts found; build before recording evidence")
    report = {
        "format": "openciv2-managed-build/2",
        "createdUtc": datetime.now(timezone.utc).isoformat(),
        "revision": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
        "workingTreeChanges": subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT, text=True).splitlines(),
        "host": {"system": platform.system(), "machine": platform.machine()},
        "compiledSources": compiled_sources,
        "reportingSources": {p.relative_to(ROOT).as_posix(): sha(p) for p in sorted((ROOT / "tools/dotnet").glob("*")) if p.is_file()},
        "artifactsAfterRun": artifacts,
        "reports": reports,
        "scope": "Build-time foundation identities verified against checkout. X02 adapter invocation evidence is recorded separately by tools/dotnet/run_conformance.py."
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n")
    print(f"Verified {len(compiled_sources)} compiled inputs across {len(reports)} foundation reports.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError) as error:
        raise SystemExit(str(error)) from error
