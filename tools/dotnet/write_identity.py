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
    "docs/algorithms/F05-game-setup-and-starting-positions.md",
    "docs/algorithms/S01-scenario-loading-and-parameters.md",
    "docs/algorithms/S02-event-language-and-execution.md",
    "docs/algorithms/P04-text-and-dialogs.md",
    "docs/algorithms/A01-ai-strategy-and-assessment.md",
    "docs/algorithms/F04-turn-sequencing.md",
    "docs/algorithms/V03-research-and-technology.md",
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
    kind = "src" if name in (LIBRARIES - {"OpenCiv2.FoundationCases"}) | {"OpenCiv2.App", "OpenCiv2.Desktop", "OpenCiv2.Browser", "OpenCiv2.Automation"} else "tests"
    directory = root / kind / name
    paths = [root / p for p in COMMON]
    paths += [directory / f"{name}.csproj", directory / "packages.lock.json"]
    paths += sorted(p for p in directory.rglob("*.cs") if not {"bin", "obj"} & set(p.relative_to(directory).parts))
    paths += sorted(p for p in (directory / "wwwroot").rglob("*") if p.is_file())
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
