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

    def test_module_interface_cannot_hide_platform_dependencies(self):
        self.check_forbidden_chain('base/example.cppm', 'platform/example.hpp')

    def test_native_content_cannot_depend_on_classic_loaders(self):
        self.check_forbidden_chain('formats/city_names.hpp', 'formats/classic_text_reader.hpp')

    def test_classic_parser_cannot_depend_on_native_content(self):
        self.check_forbidden_chain('formats/classic_rules.hpp', 'formats/content_json.hpp')

    def test_module_imports_preserve_transitive_boundaries(self):
        for origin, forbidden in (
            ('viewmodel/map', 'state/game'),
            ('rules/install', 'formats/rules'),
            ('formats/city_names', 'formats/classic_text_reader'),
            ('formats/classic_rules', 'formats/content_json'),
        ):
            with self.subTest(origin=origin), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                for name, imports in ((origin, ['base/helper']), ('base/helper', [forbidden]),
                                      (forbidden, [])):
                    path = root / 'src' / (name + '.cppm')
                    path.parent.mkdir(parents=True, exist_ok=True)
                    text = 'export module openciv.' + name.replace('/', '.') + ';\n'
                    text += ''.join('export import openciv.' + i.replace('/', '.') + ';\n'
                                    for i in imports)
                    path.write_text(text)
                with self.assertRaisesRegex(ValueError, 'forbidden include/import'):
                    check_boundaries.check(root)

    def test_import_std_does_not_allow_portable_filesystem_access(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = root / 'src/base/example.cpp'
            path.parent.mkdir(parents=True)
            path.write_text('import std;\nstd::filesystem::path file;\n')
            with self.assertRaisesRegex(ValueError, 'forbidden platform API'):
                check_boundaries.check(root)

    def test_unresolved_owned_import_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = root / 'src/base/example.cpp'
            path.parent.mkdir(parents=True)
            path.write_text('import openciv.missing;\n')
            with self.assertRaisesRegex(ValueError, 'unresolved module import'):
                check_boundaries.check(root)

    def test_module_cycles_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.write_sources(root, {
                'base/a.cppm': 'export module openciv.base.a;\nimport openciv.base.b;\n',
                'base/b.cppm': 'export module openciv.base.b;\nimport openciv.base.a;\n',
            })
            with self.assertRaisesRegex(ValueError, 'module import cycle:'):
                check_boundaries.check(root)

    def test_internal_partition_can_import_its_primary_interface(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.write_sources(root, {
                'session/runtime.cppm': 'export module openciv.session.runtime;\n',
                'session/runtime_state.cppm': ('module openciv.session.runtime:state;\n'
                                               'import openciv.session.runtime;\n'),
                'session/runtime.cpp': 'module openciv.session.runtime;\nimport :state;\n',
            })
            check_boundaries.check(root)
            (root / 'src/session/runtime.cppm').write_text(
                'export module openciv.session.runtime;\nimport :state;\n')
            with self.assertRaisesRegex(ValueError, 'module import cycle:'):
                check_boundaries.check(root)

    def test_partition_dependencies_preserve_boundaries(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.write_sources(root, {
                'base/a.cppm': 'export module openciv.base.a;\n',
                'base/a.cpp': 'module openciv.base.a;\nimport :impl;\n',
                'base/impl.cppm': 'module openciv.base.a:impl;\n#include <filesystem>\n',
            })
            with self.assertRaisesRegex(ValueError, 'forbidden platform include'):
                check_boundaries.check(root)

    def test_client_connection_is_only_accessible_through_client_handle(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.write_sources(root, {
                'session/client.cppm': ('export module openciv.session.client;\n'
                                       'import openciv.session.client_connection;\n'),
                'session/client_connection.cppm':
                    'export module openciv.session.client_connection;\n',
                'viewmodel/a.cppm': ('export module openciv.viewmodel.a;\n'
                                    'import openciv.session.client;\n'),
            })
            check_boundaries.check(root)
            (root / 'src/viewmodel/a.cppm').write_text(
                'export module openciv.viewmodel.a;\nimport openciv.session.client_connection;\n')
            with self.assertRaisesRegex(ValueError, 'forbidden include/import'):
                check_boundaries.check(root)
            (root / 'src/viewmodel/a.cppm').unlink()
            (root / 'src/session/client_connection.cppm').write_text(
                'export module openciv.session.client_connection;\nimport openciv.state.game;\n')
            self.write_sources(root, {'state/game.cppm': 'export module openciv.state.game;\n'})
            with self.assertRaisesRegex(ValueError, 'forbidden include/import'):
                check_boundaries.check(root)

    @staticmethod
    def write_sources(root, sources):
        for name, text in sources.items():
            path = root / 'src' / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text)

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
