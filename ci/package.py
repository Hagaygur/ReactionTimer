"""Validate DH 4 + DH 5 plugin archives, build the source archive, and write SHA256SUMS.txt."""
import hashlib
import json
from pathlib import Path
import zipfile

SOURCE_DIRS = ('src', 'src-dh5', 'tests', 'ci', '.github', '.vscode', 'docs')
SOURCE_FILES = (
    'README.md', 'INSTALL.txt', 'GITHUB.md', 'AGENTS.md', 'LICENSE.txt',
    'build.ps1', '.gitignore', 'ReactionTimer.sln', 'global.json',
    '.vsconfig', '.editorconfig', '.gitattributes',
)
EXCLUDED_DIRS = {'bin', 'obj', '__pycache__'}
EXCLUDED_SUFFIXES = {'.pyc', '.user', '.key', '.pfx'}
FORBIDDEN_NAMES = {'src.zip'}


def iter_source_files(repo_root=Path('.')):
    for directory in SOURCE_DIRS:
        base = repo_root / directory
        if not base.exists():
            continue
        for path in sorted(base.rglob('*')):
            rel = path.relative_to(repo_root)
            if (path.is_file()
                    and not set(rel.parts) & EXCLUDED_DIRS
                    and path.suffix.lower() not in EXCLUDED_SUFFIXES
                    and not path.name.lower().startswith('signing.')
                    and path.name not in FORBIDDEN_NAMES):
                yield rel, path
    for name in SOURCE_FILES:
        path = repo_root / name
        if path.is_file():
            yield Path(name), path


def validate_dh4_archive(plugin_path, version):
    expected = ['plugins/ReactionTimer/VoK.ReactionTimer.' + ext for ext in ('dll', 'deps.json', 'metadata')]
    with zipfile.ZipFile(plugin_path) as archive:
        if archive.namelist() != expected:
            raise AssertionError('DH 4 installer must contain exactly three runtime files, DLL first')
        if archive.testzip() is not None:
            raise AssertionError('DH 4 installer archive failed CRC check')
        metadata = json.loads(archive.read(expected[2]).decode('utf-8-sig'))
        if metadata['Version'] != version + '.0':
            raise AssertionError('DH 4 installer metadata version mismatch')


def validate_dh5_archive(plugin_path, version):
    required = {'VoK.ReactionTimer.dll', 'VoK.ReactionTimer.deps.json', 'plugin.metadata'}
    signed_set = required | {
        'Alch Reaction Timer.entitlement',
        'Alch Reaction Timer.entitlement.sig',
        'Alch Reaction Timer.manifest',
        'Alch Reaction Timer.manifest.sig',
        'Alch Reaction Timer.pem',
    }
    with zipfile.ZipFile(plugin_path) as archive:
        if archive.testzip() is not None:
            raise AssertionError('DH 5 installer archive failed CRC check')
        names = archive.namelist()
        if not names or not required.issubset(names):
            raise AssertionError(f'DH 5 installer missing required root files: {names}')
        for name in names:
            if (name.startswith(('plugins/', './', '/'))
                    or '\\' in name or '..' in name
                    or name.lower().endswith(('.pdb', '.key', '.pfx'))
                    or Path(name).name.lower() == 'vok.sdk.dll'):
                raise AssertionError(f'Forbidden or non-canonical DH 5 archive entry: {name}')
        if 'Alch Reaction Timer.manifest' in names and set(names) != signed_set:
            raise AssertionError(f'Signed DH 5 installer must have exact 8 root entries, got {sorted(names)}')
        metadata = json.loads(archive.read('plugin.metadata').decode('utf-8-sig'))
        if metadata['PluginName'] != 'Alch Reaction Timer':
            raise AssertionError('DH 5 plugin.metadata PluginName mismatch')
        if metadata['CurrentVersion'] != version + '.0':
            raise AssertionError('DH 5 plugin.metadata CurrentVersion mismatch')


def main():
    version = json.loads(Path('dist/version.json').read_text(encoding='utf-8'))['version']
    dh4_plugin = Path(f'dist/ReactionTimer-{version}-dh4-plugin.zip')
    dh5_plugin = Path(f'dist/ReactionTimer-{version}-dh5-plugin.zip')
    validate_dh4_archive(dh4_plugin, version)
    validate_dh5_archive(dh5_plugin, version)

    # Explicit source allowlist: never ship downloaded SDKs, build output, private keys, or git internals.
    source = Path(f'dist/ReactionTimer-{version}-source.zip')
    with zipfile.ZipFile(source, 'w', zipfile.ZIP_DEFLATED) as archive:
        for rel, path in iter_source_files(Path('.')):
            archive.write(path, rel.as_posix())

    files = [dh4_plugin, dh5_plugin, source, Path('dist/contracts.json'), Path('dist/version.json')]
    Path('dist/SHA256SUMS.txt').write_text(
        ''.join(f'{hashlib.sha256(p.read_bytes()).hexdigest()}  {p.name}\n' for p in files),
        encoding='utf-8',
    )
    print('PASS DH 4 and DH 5 installers verified; separate source archive and SHA256SUMS.txt created')


if __name__ == '__main__':
    main()
