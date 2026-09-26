"""Validate the plugin's final build directory and runtime-only installer."""
import argparse
import json
from pathlib import Path
import zipfile


def validate(directory, configuration, version):
    archive_name = f'ReactionTimer-{version}-plugin.zip'
    runtime = ['VoK.ReactionTimer.' + extension for extension in ('dll', 'deps.json', 'metadata')]
    expected = {archive_name}
    if configuration == 'Debug':
        expected.update(runtime)
        expected.add('VoK.ReactionTimer.pdb')
    actual = {path.name for path in directory.iterdir()}
    if actual != expected:
        raise RuntimeError(f'{configuration} output mismatch: expected {sorted(expected)}, got {sorted(actual)}')
    with zipfile.ZipFile(directory / archive_name) as archive:
        entries = ['plugins/ReactionTimer/' + name for name in runtime]
        if archive.namelist() != entries or archive.testzip() is not None:
            raise RuntimeError('Installer must contain exactly three valid runtime entries, DLL first')
        metadata = json.loads(archive.read(entries[2]).decode('utf-8-sig'))
        if metadata['Version'] != version + '.0':
            raise RuntimeError('Installer metadata version disagrees with package version')
        if configuration == 'Debug':
            for name, entry in zip(runtime, entries):
                if (directory / name).read_bytes() != archive.read(entry):
                    raise RuntimeError(f'Debug installer differs from loose file: {name}')
    print(f'PASS {configuration} output and installer contents: {directory}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('configuration', choices=('Debug', 'Release'))
    parser.add_argument('--version', default='1.0.0')
    options = parser.parse_args()
    validate(options.directory, options.configuration, options.version)
