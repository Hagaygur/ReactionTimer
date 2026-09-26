"""Semantic versioning from passing, observed compatibility/feature contracts."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess


def classify(old, new):
    if old.get('schema') != new.get('schema'):
        raise ValueError('Contract schema changed; migrate the comparison before releasing.')
    level = 'patch'
    reasons = []
    for group in ('compatibility', 'features'):
        before, after = old[group], new[group]
        for key, value in before.items():
            if key not in after or (group == 'compatibility' and after[key] != value):
                return 'major', [f'{group} contract removed or changed: {key}']
            if after[key] != value:
                level = 'minor'
                reasons.append(f'feature behavior changed: {key}')
        for key in after.keys() - before.keys():
            level = 'minor'
            reasons.append(f'contract added: {group}.{key}')
    return level, reasons or ['Passing contracts unchanged']


def bump(version, level):
    major, minor, patch = map(int, version.removeprefix('v').split('.'))
    return {'major': f'{major+1}.0.0', 'minor': f'{major}.{minor+1}.0',
            'patch': f'{major}.{minor}.{patch+1}'}[level]


def commit_level(messages):
    if re.search(r'(?m)^BREAKING[ -]CHANGE:|^[\w-]+(?:\([^\n]*\))?!:', messages):
        return 'major'
    if re.search(r'(?m)^feat(?:\([^\n]*\))?:', messages):
        return 'minor'
    return 'patch'


def git(*args):
    return subprocess.check_output(['git', *args], text=True).strip()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--contracts', default='dist/contracts.json')
    parser.add_argument('--previous', default='dist/previous-contracts.json')
    parser.add_argument('--base', default='')
    args = parser.parse_args()
    current = json.loads(Path(args.contracts).read_text(encoding='utf-8-sig'))
    base = args.base
    reason = ['First stable release']
    reused = False
    if not base:
        version, level = '1.0.0', 'initial'
    elif git('rev-list', '-n', '1', base) == git('rev-parse', 'HEAD'):
        previous = json.loads(Path(args.previous).read_text(encoding='utf-8-sig'))
        if current != previous:
            raise ValueError('Same commit produced a different contract; refusing to reuse its version.')
        version, level, reused = base[1:], 'reuse', True
        reason = ['This commit already has a release tag']
    else:
        previous = json.loads(Path(args.previous).read_text(encoding='utf-8-sig'))
        level, reason = classify(previous, current)
        declared = commit_level(git('log', '--format=%B', f'{base}..HEAD'))
        ranks = {'patch': 0, 'minor': 1, 'major': 2}
        if ranks[declared] > ranks[level]:
            level = declared
            reason.append('Conventional commit declares a larger change')
        version = bump(base, level)
    result = {'version': version, 'tag': 'v' + version, 'bump': level,
              'base': base, 'reused': reused, 'reasons': reason, 'commit': git('rev-parse', 'HEAD')}
    Path('dist/version.json').write_text(json.dumps(result, indent=2)+'\n')
    if os.environ.get('GITHUB_OUTPUT'):
        with open(os.environ['GITHUB_OUTPUT'], 'a', encoding='utf-8') as stream:
            stream.write(f'version={version}\ntag=v{version}\n')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
