#!/usr/bin/env python3
"""Run the managed X02 adapter with verified compiled sources and versioned PCG fixtures.

All 15 registered operations and 57 selected cases retain the existing comparator.
Expected observations never enter the adapter. The registry and selected fixtures retain their explicit policy and provenance.
"""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
import conformance as runner
sys.path.insert(0, str(ROOT / "tools/dotnet"))
import pcg_fixtures
import write_identity

POLICY = ("oc2-pcg32", "1", "docs/random-policy-pcg32.md")
LIBRARIES = {"OpenCiv2.Base", "OpenCiv2.IO", "OpenCiv2.Contracts", "OpenCiv2.Core", "OpenCiv2.Formats", "OpenCiv2.Query", "OpenCiv2.Network", "OpenCiv2.Session", "OpenCiv2.Conformance"}
HOSTS = {"OpenCiv2.ConformanceAdapter", "OpenCiv2.ConformanceBrowser"}


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=write_identity.unique_object, parse_constant=runner.reject_nonfinite)


def selection(root=ROOT):
    # Recalculate fixture metadata and expected PCG words independently before accepting the selection.
    for path, data in pcg_fixtures.generated().items():
        runner.require(path.read_bytes() == data, f"Stale PCG replacement or selection: {path}")
    manifest = read_json(root / "docs/conformance/selection.json")
    paths = []
    for entry in manifest["cases"]:
        path = root / entry["path"]
        runner.require(write_identity.sha(path) == entry["sha256"], "Selected fixture changed")
        case = runner.load_fixture(path, root, random_policy=POLICY)
        runner.require(case["id"] == entry["id"] and case["boundary"]["operation"] == entry["operation"], "Selection contract mismatch")
        paths.append(path)
    return manifest, paths


def validate_identity(identity, root=ROOT):
    runner.object_keys(identity, "format policy policyVersion Build", "managed adapter identity")
    runner.require(identity["format"] == "openciv2-managed-conformance-adapter/1" and identity["policy"] == POLICY[0] and identity["policyVersion"] == 1, "Unknown managed adapter/policy identity")
    builds = identity["Build"]
    runner.require(isinstance(builds, list), "Missing compiled identities")
    names = [entry["Assembly"] for entry in builds]
    hosts = set(names) & HOSTS
    runner.require(len(hosts) == 1 and len(names) == len(set(names)) and set(names) == LIBRARIES | hosts, "Incomplete or duplicate managed adapter identities")
    sdk = read_json(root / "global.json")["sdk"]["version"]
    runtime = ET.parse(root / "Directory.Build.props").findtext("PropertyGroup/RuntimeFrameworkVersion")
    sources = {}
    for entry in builds:
        name = entry["Assembly"]
        expected = write_identity.expected_inputs(root, name)
        runner.require(entry["Sources"] == expected, f"Stale or incomplete compiled adapter sources: {name}")
        target = "net11.0-browser" if name == "OpenCiv2.ConformanceBrowser" else "net11.0"
        runner.require((entry["Sdk"], entry["Runtime"], entry["Target"], entry["Configuration"]) == (sdk, runtime, target, "Release"), f"Unpinned/non-release adapter build: {name}")
        sources.update(expected)
    return sources


def reporting_sources(root=ROOT):
    paths = {root / path for path in runner.RUNNER_SOURCES if path != "docs/random-policy.md"}
    paths.update((root / "tools/dotnet").glob("*.py"))
    paths.update((root / "tools/dotnet").glob("*.cjs"))
    paths.add(root / "docs/conformance/selection.json")
    paths.add(root / "docs/conformance/registry.json")
    paths.add(root / "docs/algorithms/X02-whole-game-conformance.md")
    return {path.relative_to(root).as_posix(): write_identity.sha(path) for path in sorted(paths)}


def artifact_hashes(adapter):
    executable = Path(adapter[-1]).resolve()
    runner.require(executable.is_file(), "Adapter command must end with its DLL/apphost path")
    paths = {p for p in executable.parent.iterdir() if p.is_file() and p.suffix != ".pdb"}
    paths.add(executable)
    host = Path(shutil.which(adapter[0]) or adapter[0]).resolve()
    if host.is_file():
        paths.add(host)
    return {str(path): write_identity.sha(path) for path in sorted(paths)}


def stable(sources, artifacts, root=ROOT):
    runner.require(all(write_identity.sha(root / path) == digest for path, digest in sources.items()), "Sources changed during conformance run; rebuild and rerun")
    runner.require(all(write_identity.sha(Path(path)) == digest for path, digest in artifacts.items()), "Adapter artifacts changed during conformance run")


def browser_run(bundle, node, paths):
    cases = [runner.load_fixture(path, random_policy=POLICY) for path in paths]
    inputs = []
    for case in cases:
        args, data = runner.adapter_input(case)
        inputs.append({"arguments": args, "input": data, "hostMilliseconds": case["limits"]["hostMilliseconds"]})
    with tempfile.TemporaryDirectory(prefix="oc2-managed-conformance-") as directory:
        requests = Path(directory) / "inputs.json"
        responses = Path(directory) / "observations.json"
        requests.write_text(json.dumps(inputs) + "\n", encoding="utf-8")
        process = subprocess.run([node, str(ROOT / "tools/dotnet/run_conformance_browser.cjs"), str(bundle), str(requests), str(responses)],
                                 capture_output=True, text=True, timeout=90 + sum(c["hostMilliseconds"] for c in inputs) / 1000)
        runner.require(process.returncode == 0, "Browser conformance host failed: " + process.stderr)
        raw = read_json(responses)
    runner.require(not raw.get("pageErrors"), "Browser reported execution errors")
    runner.require(len(raw["observations"]) == len(cases), "Browser omitted observations")
    results = []
    for path, case, observation in zip(paths, cases, raw["observations"]):
        result = {"path": path.relative_to(ROOT).as_posix(), "sha256": write_identity.sha(path), "id": case["id"], "revision": case["revision"],
                  "operation": case["boundary"]["operation"], "evidenceKind": case["evidence"]["kind"], "evidence": case["evidence"], "randomness": case["randomness"],
                  "specifications": case["specifications"], "target": case["target"], "profile": case["profile"], "comparison": case["comparison"], "limits": case["limits"]}
        if observation["exitCode"] != 0:
            result.update(status="adapter-failure", exitCode=observation["exitCode"], error=observation["stderr"])
        else:
            actual = json.loads(observation["stdout"], object_pairs_hook=write_identity.unique_object, parse_constant=runner.reject_nonfinite)
            mismatch = runner.compare(case, actual)
            status = "failed" if mismatch else "passed"
            if actual["outcome"] in ("unexpected-input", "budget-exhausted", "unavailable-evidence"):
                status = actual["outcome"]
            consumed = runner.scripted.consumed(actual) if case["boundary"]["operation"] == runner.scripted.OPERATION else {**runner.consumed_inputs(case), "events": len(actual["trace"])}
            result.update(status=status, actual=actual, firstMismatch=mismatch, consumed=consumed)
        results.append(result)
    return raw["identity"], results, {"browser": raw["browser"], "playwright": raw["playwright"], "invocations": inputs}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path, required=True)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--adapter", nargs=argparse.REMAINDER)
    group.add_argument("--browser-bundle", type=Path)
    parser.add_argument("--node", default="node")
    args = parser.parse_args()
    manifest, paths = selection()
    initial_sources = {**reporting_sources(), **{path.relative_to(ROOT).as_posix(): write_identity.sha(path) for path in paths},
                       **{c["historicalPath"]: c["historicalSha256"] for c in manifest["cases"]}}
    metadata = {}
    if args.browser_bundle:
        bundle = args.browser_bundle.resolve()
        artifacts = {str(p): write_identity.sha(p) for p in sorted(bundle.rglob("*")) if p.is_file()}
        runner.require(bool(artifacts), "Missing browser bundle")
        identity, results, metadata = browser_run(bundle, args.node, paths)
    else:
        runner.require(bool(args.adapter), "Missing managed adapter command")
        # Bind all invoked native adapter files before execution, not just the DLL entry point.
        artifacts = artifact_hashes(args.adapter)
        process = subprocess.run([*args.adapter, "--identity"], capture_output=True, text=True, encoding="utf-8", timeout=10)
        runner.require(process.returncode == 0, "Managed adapter identity failed: " + process.stderr)
        identity = json.loads(process.stdout, object_pairs_hook=write_identity.unique_object, parse_constant=runner.reject_nonfinite)
        validate_identity(identity)
        results = [runner.execute(path, args.adapter, ROOT, random_policy=POLICY) for path in paths]
    compiled = validate_identity(identity)
    sources = {**compiled, **initial_sources}
    report_path = args.report.resolve()
    runner.require(not report_path.is_relative_to(ROOT / "docs/algorithms") and str(report_path) not in artifacts and report_path not in {ROOT / path for path in sources}, "Report would overwrite an input/artifact")
    report = {"format": "openciv2-managed-conformance-run/1", "runnerScope": "foundation-session-pcg32-v1", "createdUtc": datetime.now(timezone.utc).isoformat(),
              "implementationRevision": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
              "workingTreeChanges": subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT, text=True).splitlines(),
              "host": platform.platform(), "selection": manifest, "compiledSources": compiled, "reportingSources": initial_sources,
              "adapterIdentity": identity, "adapterCommand": args.adapter, "adapterArtifacts": artifacts, "browser": metadata, "cases": results}
    stable(sources, artifacts)
    for case in report["cases"]:
        print(f"{case.get('id', case['path'])}: {case['status']}")
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return 0 if all(case["status"] == "passed" for case in report["cases"]) else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError, KeyError, subprocess.TimeoutExpired) as error:
        print(f"Managed conformance runner: {error}", file=sys.stderr)
        sys.exit(2)
