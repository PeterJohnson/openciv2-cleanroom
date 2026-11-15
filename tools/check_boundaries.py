#!/usr/bin/env python3
"""Check transitive imports and includes at the portable and player-facing boundaries."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
INCLUDE = re.compile(r'^\s*#\s*include\s*[<"]([^>"\n]+)[>"]', re.MULTILINE)
IMPORT = re.compile(r'^\s*(?:export\s+)?import\s+([\w.:]+)\s*;', re.MULTILINE)
MODULE = re.compile(r'^\s*(?:export\s+)?module\s+([\w.:]+)\s*;', re.MULTILINE)
UNIT = re.compile(r'^\s*(export\s+)?module\s+([\w.:]+)\s*;', re.MULTILINE)
PORTABLE = ('io', 'base', 'state', 'world', 'unit', 'city', 'civilization', 'rules',
            'turn', 'ai', 'formats', 'query', 'network', 'session', 'viewmodel')
GAME = {'base', 'state', 'world', 'unit', 'city', 'civilization', 'rules', 'turn', 'ai'}
NATIVE_CONTENT = {f'formats/{stem}.{suffix}' for stem in ('rules', 'city_names', 'city_preferences')
                  for suffix in ('cppm', 'cpp')}
CLASSIC_LOADERS = {'formats/classic_text_reader.cppm', 'formats/classic_rules.cppm',
                   'formats/classic_city_names.cppm', 'formats/classic_city_preferences.cppm'}
PLAYER_STATE = {'state/identities.cppm', 'state/unit_types.cppm', 'state/choices.cppm'}
PLAYER_SESSION = {'session/client.cppm', 'session/client_types.cppm', 'session/command_types.cppm',
                  'session/classic_file_choice.cppm', 'session/player_setup_types.cppm',
                  'session/world_setup_types.cppm', 'session/configuration_types.cppm'}
COMMON_SETUP = {f'session/{stem}.{suffix}'
                for stem in ('world_entry', 'premade_world', 'world_setup')
                for suffix in ('cppm', 'cpp')}


def check(root=ROOT):
    source = (root / 'src').resolve()
    errors = set()
    modules = {}
    for path in source.rglob('*.cppm'):
        match = MODULE.search(path.read_text(encoding='utf-8'))
        if match:
            name = match[1]
            if name in modules:
                errors.add(f'{name}: duplicate module interface')
            modules[name] = path.resolve()

    def dependencies(path, origin):
        text = path.read_text(encoding='utf-8')
        unit = UNIT.search(text)
        if unit and not unit[1]:
            owner = unit[2].split(':')[0]
            target = modules.get(owner)
            if target is None:
                errors.add(f'{origin}: unresolved module owner {owner}')
            elif target.parent != path.parent:
                errors.add(f'{origin}: module implementation belongs outside its owner directory: {owner}')
            # Only non-partition implementation units implicitly import their primary.
            if ':' not in unit[2]:
                yield owner, target
        for name in INCLUDE.findall(text):
            target = next((p.resolve() for p in (path.parent / name, source / name)
                           if p.is_file()), None)
            yield name, target
        for name in IMPORT.findall(text):
            if name.startswith(':'):
                owner = MODULE.search(text)
                if not owner:
                    errors.add(f'{origin}: partition import outside a module unit: {name}')
                    continue
                name = owner[1].split(':')[0] + name
            target = modules.get(name)
            if name.startswith('openciv.') and not target:
                errors.add(f'{origin}: unresolved module import {name}')
            yield name, target

    visited = set()
    active = []

    def visit_module(name):
        if name in active:
            errors.add('module import cycle: ' + ' -> '.join(active[active.index(name):] + [name]))
            return
        if name in visited:
            return
        visited.add(name)
        active.append(name)
        for imported, target in dependencies(modules[name], name):
            if imported in modules:
                visit_module(imported)
        active.pop()

    for name in modules:
        visit_module(name)

    def visit(path, origin, player, portable, seen):
        if path in seen:
            return
        seen.add(path)
        if portable and re.search(r'\bstd\s*::\s*filesystem\b', path.read_text(encoding='utf-8')):
            errors.add(f'{origin}: forbidden platform API std::filesystem through {path.relative_to(source).as_posix()}')
        for name, target in dependencies(path, origin):
            if target and target.is_relative_to(source):
                relative = target.relative_to(source).as_posix().replace('.hpp', '.cppm')
                forbidden = portable and relative.startswith(('platform/', 'ui/', 'application.'))
                if origin.startswith('io/'):
                    forbidden |= not (relative.startswith(('io/', 'base/')) or relative == 'formats/bytes.cppm')
                if origin in COMMON_SETUP:
                    forbidden |= relative.startswith('formats/')
                if origin.split('/')[0] in GAME:
                    forbidden |= relative.startswith(('formats/', 'session/', 'network/', 'query/'))
                if origin in CLASSIC_LOADERS or origin in {p.replace('.cppm', '.cpp') for p in CLASSIC_LOADERS}:
                    forbidden |= relative.startswith(('formats/content_', 'formats/rules.')) or relative in {'formats/city_names.cppm', 'formats/city_preferences.cppm'}
                if origin in NATIVE_CONTENT:
                    forbidden |= relative in CLASSIC_LOADERS
                if player:
                    forbidden |= (relative.startswith('state/') and relative not in PLAYER_STATE)
                    client_bridge = (relative == 'session/client_connection.cppm' and
                                     path.relative_to(source).as_posix() == 'session/client.cppm')
                    forbidden |= (relative.startswith('session/') and relative not in PLAYER_SESSION
                                  and not client_bridge)
                    forbidden |= relative.startswith(('world/', 'unit/', 'city/', 'formats/', 'network/'))
                    forbidden |= relative == 'query/live.cppm'
                if forbidden:
                    errors.add(f'{origin}: forbidden include/import through {path.relative_to(source).as_posix()}: {target.relative_to(source).as_posix()}')
                else:
                    visit(target, origin, player, portable, seen)
            elif portable and (name.startswith(('SDL', 'imgui', 'backends/imgui')) or
                               name in ('windows.h', 'unistd.h', 'filesystem', 'sqlite3.h')):
                errors.add(f'{origin}: forbidden platform include {name}')

    for directory in PORTABLE:
        for path in (source / directory).rglob('*'):
            if path.suffix not in ('.hpp', '.cpp', '.cppm'):
                continue
            relative = path.relative_to(source).as_posix().replace('.hpp', '.cppm')
            player = directory == 'viewmodel' or relative in PLAYER_SESSION or relative == 'query/live_records.cppm'
            visit(path, relative, player, True, set())
    for path in (source / 'ui').rglob('*'):
        if path.suffix in ('.hpp', '.cpp', '.cppm'):
            visit(path, path.relative_to(source).as_posix(), True, False, set())
    if errors:
        raise ValueError('\n'.join(sorted(errors)))
    return 'Portable and player-facing module/include boundaries passed.'


if __name__ == '__main__':
    print(check())
