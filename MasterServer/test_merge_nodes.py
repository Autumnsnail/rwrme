import base64
import json
import struct
import unittest
import uuid
import zlib
import test_server
from server import ApiError


class NodeMergeTests(unittest.TestCase):
    setUp = test_server.RepositoryTests.setUp
    tearDown = test_server.RepositoryTests.tearDown
    call = test_server.RepositoryTests.call
    push = test_server.RepositoryTests.push
    submit = test_server.RepositoryTests.submit
    contents = test_server.RepositoryTests.contents

    def pair(self, left=None, right=None):
        original='<svg><rect id="a" x="0"/><rect id="b" x="0"/><rect id="c" x="0"/></svg>'
        base=self.submit(original)['head']
        a=self.submit(left or original.replace('id="a" x="0"','id="a" x="10"'),base)
        b=self.submit(right or original.replace('id="a" x="0"','id="a" x="20"').replace('id="b" x="0"','id="b" x="30"'),base,user='bob')
        return base,a,b

    def preview(self,a,b,resolutions=None):
        return self.call('merge_preview',leftRevision=a,rightRevision=b,resolutions=resolutions or [])['merge']

    def commit_merge(self,a,b,resolutions=None,request_id=None):
        preview=self.preview(a,b,resolutions)
        return self.call('merge_commit',leftRevision=a,rightRevision=b,resolutions=resolutions or [],previewId=preview['previewId'],requestId=request_id or uuid.uuid4().hex)

    def counts(self):
        return tuple(self.repo.db.execute('SELECT COUNT(*) FROM '+table).fetchone()[0] for table in ('branches','commits','blobs','merge_results'))

    def test_preview_is_read_only_and_exposes_exact_element_versions(self):
        base,a,b=self.pair()
        before=self.counts()
        plan=self.preview(a['head'],b['head'])
        self.assertEqual(before,self.counts())
        self.assertEqual(1,plan['unresolved'])
        self.assertEqual(base,plan['ancestor'])
        conflict=plan['conflicts'][0]
        self.assertEqual('a',conflict['element'])
        detail=self.call('merge_detail',leftRevision=a['head'],rightRevision=b['head'],previewId=plan['previewId'],conflictKey=conflict['key'])['mergeDetail']
        self.assertIn('x="0"',detail['ancestor']['text'])
        self.assertIn('x="10"',detail['left']['text'])
        self.assertIn('x="20"',detail['right']['text'])
        self.assertEqual(before,self.counts())

    def test_element_resolution_keeps_independent_changes_and_both_parent_nodes(self):
        _,a,b=self.pair()
        chosen=[dict(key='element:objects.svg#a',choice='left')]
        before=self.call('connect')['branches']
        result=self.commit_merge(a['head'],b['head'],chosen)
        self.assertEqual([a['head'],b['head']],self.repo.info('map',result['head'])[0])
        self.assertNotIn(result['head'],[a['head'],b['head']])
        self.assertIn('id="a" x="10"',self.contents(result['branch'])['objects.svg'])
        self.assertIn('id="b" x="30"',self.contents(result['branch'])['objects.svg'])
        self.assertTrue(all(self.repo.head('map',branch['name'])==branch['head'] for branch in before))

    def test_ancestor_merge_still_creates_new_two_parent_node(self):
        base,a,_=self.pair()
        old=self.repo.head('map','main')
        result=self.commit_merge(base,a['head'])
        self.assertEqual([base,a['head']],self.repo.info('map',result['head'])[0])
        self.assertNotEqual(a['head'],result['head'])
        self.assertEqual(old,self.repo.head('map','main'))

    def test_custom_element_edits_and_id_validation(self):
        _,a,b=self.pair()
        resolution=dict(key='element:objects.svg#a',choice='custom',parent='@root',content=base64.b64encode(b'<rect id="a" x="15"/>').decode())
        result=self.commit_merge(a['head'],b['head'],[resolution])
        self.assertIn('id="a" x="15"',self.contents(result['branch'])['objects.svg'])
        before=self.counts()
        for text in [b'<rect id="renamed"/>',b'<rect id="a"><rect id="extra"/></rect>',b'<!DOCTYPE rect><rect id="a"/>',b'<rect']:
            resolution['content']=base64.b64encode(text).decode()
            with self.assertRaises(ApiError):
                self.preview(a['head'],b['head'],[resolution])
        self.assertEqual(before,self.counts())

    def test_delete_modify_conflict_can_be_resolved_by_deletion(self):
        base=self.submit('<svg><rect id="a" x="0"/><rect id="b"/></svg>')['head']
        a=self.submit('<svg><rect id="b"/></svg>',base)
        b=self.submit('<svg><rect id="a" x="1"/><rect id="b"/></svg>',base)
        result=self.commit_merge(a['head'],b['head'],[dict(key='element:objects.svg#a',choice='delete')])
        self.assertNotIn('id="a"',self.contents(result['branch'])['objects.svg'])
        self.assertIn('id="b"',self.contents(result['branch'])['objects.svg'])

    def test_file_conflicts_can_choose_side_or_custom_text(self):
        base=self.push({'objects.svg':'<svg/>','settings.cfg':'base'})
        self.call('create_branch',newBranch='other')
        a=self.push({'objects.svg':'<svg/>','settings.cfg':'left'},head=base)
        b=self.push({'objects.svg':'<svg/>','settings.cfg':'right'},'other',base)
        for choice,content,expected in [('right','', 'right'),('base','', 'base'),('custom',base64.b64encode(b'resolved').decode(),'resolved')]:
            result=self.commit_merge(a,b,[dict(key='file:settings.cfg',choice=choice,content=content)])
            self.assertEqual(expected,self.contents(result['branch'])['settings.cfg'])

    def test_unresolved_or_wrong_preview_cannot_create_a_node(self):
        _,a,b=self.pair()
        before=self.counts()
        with self.assertRaises(ApiError) as caught:
            self.commit_merge(a['head'],b['head'])
        self.assertEqual(409,caught.exception.status)
        with self.assertRaises(ApiError):
            self.call('merge_commit',leftRevision=a['head'],rightRevision=b['head'],previewId='wrong',requestId=uuid.uuid4().hex)
        self.assertEqual(before,self.counts())

    def test_merge_commit_retry_is_idempotent(self):
        _,a,b=self.pair()
        choices=[dict(key='element:objects.svg#a',choice='right')]
        nonce=uuid.uuid4().hex
        first=self.commit_merge(a['head'],b['head'],choices,nonce)
        before=self.counts()
        again=self.commit_merge(a['head'],b['head'],choices,nonce)
        self.assertEqual(first['head'],again['head'])
        self.assertEqual(first['branch'],again['branch'])
        self.assertEqual(before,self.counts())
        with self.assertRaises(ApiError):
            self.commit_merge(a['head'],b['head'],[dict(key='element:objects.svg#a',choice='left')],nonce)
        self.assertEqual(before,self.counts())

    def test_merge_rejects_same_node_and_cross_project_revisions(self):
        _,a,b=self.pair()
        with self.assertRaises(ApiError):
            self.preview(a['head'],a['head'])
        self.config['users']['admin']={'token':'z'*32,'projects':['private']}
        with self.assertRaises(ApiError):
            self.repo.dispatch(dict(operation='merge_preview',project='private',branch='main',userId='admin',leftRevision=a['head'],rightRevision=b['head']),'z'*32)

    def test_invalid_whole_svg_resolution_and_size_limit_are_atomic(self):
        base=self.push({'objects.svg':'<svg/>','settings.cfg':'base'})
        self.call('create_branch',newBranch='other')
        a=self.push({'objects.svg':'<svg/>','settings.cfg':'left'},head=base)
        b=self.push({'objects.svg':'<svg/>','settings.cfg':'right'},'other',base)
        before=self.counts()
        with self.assertRaises(ApiError):
            self.commit_merge(a,b,[dict(key='file:settings.cfg',choice='custom',content=base64.b64encode(b'x'*1500).decode())])
        self.assertEqual(before,self.counts())

    def test_added_file_path_collisions_are_resolvable_in_preview(self):
        for first,second in [('Name.txt','name.txt'),('folder','folder/nested.txt')]:
            with self.subTest(first=first):
                base=self.push({'objects.svg':'<svg/>'},head=self.repo.head('map','main'))
                branch='case'+str(len(self.call('connect')['branches']))
                self.call('create_branch',newBranch=branch)
                a=self.push({'objects.svg':'<svg/>',first:'ours'},head=base)
                b=self.push({'objects.svg':'<svg/>',second:'theirs'},branch,base)
                preview=self.preview(a,b)
                self.assertEqual(2,preview['unresolved'])
                choices=[dict(key='file:'+first,choice='left'),dict(key='file:'+second,choice='left')]
                result=self.commit_merge(a,b,choices)
                self.assertIn(first,self.contents(result['branch']))
                self.assertNotIn(second,self.contents(result['branch']))

    def test_deleting_a_conflicted_container_requires_explicit_structural_resolution(self):
        base=self.submit('<svg><g id="layer" opacity="1"><rect id="a"/></g></svg>')['head']
        a=self.submit('<svg/>',base)
        b=self.submit('<svg><g id="layer" opacity="0.5"><rect id="a" x="1"/></g></svg>',base)
        choices=[dict(key='element:objects.svg#layer',choice='delete'),dict(key='element:objects.svg#a',choice='right')]
        plan=self.preview(a['head'],b['head'],choices)
        self.assertEqual(1,plan['unresolved'])
        self.assertTrue(any(c['kind']=='structure' and not c['resolved'] for c in plan['conflicts']))
        with self.assertRaises(ApiError):
            self.commit_merge(a['head'],b['head'],choices)
        choices.append(dict(key='structure:objects.svg',choice='left'))
        result=self.commit_merge(a['head'],b['head'],choices)
        self.assertNotIn('id="layer"',self.contents(result['branch'])['objects.svg'])

    def test_empty_custom_text_is_a_valid_file_resolution(self):
        base=self.push({'objects.svg':'<svg/>','settings.cfg':'base'})
        self.call('create_branch',newBranch='other')
        a=self.push({'objects.svg':'<svg/>','settings.cfg':'left'},head=base)
        b=self.push({'objects.svg':'<svg/>','settings.cfg':'right'},'other',base)
        result=self.commit_merge(a,b,[dict(key='file:settings.cfg',choice='custom',content='')])
        self.assertEqual('',self.contents(result['branch'])['settings.cfg'])

    def test_png_conflict_detail_contains_visual_previews(self):
        def png(red):
            def chunk(kind,data):
                return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
            return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',1,1,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(bytes([0,red,0,0,255])))+chunk(b'IEND',b'')
        def push(head,branch,color):
            return self.call('push',branch,expectedHead=head,files=[dict(path='objects.svg',content=base64.b64encode(b'<svg/>').decode()),dict(path='road.png',content=base64.b64encode(png(color)).decode())])['head']
        base=push('','main',0)
        self.call('create_branch',newBranch='other')
        a=push(base,'main',30)
        b=push(base,'other',60)
        plan=self.preview(a,b)
        detail=self.call('merge_detail',leftRevision=a,rightRevision=b,previewId=plan['previewId'],conflictKey='file:road.png')['mergeDetail']
        self.assertEqual(png(30),base64.b64decode(detail['left']['image']))
        result=self.commit_merge(a,b,[dict(key='file:road.png',choice='right')])
        files=self.call('pull',result['branch'])['files']
        self.assertEqual(png(60),base64.b64decode(next(f['content'] for f in files if f['path']=='road.png')))
