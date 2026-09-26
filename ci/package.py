import hashlib
import json
from pathlib import Path
import zipfile

version = json.loads(Path('dist/version.json').read_text())['version']
plugin = Path(f'dist/ReactionTimer-{version}-plugin.zip')
expected = ['plugins/ReactionTimer/VoK.ReactionTimer.'+ext for ext in ('dll', 'deps.json', 'metadata')]
with zipfile.ZipFile(plugin) as archive:
    assert archive.namelist() == expected, 'Installer must contain exactly three runtime files, DLL first'
    assert archive.testzip() is None
    metadata = json.loads(archive.read(expected[2]).decode('utf-8-sig'))
    assert metadata['Version'] == version + '.0'
# Explicit source allowlist: never ship downloaded SDKs, build output, or git internals.
source = Path(f'dist/ReactionTimer-{version}-source.zip')
with zipfile.ZipFile(source, 'w', zipfile.ZIP_DEFLATED) as archive:
    for directory in ('src', 'tests', 'ci', '.github', '.vscode', 'docs'):
        for path in sorted(Path(directory).rglob('*')):
            if path.is_file() and not set(path.parts) & {'bin', 'obj', '__pycache__'}:
                archive.write(path, path.as_posix())
    for name in ('README.md', 'INSTALL.txt', 'GITHUB.md', 'AGENTS.md', 'build.ps1', '.gitignore', 'ReactionTimer.sln', 'global.json', '.vsconfig', '.editorconfig', '.gitattributes'):
        archive.write(name, name)
files = [plugin, source, Path('dist/contracts.json'), Path('dist/version.json')]
Path('dist/SHA256SUMS.txt').write_text(''.join(f'{hashlib.sha256(p.read_bytes()).hexdigest()}  {p.name}\n' for p in files))
print('PASS installer contents, metadata version, ZIP integrity; separate source archive created')
