import copy
import unittest
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
from unittest.mock import patch
from version import classify, bump, commit_level, code_changed

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
        with self.assertRaises(ValueError): classify(self.old, self.new)
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
            (root/'dist').mkdir()
            current = {'schema': 1, 'compatibility': {'schema.field': 'Int32'}, 'features': {'preview': 'yes'}}
            def calculate(base=''):
                (root/'dist/contracts.json').write_text(json.dumps(current))
                output = root/'github-output'
                output.write_text('')
                env = dict(os.environ, GITHUB_OUTPUT=str(output))
                subprocess.run([sys.executable, str(script), *(['--base', base] if base else [])], cwd=root, env=env, check=True, capture_output=True)
                result = json.loads((root/'dist/version.json').read_text())
                self.assertIn(f'publish={str(result["publish"]).lower()}', output.read_text().splitlines())
                return result['version']
            def commit(message, code=True):
                if code:
                    (root/'src').mkdir(exist_ok=True)
                    with (root/'src/Plugin.cs').open('a') as source:
                        source.write('// ' + message + '\n')
                    git('add', 'src/Plugin.cs')
                git('commit', '--allow-empty', '-qm', message)
            def baseline(tag):
                git('tag', tag)
                (root/'dist/previous-contracts.json').write_text(json.dumps(current))
            commit('initial')
            self.assertEqual(calculate(), '1.0.0')
            baseline('v1.0.0')
            self.assertEqual(calculate('v1.0.0'), '1.0.0')
            self.assertTrue(json.loads((root/'dist/version.json').read_text())['publish'])
            (root/'README.md').write_text('Documentation only')
            git('add', 'README.md')
            commit('feat!: rewrite documentation', code=False)
            self.assertEqual(calculate('v1.0.0'), '1.0.0')
            self.assertFalse(json.loads((root/'dist/version.json').read_text())['publish'])
            commit('fix: colors')
            self.assertEqual(calculate('v1.0.0'), '1.0.1')
            self.assertTrue(json.loads((root/'dist/version.json').read_text())['publish'])
            baseline('v1.0.1')
            current['features']['new-mode'] = 'enabled'
            commit('new mode')
            self.assertEqual(calculate('v1.0.1'), '1.1.0')
            baseline('v1.1.0')
            current['compatibility']['schema.field'] = 'String'
            commit('schema migration')
            self.assertEqual(calculate('v1.1.0'), '2.0.0')

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
                     'src/VoK.ReactionTimer.metadata'):
            self.write(path, 'changed')
        self.commit('feat!: non-plugin changes')
        self.assertFalse(code_changed('v1.0.0'))

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


if __name__ == '__main__': unittest.main()
