import hashlib
import io
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import zipfile

from get_sdk_linux import acquire


def make_nupkg(entry_name, sdk_bytes):
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, 'w', zipfile.ZIP_DEFLATED) as archive:
        archive.writestr(entry_name, sdk_bytes)
    return buffer.getvalue()


class SdkAcquisitionTests(unittest.TestCase):
    def make_lock(self, dh4_installer_hash=None, dh4_sdk=b'pinned dh4 sdk',
                  dh5_pkg_bytes=None, dh5_pkg_hash=None, dh5_sdk=b'pinned dh5 sdk'):
        dh5_pkg_bytes = dh5_pkg_bytes if dh5_pkg_bytes is not None else make_nupkg('lib/net10.0/VoK.Sdk.dll', dh5_sdk)
        return {
            'dh4': {
                'url': 'https://example.invalid/DungeonHelper.msi',
                'installerSha256': dh4_installer_hash or hashlib.sha256(b'installer').hexdigest(),
                'sdkSha256': hashlib.sha256(dh4_sdk).hexdigest(),
                'sdkVersion': '4.2.1.0',
            },
            'dh5': {
                'url': 'https://example.invalid/vok.sdk.5.3.0.nupkg',
                'packageSha256': dh5_pkg_hash or hashlib.sha256(dh5_pkg_bytes).hexdigest(),
                'entry': 'lib/net10.0/VoK.Sdk.dll',
                'sdkSha256': hashlib.sha256(dh5_sdk).hexdigest(),
                'sdkVersion': '5.3.0.0',
            },
        }, dh5_pkg_bytes

    def run_dh4(self, installer_hash=None, sdk_bytes=b'pinned dh4 sdk', extraction_error=None):
        lock, _ = self.make_lock(dh4_installer_hash=installer_hash)

        def extract(command, **kwargs):
            if extraction_error:
                raise extraction_error
            path = Path(command[2]) / 'Program Files' / 'Dungeon Helper'
            path.mkdir(parents=True)
            (path / 'VoK.Sdk.dll').write_bytes(sdk_bytes)

        with tempfile.TemporaryDirectory() as temporary:
            destination = Path(temporary) / 'sdk'
            environment = Path(temporary) / 'github-env'
            with patch('get_sdk_linux.json.loads', return_value=lock), \
                 patch('get_sdk_linux.urllib.request.urlopen', return_value=io.BytesIO(b'installer')), \
                 patch('get_sdk_linux.subprocess.run', side_effect=extract) as extraction, \
                 patch.dict(os.environ, {'GITHUB_ENV': str(environment)}):
                try:
                    acquire(destination, target='dh4')
                except RuntimeError:
                    self.assertFalse(environment.exists())
                    self.assertFalse((destination / 'VoK.Sdk.dll').exists())
                    if installer_hash:
                        extraction.assert_not_called()
                    raise
                self.assertEqual((destination / 'VoK.Sdk.dll').read_bytes(), b'pinned dh4 sdk')
                self.assertEqual(environment.read_text(), f'DH_SDK_DIR={destination.resolve()}\n')

    def test_verified_dh4_sdk_and_environment(self):
        self.run_dh4()

    def test_changed_dh4_installer_rejected_before_extraction(self):
        with self.assertRaisesRegex(RuntimeError, 'Official DH 4 installer changed'):
            self.run_dh4(installer_hash='0' * 64)

    def test_wrong_dh4_sdk_rejected(self):
        with self.assertRaisesRegex(RuntimeError, 'Pinned DH 4 SDK DLL'):
            self.run_dh4(sdk_bytes=b'wrong sdk')

    def test_dh4_extraction_failure_propagates(self):
        with self.assertRaises(subprocess.CalledProcessError):
            self.run_dh4(extraction_error=subprocess.CalledProcessError(1, 'msiextract'))

    def test_verified_dh5_sdk_and_dual_target(self):
        lock, pkg_bytes = self.make_lock()

        def fake_open(url, timeout=120):
            return io.BytesIO(pkg_bytes if 'nupkg' in url else b'installer')

        def extract(command, **kwargs):
            path = Path(command[2]) / 'Program Files' / 'Dungeon Helper'
            path.mkdir(parents=True)
            (path / 'VoK.Sdk.dll').write_bytes(b'pinned dh4 sdk')

        with tempfile.TemporaryDirectory() as temporary:
            destination = Path(temporary) / 'sdk'
            environment = Path(temporary) / 'github-env'
            with patch('get_sdk_linux.json.loads', return_value=lock), \
                 patch('get_sdk_linux.urllib.request.urlopen', side_effect=fake_open), \
                 patch('get_sdk_linux.subprocess.run', side_effect=extract), \
                 patch.dict(os.environ, {'GITHUB_ENV': str(environment)}):
                acquire(destination, target='all')
                self.assertEqual((destination / 'dh4' / 'VoK.Sdk.dll').read_bytes(), b'pinned dh4 sdk')
                self.assertEqual((destination / 'dh5' / 'VoK.Sdk.dll').read_bytes(), b'pinned dh5 sdk')
                env_lines = environment.read_text().splitlines()
                self.assertIn(f'DH_SDK_DIR={(destination / "dh4").resolve()}', env_lines)
                self.assertIn(f'DH5_SDK_DIR={(destination / "dh5").resolve()}', env_lines)

    def test_changed_dh5_package_rejected(self):
        lock, pkg_bytes = self.make_lock(dh5_pkg_hash='0' * 64)
        with tempfile.TemporaryDirectory() as temporary:
            destination = Path(temporary) / 'sdk'
            with patch('get_sdk_linux.json.loads', return_value=lock), \
                 patch('get_sdk_linux.urllib.request.urlopen', return_value=io.BytesIO(pkg_bytes)):
                with self.assertRaisesRegex(RuntimeError, 'Official DH 5 VoK.Sdk package changed'):
                    acquire(destination, target='dh5')
                self.assertFalse((destination / 'VoK.Sdk.dll').exists())

    def test_wrong_dh5_sdk_hash_rejected(self):
        bad_pkg = make_nupkg('lib/net10.0/VoK.Sdk.dll', b'wrong dh5 sdk')
        lock, _ = self.make_lock(dh5_pkg_bytes=bad_pkg, dh5_sdk=b'pinned dh5 sdk')
        with tempfile.TemporaryDirectory() as temporary:
            destination = Path(temporary) / 'sdk'
            with patch('get_sdk_linux.json.loads', return_value=lock), \
                 patch('get_sdk_linux.urllib.request.urlopen', return_value=io.BytesIO(bad_pkg)):
                with self.assertRaisesRegex(RuntimeError, 'Pinned DH 5 SDK DLL hash mismatch'):
                    acquire(destination, target='dh5')
                self.assertFalse((destination / 'VoK.Sdk.dll').exists())
