#!/usr/bin/env python3
"""Check current specification/API/test ownership without Git history or external inputs."""
import argparse
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / 'docs/testing/coverage.json'


def registered_cases(root):
    suite = root / 'tests/OpenCiv2.FoundationCases/FoundationSuite.cs'
    table = re.search(r'\bCases\s*=\s*\[(.*?)\];', suite.read_text(), re.S)
    if table is None:
        raise ValueError('Missing shared case registration table')
    entries = re.findall(r'\("([^"\n]+)", (\w+)\)', table[1])
    methods = {method: name for name, method in entries}
    if not entries or len(methods) != len(entries) or len(set(methods.values())) != len(entries):
        raise ValueError('Missing or duplicate shared case name/method')
    return methods


def local_path(root, value):
    path = (root / value).resolve()
    if not path.is_relative_to(root.resolve()) or not path.is_file() or {'bin', 'obj'} & set(path.parts):
        raise ValueError('Missing or non-source path: ' + value)
    return path


def shared_groups(root, paths, registered):
    groups = set()
    for path in paths:
        if not path.startswith('tests/OpenCiv2.FoundationCases/'):
            continue
        methods = re.findall(r'\b(?:private|public) static void (\w+)\(', local_path(root, path).read_text())
        groups.update(registered[method] for method in methods if method in registered)
    return sorted(groups)


def validate(manifest, root=ROOT):
    if manifest['format'] != 'openciv2-implementation-coverage/1':
        raise ValueError('Unknown implementation coverage format')
    registered = registered_cases(root)
    ids, accounted = set(), set()
    for family in manifest['families']:
        key = family['id']
        if key in ids or family['status'] not in {'implemented', 'partial'}:
            raise ValueError('Duplicate family or invalid status: ' + key)
        ids.add(key)
        for field in ('contract', 'publicSurface', 'review'):
            if not isinstance(family[field], str) or not family[field].strip():
                raise ValueError('Missing ' + field + ': ' + key)
        for field in ('sources', 'tests'):
            paths = family[field]
            if not paths or paths != sorted(set(paths)):
                raise ValueError('Missing, unsorted or duplicate ' + field + ': ' + key)
            for path in paths:
                local_path(root, path)
        if family['sharedGroups'] != shared_groups(root, family['tests'], registered):
            raise ValueError('Shared group mapping changed: ' + key)
        accounted.update(family['sharedGroups'])
    if accounted != set(registered.values()):
        raise ValueError('Unaccounted shared groups: ' + ', '.join(sorted(set(registered.values()) - accounted)))
    return len(ids), len(accounted)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--list', action='store_true')
    args = parser.parse_args()
    manifest = json.loads(MANIFEST.read_text())
    families, groups = validate(manifest)
    if args.list:
        for family in manifest['families']:
            print(f"{family['id']}: {family['status']}, {len(family['sharedGroups'])} shared groups; {family['contract']}")
    print(f'Checked {families} current API/test families and all {groups} shared groups. No gameplay executed.')


if __name__ == '__main__':
    main()
