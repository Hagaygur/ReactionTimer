"""Publish only a verified commit; never overwrite an existing public release."""
import json
from pathlib import Path
import subprocess


def run(*args):
    return subprocess.check_output(args, text=True).strip()


info = json.loads(Path('dist/version.json').read_text(encoding='utf-8'))
if info.get('publish') is not True:
    print('Build only: no plugin code changes eligible for publication')
    raise SystemExit(0)
tag, commit = info['tag'], info['commit']
version = info['version']
# A failed draft can be resumed, but an existing tag must refer to this commit.
exists = subprocess.run(['git', 'rev-parse', '--verify', f'refs/tags/{tag}'], capture_output=True).returncode == 0
if exists and run('git', 'rev-list', '-n', '1', tag) != commit:
    raise RuntimeError('Release tag belongs to another commit; refusing overwrite')
lookup = subprocess.run(['gh', 'release', 'view', tag, '--json', 'isDraft'], capture_output=True, text=True)
if lookup.returncode == 0 and not json.loads(lookup.stdout)['isDraft']:
    print(f'{tag} already published; leaving its assets unchanged')
    raise SystemExit(0)

notes = (
    f"Version {version} ({info['bump']}).\n\n"
    + '\n'.join('- ' + r for r in info['reasons'])
    + '\n\n### Downloads\n'
    + f'- **Dungeon Helper 4 (`4.2.1.0`):** `ReactionTimer-{version}-dh4-plugin.zip` '
      '(install via **Settings -> Add plugin from zip file**).\n'
    + f'- **Dungeon Helper 5 Beta (`5.3.0.0`, x64, signed):** `ReactionTimer-{version}-dh5-plugin.zip` '
      '(install via **Plugins -> Install from zip**).\n'
    + f'- **Source / tests (do NOT install in DH):** `ReactionTimer-{version}-source.zip`.\n'
)
Path('dist/release-notes.md').write_text(notes, encoding='utf-8')
if lookup.returncode != 0:
    run('gh', 'release', 'create', tag, '--target', commit, '--draft', '--title', tag,
        '--notes-file', 'dist/release-notes.md')
assets = [
    f'dist/ReactionTimer-{version}-dh4-plugin.zip',
    f'dist/ReactionTimer-{version}-dh5-plugin.zip',
    f'dist/ReactionTimer-{version}-source.zip',
    'dist/contracts.json',
    'dist/version.json',
    'dist/SHA256SUMS.txt',
]
run('gh', 'release', 'upload', tag, *assets, '--clobber')
run('gh', 'release', 'edit', tag, '--draft=false', '--latest')
print(f'Published {tag}')
