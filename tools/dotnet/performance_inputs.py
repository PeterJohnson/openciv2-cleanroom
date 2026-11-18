#!/usr/bin/env python3
"""Author fixed production workloads from repository F06/content contracts only."""
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import struct
import sys

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_fixture(name):
    path = ROOT / 'tests' / (name + '.py')
    spec = importlib.util.spec_from_file_location('performance_' + name, path)
    module = importlib.util.module_from_spec(spec)
    previous = sys.argv
    try:
        sys.argv = [str(path), 'unused-fixture-only-driver']
        spec.loader.exec_module(module)
    finally:
        sys.argv = previous
    return module


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')


def prepare(directory):
    directory.mkdir(parents=True, exist_ok=False)
    fixture = load_fixture('inspection_cli')
    small = fixture.save_bytes()
    width = height = 252
    area = width * height // 2
    coarse = (width + 3) // 4
    units, cities = 2048, 256
    # F06 §§2–4/8. Prefix and tail use the independent minimal authored fixture;
    # expanded records have specified coordinates, tokens and absent references.
    prefix = bytearray(small[:fixture.MAP])
    struct.pack_into('<hh', prefix, 12 + 0x2e, units, cities)
    header = struct.pack('<hhhHHhh', width, height, area, 0, 24, coarse, coarse)
    tiles = bytes([10, 0, 0, 0, 0, 0]) * area
    data = prefix + header + bytes(7 * area) + tiles + bytes(2 * coarse * coarse + 64 * 16)
    unit_offset = len(data)
    for slot in range(units):
        record = bytearray(32)
        y, column = divmod(slot, width // 2)
        struct.pack_into('<hh', record, 0, column * 2 + y % 2, y)
        record[7] = 1
        record[0x10] = 255
        struct.pack_into('<hhI', record, 0x16, -1, -1, slot + 1)
        data += record
    for slot in range(cities):
        record = bytearray(88)
        y, column = divmod(slot, width // 2)
        struct.pack_into('<hh', record, 0, column * 2 + y % 2, y)
        record[8] = record[10] = 1
        name = f'City {slot}'.encode()
        record[0x20:0x20 + len(name)] = name
        struct.pack_into('<I', record, 0x54, slot + 1)
        data += record
    data += small[fixture.CITY + 88:]
    (directory / 'large.sav').write_bytes(data)
    changed = bytearray(data)
    struct.pack_into('<I', changed, unit_offset + 0x1a, 0xffffffff)
    (directory / 'changed.sav').write_bytes(changed)
    (directory / 'large.mp').write_bytes(header + b'\xff' * 84 + tiles)
    (directory / 'RULES.TXT').write_bytes(load_fixture('rules_cli').authored_classic_rules())
    shutil.copytree(ROOT / 'docs/content/examples', directory / 'pack')
    parent = directory / 'pack/content.json'
    for level in range(2):
        source = json.loads(parent.read_text())
        target = directory / f'overlay-{level}'
        target.mkdir()
        child = {'format': 'openciv2-content-pack/1', 'id': f'performance.overlay.{level}',
                 'revision': 1, 'profile': 'classic-mge-5.4.0f', 'defaultLocale': 'en',
                 'files': {}, 'base': {'id': source['id'], 'revision': source['revision'], 'sha256': sha(parent)},
                 'provenance': {'authors': ['OpenCiv2 workload'], 'license': 'MIT', 'notes': []}}
        parent = target / 'content.json'
        write_json(parent, child)
    return {'saveBytes': len(data), 'mapBytes': 14 + 84 + len(tiles), 'unitOffset': unit_offset,
            'width': width, 'height': height, 'tiles': area, 'units': units, 'cities': cities,
            'files': inventory(directory)}


def inventory(directory):
    return {p.relative_to(directory).as_posix(): {'bytes': p.stat().st_size, 'sha256': sha(p)}
            for p in sorted(directory.rglob('*')) if p.is_file()}
