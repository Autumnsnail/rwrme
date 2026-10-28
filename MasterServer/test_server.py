import base64
import concurrent.futures
import json
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
from http.server import ThreadingHTTPServer
from pathlib import Path
from server import ApiError, Repository, make_handler, valid_path, valid_branch


class RepositoryTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.config = {'projects': ['map', 'private'], 'users': {
            'alice': {'token': 'a' * 32, 'projects': ['map']},
            'bob': {'token': 'b' * 32, 'projects': ['map']}}, 'max_snapshot_bytes': 1024}
        self.repo = Repository(str(Path(self.directory.name) / 'repo.db'), self.config)

    def tearDown(self):
        self.repo.db.close()
        self.directory.cleanup()

    def call(self, operation, branch='main', user='alice', **kwargs):
        return self.repo.dispatch(dict(operation=operation, project='map', userId=user, branch=branch, **kwargs), user[0] * 32)

    def push(self, files, branch='main', head='', user='alice'):
        return self.call('push', branch, user=user, expectedHead=head,
                         files=[dict(path=p, content=base64.b64encode(c.encode()).decode()) for p, c in files.items()])['head']

    def contents(self, branch='main'):
        return {f['path']: base64.b64decode(f['content']).decode() for f in self.call('pull', branch)['files']}

    def test_user_id_and_token_required(self):
        for user, token in [('', ''), ('alice', ''), ('alice', 'b' * 32), ('unknown', 'a' * 32)]:
            with self.assertRaises(ApiError) as caught:
                self.repo.dispatch(dict(operation='connect', project='map', userId=user), token)
            self.assertEqual(401, caught.exception.status)

    def test_project_permission(self):
        with self.assertRaises(ApiError) as caught:
            self.repo.dispatch(dict(operation='connect', project='private', userId='alice'), 'a' * 32)
        self.assertEqual(403, caught.exception.status)

    def test_snapshot_roundtrip_and_author(self):
        head = self.push({'objects.svg': '<svg/>', 'terrain.cfg': 'settings'})
        self.assertEqual({'objects.svg': '<svg/>', 'terrain.cfg': 'settings'}, self.contents())
        self.assertEqual('alice', self.repo.db.execute('SELECT author FROM commits WHERE id=?', (head,)).fetchone()[0])

    def test_branch_isolation_and_fast_forward(self):
        base = self.push({'objects.svg': 'base'})
        self.call('create_branch', newBranch='users/bob')
        changed = self.push({'objects.svg': 'bob'}, 'users/bob', base, 'bob')
        self.assertEqual('base', self.contents()['objects.svg'])
        merged = self.call('merge', expectedHead=base, sourceBranch='users/bob')['head']
        self.assertEqual(changed, merged)
        self.assertEqual('bob', self.contents()['objects.svg'])

    def test_three_way_merge_and_delete(self):
        base = self.push({'objects.svg': 'base', 'remove.txt': 'old'})
        self.call('create_branch', newBranch='feature')
        left = self.push({'objects.svg': 'base', 'a.txt': 'ours'}, head=base)
        right = self.push({'objects.svg': 'base', 'remove.txt': 'old', 'b.txt': 'theirs'}, 'feature', base, 'bob')
        merged = self.call('merge', expectedHead=left, sourceBranch='feature')['head']
        self.assertEqual({'objects.svg': 'base', 'a.txt': 'ours', 'b.txt': 'theirs'}, self.contents())
        self.assertEqual([left, right], self.repo.info('map', merged)[0])

    def test_conflict_keeps_target_unchanged(self):
        base = self.push({'objects.svg': 'base'})
        self.call('create_branch', newBranch='feature')
        left = self.push({'objects.svg': 'ours'}, head=base)
        self.push({'objects.svg': 'theirs'}, 'feature', base)
        with self.assertRaises(ApiError) as caught:
            self.call('merge', expectedHead=left, sourceBranch='feature')
        self.assertEqual(['objects.svg'], caught.exception.conflicts)
        self.assertEqual(left, self.repo.head('map', 'main'))

    def test_concurrent_push_rejects_stale_head(self):
        def push(value):
            try:
                return self.push({'objects.svg': value})
            except ApiError as error:
                return error.status
        with concurrent.futures.ThreadPoolExecutor() as pool:
            results = list(pool.map(push, ['one', 'two']))
        self.assertEqual(1, results.count(409))

    def test_invalid_paths_and_duplicate_case(self):
        for path in ['../secret', '/absolute', 'a/../b', 'C:/secret', 'a\\b', '.git/config', 'x.meta', 'a//b', 'CON.txt', 'nul', 'LPT1.png']:
            self.assertFalse(valid_path(path), path)
            with self.assertRaises(ApiError):
                self.push({path: 'bad'})
        with self.assertRaises(ApiError):
            self.push({'A.txt': 'one', 'a.txt': 'two'})
        self.assertEqual('', self.repo.head('map', 'main'))
        self.assertEqual(0, self.repo.db.execute('SELECT COUNT(*) FROM blobs').fetchone()[0])

    def test_limits_and_empty_snapshot(self):
        for files in [{}, {'huge': 'x' * 1025}]:
            with self.assertRaises(ApiError):
                self.push(files)
        self.assertEqual('', self.repo.head('map', 'main'))

    def test_other_project_revision_not_visible(self):
        head = self.push({'objects.svg': 'data'})
        with self.assertRaises(ApiError):
            self.repo.info('private', head)

    def test_duplicate_branch_does_not_change_head(self):
        self.call('create_branch', newBranch='feature')
        with self.assertRaises(ApiError):
            self.call('create_branch', newBranch='feature')
        self.assertEqual('', self.repo.head('map', 'feature'))

    def test_restart_preserves_data(self):
        head = self.push({'objects.svg': 'data'})
        self.repo.db.close()
        self.repo = Repository(str(Path(self.directory.name) / 'repo.db'), self.config)
        self.assertEqual(head, self.repo.head('map', 'main'))
        self.assertEqual('data', self.contents()['objects.svg'])

    def test_file_directory_alias_rejected_atomically(self):
        with self.assertRaises(ApiError):
            self.push({'folder': 'file', 'folder/nested': 'another'})
        self.assertEqual('', self.repo.head('map', 'main'))
        self.assertEqual(0, self.repo.db.execute('SELECT COUNT(*) FROM blobs').fetchone()[0])

    def test_delete_modify_conflict(self):
        base = self.push({'objects.svg': 'base', 'remove.txt': 'original'})
        self.call('create_branch', newBranch='feature')
        left = self.push({'objects.svg': 'base'}, head=base)
        self.push({'objects.svg': 'base', 'remove.txt': 'changed'}, 'feature', base)
        with self.assertRaises(ApiError) as caught:
            self.call('merge', expectedHead=left, sourceBranch='feature')
        self.assertEqual(['remove.txt'], caught.exception.conflicts)
        self.assertEqual(left, self.repo.head('map', 'main'))

    def test_merge_size_limit_is_atomic(self):
        base = self.push({'objects.svg': 'base'})
        self.call('create_branch', newBranch='feature')
        left = self.push({'objects.svg': 'base', 'ours.bin': 'a' * 600}, head=base)
        self.push({'objects.svg': 'base', 'theirs.bin': 'b' * 600}, 'feature', base)
        with self.assertRaises(ApiError) as caught:
            self.call('merge', expectedHead=left, sourceBranch='feature')
        self.assertEqual(413, caught.exception.status)
        self.assertEqual(left, self.repo.head('map', 'main'))

    def svg_branches(self):
        xml = '<svg xmlns="http://www.w3.org/2000/svg"><g id="layer"><rect id="a" x="0"/><rect id="b" x="0"/></g></svg>'
        base = self.push({'objects.svg': xml})
        self.call('create_branch', newBranch='feature')
        return xml, base

    def test_svg_different_elements_merge_through_repository(self):
        xml, base = self.svg_branches()
        left = self.push({'objects.svg': xml.replace('id="a" x="0"', 'id="a" x="10"')}, head=base)
        right = self.push({'objects.svg': xml.replace('id="b" x="0"', 'id="b" x="20"')}, 'feature', base, 'bob')
        result = self.call('merge', expectedHead=left, sourceBranch='feature')
        from xml.etree import ElementTree as ET
        elements = {n.get('id'): n for n in ET.fromstring(self.contents()['objects.svg']).iter()}
        self.assertEqual('10', elements['a'].get('x'))
        self.assertEqual('20', elements['b'].get('x'))
        self.assertEqual([left, right], self.repo.info('map', result['head'])[0])

    def test_svg_conflict_reports_id_and_keeps_target(self):
        xml, base = self.svg_branches()
        left = self.push({'objects.svg': xml.replace('id="a" x="0"', 'id="a" x="10"')}, head=base)
        self.push({'objects.svg': xml.replace('id="a" x="0"', 'id="a" x="20"')}, 'feature', base)
        with self.assertRaises(ApiError) as caught:
            self.call('merge', expectedHead=left, sourceBranch='feature')
        self.assertEqual(['objects.svg#a'], caught.exception.conflicts)
        self.assertEqual(left, self.repo.head('map', 'main'))

    def test_new_svg_files_on_empty_branches_merge_elements(self):
        self.call('create_branch', newBranch='feature')
        left = self.push({'objects.svg': '<svg><rect id="a"/></svg>'})
        self.push({'objects.svg': '<svg><rect id="b"/></svg>'}, 'feature')
        self.call('merge', expectedHead=left, sourceBranch='feature')
        self.assertIn('id="a"', self.contents()['objects.svg'])
        self.assertIn('id="b"', self.contents()['objects.svg'])

    def test_svg_merged_blob_rolls_back_when_other_file_conflicts(self):
        xml = '<svg><rect id="a" x="0"/><rect id="b" x="0"/></svg>'
        base = self.push({'objects.svg': xml, 'z.bin': 'base'})
        self.call('create_branch', newBranch='feature')
        left = self.push({'objects.svg': xml.replace('id="a" x="0"', 'id="a" x="1"'), 'z.bin': 'ours'}, head=base)
        self.push({'objects.svg': xml.replace('id="b" x="0"', 'id="b" x="2"'), 'z.bin': 'theirs'}, 'feature', base)
        blobs_before = self.repo.db.execute('SELECT COUNT(*) FROM blobs').fetchone()[0]
        with self.assertRaises(ApiError):
            self.call('merge', expectedHead=left, sourceBranch='feature')
        self.assertEqual(blobs_before, self.repo.db.execute('SELECT COUNT(*) FROM blobs').fetchone()[0])
        self.assertEqual(left, self.repo.head('map', 'main'))

    def test_branch_tree_retains_creation_parent_after_restart(self):
        self.call('create_branch', newBranch='users/alice')
        self.call('create_branch', 'users/alice', newBranch='feature/road')
        self.repo.db.close()
        self.repo = Repository(str(Path(self.directory.name) / 'repo.db'), self.config)
        branches = {b['name']: b for b in self.call('connect')['branches']}
        self.assertEqual('', branches['main']['parent'])
        self.assertEqual('main', branches['users/alice']['parent'])
        self.assertEqual('users/alice', branches['feature/road']['parent'])

    def test_old_branch_schema_migrates_without_losing_heads(self):
        import sqlite3
        path = str(Path(self.directory.name) / 'legacy.db')
        with sqlite3.connect(path) as legacy:
            legacy.execute('CREATE TABLE branches(project TEXT,name TEXT,head TEXT NOT NULL,PRIMARY KEY(project,name))')
            legacy.execute("INSERT INTO branches VALUES('map','main','legacy-head')")
            legacy.execute("INSERT INTO branches VALUES('map','feature/old','feature-head')")
        legacy.close()
        migrated = Repository(path, self.config)
        try:
            self.assertEqual('legacy-head', migrated.head('map', 'main'))
            self.assertEqual('feature-head', migrated.head('map', 'feature/old'))
            response = migrated.dispatch(dict(operation='connect', userId='alice', project='map', branch='main'), 'a'*32)
            self.assertEqual({'main', 'feature/old'}, {b['name'] for b in response['branches']})
            self.assertTrue(all(b['parent'] == '' for b in response['branches']))
        finally:
            migrated.db.close()

    def test_history_includes_forks_merge_parents_and_metadata(self):
        base = self.push({'objects.svg': 'base'})
        self.call('create_branch', newBranch='feature')
        left = self.push({'objects.svg': 'base', 'a.txt': 'ours'}, head=base)
        right = self.push({'objects.svg': 'base', 'b.txt': 'theirs'}, 'feature', base, 'bob')
        merged = self.call('merge', expectedHead=left, sourceBranch='feature')['head']
        history = self.call('history')
        nodes = {node['id']: node for node in history['commits']}
        self.assertEqual({base, left, right, merged}, set(nodes))
        self.assertEqual([left, right], nodes[merged]['parents'])
        self.assertEqual([], nodes[base]['parents'])
        self.assertEqual('bob', nodes[right]['author'])
        self.assertIsInstance(nodes[right]['created'], float)
        self.assertTrue(nodes[merged]['message'].startswith('Merge'))
        self.assertNotIn('tree', nodes[merged])

    def test_history_pagination_has_no_duplicates(self):
        head = ''
        heads = []
        for number in range(5):
            head = self.push({'objects.svg': str(number)}, head=head)
            heads.append(head)
        cursor, seen = '', []
        for _ in range(4):
            response = self.call('history', limit=2, cursor=cursor)
            seen += [item['id'] for item in response['commits']]
            cursor = response['nextCursor']
            if not cursor:
                break
        self.assertEqual(list(reversed(heads)), seen)
        self.assertEqual('', cursor)
        for bad in [dict(limit=0), dict(limit=501), dict(cursor='bad'), dict(cursor='-1')]:
            with self.assertRaises(ApiError):
                self.call('history', **bad)

    def test_pull_historical_node_keeps_branch_tip(self):
        old = self.push({'objects.svg': 'old'})
        tip = self.push({'objects.svg': 'new'}, head=old)
        response = self.call('pull', revision=old)
        self.assertEqual(tip, response['head'])
        self.assertEqual(old, response['revision'])
        self.assertEqual('old', base64.b64decode(response['files'][0]['content']).decode())
        self.assertEqual(tip, self.repo.head('map', 'main'))

    def test_branch_can_start_at_selected_historical_node(self):
        old = self.push({'objects.svg': 'old'})
        tip = self.push({'objects.svg': 'new'}, head=old)
        self.call('create_branch', newBranch='from-history', revision=old)
        self.assertEqual(old, self.repo.head('map', 'from-history'))
        self.assertEqual(tip, self.repo.head('map', 'main'))
        self.assertEqual('old', self.contents('from-history')['objects.svg'])

    def test_history_and_revision_access_stay_in_project(self):
        revision = self.push({'objects.svg': 'private data'})
        config = dict(self.config)
        config['users'] = dict(config['users'], admin={'token': 'z'*32, 'projects':['private']})
        self.repo.config = config
        body = dict(operation='history', project='private', userId='admin', branch='main')
        self.assertEqual([], self.repo.dispatch(body, 'z'*32)['commits'])
        for operation in ('pull', 'create_branch'):
            with self.assertRaises(ApiError):
                self.repo.dispatch(dict(body, operation=operation, revision=revision, newBranch='stolen'), 'z'*32)
        self.assertEqual('', self.repo.head('private','main'))
        for invalid in ['not-a-hash', '../secret', ['bad']]:
            with self.assertRaises(ApiError):
                self.call('pull', revision=invalid)

    def submit(self, content, base='', branch='main', user='alice'):
        return self.call('submit', branch, user=user, baseRevision=base, message='Map change',
                         files=[dict(path='objects.svg', content=base64.b64encode(content.encode()).decode())])

    def test_submit_continues_leaf_without_manual_branch(self):
        first = self.submit('first')
        second = self.submit('second', first['head'])
        self.assertFalse(first['forked'])
        self.assertFalse(second['forked'])
        self.assertEqual('main', second['branch'])
        self.assertEqual([first['head']], self.repo.info('map', second['head'])[0])
        self.assertEqual('second', self.contents()['objects.svg'])
        self.assertEqual(1, len(self.call('connect')['branches']))

    def test_stale_submit_automatically_forks_from_actual_base(self):
        base = self.submit('base')['head']
        latest = self.submit('alice latest', base)['head']
        fork = self.submit('bob edit', base, user='bob')
        self.assertTrue(fork['forked'])
        self.assertNotEqual('main', fork['branch'])
        self.assertEqual(base, fork['baseRevision'])
        self.assertEqual([base], self.repo.info('map', fork['head'])[0])
        self.assertEqual(latest, self.repo.head('map','main'))
        self.assertEqual('bob edit', self.contents(fork['branch'])['objects.svg'])
        self.assertEqual('bob', self.repo.db.execute('SELECT author FROM commits WHERE id=?',(fork['head'],)).fetchone()[0])

    def test_child_on_another_branch_also_triggers_fork(self):
        base = self.submit('base')['head']
        self.call('create_branch',newBranch='other')
        other = self.push({'objects.svg':'other'},'other',base)
        fork = self.submit('from still-current main',base)
        self.assertTrue(fork['forked'])
        self.assertEqual(base,self.repo.head('map','main'))
        self.assertEqual(other,self.repo.head('map','other'))
        self.assertEqual([base],self.repo.info('map',fork['head'])[0])

    def test_historical_submit_does_not_attach_to_latest_tip(self):
        base = self.submit('old')['head']
        middle = self.submit('middle',base)['head']
        latest = self.submit('latest',middle)['head']
        fork = self.submit('historical edit',base)
        self.assertEqual([base],self.repo.info('map',fork['head'])[0])
        self.assertEqual(latest,self.repo.head('map','main'))
        self.assertNotIn(latest,self.repo.ancestors('map',fork['head']))

    def test_submit_continues_automatically_selected_fork(self):
        base = self.submit('base')['head']
        self.submit('main next',base)
        fork = self.submit('fork',base,user='bob')
        next_node = self.submit('fork next',fork['head'],fork['branch'],'bob')
        self.assertFalse(next_node['forked'])
        self.assertEqual(fork['branch'],next_node['branch'])
        self.assertEqual([fork['head']],self.repo.info('map',next_node['head'])[0])
        self.assertEqual(2,len(self.call('connect')['branches']))

    def test_leaf_on_other_workline_can_be_extended_by_node(self):
        base = self.submit('base')['head']
        self.call('create_branch',newBranch='other')
        leaf = self.push({'objects.svg':'other'},'other',base)
        result = self.submit('continue selected leaf',leaf,branch='main',user='bob')
        self.assertFalse(result['forked'])
        self.assertEqual('other',result['branch'])
        self.assertEqual(base,self.repo.head('map','main'))

    def test_simultaneous_submissions_preserve_both_snapshots(self):
        base = self.submit('base')['head']
        gate = threading.Barrier(2)
        def submit(user):
            gate.wait()
            return self.submit(user,base,user=user)
        with concurrent.futures.ThreadPoolExecutor() as pool:
            futures = [pool.submit(submit,user) for user in ['alice','bob']]
            results = [future.result() for future in futures]
        self.assertEqual([False,True],sorted(r['forked'] for r in results))
        self.assertEqual(2,len({r['branch'] for r in results}))
        self.assertEqual({'alice','bob'},{self.contents(r['branch'])['objects.svg'] for r in results})
        self.assertTrue(all(self.repo.info('map',r['head'])[0]==[base] for r in results))

    def test_simultaneous_initial_submissions_fork_instead_of_overwriting(self):
        with concurrent.futures.ThreadPoolExecutor() as pool:
            results = list(pool.map(lambda user:self.submit(user,user=user),['alice','bob']))
        self.assertEqual([False,True],sorted(r['forked'] for r in results))
        self.assertEqual({'alice','bob'},{self.contents(r['branch'])['objects.svg'] for r in results})

    def test_submit_invalid_snapshot_rolls_back_fork_and_blobs(self):
        base = self.submit('base')['head']
        latest = self.submit('latest',base)['head']
        counts = [self.repo.db.execute('SELECT COUNT(*) FROM '+table).fetchone()[0] for table in ('branches','commits','blobs')]
        with self.assertRaises(ApiError):
            self.call('submit',baseRevision=base,files=[dict(path='../bad',content='YQ==')])
        self.assertEqual(counts,[self.repo.db.execute('SELECT COUNT(*) FROM '+table).fetchone()[0] for table in ('branches','commits','blobs')])
        self.assertEqual(latest,self.repo.head('map','main'))

    def test_submit_requires_explicit_project_base(self):
        for value in [None,[], 'not-an-id','a'*64]:
            with self.assertRaises(ApiError):
                self.submit('bad',base=value)
        with self.assertRaises(ApiError):
            self.call('submit',files=[dict(path='objects.svg',content='YQ==')])
        base = self.submit('private')['head']
        self.repo.config['users']['admin']={'token':'z'*32,'projects':['private']}
        with self.assertRaises(ApiError):
            self.repo.dispatch(dict(operation='submit',project='private',userId='admin',branch='main',baseRevision=base,
                                   files=[dict(path='objects.svg',content='YQ==')]),'z'*32)
        self.assertEqual('',self.repo.head('private','main'))

    def test_automatic_fork_survives_restart_and_merges_svg_by_id(self):
        xml='<svg><rect id="a" x="0"/><rect id="b" x="0"/></svg>'
        base=self.submit(xml)['head']
        left=self.submit(xml.replace('id="a" x="0"','id="a" x="10"'),base)
        right=self.submit(xml.replace('id="b" x="0"','id="b" x="20"'),base,user='bob')
        self.repo.db.close()
        self.repo=Repository(str(Path(self.directory.name)/'repo.db'),self.config)
        merged=self.call('merge',expectedHead=left['head'],sourceBranch=right['branch'])
        self.assertEqual([left['head'],right['head']],self.repo.info('map',merged['head'])[0])
        self.assertIn('x="10"',self.contents()['objects.svg'])
        self.assertIn('x="20"',self.contents()['objects.svg'])

    def test_auto_workline_name_handles_long_and_reserved_user_ids(self):
        base=self.submit('base')['head']
        self.submit('latest',base)
        for user in ['CON','a'*64]:
            self.config['users'][user]={'token':user[0]*32,'projects':['map']}
            result=self.submit(user,base,user=user)
            self.assertTrue(valid_branch(result['branch']))
            self.assertTrue(result['forked'])

    def test_failure_after_auto_workline_creation_rolls_back_everything(self):
        from unittest.mock import patch
        base=self.submit('base')['head']
        latest=self.submit('latest',base)['head']
        counts=[self.repo.db.execute('SELECT COUNT(*) FROM '+table).fetchone()[0] for table in ('branches','commits','blobs')]
        with patch.object(self.repo,'commit',side_effect=ApiError('injected commit failure')):
            with self.assertRaises(ApiError):
                self.submit('must roll back',base)
        self.assertEqual(counts,[self.repo.db.execute('SELECT COUNT(*) FROM '+table).fetchone()[0] for table in ('branches','commits','blobs')])
        self.assertEqual(latest,self.repo.head('map','main'))

    def test_http_endpoint_auth_and_validation(self):
        http = ThreadingHTTPServer(('127.0.0.1', 0), make_handler(self.repo))
        worker = threading.Thread(target=http.serve_forever, daemon=True)
        worker.start()
        url = 'http://127.0.0.1:' + str(http.server_address[1])
        try:
            with urllib.request.urlopen(url + '/health') as response:
                self.assertTrue(json.load(response)['success'])
            data = json.dumps(dict(operation='connect', project='map', userId='alice', branch='main')).encode()
            request = urllib.request.Request(url + '/v1/repository', data=data)
            with self.assertRaises(urllib.error.HTTPError) as caught:
                urllib.request.urlopen(request)
            self.assertEqual(401, caught.exception.code)
            request.add_header('Authorization', 'Bearer ' + 'a' * 32)
            with urllib.request.urlopen(request) as response:
                self.assertEqual('main', json.load(response)['branches'][0]['name'])
        finally:
            http.shutdown()
            worker.join()
            http.server_close()


if __name__ == '__main__':
    unittest.main()
