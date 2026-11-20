#!/usr/bin/env python3
"""Validate the owned stock-map binding contract; optionally inspect a pinned pack."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct

from check_conformance import read_json, require

ROOT = Path(__file__).resolve().parents[1]


def validate(root=ROOT, pack=None, decoded_report=None):
    catalog = read_json(root / "docs/content/map-asset-bindings.json")
    assets = read_json(root / "docs/content/assets.json")
    require(catalog["schemaVersion"] == 1 and catalog["profile"] == "classic-hd-map-2026.11.08.1", "unknown map binding profile")
    source = catalog["assetSource"]
    require(source["revision"] == assets["sourceRevision"] and
            source["packSha256"] == assets["runtimeRelease"]["files"]["data/atlas-hd.pack"]["sha256"], "map asset pin mismatch")
    for path, digest in source["dataFiles"].items():
        require(digest == assets["inspectedFiles"][path]["sha256"], "map source-data pin mismatch")
    images = catalog["images"]
    referenced = set()

    def image(key, size):
        require(key in images and images[key]["logicalSize"] == size, f"missing/wrong-size map binding: {key}")
        require(re.fullmatch(r"[0-9a-f]{64}", images[key]["pngSha256"]) is not None, "invalid PNG hash")
        physical = images[key]["physicalSize"]
        require(len(physical) == 2 and all(type(x) is int and x > 0 for x in physical), "invalid physical size")
        referenced.add(key)
        return key

    diagonal = ["NE", "SE", "SW", "NW"]
    directions = ["NE", "E", "SE", "S", "SW", "W", "NW", "N"]
    require(catalog["registration"] == {"tileSize": [64,32], "bodySize": [64,48], "bodyTileOffset": [0,-16],
            "diagonalOrder": diagonal, "routeOrder": directions,
            "edgeOffsets": dict(zip(diagonal, [[32,0],[32,16],[0,16],[0,0]]))}, "map registration mismatch")
    terrain_names = ["desert", "plains", "grassland", "forest", "hills", "mountains", "tundra", "glacier", "swamp", "jungle", "ocean"]
    require(len(catalog["terrain"]) == 11, "incomplete terrain coverage")
    for t, terrain in enumerate(catalog["terrain"]):
        require(terrain == {"id": terrain_names[t], "classicIndex": t, "base": image(f"terrain/{t}", [64,32]),
                "specialResources": [{"special": s, "atlasKey": image(f"resource/{2*t+s-1}", [64,32])} for s in (1,2)],
                "edgeBlends": {d: image(f"dither/{12 if t == 10 else t}/{q}", [32,16]) for q,d in enumerate(diagonal)}}, "terrain selector mismatch")
    require(catalog["grasslandShield"] == image("resource/shield", [64,32]), "grassland shield mismatch")
    require(len(catalog["coasts"]) == 4, "incomplete coastline coverage")
    for q, (name, contacts, offset) in enumerate(zip(["top","right","bottom","left"],
            [["NW","N","NE"],["NE","E","SE"],["SE","S","SW"],["SW","W","NW"]], [[16,0],[32,8],[16,16],[0,8]])):
        require(catalog["coasts"][q] == {"quadrant": name, "offset": offset, "variants": [
            {"connectionIndex": m, "landContacts": [d for i,d in enumerate(contacts) if m & (1 << i)],
             "atlasKey": image(f"coast/{q}/{m}", [32,16])} for m in range(8)]}, "coast selector mismatch")

    def connected(family):
        return [{"connectionIndex": m, "neighbors": [d for i,d in enumerate(diagonal) if m & (1 << i)],
                 "atlasKey": image(f"{family}/{m}", [64,32])} for m in range(16)]

    require(catalog["connectedTerrain"] == {name: connected("feature/" + name) for name in ("forest","hills","mountains")}, "relief selector mismatch")
    require(catalog["rivers"] == {"connections": connected("river"), "mouths": {d: image(f"mouth/{q}", [64,32]) for q,d in enumerate(diagonal)}}, "river selector mismatch")
    require(catalog["routes"] == {name: {"isolated": image(f"{prefix}/0", [64,32]), "directions": {d: image(f"{prefix}/{i+1}", [64,32]) for i,d in enumerate(directions)}}
            for name,prefix in (("road","road"),("railroad","rail"))}, "route selector mismatch")
    require(catalog["fog"] == {"unknown": image("fog/full", [64,32]), "edges": {d: image(f"fog/{q}", [32,16]) for q,d in enumerate(diagonal)}}, "fog binding mismatch")
    require(catalog["improvements"] == {name: image("improvement/" + name, [64,32 if name in ("irrigation","farmland","mine","pollution","village") else 48])
            for name in ("irrigation","farmland","mine","pollution","village","fortress","airbase","fortified")}, "improvement binding mismatch")
    require(catalog["units"] == {"variants": [{"ruleSlot": i, "atlasKey": image(f"unit/{i}", [64,48]), "shieldAnchor": [2,0] if 32 <= i <= 43 else [48,4]} for i in range(63)],
            "barbarianLeader": {"ownerSlot": 0, "ruleSlot": 46, "atlasKey": image("unit/62", [64,48])}, "shieldMask": image("shield", [12,20])}, "unit/anchor binding mismatch")
    require(len(catalog["cities"]["variants"]) == 48 and catalog["cities"]["flagAnchor"] == [48,4], "city coverage mismatch")
    for i, city in enumerate(catalog["cities"]["variants"]):
        anchor = city["populationAnchor"]
        require(len(anchor) == 2 and all(type(v) is int and 0 <= v < maximum for v,maximum in zip(anchor,[64,48])), "invalid city population anchor")
        require(city == {"style": i // 8, "sizeClass": i % 8 // 2, "walled": bool(i % 2),
                         "atlasKey": image(f"city/{i}", [64,48]), "populationAnchor": anchor}, "city selector mismatch")
    require(catalog["cities"]["disorderMask"] == image("crop/file/icons/166,223,30,30", [30,30]), "disorder binding mismatch")
    colors = ["#c73e3e", "#eee4bf", "#76b470", "#6997c9", "#e7c353", "#80c5c5", "#d49361", "#b98fc4"]
    require(catalog["factions"] == [{"colorRole": c, "rgb": colors[c], "brightFlag": image(f"tint/faction/flag/{208+3*c}", [14,22]),
            "darkFlag": image(f"tint/crop/file/cities/1,448,14,22/{208+3*c}", [14,22]),
            "occupiedAirbase": image(f"tint/crop/file/cities/338,423,64,48/{208+3*c}", [64,48])} for c in range(8)], "faction mapping mismatch")
    require(catalog["overlays"] == {"selection": image("crop/file/icons/199,256,64,32", [64,32]), "grid": image("grid", [64,32]), "cityRadius": image("grid/radius", [64,32])}, "overlay binding mismatch")
    require(catalog["effects"] == {"explosion": [image(f"crop/file/icons/{1+33*i},356,32,32", [32,32]) for i in range(8)],
            "nuclear": [image(f"crop/resource/tiles.dll/gifs/85/{1+92*(i%6)},{1+73*(i//6)},91,72", [91,72]) for i in range(11)]}, "effect sequence mismatch")
    require(set(images) == referenced, "unreferenced or incomplete image inventory")
    require(len(catalog["proceduralRoles"]) == len(set(catalog["proceduralRoles"])) and catalog["proceduralRoles"], "invalid procedural role list")
    require(all(re.fullmatch(r"#[0-9a-f]{6}", color) for color in catalog["displayColors"].values()), "invalid display color")
    if pack is not None:
        data = Path(pack).read_bytes()
        require(hashlib.sha256(data).hexdigest() == source["packSha256"], "pack does not match selected release")
        require(data[:8] == b"C2ATLAS1", "invalid atlas signature")
        entries = {}
        records = {}
        decoded = None
        if decoded_report is not None:
            from io import BytesIO
            from PIL import Image
            report = json.loads(Path(decoded_report).read_text())
            require(report.get('format') == 'openciv2-atlas-pixels/1', 'unknown decoder receipt format')
            decoded = report['sprites']
        for i in range(struct.unpack_from("<I", data, 8)[0]):
            pos = 780 + 180 * i
            key = data[pos:pos+160].split(b"\0")[0].decode("utf-8")
            require(key not in entries, "duplicate atlas key")
            w,h,png,length,proxy = struct.unpack_from("<5I", data, pos+160)
            records[key] = (w, h, proxy)
            entries[key] = {"logicalSize": [w,h], "physicalSize": list(struct.unpack_from(">II", data, png+16)),
                            "pngSha256": hashlib.sha256(data[png:png+length]).hexdigest()}
            if decoded is not None:
                with Image.open(BytesIO(data[png:png+length])) as raster:
                    require(raster.mode == 'RGBA' and hashlib.sha256(raster.tobytes()).hexdigest() == decoded.get(key),
                            f'independent straight-alpha pixel comparison differs: {key}')
        if decoded is not None:
            require(set(decoded) == set(entries), 'decoder receipt does not cover the exact atlas inventory')
        for key, description in images.items():
            require(entries.get(key) == description, f"published atlas binding differs: {key}")
        # P03's declared sheet-marker format independently checks the owned
        # anchor transcription. These proxies never supply rendered pixels.
        def marker(sheet, x, y, color):
            width, height, proxy = records[sheet]
            require(proxy > 0, "missing registration proxy")
            columns = [i for i in range(64) if data[proxy + (y-1)*width + x+i] == color]
            rows = [i for i in range(48) if data[proxy + (y+i)*width + x-1] == color]
            require(columns and rows, f"missing sheet registration marker: {sheet} ({x},{y}) color {color}")
            return [columns[-1], rows[-1]]
        for city in catalog["cities"]["variants"]:
            x = (334 if city["walled"] else 1) + 65 * city["sizeClass"]
            y = 39 + 49 * city["style"]
            require(marker("file/cities", x, y, 3) == city["populationAnchor"], "city anchor differs from published pack")
            # City flag placement comes from the pinned binding JSON. Its
            # legacy proxy marker can be overwritten by a population marker
            # at the same pixel, so it is not an authoritative anchor channel.
        for unit in catalog["units"]["variants"]:
            i = unit["ruleSlot"]
            require(marker("file/units", 1+65*(i%9), 1+49*(i//9), 4) == unit["shieldAnchor"], "unit anchor differs from pack")
        for faction in catalog["factions"]:
            pos = 12 + 3 * (208 + 3 * faction["colorRole"])
            require('#' + data[pos:pos+3].hex() == faction["rgb"], "faction RGB differs from pack")
    message = f"Validated {len(images)} exact map image bindings and all selector families" + (" against the pinned pack." if pack else "; pack bytes not supplied.")
    if decoded_report is not None:
        message += f" Compared all {len(entries)} decoded RGBA images with Pillow."
    return message


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pack", type=Path)
    parser.add_argument("--decoded-report", type=Path, help="Compare the production atlas pixel receipts with Pillow")
    args = parser.parse_args()
    if args.decoded_report is not None and args.pack is None:
        parser.error("--decoded-report requires --pack")
    try:
        print(validate(pack=args.pack, decoded_report=args.decoded_report))
    except (OSError, ValueError, KeyError, TypeError, struct.error) as error:
        parser.exit(1, f"Map asset contract: {error}\n")
