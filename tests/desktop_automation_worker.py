#!/usr/bin/env python3
"""Exercise the desktop Lua worker's resource and framing contract without a display."""
import json
import os
from pathlib import Path
import struct
import subprocess
import sys


def frame(value):
    body = json.dumps(value, ensure_ascii=False).encode('utf-8')
    return struct.pack('<i', len(body)) + body


def start(script):
    return {'protocol': 1, 'script': script, 'instructions': 100000,
            'ticks': 200, 'commands': 64, 'outputBytes': 4096}


def run(assembly, payload, limited=True):
    environment = {key: value for key, value in os.environ.items()
                   if not key.upper().startswith(('DOTNET_GCHEAPHARDLIMIT', 'COMPLUS_GCHEAPHARDLIMIT'))}
    if limited:
        environment['DOTNET_GCHeapHardLimit'] = '8000000'
    completed = subprocess.run(['dotnet', str(assembly), '--lua-worker'], input=payload,
                               capture_output=True, env=environment, timeout=20)
    remaining = completed.stdout
    messages = []
    while remaining:
        assert len(remaining) >= 4, 'Truncated header'
        length, = struct.unpack('<i', remaining[:4])
        assert 0 < length <= 512 * 1024 and len(remaining) >= length + 4, 'Invalid response frame'
        messages.append(json.loads(remaining[4:length + 4]))
        remaining = remaining[length + 4:]
    return completed.returncode, messages


def main():
    assembly = Path(sys.argv[1]).resolve()
    source = """
local units = game.units()
assert(units[1].token == '18446744073709551615' and units[1].x == -2147483648)
assert(units[1].name == 'Évora')
game.log('é')
"""
    code, messages = run(assembly, frame(start(source)) + frame({'reply': [
        {'token': '18446744073709551615', 'x': -2147483648, 'name': 'Évora'}]}) + frame({'reply': None}))
    assert code == 0 and [m['state'] for m in messages] == ['Request', 'Request', 'Completed'], messages
    assert messages[0]['operation'] == 'units' and messages[1]['argument'] == 'é', messages

    for script, reason in [("while true do end", 'instruction_budget'),
                           ("local x='allocation'; while true do x=x..x end", 'memory_budget')]:
        code, messages = run(assembly, frame(start(script)))
        assert code in (0, 42) and len(messages) == 1, (code, messages)
        assert messages[0]['state'] == 'Failed' and messages[0]['error'] == reason, messages

    # The private endpoint must reject missing configuration and broken frames
    # before running any source. A missing reply differs from a valid JSON null.
    invalid = [b'\x01', struct.pack('<i', -1), struct.pack('<i', 512 * 1024 + 1),
               frame({'protocol': 2}), frame({'protocol': 1}),
               frame(start('game.wait()')) + frame({})]
    for payload in invalid:
        code, messages = run(assembly, payload)
        assert code == 2 and all(m['state'] == 'Request' for m in messages), (code, messages)
    code, messages = run(assembly, frame(start("game.log('must not run')")), limited=False)
    assert code == 2 and messages[0]['error'] == 'script_worker_heap_limit_unavailable', (code, messages)
    print('Desktop Lua worker: replies, instruction/heap limits, framing and heap admission passed.')


if __name__ == '__main__':
    main()
