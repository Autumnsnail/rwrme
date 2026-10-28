"""RWRME masterserver: authenticated, transactional branch/snapshot repository."""
import argparse
import base64
import hashlib
import hmac
import json
import os
import re
import sqlite3
import threading
import time
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from svg_merge import SvgConflict, merge_svg
from merge_plan import MergeError, MergePlan


class ApiError(Exception):
    def __init__(self, message, status=400, conflicts=None):
        super().__init__(message)
        self.status, self.conflicts = status, conflicts or []


def valid_name(value):
    return isinstance(value, str) and re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]{0,63}", value) is not None


def valid_branch(value):
    return (isinstance(value, str) and len(value) <= 128
            and all(valid_name(p) and p not in ('.', '..') and p.split('.')[0].upper() not in {'CON', 'PRN', 'AUX', 'NUL', *('COM' + str(n) for n in range(1, 10)), *('LPT' + str(n) for n in range(1, 10))}
                    for p in value.split('/')))


def valid_path(value):
    if not isinstance(value, str) or not value or len(value) > 240:
        return False
    return ('\\' not in value and not value.lower().endswith('.meta')
            and all(p and not p.startswith('.') and not p.endswith(('.', ' '))
                    and not any(c in p for c in ':<>"|?*') and not any(ord(c) < 32 for c in p)
                    and p.split('.')[0].upper() not in {'CON', 'PRN', 'AUX', 'NUL', *('COM' + str(n) for n in range(1, 10)), *('LPT' + str(n) for n in range(1, 10))}
                    for p in value.split('/')))


class Repository:
    def __init__(self, database, config):
        self.config, self.lock = config, threading.RLock()
        self.db = sqlite3.connect(database, check_same_thread=False)
        self.db.execute('PRAGMA journal_mode=WAL')
        self.db.executescript('''
          CREATE TABLE IF NOT EXISTS branches(project TEXT,name TEXT,head TEXT NOT NULL,PRIMARY KEY(project,name));
          CREATE TABLE IF NOT EXISTS commits(id TEXT PRIMARY KEY,project TEXT,parents TEXT,tree TEXT,author TEXT,message TEXT,created REAL);
          CREATE TABLE IF NOT EXISTS blobs(id TEXT PRIMARY KEY,content BLOB);
          CREATE TABLE IF NOT EXISTS merge_results(project TEXT,request_id TEXT,author TEXT,left_revision TEXT,right_revision TEXT,
            signature TEXT,head TEXT,branch TEXT,PRIMARY KEY(project,request_id));
        ''')
        if 'created_from' not in {row[1] for row in self.db.execute('PRAGMA table_info(branches)')}:
            self.db.execute("ALTER TABLE branches ADD COLUMN created_from TEXT NOT NULL DEFAULT ''")
        for project in config['projects']:
            if not valid_name(project):
                raise ValueError('Invalid project ID')
            self.db.execute("INSERT OR IGNORE INTO branches(project,name,head) VALUES(?,'main','')", (project,))
        self.db.commit()

    def authenticate(self, body, token):
        user, project = body.get('userId', ''), body.get('project', '')
        account = self.config.get('users', {}).get(user)
        if not valid_name(user) or not account or not token or not hmac.compare_digest(token, account.get('token', '')):
            raise ApiError('User ID and token are required and must match', 401)
        if project not in self.config['projects'] or project not in account.get('projects', []):
            raise ApiError('Project access denied', 403)
        return user, project

    def head(self, project, branch):
        if not valid_branch(branch):
            raise ApiError('Invalid branch name')
        row = self.db.execute('SELECT head FROM branches WHERE project=? AND name=?', (project, branch)).fetchone()
        if row is None:
            raise ApiError('Branch does not exist', 404)
        return row[0]

    def info(self, project, revision):
        if not revision:
            return [], {}
        row = self.db.execute('SELECT parents,tree FROM commits WHERE project=? AND id=?', (project, revision)).fetchone()
        if row is None:
            raise ApiError('Unknown project revision', 404)
        return json.loads(row[0]), json.loads(row[1])

    def resolve_revision(self, project, value, default):
        if value is None or value == '':
            return default
        if not isinstance(value, str) or not re.fullmatch(r'[0-9a-f]{64}', value):
            raise ApiError('Invalid revision ID')
        self.info(project, value)
        return value

    def commit(self, project, branch, parents, tree, author, message):
        created = time.time()
        revision = hashlib.sha256(json.dumps([project, parents, tree, author, message, created], sort_keys=True).encode()).hexdigest()
        self.db.execute('INSERT INTO commits VALUES(?,?,?,?,?,?,?)',
                        (revision, project, json.dumps(parents), json.dumps(tree), author, message, created))
        self.db.execute('UPDATE branches SET head=? WHERE project=? AND name=?', (revision, project, branch))
        return revision

    def submission_target(self, project, branch, base, user, force_new=False):
        # A direct child proves there is downstream history, even when a stale
        # branch reference still points at base. SHA IDs are matched as JSON tokens.
        if base:
            downstream = self.db.execute('SELECT 1 FROM commits WHERE project=? AND instr(parents,?)>0 LIMIT 1',
                                         (project, json.dumps(base))).fetchone() is not None
        else:
            downstream = self.db.execute('SELECT 1 FROM commits WHERE project=? LIMIT 1', (project,)).fetchone() is not None
        if not downstream and not force_new:
            if self.head(project, branch) == base:
                return branch, False
            tip = self.db.execute('SELECT name FROM branches WHERE project=? AND head=? ORDER BY name LIMIT 1',
                                  (project, base)).fetchone()
            if tip:
                return tip[0], False
        # Naming is an internal detail. Prefix the user segment so reserved Windows
        # names and long, otherwise valid user IDs also produce valid branch names.
        for _ in range(8):
            name = 'auto/u-' + user[:48] + '/' + uuid.uuid4().hex
            try:
                self.db.execute('INSERT INTO branches(project,name,head,created_from) VALUES(?,?,?,?)',
                                (project, name, base, branch))
                return name, downstream
            except sqlite3.IntegrityError:
                continue
        raise ApiError('Could not allocate a workline', 503)

    def ancestors(self, project, revision):
        result, queue = {revision: 0}, [revision]
        for item in queue:
            for parent in self.info(project, item)[0]:
                if parent not in result:
                    result[parent] = result[item] + 1
                    queue.append(parent)
        return result

    def validate_tree(self, tree):
        if len(tree) > 4096:
            raise ApiError('Snapshot exceeds file limit', 413)
        total = sum(self.db.execute('SELECT LENGTH(content) FROM blobs WHERE id=?', (digest,)).fetchone()[0]
                    for digest in tree.values())
        if total > self.config.get('max_snapshot_bytes', 64 * 1024 * 1024):
            raise ApiError('Merged snapshot exceeds size limit', 413)
        names = {p.casefold() for p in tree}
        for path in names:
            parts = path.split('/')
            if any('/'.join(parts[:i]) in names for i in range(1, len(parts))):
                raise ApiError('File/directory path conflict', 409, [path])

    def node_merge(self, project, branch, user, operation, body):
        try:
            left = self.resolve_revision(project, body.get('leftRevision'), '')
            right = self.resolve_revision(project, body.get('rightRevision'), '')
            resolutions = body.get('resolutions') or []
            message = str(body.get('message') or 'Merge nodes')[:1024]
            nonce = body.get('requestId', '')
            fingerprint = hashlib.sha256(json.dumps([left, right, resolutions, message], sort_keys=True).encode()).hexdigest()
            if operation == 'merge_commit':
                if not isinstance(nonce, str) or not re.fullmatch('[0-9a-f]{32}', nonce):
                    raise MergeError('A unique merge requestId is required')
                saved = self.db.execute('SELECT author,left_revision,right_revision,signature,head,branch FROM merge_results '
                                        'WHERE project=? AND request_id=?', (project, nonce)).fetchone()
                if saved:
                    if saved[:4] != (user, left, right, fingerprint):
                        raise ApiError('This merge request was already used with different inputs', 409)
                    return dict(head=saved[4], branch=saved[5])
            plan = MergePlan(self, project, left, right, resolutions)
            if operation in ('merge_commit', 'merge_detail') and body.get('previewId') != plan.preview_id:
                raise ApiError('Refresh the merge preview before continuing', 409)
            if operation == 'merge_preview':
                if not any(not c['resolved'] for c in plan.conflicts):
                    plan.validated_files()
                return dict(merge=plan.public())
            if operation == 'merge_detail':
                return dict(mergeDetail=plan.detail(body.get('conflictKey')))
            if any(not c['resolved'] for c in plan.conflicts):
                raise ApiError('Resolve every conflict before creating the merge node', 409,
                               [c['key'] for c in plan.conflicts if not c['resolved']])
            files = plan.validated_files()
            tree = {}
            for path, content in files.items():
                digest = hashlib.sha256(content).hexdigest()
                self.db.execute('INSERT OR IGNORE INTO blobs VALUES(?,?)', (digest, content))
                tree[path] = digest
            self.validate_tree(tree)
            # Always create a new workline and a two-parent commit. Even an ancestor
            # merge never fast-forwards, rewrites, or deletes either input node.
            target, _ = self.submission_target(project, branch, left, user, force_new=True)
            head = self.commit(project, target, [left, right], tree, user, message)
            self.db.execute('INSERT INTO merge_results VALUES(?,?,?,?,?,?,?,?)',
                            (project, nonce, user, left, right, fingerprint, head, target))
            return dict(head=head, branch=target, merge=plan.public())
        except MergeError as error:
            raise ApiError(str(error))

    def dispatch(self, body, token):
        user, project = self.authenticate(body, token)
        branch, operation = body.get('branch', 'main'), body.get('operation')
        with self.lock, self.db:
            head = self.head(project, branch)
            result = dict(success=True, head=head)
            if operation == 'connect':
                result['branches'] = [dict(name=n, head=h, parent=p) for n, h, p in self.db.execute(
                    'SELECT name,head,created_from FROM branches WHERE project=? ORDER BY name', (project,))]
            elif operation == 'history':
                try:
                    limit = int(body.get('limit', 200))
                    offset = int(body.get('cursor') or 0)
                except (ValueError, TypeError):
                    raise ApiError('Invalid history pagination')
                if not 1 <= limit <= 500 or not 0 <= offset <= 10000000:
                    raise ApiError('Invalid history pagination')
                rows = self.db.execute('SELECT id,parents,author,message,created FROM commits '
                    'WHERE project=? ORDER BY created DESC,id DESC LIMIT ? OFFSET ?', (project, limit + 1, offset)).fetchall()
                result['commits'] = [dict(id=revision, parents=[p for p in json.loads(parents) if p],
                    author=author, message=message, created=created) for revision, parents, author, message, created in rows[:limit]]
                result['nextCursor'] = str(offset + limit) if len(rows) > limit else ''
                result['branches'] = [dict(name=n, head=h, parent=p) for n, h, p in self.db.execute(
                    'SELECT name,head,created_from FROM branches WHERE project=? ORDER BY name', (project,))]
            elif operation == 'create_branch':
                revision = self.resolve_revision(project, body.get('revision'), head)
                name = body.get('newBranch', '')
                if not valid_branch(name):
                    raise ApiError('Invalid new branch name')
                try:
                    self.db.execute('INSERT INTO branches(project,name,head,created_from) VALUES(?,?,?,?)', (project, name, revision, branch))
                except sqlite3.IntegrityError:
                    raise ApiError('Branch already exists', 409)
            elif operation == 'pull':
                revision = self.resolve_revision(project, body.get('revision'), head)
                result['revision'] = revision
                tree = self.info(project, revision)[1]
                result['files'] = [dict(path=p, content=base64.b64encode(self.db.execute(
                    'SELECT content FROM blobs WHERE id=?', (digest,)).fetchone()[0]).decode()) for p, digest in sorted(tree.items())]
            elif operation in ('push', 'submit'):
                if operation == 'push' and body.get('expectedHead') != head:
                    raise ApiError('Remote branch changed. Pull or merge before pushing', 409)
                base = head
                if operation == 'submit':
                    value = body.get('baseRevision')
                    if not isinstance(value, str):
                        raise ApiError('An explicit baseRevision is required for submit')
                    base = self.resolve_revision(project, value, '')
                files = body.get('files')
                if not isinstance(files, list) or not files or len(files) > 4096:
                    raise ApiError('Snapshot must contain 1 to 4096 files')
                tree, names, total = {}, set(), 0
                for entry in files:
                    path = entry.get('path')
                    if not valid_path(path) or path.casefold() in names:
                        raise ApiError('Invalid or duplicate snapshot path')
                    names.add(path.casefold())
                    try:
                        content = base64.b64decode(entry['content'], validate=True)
                    except (ValueError, KeyError, TypeError):
                        raise ApiError('Invalid base64 content')
                    total += len(content)
                    if total > self.config.get('max_snapshot_bytes', 64 * 1024 * 1024):
                        raise ApiError('Snapshot exceeds size limit', 413)
                    digest = hashlib.sha256(content).hexdigest()
                    self.db.execute('INSERT OR IGNORE INTO blobs VALUES(?,?)', (digest, content))
                    tree[path] = digest
                self.validate_tree(tree)
                if operation == 'submit':
                    branch, forked = self.submission_target(project, branch, base, user)
                    result.update(branch=branch, forked=forked, baseRevision=base)
                result['head'] = self.commit(project, branch, [base], tree, user, str(body.get('message', ''))[:1024])
            elif operation in ('merge_preview', 'merge_detail', 'merge_commit'):
                result.update(self.node_merge(project, branch, user, operation, body))
            elif operation == 'merge':
                if body.get('expectedHead') != head:
                    raise ApiError('Target branch changed', 409)
                source = self.head(project, body.get('sourceBranch', ''))
                ours, theirs = self.ancestors(project, head), self.ancestors(project, source)
                if source in ours:
                    return result
                if head in theirs:
                    self.db.execute('UPDATE branches SET head=? WHERE project=? AND name=?', (source, project, branch))
                    result['head'] = source
                    return result
                common = set(ours) & set(theirs)
                if not common:
                    raise ApiError('No common ancestor', 409)
                base = min(common, key=lambda c: (ours[c] + theirs[c], c))
                original, left, right = (self.info(project, c)[1] for c in (base, head, source))
                merged, conflicts = {}, []
                for path in sorted(set(original) | set(left) | set(right)):
                    before, a, b = original.get(path), left.get(path), right.get(path)
                    if a == b or b == before:
                        chosen = a
                    elif a == before:
                        chosen = b
                    elif path.lower() == 'objects.svg' and a is not None and b is not None:
                        def content(digest):
                            return self.db.execute('SELECT content FROM blobs WHERE id=?', (digest,)).fetchone()[0]
                        try:
                            combined = merge_svg(content(before) if before else b'<svg xmlns="http://www.w3.org/2000/svg"/>', content(a), content(b))
                        except SvgConflict as error:
                            conflicts.extend(path if element == '@invalid-svg' else path + '#' + element for element in error.elements)
                            continue
                        chosen = hashlib.sha256(combined).hexdigest()
                        self.db.execute('INSERT OR IGNORE INTO blobs VALUES(?,?)', (chosen, combined))
                    else:
                        conflicts.append(path)
                        continue
                    if chosen is not None:
                        merged[path] = chosen
                folded = [p.casefold() for p in merged]
                if len(folded) != len(set(folded)):
                    conflicts += list(merged)
                if conflicts:
                    raise ApiError('Merge conflicts: ' + ', '.join(conflicts), 409, conflicts)
                self.validate_tree(merged)
                result['head'] = self.commit(project, branch, [head, source], merged, user, 'Merge ' + body['sourceBranch'])
            else:
                raise ApiError('Unknown operation')
            return result


def make_handler(repository):
    class Handler(BaseHTTPRequestHandler):
        def reply(self, status, payload):
            data = json.dumps(payload).encode()
            self.send_response(status)
            self.send_header('Content-Type', 'application/json; charset=utf-8')
            self.send_header('Content-Length', str(len(data)))
            self.end_headers()
            self.wfile.write(data)

        def do_GET(self):
            self.reply(200 if self.path == '/health' else 404, dict(success=self.path == '/health'))

        def do_POST(self):
            if self.path != '/v1/repository':
                self.reply(404, dict(success=False, error='Not found'))
                return
            try:
                length = int(self.headers.get('Content-Length', '0'))
                maximum = repository.config.get('max_snapshot_bytes', 64 * 1024 * 1024) * 2 + 1048576
                if length <= 0 or length > maximum:
                    raise ApiError('Invalid request size', 413)
                self.connection.settimeout(30)
                body = json.loads(self.rfile.read(length))
                if not isinstance(body, dict):
                    raise ApiError('Expected JSON object')
                auth = self.headers.get('Authorization', '')
                self.reply(200, repository.dispatch(body, auth[7:] if auth.startswith('Bearer ') else ''))
            except ApiError as error:
                self.reply(error.status, dict(success=False, error=str(error), conflicts=error.conflicts))
            except (ValueError, TypeError, AttributeError, KeyError):
                self.reply(400, dict(success=False, error='Malformed request'))
            except Exception:
                self.log_error('Repository request failed')
                self.reply(500, dict(success=False, error='Internal server error'))
    return Handler


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', default='config.json')
    parser.add_argument('--database', default='data/repository.sqlite3')
    parser.add_argument('--host', default='127.0.0.1')
    parser.add_argument('--port', type=int, default=8090)
    args = parser.parse_args()
    with open(args.config, encoding='utf-8-sig') as handle:
        config = json.load(handle)
    if not config.get('users') or any(len(u.get('token', '')) < 32 or u['token'].startswith('REPLACE_') for u in config['users'].values()):
        parser.error('Configure users with independently generated random tokens of at least 32 characters')
    os.makedirs(os.path.dirname(os.path.abspath(args.database)), exist_ok=True)
    repo = Repository(args.database, config)
    server = ThreadingHTTPServer((args.host, args.port), make_handler(repo))
    print(f'RWRME masterserver listening on {args.host}:{args.port}', flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
        repo.db.close()


if __name__ == '__main__':
    main()
