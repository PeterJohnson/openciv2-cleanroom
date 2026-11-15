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

    def test_domain_cannot_depend_on_format_codecs(self):
        self.check_forbidden_chain('rules/install.hpp', 'formats/rules.hpp')

    def test_noncanonical_root_preserves_boundary_diagnostics(self):
        self.check_forbidden_chain('rules/install.hpp', 'formats/rules.hpp', aliased_root=True)

    def test_native_content_cannot_depend_on_classic_loaders(self):
        self.check_forbidden_chain('formats/city_names.hpp', 'formats/classic_text_reader.hpp')

    def test_classic_parser_cannot_depend_on_native_content(self):
        self.check_forbidden_chain('formats/classic_rules.hpp', 'formats/content_json.hpp')

    def check_forbidden_chain(self, origin, forbidden, *, aliased_root=False):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            if aliased_root:
                (root / 'alias').mkdir()
                root = root / 'alias' / '..'
            for name, text in {
                origin: '#include "base/helper.hpp"\n',
                'base/helper.hpp': f'#include "{forbidden}"\n',
                forbidden: '// implementation dependency\n',
            }.items():
                path = root / 'src' / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(text)
            with self.assertRaisesRegex(ValueError, 'forbidden include.*' + forbidden):
                check_boundaries.check(root)
