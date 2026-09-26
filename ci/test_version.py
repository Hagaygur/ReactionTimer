import copy
import unittest
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
from version import classify, bump, commit_level

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
                env = dict(os.environ); env.pop('GITHUB_OUTPUT', None)
                subprocess.run([sys.executable, str(script), *(['--base', base] if base else [])], cwd=root, env=env, check=True, capture_output=True)
                return json.loads((root/'dist/version.json').read_text())['version']
            def commit(message):
                git('commit', '--allow-empty', '-qm', message)
            def baseline(tag):
                git('tag', tag)
                (root/'dist/previous-contracts.json').write_text(json.dumps(current))
            commit('initial')
            self.assertEqual(calculate(), '1.0.0')
            baseline('v1.0.0')
            self.assertEqual(calculate('v1.0.0'), '1.0.0')
            commit('fix: colors')
            self.assertEqual(calculate('v1.0.0'), '1.0.1')
            baseline('v1.0.1')
            current['features']['new-mode'] = 'enabled'
            commit('new mode')
            self.assertEqual(calculate('v1.0.1'), '1.1.0')
            baseline('v1.1.0')
            current['compatibility']['schema.field'] = 'String'
            commit('schema migration')
            self.assertEqual(calculate('v1.1.0'), '2.0.0')

if __name__ == '__main__': unittest.main()
