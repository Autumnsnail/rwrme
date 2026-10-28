"""Read-only node merge plans and explicit, ID-scoped conflict resolutions."""
import base64
import copy
import hashlib
import json
import re
import struct
import xml.etree.ElementTree as ET
from svg_merge import Document, Node, SvgConflict, canonical, local, merge_svg, normalize, signature, INLINE


class MergeError(ValueError):
    pass


def decoded(resolution):
    try:
        value = base64.b64decode(resolution.get('content', ''), validate=True)
    except (ValueError, TypeError):
        raise MergeError('Invalid custom resolution encoding')
    if len(value) > 1024 * 1024:
        raise MergeError('Custom text resolution exceeds 1 MiB')
    return value


def choose(resolution, values):
    choice = resolution.get('choice')
    if choice in ('base', 'left', 'right'):
        return values[('base', 'left', 'right').index(choice)]
    if choice == 'delete':
        return None
    if choice == 'custom':
        return decoded(resolution)
    raise MergeError('Choose base, left, right, delete or custom')


def custom_node(key, resolution, values):
    chosen = choose(resolution, values)
    if not isinstance(chosen, bytes):
        if key == '@root' and chosen is None:
            raise MergeError('The SVG root cannot be deleted')
        return chosen
    if re.search(br'<!\s*(DOCTYPE|ENTITY)\b', chosen, re.I):
        raise MergeError('DTD and entities are not supported')
    try:
        element = ET.fromstring(chosen)
    except (ET.ParseError, ValueError):
        raise MergeError('Custom element must contain valid XML')
    normalize(element)
    if key == '@root':
        if local(element.tag) != 'svg':
            raise MergeError('SVG root must remain svg')
    elif key.startswith('@anonymous:'):
        if element.get('id'):
            raise MergeError('Anonymous container identity cannot change')
    elif element.get('id') != key:
        raise MergeError('Keep the original SVG element ID: ' + key)
    if any(local(child.tag) not in INLINE for child in element):
        raise MergeError('Edit this element only; its named children are merged separately')
    reference = next((node for node in values if node is not None), None)
    parent = resolution.get('parent', reference.parent if reference else '@root')
    if key == '@root':
        parent = None
    if parent == key:
        raise MergeError('An element cannot be its own parent')
    return Node(element, parent, [], (parent, canonical(element), element.tail))


def value_info(value, include_content=False):
    node = value if isinstance(value, Node) else None
    raw = ET.tostring(node.element, encoding='utf-8') if node else value
    fields = []
    if node:
        fields = [dict(name=k, value=v) for k, v in sorted(node.element.attrib.items())]
        fields.insert(0, dict(name='layer', value=node.parent or '(root)'))
        fields.insert(0, dict(name='type', value=local(node.element.tag)))
    result = dict(exists=raw is not None, bytes=len(raw or b''), text='', image='', editable=False,
                  parent=node.parent if node and node.parent else '', fields=fields, digest='')
    if raw is None:
        return result
    result['digest'] = hashlib.sha256(raw).hexdigest()[:12]
    if len(raw) <= 256 * 1024:
        try:
            text = raw.decode('utf-8-sig')
            if '\0' not in text:
                result.update(text=text, editable=True)
        except UnicodeError:
            pass
    if include_content and raw.startswith(b'\x89PNG\r\n\x1a\n') and len(raw) <= 4 * 1024 * 1024 and len(raw) >= 24:
        width, height = struct.unpack('>II', raw[16:24])
        if width > 0 and height > 0 and width * height <= 16 * 1024 * 1024:
            result['image'] = base64.b64encode(raw).decode()
    return result


class MergePlan:
    def __init__(self, repository, project, left, right, resolutions):
        self.repo, self.project, self.left, self.right = repository, project, left, right
        if not left or not right or left == right:
            raise MergeError('Select two different existing nodes')
        self.repo.info(project, left)
        self.repo.info(project, right)
        ancestors = [repository.ancestors(project, rev) for rev in (left, right)]
        common = set(ancestors[0]) & set(ancestors[1])
        if not common:
            raise MergeError('These nodes have no common ancestor')
        # Prefer a concrete revision over the virtual empty root on distance ties.
        self.base = min(common, key=lambda rev: (ancestors[0][rev] + ancestors[1][rev], not bool(rev), rev))
        self.preview_id = hashlib.sha256(json.dumps(['node-merge-v1', project, left, right, self.base]).encode()).hexdigest()
        if resolutions is None:
            resolutions = []
        if not isinstance(resolutions, list) or len(resolutions) > 16384:
            raise MergeError('Invalid conflict resolutions')
        self.choices = {}
        for resolution in resolutions:
            if not isinstance(resolution, dict) or not isinstance(resolution.get('key'), str) or resolution['key'] in self.choices:
                raise MergeError('Conflict resolutions need unique keys')
            self.choices[resolution['key']] = resolution
        self.trees = [repository.info(project, rev)[1] for rev in (self.base, left, right)]
        self.files, self.conflicts, self.values = {}, [], {}
        self.automatic_files = 0
        for path in sorted(set().union(*self.trees)):
            values = tuple(self.content(tree.get(path)) for tree in self.trees)
            before, a, b = values
            if a == b or b == before:
                chosen = a
            elif a == before:
                chosen = b
            elif path.lower() == 'objects.svg' and a is not None and b is not None:
                chosen = self.svg(path, (before or b'<svg xmlns="http://www.w3.org/2000/svg"/>', a, b))
            else:
                key = 'file:' + path
                chosen = self.conflict(key, path, '', 'file', values)
            if chosen is not None:
                self.files[path] = chosen
            if not any(c['path'] == path for c in self.conflicts):
                self.automatic_files += 1
        # Independently added file names can still alias on Windows or collide
        # as file/directory paths. Expose both entries as explicit decisions.
        paths = list(self.files)
        collisions = set()
        for i, path in enumerate(paths):
            folded = path.casefold()
            for other in paths[i+1:]:
                other_folded = other.casefold()
                if folded == other_folded or folded.startswith(other_folded + '/') or other_folded.startswith(folded + '/'):
                    collisions.update((path, other))
        for path in sorted(collisions):
            if any(c['path'] == path for c in self.conflicts):
                continue
            values = tuple(self.content(tree.get(path)) for tree in self.trees)
            chosen = self.conflict('file:' + path, path, '', 'file', values)
            if chosen is None:
                self.files.pop(path, None)
            else:
                self.files[path] = chosen
            self.automatic_files = max(0, self.automatic_files - 1)
        allowed_prefixes = {'file:' + p for p in set().union(*self.trees)} | {'structure:objects.svg'}
        if any(key not in allowed_prefixes and not key.startswith('element:objects.svg#') for key in self.choices):
            raise MergeError('Unknown conflict key')

    def content(self, digest):
        return self.repo.db.execute('SELECT content FROM blobs WHERE id=?', (digest,)).fetchone()[0] if digest else None

    def conflict(self, key, path, element, kind, values):
        resolution = self.choices.get(key)
        self.conflicts.append(dict(key=key, path=path, element=element, kind=kind,
                                   resolved=resolution is not None, choice=resolution.get('choice', '') if resolution else ''))
        self.values[key] = values
        return choose(resolution, values) if resolution else None

    def svg(self, path, values):
        try:
            docs = [Document(value) for value in values]
        except SvgConflict:
            return self.conflict('structure:' + path, path, '', 'structure', values)
        overrides = {}
        for key in sorted(set().union(*(doc.nodes for doc in docs))):
            nodes = tuple(doc.nodes.get(key) for doc in docs)
            before, a, b = map(signature, nodes)
            if a == b or b == before or a == before:
                continue
            identity = 'element:' + path + '#' + key
            resolution = self.choices.get(identity)
            self.conflicts.append(dict(key=identity, path=path, element=key, kind='element',
                                       resolved=resolution is not None, choice=resolution.get('choice', '') if resolution else ''))
            self.values[identity] = nodes
            if resolution:
                overrides[key] = custom_node(key, resolution, nodes)
        try:
            return merge_svg(*values, overrides=overrides)
        except SvgConflict as error:
            missing = {c['element'] for c in self.conflicts if c['path'] == path and not c['resolved']}
            if set(error.elements) <= missing:
                return None
            # Structural/order conflicts need a whole-document decision. This is
            # explicitly distinguished in the UI from an individual element choice.
            return self.conflict('structure:' + path, path, '', 'structure', values)

    def public(self):
        return dict(left=self.left, right=self.right, ancestor=self.base, previewId=self.preview_id,
                    unresolved=sum(not c['resolved'] for c in self.conflicts), conflicts=self.conflicts,
                    automaticFiles=self.automatic_files, fileCount=len(set().union(*self.trees)))

    def detail(self, key):
        conflict = next((c for c in self.conflicts if c['key'] == key), None)
        if conflict is None:
            raise MergeError('Unknown merge conflict')
        values = [value_info(value, True) for value in self.values[key]]
        return dict(key=key, ancestor=values[0], left=values[1], right=values[2],
                    canEdit=any(v['editable'] for v in values),
                    canDelete=(conflict['kind'] == 'element' and conflict['element'] != '@root')
                    or (conflict['kind'] != 'element' and conflict['path'].lower() != 'objects.svg'))

    def validated_files(self):
        if any(not c['resolved'] for c in self.conflicts):
            raise MergeError('Resolve every conflict before creating the merge node')
        if 'objects.svg' not in self.files:
            raise MergeError('The merged map must include objects.svg')
        try:
            Document(self.files['objects.svg'])
        except SvgConflict:
            raise MergeError('The resolved SVG is invalid or contains duplicate IDs')
        folded = [path.casefold() for path in self.files]
        if len(set(folded)) != len(folded):
            raise MergeError('Case-insensitive file path collision')
        if len(self.files) > 4096 or sum(len(content) for content in self.files.values()) > self.repo.config.get('max_snapshot_bytes', 64 * 1024 * 1024):
            raise MergeError('Resolved map exceeds the snapshot size limit')
        for path in self.files:
            parts = path.casefold().split('/')
            if any('/'.join(parts[:i]) in set(folded) for i in range(1,len(parts))):
                raise MergeError('File/directory path collision')
        return self.files
