#!/usr/bin/env python3
"""Combine six completed theme/density browser batches without relabeling their identities."""
import argparse
import hashlib
import json
from pathlib import Path

from run_application import validate


def merge(root):
    combined = None
    expected_cases = {f'MV{i:02}' for i in range(1, 31)} | {'Mixed'}
    for density in (1, 2):
        for theme in ('stock', 'native', 'classic'):
            batch = f'{theme}-{density}x'
            path = root / batch / 'browser.json'
            report = json.loads(path.read_text())
            if report.get('completed') is not True or report['errors']:
                raise ValueError(f'{batch}: incomplete or failed browser run')
            validate(report['identity'])
            expected_names = {f'{batch}-{case}' for case in expected_cases}
            actual_names = {item['name'] for item in report['captures']}
            if not expected_names <= actual_names or len(actual_names) != len(report['captures']):
                raise ValueError(f'{batch}: missing or duplicate gallery captures')
            if any(item['density'] != density or item['theme'] != theme for item in report['captures']):
                raise ValueError(f'{batch}: unexpected theme/density')
            driver = root / batch / 'driver.cjs'
            if hashlib.sha256(driver.read_bytes()).hexdigest() != report['driverSha256']:
                raise ValueError(f'{batch}: driver identity mismatch')
            if combined is None:
                combined = {key: report[key] for key in
                            ('format', 'browser', 'backend', 'renderer', 'rendererChoice', 'gpuFeatures', 'identity', 'driverSha256')}
                combined.update(completed=True, batches=[], checks=[], captures=[], requests=[], errors=[])
            elif any(report[key] != combined[key] for key in
                     ('format', 'browser', 'backend', 'renderer', 'rendererChoice', 'gpuFeatures', 'identity', 'driverSha256')):
                raise ValueError(f'{batch}: build/browser/driver changed between batches')
            combined['batches'].append(dict(path=f'{batch}/browser.json',
                                           sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
            combined['checks'].extend(report['checks'])
            combined['requests'].extend(report['requests'])
            for item in report['captures']:
                for suffix in ('.png', '-fresh.png', '.json'):
                    if not (root / batch / (item['name'] + suffix)).is_file():
                        raise ValueError(f'{batch}: missing capture artifact')
                combined['captures'].append(dict(item, name=f'{batch}/{item["name"]}'))
    combined['mergerSha256'] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    (root / 'browser.json').write_text(json.dumps(combined, indent=2) + '\n')
    print(f'Merged six completed batches: {len(combined["captures"])} captures, {len(combined["checks"])} checks.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    merge(parser.parse_args().output)
