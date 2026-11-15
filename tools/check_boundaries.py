#!/usr/bin/env python3
"""Check transitive imports and includes at the portable and player-facing boundaries."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
INCLUDE = re.compile(r'^\s*#\s*include\s*[<"]([^>"\n]+)[>"]', re.MULTILINE)
IMPORT = re.compile(r'^\s*(?:export\s+)?import\s+([\w.]+)\s*;', re.MULTILINE)
MODULE = re.compile(r'^\s*export\s+module\s+([\w.]+)\s*;', re.MULTILINE)
PORTABLE = ('base', 'state', 'world', 'unit', 'city', 'civilization', 'rules',
            'turn', 'ai', 'formats', 'query', 'network', 'session', 'viewmodel')
GAME = {'base', 'state', 'world', 'unit', 'city', 'civilization', 'rules', 'turn', 'ai'}
NATIVE_CONTENT = {f'formats/{stem}.{suffix}' for stem in ('rules', 'city_names', 'city_preferences')
                  for suffix in ('cppm', 'cpp')}
CLASSIC_LOADERS = {'formats/classic_text_reader.cppm', 'formats/classic_rules.cppm',
                   'formats/classic_city_names.cppm', 'formats/classic_city_preferences.cppm'}
PLAYER_STATE = {'state/identities.cppm', 'state/unit_types.cppm', 'state/choices.cppm'}
PLAYER_SESSION = {'session/client.cppm', 'session/command_types.cppm',
                  'session/classic_file_choice.cppm'}


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
        for name in INCLUDE.findall(text):
            target = next((p.resolve() for p in (path.parent / name, source / name)
                           if p.is_file()), None)
            yield name, target
        for name in IMPORT.findall(text):
            target = modules.get(name)
            if name.startswith('openciv.') and not target:
                errors.add(f'{origin}: unresolved module import {name}')
            yield name, target

    def visit(path, origin, player, portable, seen):
        if path in seen:
            return
        seen.add(path)
        if portable and re.search(r'\bstd\s*::\s*filesystem\b', path.read_text(encoding='utf-8')):
            errors.add(f'{origin}: forbidden platform API std::filesystem through {path.relative_to(source)}')
        for name, target in dependencies(path, origin):
            if target and target.is_relative_to(source):
                relative = target.relative_to(source).as_posix().replace('.hpp', '.cppm')
                forbidden = portable and relative.startswith(('platform/', 'ui/', 'application.'))
                if origin.split('/')[0] in GAME:
                    forbidden |= relative.startswith(('formats/', 'session/', 'network/', 'query/'))
                if origin in CLASSIC_LOADERS or origin in {p.replace('.cppm', '.cpp') for p in CLASSIC_LOADERS}:
                    forbidden |= relative.startswith(('formats/content_', 'formats/rules.')) or relative in {'formats/city_names.cppm', 'formats/city_preferences.cppm'}
                if origin in NATIVE_CONTENT:
                    forbidden |= relative in CLASSIC_LOADERS
                if player:
                    forbidden |= (relative.startswith('state/') and relative not in PLAYER_STATE)
                    forbidden |= (relative.startswith('session/') and relative not in PLAYER_SESSION)
                    forbidden |= relative.startswith(('world/', 'unit/', 'city/', 'formats/', 'network/'))
                    forbidden |= relative == 'query/live.cppm'
                if forbidden:
                    errors.add(f'{origin}: forbidden include/import through {path.relative_to(source)}: {target.relative_to(source)}')
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
