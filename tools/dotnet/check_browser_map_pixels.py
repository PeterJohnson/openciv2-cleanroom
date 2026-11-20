#!/usr/bin/env python3
"""Compare WebGL-presented production map pixels with independently fresh WASM renders."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw
from run_map_showcase_native import inspect_footprint


def check(root):
    report = json.loads((root / 'browser.json').read_text())
    if report.get('completed') is False:
        raise ValueError('Browser run is incomplete; retain its checkpoint as diagnostic evidence')
    if not report['captures']:
        raise ValueError('Missing browser captures')
    rows = []
    for case in report['captures']:
        name, state = case['name'], case['state']
        viewport, overview, density = state['viewport'], state['overview'], state['scaling']
        presented_path, fresh_path = root / (name + '.png'), root / (name + '-fresh.png')
        footprint = inspect_footprint(state, presented_path)
        with Image.open(presented_path) as screenshot, Image.open(fresh_path) as reference:
            left, top = round(viewport['x'] * density), round(viewport['y'] * density)
            width, height = round(viewport['width'] * density), round(viewport['height'] * density)
            actual = screenshot.convert('RGB').crop((left, top, left + width, top + height))
            expected = reference.convert('RGB')
            if actual.size != expected.size:
                raise ValueError(f'{name}: displayed and reference dimensions differ')
            diff = ImageChops.difference(actual, expected)
            # The inset overview is a separate production control drawn above the map.
            # Its footprint/ownership/input have their own matrix and browser checks.
            excluded = [(overview['x'] - viewport['x'] - 3) * density,
                        (overview['y'] - viewport['y'] - 3) * density,
                        (overview['x'] + overview['width'] - viewport['x'] + 3) * density,
                        (overview['y'] + overview['height'] - viewport['y'] + 3) * density]
            ImageDraw.Draw(diff).rectangle(excluded, fill=(0, 0, 0))
            if diff.getbbox():
                diff.save(root / (name + '-difference.png'))
                raise ValueError(f'{name}: WebGL map differs from fresh WASM pixels in {diff.getbbox()}')
            if state['colorMode'] == 'Greyscale':
                red, green, blue = actual.split()
                chroma = ImageChops.lighter(ImageChops.difference(red, green), ImageChops.difference(red, blue))
                ImageDraw.Draw(chroma).rectangle(excluded, fill=0)
                if chroma.getbbox():
                    raise ValueError(f'{name}: greyscale presentation retains colored map pixels')
            rows.append(dict(name=name, id=case['id'], theme=case['theme'], density=density,
                             width=width, height=height, differingPixels=0, excludedOverview=excluded,
                             overviewFootprint=footprint,
                             presentedSha256=hashlib.sha256(presented_path.read_bytes()).hexdigest(),
                             freshSha256=hashlib.sha256(fresh_path.read_bytes()).hexdigest()))
    (root / 'pixel-comparisons.json').write_text(json.dumps(dict(tolerance=0, cases=rows), indent=2) + '\n')
    print(f'{len(rows)} WebGL map captures exactly match fresh WASM renders.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    check(parser.parse_args().output)
