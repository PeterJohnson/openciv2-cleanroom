#!/usr/bin/env python3
"""Inspect explicitly selected asset data; never build, download or run asset code."""

import argparse
import hashlib
from pathlib import Path, PurePosixPath
import re
import struct
import sys

from check_conformance import ROOT, read_json, require

PACKAGE_FILES = {
    "data/atlas-hd.pack", "data/fonts.bin", "data/fonts_aa.bin",
    "data/ATLAS-PROVENANCE.md", "data/cityview-rects.json",
    "include/hd_layouts.h", "LICENSE", "THIRD-PARTY.txt",
}
LEGACY_FONT_FILES = {"data/fonts.bin", "data/fonts_aa.bin"}


def data_file(root, name):
    require(isinstance(name, str) and name and "\\" not in name and ":" not in name,
            f"unsafe asset name: {name}")
    parts = name.split("/")
    require(not PurePosixPath(name).is_absolute() and all(p not in {"", ".", ".."} for p in parts),
            f"unsafe asset path: {name}")
    path = (root / name).resolve()
    require(path.is_relative_to(root.resolve()) and path.is_file(), f"missing/outside asset: {name}")
    return path


def identity(path):
    hasher = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            hasher.update(chunk)
    return {"size": path.stat().st_size, "sha256": hasher.hexdigest()}


def selection_identity(root):
    names = read_json(data_file(root, "runtime-inputs.json"))
    require(isinstance(names, list) and all(isinstance(n, str) for n in names), "invalid runtime selection")
    require(len(names) == len(set(names)), "duplicate runtime selection")
    summary = hashlib.sha256()
    total = 0
    extensions = {}
    for name in sorted(names):
        require(not {"review", "authoring", "masters"} & set(name.split("/")), "non-runtime asset selected")
        info = identity(data_file(root, "assets/atlas-hd/" + name))
        suffix = PurePosixPath(name).suffix
        require(suffix in {".png", ".json"}, "unregistered runtime input type")
        extensions[suffix] = extensions.get(suffix, 0) + 1
        total += info["size"]
        summary.update(f"{info['sha256']}  {info['size']}  {name}\n".encode("utf-8"))
    return {"files": len(names), "bytes": total, "extensions": extensions,
            "inventorySha256": summary.hexdigest()}


def check_source(root, inventory):
    require(selection_identity(root) == inventory["selection"], "runtime selection/content differs from inspected baseline")
    for name, expected in inventory["inspectedFiles"].items():
        require(identity(data_file(root, name)) == expected, f"inspected contract changed: {name}")
    return f"Source data matches {inventory['sourceRevision']}; {inventory['selection']['files']} selected inputs."


def check_inventory(root=ROOT):
    inventory = read_json(root / "docs/content/assets.json")
    require(inventory["schemaVersion"] == 2, "unknown asset inventory schema")
    require(re.fullmatch(r"[0-9a-f]{40}", inventory["sourceRevision"]), "invalid source revision")
    require(set(inventory["packageFiles"]) == PACKAGE_FILES, "package inventory file set mismatch")
    require(set(inventory["ignoredRuntimeFiles"]) == LEGACY_FONT_FILES, "legacy bitmap fonts must be excluded")
    require(set(inventory["selectedRuntimeFiles"]) == PACKAGE_FILES - LEGACY_FONT_FILES, "runtime selection mismatch")
    selection = inventory["selection"]
    require(selection["files"] == sum(selection["extensions"].values()), "selection counts disagree")
    require(re.fullmatch(r"[0-9a-f]{64}", selection["inventorySha256"]), "invalid selection hash")
    release = inventory["runtimeRelease"]
    lock = read_json(root / "dependencies.json")
    require(lock["assets"] == {key: release[key] for key in ("version", "url", "sha256")}, "asset release pin mismatch")
    require(set(release["files"]) == PACKAGE_FILES, "release file set mismatch")
    for info in [*inventory["inspectedFiles"].values(), *release["files"].values()]:
        require(type(info["size"]) is int and info["size"] >= 0, "invalid inventory size")
        require(re.fullmatch(r"[0-9a-f]{64}", info["sha256"]), "invalid inventory hash")
    gaps = {g["id"] for g in read_json(root / "docs/conformance/coverage.json")["gaps"]}
    require(set(inventory["gaps"]) <= gaps, "unknown content gap")
    fonts = read_json(data_file(root, inventory["fontManifest"]))
    require(fonts["schemaVersion"] == 1 and fonts["version"] == lock["libraries"][fonts["dependency"]]["version"],
            "font dependency pin mismatch")
    expected_faces = {f"{family}.{style}" for family in ("sans", "serif", "mono")
                      for style in ("regular", "bold", "italic", "bold-italic")}
    require(len(fonts["faces"]) == len(expected_faces) and {f["id"] for f in fonts["faces"]} == expected_faces,
            "font face inventory mismatch")
    require(fonts["defaultFace"] == "sans.regular", "unknown default font")
    for info in [*fonts["faces"], fonts["license"]]:
        require(type(info["size"]) is int and 0 < info["size"] < 2147483647, "invalid font input size")
        require(re.fullmatch(r"[0-9a-f]{64}", info["sha256"]), "invalid font input hash")
    return "Asset and TrueType inventories agree with dependency pins; no external files required."


def check_fonts(root, manifest):
    for info in [*manifest["faces"], manifest["license"]]:
        expected = {key: info[key] for key in ("size", "sha256")}
        require(identity(data_file(root, info["file"])) == expected, f"font input differs from pin: {info['file']}")
    return f"DejaVu {manifest['version']}: {len(manifest['faces'])} TrueType faces and license match their pins."


def check_atlas(path):
    # A format check only: PNG decoding, pixel budgets and texture upload belong to M2.
    size = path.stat().st_size
    require(780 <= size <= 1024 * 1024 * 1024, "atlas size outside inspection budget")
    with path.open("rb") as stream:
        prefix = stream.read(12)
        require(prefix[:8] == b"C2ATLAS1", "unknown atlas magic")
        count, = struct.unpack_from("<I", prefix, 8)
        require(0 < count <= 100000, "invalid atlas entry count")
        start = 780 + 180 * count
        require(start <= size, "truncated atlas index")
        stream.seek(780)
        entries = [stream.read(180) for _ in range(count)]
        previous = b""
        for entry in entries:
            key, terminator, padding = entry[:160].partition(b"\0")
            require(terminator and key and not any(padding), "invalid atlas key padding")
            key.decode("utf-8", errors="strict")
            require(key > previous, "atlas keys must be unique and sorted")
            previous = key
            width, height, offset, length, proxy = struct.unpack_from("<5I", entry, 160)
            require(width > 0 and height > 0, "empty atlas dimensions")
            require(offset >= start and length >= 24 and offset + length <= size, "atlas PNG outside payload")
            require(proxy == 0 or (proxy >= start and proxy + width * height <= size), "atlas proxy outside payload")
            stream.seek(offset)
            png = stream.read(24)
            require(png[:8] == b"\x89PNG\r\n\x1a\n" and png[12:16] == b"IHDR", "invalid PNG signature/header")
            require(all(struct.unpack_from(">2I", png, 16)), "empty PNG dimensions")
    return count


def check_package(root):
    manifest = read_json(data_file(root, "package.json"))
    require(manifest["schema"] == 1 and manifest["kind"] == "openciv2-assets", "unknown package schema/kind")
    require(isinstance(manifest["version"], str) and manifest["version"], "missing package version")
    require(set(manifest["files"]) == PACKAGE_FILES, "runtime manifest file set mismatch")
    for name, expected in manifest["files"].items():
        require(type(expected["size"]) is int and expected["size"] >= 0, "invalid file size")
        require(re.fullmatch(r"[0-9a-f]{64}", expected["sha256"]) is not None, "invalid file hash")
        require(identity(data_file(root, name)) == expected, f"package file changed: {name}")
    count = check_atlas(data_file(root, "data/atlas-hd.pack"))
    rects = read_json(data_file(root, "data/cityview-rects.json"))
    require(isinstance(rects, list) and len(rects) == 62, "unexpected city rectangle count")
    require(all(isinstance(r, list) and len(r) == 4 and all(type(n) is int and n >= 0 for n in r) for r in rects),
            "invalid city rectangles")
    return (f"Package {manifest['version']}: hashes and {count} atlas entries checked. "
            "Legacy bitmap fonts are not consumed. Layout semantics and PNG decoding remain M2; no release authenticity claim.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path, nargs="?", help="asset source root or extracted runtime package; omit to check repository metadata")
    parser.add_argument("--fonts", type=Path, help="inspect an extracted pinned DejaVu TrueType archive")
    args = parser.parse_args()
    if args.fonts and args.directory:
        parser.error("select either an asset directory or --fonts")
    try:
        if args.fonts:
            print(check_fonts(args.fonts.resolve(), read_json(ROOT / "docs/content/fonts.json")))
            return 0
        if args.directory is None:
            print(check_inventory())
            return 0
        root = args.directory.resolve()
        if (root / "package.json").is_file():
            print(check_package(root))
        else:
            inventory = read_json(ROOT / "docs/content/assets.json")
            print(check_source(root, inventory))
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f"Asset inventory error: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
