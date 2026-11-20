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
            'targets': {'net11.0': {'Example/1.0': {'type': 'package', 'runtime': {}}}},
            'packageFolders': {str(self.root / 'cache'): {}}}))
        self.lock = self.root / 'lock.json'
        self.lock.write_text(json.dumps({'dependencies': {'net11.0': {'Example': {'resolved': '1.0', 'contentHash': self.content_hash}}}}))
        self.sdk = self.root / 'sdk'
        self.sdk.mkdir()
        for name in ['LICENSE.txt', 'ThirdPartyNotices.txt']:
            (self.sdk / name).write_text('SDK notice fixture')
        self.output = self.root / 'publish'

    def generate(self):
        notices.generate(self.project, self.assets, self.lock, self.output,
                         self.sdk, '11.0.100', '11.0.0', '', False)
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
        locked['dependencies']['net11.0']['Example']['contentHash'] = 'wrong'
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
        for name in ('LuaCSharp', 'LuaCSharp.Annotations', 'LuaCSharp.SourceGenerator'):
            meta = {'repository': {'commit': 'c735770a21407226cfd1204bbe11a25de27cddf9'}}
            self.assertEqual(notices.supplements(name, '0.5.8', meta), ['LuaCSharp'])
            with self.assertRaisesRegex(ValueError, 'changed dependency'):
                notices.supplements(name, '0.5.9', meta)
            with self.assertRaisesRegex(ValueError, 'changed dependency'):
                notices.supplements(name, '0.5.8', {})
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

    def test_native_aot_uses_pinned_aot_runtime_not_coreclr(self):
        package = 'microsoft.netcore.app.runtime.nativeaot.linux-x64'
        archive = self.root / 'cache' / package / '11.0.0' / (package + '.11.0.0.nupkg')
        archive.parent.mkdir(parents=True)
        with zipfile.ZipFile(archive, 'w') as bundle:
            bundle.writestr('LICENSE.TXT', 'Native AOT license fixture')
            bundle.writestr('THIRD-PARTY-NOTICES.TXT', 'Native AOT notices fixture')
        (self.root / 'global.json').write_bytes((notices.ROOT / 'global.json').read_bytes())
        (self.root / 'LICENSE').write_text('Project license fixture')
        (self.root / 'packaging').mkdir()
        pins = self.root / 'packaging/aot-runtime-packs.json'
        pins.write_text(json.dumps({'version': '11.0.0', 'sha512': {'linux-x64': notices.sha512(archive.read_bytes())}}))
        with patch.object(notices, 'ROOT', self.root):
            notices.generate(self.project, self.assets, self.lock, self.output,
                             self.sdk, '11.0.100', '11.0.0', 'linux-x64', False, True, True)
            report = notices.verify(self.output)
            self.assertEqual(report['runtime']['kind'], 'native-aot')
            self.assertTrue(report['runtime']['redistributed'])
            self.assertEqual((self.output / 'DEPENDENCY-NOTICES/dotnet/LICENSE.TXT').read_text(), 'Native AOT license fixture')
            archive.write_bytes(archive.read_bytes() + b'tampered')
            with self.assertRaisesRegex(ValueError, 'Runtime archive differs from pin'):
                notices.generate(self.project, self.assets, self.lock, self.output,
                                 self.sdk, '11.0.100', '11.0.0', 'linux-x64', False, True, True)

    def test_native_aot_rejects_browser_and_framework_dependent_metadata(self):
        for rid, browser, self_contained in [('', False, True), ('linux-x64', True, True), ('linux-x64', False, False)]:
            with self.subTest(rid=rid, browser=browser, self_contained=self_contained):
                with self.assertRaisesRegex(ValueError, 'requires a self-contained desktop RID'):
                    notices.generate(self.project, self.assets, self.lock, self.output,
                                     self.sdk, '11.0.100', '11.0.0', rid, browser, self_contained, True)

    def test_compiler_packages_receive_the_matching_sdk_license(self):
        for name in ('Microsoft.DotNet.ILCompiler', 'runtime.linux-x64.Microsoft.DotNet.ILCompiler'):
            with self.subTest(package=name):
                archive = self.root / 'cache' / name.lower() / '11.0.0' / (name.lower() + '.11.0.0.nupkg')
                archive.parent.mkdir(parents=True)
                with zipfile.ZipFile(archive, 'w') as bundle:
                    bundle.writestr(name + '.nuspec', '<package><metadata><license type="expression">MIT</license></metadata></package>')
                    bundle.writestr('THIRD-PARTY-NOTICES.TXT', 'Compiler notices fixture')
                digest = notices.sha512(archive.read_bytes())
                self.assets.write_text(json.dumps({
                    'libraries': {name + '/11.0.0': {'type': 'package', 'sha512': digest, 'path': name.lower() + '/11.0.0'}},
                    'targets': {}, 'packageFolders': {str(self.root / 'cache'): {}}}))
                self.lock.write_text(json.dumps({'dependencies': {'net11.0': {name: {'resolved': '11.0.0', 'contentHash': digest}}}}))
                report = self.generate()
                self.assertIn('dotnet/SDK-LICENSE.txt', report['packages'][0]['notices'])
                self.assertIn(name + '/THIRD-PARTY-NOTICES.TXT', report['packages'][0]['notices'])


if __name__ == '__main__':
    unittest.main()
