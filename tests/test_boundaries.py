"""Architecture boundary checks must follow helper headers as well as direct imports."""
from pathlib import Path
import sys
import tempfile
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
import check_boundaries


class BoundaryTests(unittest.TestCase):
    def test_repository(self):
        check_boundaries.check()

    def test_transitive_player_access_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name, text in {
                'viewmodel/map.cpp': '#include "query/helper.hpp"\n',
                'query/helper.hpp': '#include "state/game.hpp"\n',
                'state/game.hpp': '// engine-owned state\n',
            }.items():
                path = root / 'src' / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(text)
            with self.assertRaisesRegex(ValueError, 'forbidden include.*state/game.hpp'):
                check_boundaries.check(root)

    def test_portable_platform_dependency_is_rejected(self):
        for header in ('SDL3/SDL.h', 'filesystem', 'sqlite3.h'):
            with self.subTest(header=header), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                path = root / 'src/base/example.cpp'
                path.parent.mkdir(parents=True)
                path.write_text(f'#include <{header}>\n')
                with self.assertRaisesRegex(ValueError, 'forbidden platform include'):
                    check_boundaries.check(root)
