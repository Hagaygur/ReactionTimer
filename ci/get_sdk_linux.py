"""Extract hash-pinned DH 4 and DH 5 build SDKs on Linux; DH 4 requires msitools (msiextract)."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import urllib.request
import zipfile


def sha256(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_bytes(data):
    return hashlib.sha256(data).hexdigest()


def _export_env(name, value):
    if os.environ.get('GITHUB_ENV'):
        with open(os.environ['GITHUB_ENV'], 'a', encoding='utf-8') as environment:
            environment.write(f'{name}={value}\n')


def _acquire_dh4(spec, destination):
    destination = destination.resolve()
    destination.mkdir(parents=True, exist_ok=True)
    # A fresh extraction cannot accidentally select a stale cached SDK.
    with tempfile.TemporaryDirectory(prefix='reaction-timer-dh4-') as temporary:
        root = Path(temporary)
        installer = root / 'DungeonHelper.msi'
        with urllib.request.urlopen(spec['url'], timeout=120) as response:
            with installer.open('wb') as output:
                shutil.copyfileobj(response, output)
        if sha256(installer) != spec['installerSha256'].lower():
            raise RuntimeError('Official DH 4 installer changed; review ci/sdk-lock.json before building.')
        extracted = root / 'extracted'
        extracted.mkdir()
        subprocess.run(['msiextract', '-C', str(extracted), str(installer)],
                       check=True, stdout=subprocess.DEVNULL)
        matches = [path for path in extracted.rglob('VoK.Sdk.dll')
                   if sha256(path) == spec['sdkSha256'].lower()]
        if not matches:
            raise RuntimeError('Pinned DH 4 SDK DLL was not found in the installer.')
        shutil.copyfile(matches[0], destination / 'VoK.Sdk.dll')
    _export_env('DH_SDK_DIR', destination)
    print(f'Verified DH 4 SDK {spec["sdkVersion"]}: {destination}')


def _acquire_dh5(spec, destination):
    destination = destination.resolve()
    destination.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='reaction-timer-dh5-') as temporary:
        root = Path(temporary)
        package = root / 'vok.sdk.nupkg'
        with urllib.request.urlopen(spec['url'], timeout=120) as response:
            with package.open('wb') as output:
                shutil.copyfileobj(response, output)
        if sha256(package) != spec['packageSha256'].lower():
            raise RuntimeError('Official DH 5 VoK.Sdk package changed; review ci/sdk-lock.json before building.')
        with zipfile.ZipFile(package) as archive:
            entry_name = spec['entry']
            if entry_name not in archive.namelist():
                raise RuntimeError(f'Pinned DH 5 SDK entry missing from package: {entry_name}')
            sdk_bytes = archive.read(entry_name)
        if sha256_bytes(sdk_bytes) != spec['sdkSha256'].lower():
            raise RuntimeError('Pinned DH 5 SDK DLL hash mismatch.')
        (destination / 'VoK.Sdk.dll').write_bytes(sdk_bytes)
    _export_env('DH5_SDK_DIR', destination)
    print(f'Verified DH 5 SDK {spec["sdkVersion"]}: {destination}')


def acquire(destination, target='all'):
    lock = json.loads(Path(__file__).with_name('sdk-lock.json').read_text(encoding='utf-8'))
    if target == 'all':
        _acquire_dh4(lock['dh4'], destination / 'dh4')
        _acquire_dh5(lock['dh5'], destination / 'dh5')
    elif target == 'dh4':
        _acquire_dh4(lock['dh4'], destination)
    elif target == 'dh5':
        _acquire_dh5(lock['dh5'], destination)
    else:
        raise ValueError(f'Unknown target: {target}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--destination', type=Path, default=Path('.build/dh-sdk'))
    parser.add_argument('--target', choices=('all', 'dh4', 'dh5'), default='all')
    args = parser.parse_args()
    acquire(args.destination, args.target)
