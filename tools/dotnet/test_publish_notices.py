#!/usr/bin/env python3
"""Reject incomplete or mislabeled distribution notices without network access."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

import publish_notices as notices


class PublicationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.archive = self.root / 'cache/example/1.0/example.1.0.nupkg'
        self.archive.parent.mkdir(parents=True)
        with zipfile.ZipFile(self.archive, 'w') as bundle:
            bundle.writestr('example.nuspec', '<package><metadata><id>Example</id><version>1.0</version><license type="expression">MIT</license></metadata></package>')
            bundle.writestr('LICENSE.txt', 'Example license fixture')
        self.content_hash = notices.sha512(self.archive.read_bytes())
        self.project = self.root / 'Example.csproj'
        self.project.write_text('<Project />')
        self.assets = self.root / 'assets.json'
        self.assets.write_text(json.dumps({
            'libraries': {'Example/1.0': {'type': 'package', 'sha512': self.content_hash, 'path': 'example/1.0'}},
            'targets': {'net10.0': {'Example/1.0': {'type': 'package', 'runtime': {}}}},
            'packageFolders': {str(self.root / 'cache'): {}}}))
        self.lock = self.root / 'lock.json'
        self.lock.write_text(json.dumps({'dependencies': {'net10.0': {'Example': {'resolved': '1.0', 'contentHash': self.content_hash}}}}))
        self.sdk = self.root / 'sdk'
        self.sdk.mkdir()
        for name in ['LICENSE.txt', 'ThirdPartyNotices.txt']:
            (self.sdk / name).write_text('SDK notice fixture')
        self.output = self.root / 'publish'

    def generate(self):
        notices.generate(self.project, self.assets, self.lock, self.output,
                         self.sdk, '10.0.404', '10.0.15', '', False)
        return notices.verify(self.output)

    def test_republish_and_tampered_notice_rejection(self):
        first = self.generate()
        self.assertEqual(first, self.generate())
        self.assertEqual(first['packages'][0]['contentHash'], self.content_hash)
        self.assertFalse(first['runtime']['redistributed'])
        notice = self.output / 'DEPENDENCY-NOTICES/Example/LICENSE.txt'
        notice.write_text('changed')
        with self.assertRaisesRegex(ValueError, 'missing or changed'):
            notices.verify(self.output)
        self.generate()
        notice.unlink()
        with self.assertRaisesRegex(ValueError, 'missing or changed'):
            notices.verify(self.output)

    def test_restored_graph_cannot_override_lock(self):
        locked = json.loads(self.lock.read_text())
        locked['dependencies']['net10.0']['Example']['contentHash'] = 'wrong'
        self.lock.write_text(json.dumps(locked))
        with self.assertRaisesRegex(ValueError, 'does not match lock'):
            self.generate()

    def test_license_expression_alone_is_insufficient(self):
        with zipfile.ZipFile(self.archive, 'w') as bundle:
            bundle.writestr('example.nuspec', '<package><metadata><license type="expression">MIT</license></metadata></package>')
        digest = notices.sha512(self.archive.read_bytes())
        self.assets.write_text(self.assets.read_text().replace(self.content_hash, digest))
        self.lock.write_text(self.lock.read_text().replace(self.content_hash, digest))
        with self.assertRaisesRegex(ValueError, 'Missing full license'):
            self.generate()

    def test_changed_vendored_attribution_rejected(self):
        with self.assertRaisesRegex(ValueError, 'changed dependency'):
            notices.supplements('Avalonia.Fonts.Inter', '12.1.7', {})
        source = copy.deepcopy(json.loads((notices.NOTICES / 'sources.json').read_text()))
        source['RandN']['sha256'] = 'wrong'
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            (folder / 'sources.json').write_text(json.dumps(source))
            (folder / source['RandN']['file']).write_text('incorrect license')
            with patch.object(notices, 'NOTICES', folder), patch.object(notices, 'supplements', return_value=['RandN']):
                with self.assertRaisesRegex(ValueError, 'Vendored notice changed'):
                    self.generate()

    def test_signed_verification_must_succeed_and_match_lock(self):
        for status, digest in [(1, 'expected'), (0, 'wrong')]:
            with self.subTest(status=status, digest=digest):
                result = notices.subprocess.CompletedProcess([], status, 'Content hash: ' + digest + '\n', '')
                with patch.object(notices.subprocess, 'run', return_value=result):
                    with self.assertRaisesRegex(ValueError, 'failed signature verification'):
                        notices.verify_archive(self.archive, 'expected', self.sdk)
        result = notices.subprocess.CompletedProcess([], 0, 'Content hash: expected\n', '')
        with patch.object(notices.subprocess, 'run', return_value=result):
            self.assertEqual(notices.verify_archive(self.archive, 'expected', self.sdk), self.content_hash)

    def test_reviewed_signed_archive_is_verified_without_certificate_process(self):
        pins = self.root / 'archive-pins.json'
        pins.write_text(json.dumps({'format': 'openciv2-package-archives/1', 'packages': {
            self.archive.name: {'contentHash': 'signed-content', 'archiveSha512': self.content_hash}}}))
        with patch.object(notices, 'ARCHIVE_PINS', pins), patch.object(notices.subprocess, 'run') as verify:
            self.assertEqual(notices.verify_archive(self.archive, 'signed-content', self.sdk), self.content_hash)
            verify.assert_not_called()
            with self.assertRaisesRegex(ValueError, 'differs from reviewed'):
                notices.verify_archive(self.archive, 'changed-lock', self.sdk)
            self.archive.write_bytes(self.archive.read_bytes() + b'tampered')
            with self.assertRaisesRegex(ValueError, 'differs from reviewed'):
                notices.verify_archive(self.archive, 'signed-content', self.sdk)
            verify.assert_not_called()

    def test_silent_verifier_failure_retains_exit_status(self):
        result = notices.subprocess.CompletedProcess([], -9, '', '')
        with patch.object(notices.subprocess, 'run', return_value=result):
            with self.assertRaisesRegex(ValueError, 'verifier exit -9'):
                notices.verify_archive(self.archive, 'expected', self.sdk)


if __name__ == '__main__':
    unittest.main()
