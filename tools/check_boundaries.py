#!/usr/bin/env python3
"""Check transitive includes at the portable and player-facing boundaries."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
INCLUDE = re.compile(r'^\s*#\s*include\s*[<"]([^>"\n]+)[>"]', re.MULTILINE)
PORTABLE = ('base', 'state', 'world', 'unit', 'city', 'civilization', 'rules',
            'turn', 'ai', 'formats', 'query', 'network', 'session', 'viewmodel')
GAME = {'base', 'state', 'world', 'unit', 'city', 'civilization', 'rules', 'turn', 'ai'}
NATIVE_CONTENT = {f'formats/{stem}.{suffix}' for stem in ('rules', 'city_names', 'city_preferences')
                  for suffix in ('hpp', 'cpp')}
CLASSIC_LOADERS = {'formats/classic_text_reader.hpp', 'formats/classic_rules.hpp',
                   'formats/classic_city_names.hpp', 'formats/classic_city_preferences.hpp'}
PLAYER_STATE = {'state/identities.hpp', 'state/unit_types.hpp', 'state/choices.hpp'}
PLAYER_SESSION = {'session/client.hpp', 'session/command_types.hpp',
                  'session/classic_file_choice.hpp'}


def check(root=ROOT):
    source = (root / 'src').resolve()
    errors = set()

    def visit(path, origin, player, portable, seen):
        if path in seen:
            return
        seen.add(path)
        for name in INCLUDE.findall(path.read_text(encoding='utf-8')):
            target = next((p.resolve() for p in (path.parent / name, source / name)
                           if p.is_file()), None)
            if target and target.is_relative_to(source):
                relative = target.relative_to(source).as_posix()
                forbidden = portable and relative.startswith(('platform/', 'ui/', 'application.'))
                if origin.split('/')[0] in GAME:
                    forbidden |= relative.startswith(('formats/', 'session/', 'network/', 'query/'))
                if origin in CLASSIC_LOADERS or origin in {p.replace('.hpp', '.cpp') for p in CLASSIC_LOADERS}:
                    forbidden |= relative.startswith(('formats/content_', 'formats/rules.')) or relative in {'formats/city_names.hpp', 'formats/city_preferences.hpp'}
                if origin in NATIVE_CONTENT:
                    forbidden |= relative in CLASSIC_LOADERS
                if player:
                    forbidden |= (relative.startswith('state/') and relative not in PLAYER_STATE)
                    forbidden |= (relative.startswith('session/') and relative not in PLAYER_SESSION)
                    forbidden |= relative.startswith(('world/', 'unit/', 'city/', 'formats/', 'network/'))
                    forbidden |= relative == 'query/live.hpp'
                if forbidden:
                    errors.add(f'{origin}: forbidden include through {path.relative_to(source)}: {relative}')
                else:
                    visit(target, origin, player, portable, seen)
            elif portable and (name.startswith(('SDL', 'imgui', 'backends/imgui')) or
                               name in ('windows.h', 'unistd.h', 'filesystem', 'sqlite3.h')):
                errors.add(f'{origin}: forbidden platform include {name}')

    for directory in PORTABLE:
        for path in (source / directory).rglob('*'):
            if path.suffix not in ('.hpp', '.cpp'):
                continue
            relative = path.relative_to(source).as_posix()
            player = directory == 'viewmodel' or relative in PLAYER_SESSION or relative == 'query/live_records.hpp'
            visit(path, relative, player, True, set())
    for path in (source / 'ui').rglob('*'):
        if path.suffix in ('.hpp', '.cpp'):
            visit(path, path.relative_to(source).as_posix(), True, False, set())
    if errors:
        raise ValueError('\n'.join(sorted(errors)))
    return 'Portable and player-facing include boundaries passed.'


if __name__ == '__main__':
    print(check())
