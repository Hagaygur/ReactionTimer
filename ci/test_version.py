import copy
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

from package import iter_source_files
from sign_dh5 import package_dh5
from version import bump, classify, code_changed, commit_level


class VersionTests(unittest.TestCase):
    def setUp(self):
        self.old = {'schema': 1, 'compatibility': {'font-type': 'Int32'},
                    'features': {'preview': 'PREVIEW  3.5'}}
        self.new = copy.deepcopy(self.old)

    def test_unchanged_patch(self):
        self.assertEqual(classify(self.old, self.new)[0], 'patch')

    def test_breaking_contract_major(self):
        self.new['compatibility']['font-type'] = 'String'
        self.assertEqual(classify(self.old, self.new)[0], 'major')

    def test_removed_feature_major(self):
        del self.new['features']['preview']
        self.assertEqual(classify(self.old, self.new)[0], 'major')

    def test_added_feature_minor(self):
        self.new['features']['sound'] = True
        self.assertEqual(classify(self.old, self.new)[0], 'minor')

    def test_changed_feature_minor(self):
        self.new['features']['preview'] = 'DEMO  3.5'
        self.assertEqual(classify(self.old, self.new)[0], 'minor')

    def test_added_compatible_field_minor(self):
        self.new['compatibility']['position-type'] = 'Double'
        self.assertEqual(classify(self.old, self.new)[0], 'minor')

    def test_major_wins(self):
        self.new['features']['new'] = 1
        self.new['compatibility']['font-type'] = 'String'
        self.assertEqual(classify(self.old, self.new)[0], 'major')

    def test_schema_change_rejected(self):
        self.new['schema'] = 2
        with self.assertRaises(ValueError):
            classify(self.old, self.new)

    def test_versions(self):
        self.assertEqual(bump('v1.2.3', 'major'), '2.0.0')
        self.assertEqual(bump('v1.2.3', 'minor'), '1.3.0')
        self.assertEqual(bump('v1.2.3', 'patch'), '1.2.4')

    def test_commit_declarations(self):
        for msg in ['feat!: remove field', 'fix(config)!: migration', 'fix: x\n\nBREAKING CHANGE: y']:
            self.assertEqual(commit_level(msg), 'major')
        self.assertEqual(commit_level('feat(hud): add mode'), 'minor')
        self.assertEqual(commit_level('fix: color'), 'patch')


class GitWorkflowTests(unittest.TestCase):
    def test_release_sequence_and_rerun(self):
        script = Path(__file__).with_name('version.py').resolve()
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)

            def git(*args):
                return subprocess.check_output(['git', *args], cwd=root, text=True).strip()

            git('init', '-q')
            git('config', 'user.name', 'Version test')
            git('config', 'user.email', 'test@example.invalid')
            (root / 'dist').mkdir()
            current = {'schema': 1, 'compatibility': {'schema.field': 'Int32'}, 'features': {'preview': 'yes'}}

            def calculate(base='', tag_floor=''):
                (root / 'dist/contracts.json').write_text(json.dumps(current))
                output = root / 'github-output'
                output.write_text('')
                env = dict(os.environ, GITHUB_OUTPUT=str(output))
                args = [sys.executable, str(script)]
                if base:
                    args += ['--base', base]
                if tag_floor:
                    args += ['--tag-floor', tag_floor]
                subprocess.run(args, cwd=root, env=env, check=True, capture_output=True)
                result = json.loads((root / 'dist/version.json').read_text())
                self.assertIn(f'publish={str(result["publish"]).lower()}', output.read_text().splitlines())
                return result['version']

            def commit(message, code=True):
                if code:
                    (root / 'src').mkdir(exist_ok=True)
                    with (root / 'src/Plugin.cs').open('a') as source:
                        source.write('// ' + message + '\n')
                    git('add', 'src/Plugin.cs')
                git('commit', '--allow-empty', '-qm', message)

            def baseline(tag):
                git('tag', tag)
                (root / 'dist/previous-contracts.json').write_text(json.dumps(current))

            commit('initial')
            self.assertEqual(calculate(), '1.0.0')
            baseline('v1.0.0')
            self.assertEqual(calculate('v1.0.0'), '1.0.0')
            self.assertTrue(json.loads((root / 'dist/version.json').read_text())['publish'])
            (root / 'README.md').write_text('Documentation only')
            git('add', 'README.md')
            commit('feat!: rewrite documentation', code=False)
            self.assertEqual(calculate('v1.0.0'), '1.0.0')
            self.assertFalse(json.loads((root / 'dist/version.json').read_text())['publish'])
            commit('fix: colors')
            self.assertEqual(calculate('v1.0.0'), '1.0.1')
            self.assertTrue(json.loads((root / 'dist/version.json').read_text())['publish'])
            baseline('v1.0.1')
            current['features']['new-mode'] = 'enabled'
            commit('new mode')
            self.assertEqual(calculate('v1.0.1'), '1.1.0')
            baseline('v1.1.0')
            current['compatibility']['schema.field'] = 'String'
            commit('schema migration')
            self.assertEqual(calculate('v1.1.0'), '2.0.0')

    def test_tag_floor_bumps_above_orphan_tag(self):
        script = Path(__file__).with_name('version.py').resolve()
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)

            def git(*args):
                return subprocess.check_output(['git', *args], cwd=root, text=True).strip()

            git('init', '-q')
            git('config', 'user.name', 'Orphan tag test')
            git('config', 'user.email', 'test@example.invalid')
            (root / 'dist').mkdir()
            (root / 'src').mkdir()
            (root / 'src/Plugin.cs').write_text('class Plugin {}\n')
            git('add', 'src/Plugin.cs')
            git('commit', '-qm', 'initial')
            git('tag', 'v1.0.0')
            base_contracts = {'schema': 1, 'compatibility': {'schema.field': 'Int32'}, 'features': {'preview': 'yes'}}
            (root / 'dist/previous-contracts.json').write_text(json.dumps(base_contracts))

            # Simulate orphan v1.0.1 tag without release assets
            (root / 'README.md').write_text('docs')
            git('add', 'README.md')
            git('commit', '-qm', 'docs only')
            git('tag', 'v1.0.1')

            (root / 'src/Plugin.cs').write_text('class Plugin { int x; }\n')
            git('add', 'src/Plugin.cs')
            git('commit', '-qm', 'fix: bug')
            (root / 'dist/contracts.json').write_text(json.dumps(base_contracts))
            subprocess.run(
                [sys.executable, str(script), '--base', 'v1.0.0', '--tag-floor', 'v1.0.1'],
                cwd=root, check=True, capture_output=True,
            )
            self.assertEqual(json.loads((root / 'dist/version.json').read_text())['version'], '1.0.2')

            new_contracts = copy.deepcopy(base_contracts)
            new_contracts['features']['theme'] = 'EberronGold'
            (root / 'dist/contracts.json').write_text(json.dumps(new_contracts))
            subprocess.run(
                [sys.executable, str(script), '--base', 'v1.0.0', '--tag-floor', 'v1.0.1'],
                cwd=root, check=True, capture_output=True,
            )
            self.assertEqual(json.loads((root / 'dist/version.json').read_text())['version'], '1.1.0')


class CodeReleaseTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.git('init', '-q')
        self.git('config', 'user.name', 'Release policy test')
        self.git('config', 'user.email', 'test@example.invalid')
        self.write('src/Plugin.cs', 'class Plugin {}')
        self.commit('initial')
        self.git('tag', 'v1.0.0')
        git_patch = patch('version.git', side_effect=self.git)
        git_patch.start()
        self.addCleanup(git_patch.stop)

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.root, text=True).strip()

    def write(self, path, text):
        destination = self.root / path
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(text)

    def commit(self, message):
        self.git('add', '-A')
        self.git('commit', '-qm', message)

    def test_non_plugin_changes_do_not_release(self):
        for path in ('README.md', 'docs/guide.md', 'tests/Test.cs', 'ci/version.py',
                     '.github/workflows/build.yml', 'src/ReactionTimer.csproj',
                     'src/VoK.ReactionTimer.metadata', 'src-dh5/ReactionTimer.Dh5.csproj'):
            self.write(path, 'changed')
        self.commit('feat!: non-plugin changes')
        self.assertFalse(code_changed('v1.0.0'))

    def test_src_dh5_changes_trigger_release(self):
        self.write('src-dh5/Dh5Plugin.cs', 'class Dh5Plugin {}')
        self.commit('feat: add dh5 plugin')
        self.assertTrue(code_changed('v1.0.0'))

    def test_edit_followed_by_docs_still_releases(self):
        self.write('src/Plugin.cs', 'class Plugin { int value; }')
        self.commit('fix: code')
        self.write('README.md', 'later docs')
        self.commit('docs: update')
        self.assertTrue(code_changed('v1.0.0'))

    def test_new_nested_source_releases(self):
        self.write('src/Nested/New.cs', 'class New {}')
        self.commit('feat: new code')
        self.assertTrue(code_changed('v1.0.0'))

    def test_deleted_source_releases(self):
        self.git('rm', 'src/Plugin.cs')
        self.commit('remove code')
        self.assertTrue(code_changed('v1.0.0'))

    def test_renamed_source_releases(self):
        self.git('mv', 'src/Plugin.cs', 'src/Renamed.cs')
        self.commit('rename code')
        self.assertTrue(code_changed('v1.0.0'))

    def test_reverted_source_does_not_release(self):
        self.write('src/Plugin.cs', 'class Plugin { int value; }')
        self.commit('fix: code')
        self.git('revert', '--no-edit', 'HEAD')
        self.assertFalse(code_changed('v1.0.0'))

    def test_first_release_requires_source(self):
        self.assertTrue(code_changed(''))
        self.git('rm', 'src/Plugin.cs')
        self.write('README.md', 'docs only')
        self.commit('remove code')
        self.assertFalse(code_changed(''))

    def test_publisher_skips_build_only_before_using_git_or_github(self):
        self.write('dist/version.json', json.dumps({'publish': False}))
        script = Path(__file__).with_name('release.py').resolve()
        environment = dict(os.environ, PATH='')
        result = subprocess.run([sys.executable, str(script)], cwd=self.root,
                                env=environment, check=True, capture_output=True, text=True)
        self.assertIn('Build only', result.stdout)


class PackagingSecurityAndDocsTests(unittest.TestCase):
    def test_package_allowlist_covers_tracked_repository_files(self):
        repo_root = Path(__file__).resolve().parent.parent
        if not (repo_root / '.git').exists():
            return
        tracked = set(
            subprocess.check_output(['git', 'ls-files'], cwd=repo_root, text=True).splitlines()
        )
        packaged = {rel.as_posix() for rel, _ in iter_source_files(repo_root)}
        missing = sorted(tracked - packaged)
        self.assertEqual(missing, [], f'Tracked files missing from ci/package.py allowlist: {missing}')

    def test_no_private_keys_or_pfx_tracked_or_packaged(self):
        repo_root = Path(__file__).resolve().parent.parent
        if not (repo_root / '.git').exists():
            return
        tracked = subprocess.check_output(['git', 'ls-files'], cwd=repo_root, text=True).splitlines()
        packaged = [rel.as_posix() for rel, _ in iter_source_files(repo_root)]
        for file_list in (tracked, packaged):
            for rel in file_list:
                lower = rel.lower()
                self.assertFalse(lower.endswith(('.key', '.pfx')), f'Forbidden secret file extension: {rel}')
                self.assertFalse(Path(lower).name.startswith('signing.'), f'Forbidden signing file: {rel}')
                self.assertNotIn('.sdk-cache/', lower, f'SDK cache leaked: {rel}')
                self.assertNotEqual(Path(lower).name, 'src.zip', f'src.zip leaked: {rel}')
                full = repo_root / rel
                if full.is_file() and full.suffix.lower() not in {'.png', '.sig', '.zip'}:
                    text = full.read_text(encoding='utf-8', errors='ignore')
                    self.assertNotRegex(text, r'-----BEGIN [A-Z0-9 ]*PRIVATE KEY-----', f'Private key header in {rel}')

    def test_readme_documents_all_settings(self):
        repo_root = Path(__file__).resolve().parent.parent
        if not (repo_root / '.git').exists():
            return
        settings_src = (repo_root / 'src/Settings.cs').read_text(encoding='utf-8')
        settings_class_src = settings_src.split('internal sealed class SettingsStore', 1)[0]
        readme = (repo_root / 'README.md').read_text(encoding='utf-8')
        properties = re.findall(
            r'\bpublic\s+(?!static\b|const\b|void\b|Settings\b)[\w<>?.\[\]]+\s+(\w+)\s*\{\s*get;\s*(?:set;|init;)\s*\}',
            settings_class_src,
        )
        self.assertGreaterEqual(len(properties), 16, f'Expected >= 16 public Settings properties, got {properties}')
        missing = [prop for prop in properties if prop not in readme]
        self.assertEqual(missing, [], f'Settings properties missing from README.md: {missing}')
        for required in ('EberronGold', 'CannithArcane', 'FeywildTwilight',
                         'dh4-plugin.zip', 'dh5-plugin.zip', '/alch'):
            self.assertIn(required, readme, f'Expected {required} documented in README.md')

    def test_sign_dh5_requires_key_unless_allow_unsigned(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            inp = root / 'in'
            inp.mkdir()
            (inp / 'VoK.ReactionTimer.dll').write_bytes(b'MZ-dummy')
            (inp / 'VoK.ReactionTimer.deps.json').write_text('{}')
            out = root / 'out.zip'
            with patch.dict(os.environ, {'DH_AUTHOR_KEY_FILE': '', 'DH_AUTHOR_KEY_B64': '', 'DH5_AUTHOR_KEY_B64': ''}, clear=False):
                with self.assertRaisesRegex(RuntimeError, 'DH 5 signing key is required'):
                    package_dh5(inp, out, '1.1.0', root / 'missing.ent', root / 'missing.sig', allow_unsigned=False)
                package_dh5(inp, out, '1.1.0', root / 'missing.ent', root / 'missing.sig', allow_unsigned=True)
            with zipfile.ZipFile(out) as archive:
                self.assertEqual(
                    sorted(archive.namelist()),
                    ['VoK.ReactionTimer.deps.json', 'VoK.ReactionTimer.dll', 'plugin.metadata'],
                )


if __name__ == '__main__':
    unittest.main()
