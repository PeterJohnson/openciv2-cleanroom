#!/usr/bin/env python3
"""Stage reviewed pinned data and optional authored map overrides in a published browser bundle."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[2]


def stage(bundle, assets, fonts, art=None, acceptance=False):
    target = bundle / 'content'
    asset_pins = json.loads((ROOT / 'docs/content/assets.json').read_text())['runtimeRelease']
    font_pins = json.loads((ROOT / 'docs/content/fonts.json').read_text())
    manifest = json.loads((assets / 'package.json').read_text())
    if manifest != dict(schema=1, kind='openciv2-assets', version=asset_pins['version'], files=asset_pins['files']):
        raise ValueError('Asset package manifest differs from the pinned release')
    inputs = [(assets / name, target / 'assets' / name, pin) for name, pin in asset_pins['files'].items()]
    inputs += [(fonts / pin['file'], target / 'fonts' / pin['file'], pin)
               for pin in [font_pins['license'], *font_pins['faces']]]
    # Validate every source before replacing any previously staged content.
    for source, _, pin in inputs:
        with source.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        if source.stat().st_size != pin['size'] or digest != pin['sha256']:
            raise ValueError(f'Pinned content mismatch: {source}')
    for source, destination, _ in inputs:
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
    shutil.copyfile(assets / 'package.json', target / 'assets/package.json')
    if art:
        staged_art = target / 'art'
        if staged_art.exists():
            shutil.rmtree(staged_art)
        shutil.copytree(art, staged_art)
        write_index(staged_art)
    if acceptance:
        fixtures = target / 'acceptance'
        for name in ['docs/algorithms/P02-map-rendering-and-minimap.md', 'docs/testing/map-rendering.md',
                     'docs/performance/map-rendering.md', 'docs/content/map-asset-bindings.json',
                     'tools/dotnet/build_map_art_cases.py']:
            destination = fixtures / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / name, destination)
        write_index(fixtures)
    print(f'Staged pinned graphics and {len(font_pins["faces"])} fonts in {target}')


def write_index(root):
    rows = [dict(path=p.relative_to(root).as_posix(), size=p.stat().st_size,
                 sha256=hashlib.sha256(p.read_bytes()).hexdigest())
            for p in sorted(root.rglob('*')) if p.is_file() and p != root / 'index.json']
    (root / 'index.json').write_text(json.dumps(rows, indent=2) + '\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bundle', type=Path, required=True)
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--fonts', type=Path, required=True)
    parser.add_argument('--art-cases', type=Path)
    parser.add_argument('--acceptance', action='store_true')
    args = parser.parse_args()
    stage(args.bundle, args.assets, args.fonts, args.art_cases, args.acceptance)
