#!/usr/bin/env python3
"""Coverage admission checks, including the final C# collection entry."""
from copy import deepcopy
from pathlib import Path
import tempfile
import unittest

import check_coverage as coverage


class CoverageTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.suite = self.root / 'tests/OpenCiv2.FoundationCases/FoundationSuite.cs'
        self.suite.parent.mkdir(parents=True)
        self.suite.write_text('Cases = [("first", First), ("last", Last)];\n'
                              'private static void First() {}\nprivate static void Last() {}')
        self.source = self.root / 'src/OpenCiv2.Base/Arithmetic.cs'
        self.source.parent.mkdir(parents=True)
        self.source.write_text('authored inventory input')
        self.manifest = {'format': 'openciv2-implementation-coverage/1', 'families': [
            {'id': 'arithmetic', 'status': 'implemented', 'contract': 'F01', 'publicSurface': 'arithmetic',
             'review': 'Boundary cases', 'sources': [self.source.relative_to(self.root).as_posix()],
             'tests': [self.suite.relative_to(self.root).as_posix()], 'sharedGroups': ['first', 'last']}]}

    def test_final_entry_without_trailing_comma_is_required(self):
        self.assertEqual(coverage.validate(self.manifest, self.root), (1, 2))
        self.manifest['families'][0]['sharedGroups'].pop()
        with self.assertRaisesRegex(ValueError, 'mapping changed'):
            coverage.validate(self.manifest, self.root)

    def test_missing_source_and_duplicate_family_are_rejected(self):
        duplicate = deepcopy(self.manifest)
        duplicate['families'].append(deepcopy(duplicate['families'][0]))
        with self.assertRaisesRegex(ValueError, 'Duplicate family'):
            coverage.validate(duplicate, self.root)
        self.source.unlink()
        with self.assertRaisesRegex(ValueError, 'Missing or non-source'):
            coverage.validate(self.manifest, self.root)

    def test_new_registration_cannot_be_omitted(self):
        self.suite.write_text(self.suite.read_text().replace('("last", Last)', '("last", Last), ("new", New)') +
                              '\nprivate static void New() {}')
        with self.assertRaisesRegex(ValueError, 'mapping changed'):
            coverage.validate(self.manifest, self.root)


if __name__ == '__main__':
    unittest.main()
