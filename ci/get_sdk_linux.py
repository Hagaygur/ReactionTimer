"""Extract the hash-pinned build SDK on Linux; requires msitools (msiextract)."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import urllib.request


def sha256(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def acquire(destination):
    lock = json.loads(Path(__file__).with_name('sdk-lock.json').read_text())
    destination = destination.resolve()
    destination.mkdir(parents=True, exist_ok=True)
    # A fresh extraction cannot accidentally select a stale cached SDK.
    with tempfile.TemporaryDirectory(prefix='reaction-timer-sdk-') as temporary:
        root = Path(temporary)
        installer = root / 'DungeonHelper.msi'
        with urllib.request.urlopen(lock['url'], timeout=120) as response:
            with installer.open('wb') as output:
                shutil.copyfileobj(response, output)
        if sha256(installer) != lock['installerSha256'].lower():
            raise RuntimeError('Official installer changed; review ci/sdk-lock.json before building.')
        extracted = root / 'extracted'
        extracted.mkdir()
        subprocess.run(['msiextract', '-C', str(extracted), str(installer)],
                       check=True, stdout=subprocess.DEVNULL)
        matches = [path for path in extracted.rglob('VoK.Sdk.dll')
                   if sha256(path) == lock['sdkSha256'].lower()]
        if not matches:
            raise RuntimeError('Pinned SDK DLL was not found in the installer.')
        # The exact binary hash also pins the assembly version checked on Windows.
        shutil.copyfile(matches[0], destination / 'VoK.Sdk.dll')
    if os.environ.get('GITHUB_ENV'):
        with open(os.environ['GITHUB_ENV'], 'a', encoding='utf-8') as environment:
            environment.write(f'DH_SDK_DIR={destination}\n')
    print(f'Verified SDK {lock["sdkVersion"]}: {destination}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--destination', type=Path, default=Path('.build/dh-sdk'))
    acquire(parser.parse_args().destination)
