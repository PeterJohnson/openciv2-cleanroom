#!/usr/bin/env python3
"""Audit paired browser performance inputs and exact optimization endpoint pixels."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw


def compare(baseline, candidate, output):
    reports = [json.loads((p / 'performance.json').read_text()) for p in (baseline, candidate)]
    previous = {(row['run'], row['detail'], row['mode']): row for row in reports[0]['results']}
    pairs = []
    for current in reports[1]['results']:
        key = (current['run'], current['detail'], current['mode'])
        if key not in previous:
            raise ValueError(f'{key}: missing baseline workload')
        former = previous[key]
        before = former.get('warmupSamples', []) + former['samples']
        after = current.get('warmupSamples', []) + current['samples']
        if len(before) != len(after):
            raise ValueError(f'{key}: different workload lengths')
        for index, (a, b) in enumerate(zip(before, after)):
            for field in ('x', 'y', 'zoom', 'width', 'height', 'scaling', 'tiles', 'objects', 'labels'):
                if a['draw'][field] != b['draw'][field]:
                    raise ValueError(f'{key}/{index}: different {field}')
            if (a.get('update') or {}).get('revision') != (b.get('update') or {}).get('revision'):
                raise ValueError(f'{key}/{index}: different observed revision')
        pairs.append(dict(run=key[0], detail=key[1], mode=key[2], frames=len(after),
                          baseline=former.get('summary'), candidate=current.get('summary')))
    captures = {row['name']: row for row in reports[0]['captures']}
    pixels = []
    for row in reports[1]['captures']:
        name = row['name']
        if name not in captures:
            raise ValueError(f'{name}: missing baseline capture')
        old, state = captures[name]['state'], row['state']
        for field in ('x', 'y', 'zoom', 'revision', 'viewport', 'scaling', 'worldWidth', 'worldHeight', 'reducedMotion', 'flat'):
            if old[field] != state[field]:
                raise ValueError(f'{name}: different {field}')
        v, o, scale = state['viewport'], state['overview'], state['scaling']
        crop = tuple(round(value * scale) for value in (v['x'], v['y'], v['x'] + v['width'], v['y'] + v['height']))
        files = [p / (name + '.png') for p in (baseline, candidate)]
        with Image.open(files[0]) as a, Image.open(files[1]) as b:
            diff = ImageChops.difference(a.convert('RGB').crop(crop), b.convert('RGB').crop(crop))
            excluded = [(o['x'] - v['x'] - 3) * scale, (o['y'] - v['y'] - 3) * scale,
                        (o['x'] + o['width'] - v['x'] + 3) * scale, (o['y'] + o['height'] - v['y'] + 3) * scale]
            ImageDraw.Draw(diff).rectangle(excluded, fill=(0, 0, 0))
            pixels.append(dict(name=name, exact=diff.getbbox() is None, bounds=diff.getbbox(),
                               maxChannelDifference=max(high for low, high in diff.getextrema()),
                               hashes=[hashlib.sha256(p.read_bytes()).hexdigest() for p in files]))
    result = dict(format='openciv2-browser-map-optimization-comparison/1', baseline=str(baseline), candidate=str(candidate),
                  completed=all(r['completed'] for r in reports), pairedFrames=sum(row['frames'] for row in pairs),
                  exactEndpoints=bool(pixels) and all(row['exact'] for row in pixels), pairs=pairs, pixels=pixels,
                  scope='Exact optimization endpoints and paired sampled inputs; not every timed frame or complete acceptance')
    output.write_text(json.dumps(result, indent=2) + '\n')
    print(f"{result['pairedFrames']} paired frames; {sum(row['exact'] for row in pixels)}/{len(pixels)} exact endpoint pairs")
    return result['completed'] and result['pairedFrames'] > 0 and result['exactEndpoints']


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('baseline', type=Path)
    parser.add_argument('candidate', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    raise SystemExit(0 if compare(args.baseline, args.candidate, args.output) else 1)
