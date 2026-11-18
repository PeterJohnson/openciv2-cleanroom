#!/usr/bin/env python3
"""Require complete native/browser managed X02 equality, including consumed prefixes."""
import argparse
from pathlib import Path
import run_conformance as runner


def compare(left, right):
    manifest, paths = runner.selection()
    for report in (left, right):
        runner.runner.require(report.get("format") == "openciv2-managed-conformance-run/1", "Unknown managed conformance report")
        runner.validate_identity(report["adapterIdentity"])
        runner.runner.require(len(report["cases"]) == 57 and all(c["status"] == "passed" for c in report["cases"]), "Missing or failed conformance cases")
        runner.runner.require(report["selection"] == manifest, "Report fixture selection is stale")
        runner.stable({**report["compiledSources"], **report["reportingSources"]}, report["adapterArtifacts"])
        for path, result in zip(paths, report["cases"]):
            case = runner.runner.load_fixture(path, random_policy=runner.POLICY)
            runner.runner.require(result["id"] == case["id"] and result["sha256"] == runner.write_identity.sha(path), "Missing, duplicate or reordered report case")
            runner.runner.require(runner.runner.compare(case, result["actual"]) is None, "Report contains a false passing observation")
            consumed = runner.runner.scripted.consumed(result["actual"]) if case["boundary"]["operation"] == runner.runner.scripted.OPERATION else {**runner.runner.consumed_inputs(case), "events": len(result["actual"]["trace"])}
            runner.runner.require(result["consumed"] == consumed, "Report consumed prefix is incorrect")
    runner.runner.require(left["selection"] == right["selection"], "Different fixture selections")
    def observations(report):
        return [(c["id"], c["sha256"], c["actual"], c["consumed"]) for c in report["cases"]]
    runner.runner.require(observations(left) == observations(right), "Native/browser observations or consumed prefixes differ")
    common = set(left["compiledSources"]) & set(right["compiledSources"])
    runner.runner.require(all(left["compiledSources"][p] == right["compiledSources"][p] for p in common), "Native/browser compiled sources differ")
    return len(common)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("left"); parser.add_argument("right")
    args = parser.parse_args()
    count = compare(runner.read_json(Path(args.left)), runner.read_json(Path(args.right)))
    print(f"All 57 observations and consumed prefixes match across {count} common compiled inputs.")
