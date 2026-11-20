#!/usr/bin/env python3
"""Exact fresh-frame endpoint checks for diagnostic browser performance reports."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw


def check(root):
    report = json.loads((root / 'performance.json').read_text())
    rows = []
    expected = {(run, 'continuous' if mode == 'zoom' else detail, mode)
                for run in range(1, report['runs'] + 1) for detail in report['details']
                for mode in report['modes'] if mode != 'zoom' or detail == report['details'][0]}
    actual = [(row['run'], row['detail'], row['mode']) for row in report['results']]
    if report['completed'] and (set(actual) != expected or len(actual) != len(expected)):
        raise ValueError('Missing or duplicate workload series')
    if report['completed'] and len(report['captures']) != len(expected):
        raise ValueError('Missing endpoint captures')
    for workload in report['results']:
        for sample in workload.get('warmupSamples', []) + workload['samples']:
            draw = sample['draw']
            if draw['error'] or draw['rendererFallback'] or draw['composition'] != report['renderer']:
                raise ValueError('Invalid renderer sample')
            for key in ('width', 'height'):
                if abs(draw[key] * report['density'] - report['physicalViewport'][key]) > .1:
                    raise ValueError('Measured physical viewport changed')
        if report['completed'] and len(workload['samples']) != report['count']:
            raise ValueError('Missing measured samples')
        if report['completed'] and len(workload.get('warmupSamples', [])) != report['warmup']:
            raise ValueError('Missing warmup samples')
    for capture in report['captures']:
        name, state = capture['name'], capture['state']
        viewport, overview, density = state['viewport'], state['overview'], state['scaling']
        actual_path, fresh_path = root / (name + '.png'), root / (name + '-fresh.png')
        with Image.open(actual_path) as screenshot, Image.open(fresh_path) as fresh:
            x, y = round(viewport['x'] * density), round(viewport['y'] * density)
            width, height = round(viewport['width'] * density), round(viewport['height'] * density)
            actual = screenshot.convert('RGB').crop((x, y, x + width, y + height))
            expected = fresh.convert('RGB')
            if actual.size != expected.size:
                raise ValueError(f'{name}: different dimensions')
            diff = ImageChops.difference(actual, expected)
            excluded = [(overview['x'] - viewport['x'] - 3) * density,
                        (overview['y'] - viewport['y'] - 3) * density,
                        (overview['x'] + overview['width'] - viewport['x'] + 3) * density,
                        (overview['y'] + overview['height'] - viewport['y'] + 3) * density]
            ImageDraw.Draw(diff).rectangle(excluded, fill=(0, 0, 0))
            bounds = diff.getbbox()
            differing = width * height - ImageChops.lighter(ImageChops.lighter(*diff.split()[:2]), diff.split()[2]).histogram()[0]
            if bounds:
                diff.save(root / (name + '-difference.png'))
            recovery = None
            recovered_path = root / (name + '-recovered.png')
            if capture.get('recoveredState') is not None:
                with Image.open(recovered_path) as recovered:
                    after = recovered.convert('RGB').crop((x, y, x + width, y + height))
                    recovery_diff = ImageChops.difference(actual, after)
                    ImageDraw.Draw(recovery_diff).rectangle(excluded, fill=(0, 0, 0))
                    recovery = dict(exact=recovery_diff.getbbox() is None, bounds=recovery_diff.getbbox(),
                                    maxChannelDifference=max(high for low, high in recovery_diff.getextrema()),
                                    sha256=hashlib.sha256(recovered_path.read_bytes()).hexdigest())
                    if not recovery['exact']:
                        recovery_diff.save(root / (name + '-recovery-difference.png'))
            cross = None
            cpu_path = root / (name + '-cpu.png')
            if cpu_path.exists():
                with Image.open(cpu_path) as cpu:
                    cross_diff = ImageChops.difference(actual, cpu.convert('RGB'))
                    ImageDraw.Draw(cross_diff).rectangle(excluded, fill=(0, 0, 0))
                    mask = Image.new('L', actual.size, 255)
                    ImageDraw.Draw(mask).rectangle(excluded, fill=0)
                    pixels = mask.histogram()[255]
                    channels = cross_diff.split()
                    histograms = [channel.histogram() for channel in channels]
                    mean = sum(sum(value * count for value, count in enumerate(histogram)) for histogram in histograms) / (pixels * 3)
                    maxima = ImageChops.lighter(ImageChops.lighter(channels[0], channels[1]), channels[2]).histogram()
                    over8 = sum(maxima[9:])
                    cross = dict(meanChannelDifference=mean, pixelsOver8=over8, comparedPixels=pixels,
                                 withinDeclaredRasterBounds=mean <= 2 and over8 <= pixels * .10,
                                 sha256=hashlib.sha256(cpu_path.read_bytes()).hexdigest())
            rows.append(dict(name=name, exact=bounds is None, differingPixels=differing, bounds=bounds,
                             maxChannelDifference=max(high for low, high in diff.getextrema()),
                             hostRecovery=recovery, cpuReference=cross,
                             presentedSha256=hashlib.sha256(actual_path.read_bytes()).hexdigest(),
                             freshSha256=hashlib.sha256(fresh_path.read_bytes()).hexdigest()))
    result = dict(tolerance=0, completed=report['completed'], scope='Sampled endpoints only, not every timed frame',
                  passed=report['completed'] and bool(rows) and all(row['exact'] for row in rows),
                  hostRecoveryPassed=(report['completed'] and all(row['hostRecovery'] and row['hostRecovery']['exact'] for row in rows)) if rows and any(row['hostRecovery'] for row in rows) else None,
                  cases=rows)
    (root / 'pixel-comparisons.json').write_text(json.dumps(result, indent=2) + '\n')
    print(f"{root}: {sum(row['exact'] for row in rows)}/{len(rows)} exact endpoint comparisons")
    return result['passed'] and (not report.get('recoveryCheck') or result['hostRecoveryPassed'] is True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    raise SystemExit(0 if check(parser.parse_args().output) else 1)
