"""Validate DH 4 and DH 5 build directories and runtime-only DH 4 installer."""
import argparse
import json
from pathlib import Path
import zipfile


def validate_dh4(directory, configuration, version):
    archive_name = f'ReactionTimer-{version}-dh4-plugin.zip'
    runtime = ['VoK.ReactionTimer.' + extension for extension in ('dll', 'deps.json', 'metadata')]
    expected = {archive_name}
    if configuration == 'Debug':
        expected.update(runtime)
        expected.add('VoK.ReactionTimer.pdb')
    actual = {path.name for path in directory.iterdir()}
    if actual != expected:
        raise RuntimeError(f'{configuration} DH 4 output mismatch: expected {sorted(expected)}, got {sorted(actual)}')
    with zipfile.ZipFile(directory / archive_name) as archive:
        entries = ['plugins/ReactionTimer/' + name for name in runtime]
        if archive.namelist() != entries or archive.testzip() is not None:
            raise RuntimeError('DH 4 installer must contain exactly three valid runtime entries, DLL first')
        metadata = json.loads(archive.read(entries[2]).decode('utf-8-sig'))
        if metadata['Version'] != version + '.0':
            raise RuntimeError('DH 4 installer metadata version disagrees with package version')
        if configuration == 'Debug':
            for name, entry in zip(runtime, entries):
                if (directory / name).read_bytes() != archive.read(entry):
                    raise RuntimeError(f'Debug DH 4 installer differs from loose file: {name}')
    print(f'PASS {configuration} DH 4 output and installer contents: {directory}')


def validate_dh5(directory, configuration):
    expected = {'VoK.ReactionTimer.dll', 'VoK.ReactionTimer.deps.json', 'VoK.ReactionTimer.pdb'}
    actual = {path.name for path in directory.iterdir()}
    if not expected.issubset(actual):
        raise RuntimeError(f'{configuration} DH 5 output missing files: expected {sorted(expected)}, got {sorted(actual)}')
    if 'VoK.Sdk.dll' in actual:
        raise RuntimeError(f'{configuration} DH 5 output leaked host VoK.Sdk.dll')
    print(f'PASS {configuration} DH 5 output directory: {directory}')


def validate(directory, configuration, version, target='dh4'):
    if target == 'dh4':
        validate_dh4(directory, configuration, version)
    else:
        validate_dh5(directory, configuration)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('configuration', choices=('Debug', 'Release'))
    parser.add_argument('--version', default='1.0.0')
    parser.add_argument('--target', choices=('dh4', 'dh5'), default='dh4')
    options = parser.parse_args()
    validate(options.directory, options.configuration, options.version, options.target)
