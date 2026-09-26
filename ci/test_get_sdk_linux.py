import hashlib
import io
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from get_sdk_linux import acquire


class SdkAcquisitionTests(unittest.TestCase):
    def run_acquisition(self, installer_hash=None, sdk_bytes=b'pinned sdk', extraction_error=None):
        lock = {
            'url': 'https://example.invalid/DungeonHelper.msi',
            'installerSha256': installer_hash or hashlib.sha256(b'installer').hexdigest(),
            'sdkSha256': hashlib.sha256(b'pinned sdk').hexdigest(),
            'sdkVersion': '4.2.1.0',
        }

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
                    acquire(destination)
                except RuntimeError:
                    self.assertFalse(environment.exists())
                    self.assertFalse((destination / 'VoK.Sdk.dll').exists())
                    if installer_hash:
                        extraction.assert_not_called()
                    raise
                self.assertEqual((destination / 'VoK.Sdk.dll').read_bytes(), b'pinned sdk')
                self.assertEqual(environment.read_text(), f'DH_SDK_DIR={destination.resolve()}\n')

    def test_verified_sdk_and_environment(self):
        self.run_acquisition()

    def test_changed_installer_rejected_before_extraction(self):
        with self.assertRaisesRegex(RuntimeError, 'Official installer changed'):
            self.run_acquisition(installer_hash='0' * 64)

    def test_wrong_sdk_rejected(self):
        with self.assertRaisesRegex(RuntimeError, 'Pinned SDK DLL'):
            self.run_acquisition(sdk_bytes=b'wrong sdk')

    def test_extraction_failure_propagates(self):
        with self.assertRaises(subprocess.CalledProcessError):
            self.run_acquisition(extraction_error=subprocess.CalledProcessError(1, 'msiextract'))
