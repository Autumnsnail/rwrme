"""Three-way SVG merge by stable element ID, independent of XML formatting."""
import copy
import difflib
import heapq
import io
import re
import xml.etree.ElementTree as ET
from dataclasses import dataclass

SVG = 'http://www.w3.org/2000/svg'
INKSCAPE = 'http://www.inkscape.org/namespaces/inkscape'
LABEL = '{' + INKSCAPE + '}label'
INLINE = {'desc', 'title', 'metadata'}
CONTAINERS = {'svg', 'g', 'defs', 'symbol', 'clipPath', 'mask', 'pattern'}


class SvgConflict(Exception):
    def __init__(self, elements):
        self.elements = sorted(set(elements))
        super().__init__('SVG element conflicts: ' + ', '.join(self.elements))


def local(tag):
    return tag.rsplit('}', 1)[-1] if isinstance(tag, str) else ''


def text_value(element, value):
    if value is None:
        return ''
    if local(element.tag) in {'text', 'tspan', 'desc', 'title', 'style', 'script'} or element.get('{http://www.w3.org/XML/1998/namespace}space') == 'preserve':
        return value
    return value if value.strip() else ''


def canonical(element):
    """Attribute order, indentation and namespace prefixes are not edits."""
    return (element.tag, tuple(sorted(element.attrib.items())), text_value(element, element.text),
            tuple((canonical(child), text_value(element, child.tail)) for child in element))


def normalize(root):
    # Older exporters generated these IDs/labels from list indexes. They are
    # presentation details, not independent objects. Canonicalize by owner ID.
    for element in root.iter():
        # XmlDocument exports some inserted nodes with xmlns="". The existing
        # Unity importer treats these as SVG too; normalize that legacy form.
        if isinstance(element.tag, str) and '}' not in element.tag and element.tag in {
                'svg', 'g', 'rect', 'path', 'circle', 'ellipse', 'line', 'polyline', 'polygon',
                'text', 'tspan', 'desc', 'title', 'defs', 'style', 'use', 'image', 'symbol',
                'clipPath', 'mask', 'pattern', 'linearGradient', 'radialGradient', 'stop'}:
            element.tag = '{' + SVG + '}' + element.tag
        identity = element.get('id', '')
        if local(element.tag) == 'g':
            paths = [child for child in element if local(child.tag) == 'path']
            if len(paths) == 2:
                start, end = paths
                key = end.get('id', '')
                if key.startswith('platform') and start.get('id') == key + '_s':
                    identity = 'group_' + key
                    element.set('id', identity)
        if not identity:
            continue
        label = element.get(LABEL, '')
        if identity.startswith('building') and re.fullmatch(r'#rect6406\d+', label):
            element.set(LABEL, '#rect_' + identity)
        elif identity.startswith('base') and re.fullmatch(r'#rect\d+_base', label):
            element.set(LABEL, '#rect_' + identity + '_base')
        counters = {}
        for child in element:
            kind = local(child.tag)
            if kind in {'desc', 'title'}:
                number = counters.get(kind, 0)
                counters[kind] = number + 1
                if 'id' in child.attrib:
                    child.set('id', kind + '_' + identity + (('_' + str(number)) if number else ''))


@dataclass
class Node:
    element: ET.Element
    parent: str | None
    children: list
    signature: tuple


class Document:
    def __init__(self, content):
        if re.search(br'<!\s*(DOCTYPE|ENTITY)\b', content, re.I):
            raise SvgConflict(['@invalid-svg'])
        try:
            self.namespaces = {}
            for _, (prefix, uri) in ET.iterparse(io.BytesIO(content), events=['start-ns']):
                self.namespaces.setdefault(prefix, uri)
            parser = ET.XMLParser(target=ET.TreeBuilder(insert_comments=True, insert_pis=True))
            root = ET.fromstring(content, parser=parser)
        except (ET.ParseError, ValueError, UnicodeError):
            raise SvgConflict(['@invalid-svg'])
        if root.tag not in ('svg', '{' + SVG + '}svg'):
            raise SvgConflict(['@invalid-svg'])
        normalize(root)
        self.nodes = {}
        self.order = []
        self._addressable = {}
        # Named graphics and containers are separate records. Description/title
        # children belong to their owner so changing a description conflicts at
        # the mapitem ID, never at a generated desc123 ID.
        for node in reversed(list(root.iter())):
            self._addressable[node] = (local(node.tag) not in INLINE and
                (bool(node.get('id')) or any(self._addressable[c] for c in node)))
        self._visit(root, '@root', None)

    def _visit(self, element, key, parent, tail=None):
        if key in self.nodes:
            raise SvgConflict([key])
        shell = copy.copy(element)
        shell[:] = []
        shell.text = text_value(element, element.text) or None
        shell.tail = tail
        children = []
        counts = {}
        self.nodes[key] = None  # Reserve the ID before descending.
        self.order.append(key)
        for child in element:
            if not self._addressable[child]:
                inline = copy.deepcopy(child)
                inline.tail = text_value(element, child.tail) or None
                shell.append(inline)
                continue
            identity = child.get('id')
            if identity:
                child_key = identity
            else:
                label = (child.tag, child.get(LABEL, ''))
                occurrence = counts.get(label, 0)
                counts[label] = occurrence + 1
                child_key = '@anonymous:' + key + ':' + repr(label) + ':' + str(occurrence)
            children.append(child_key)
            self._visit(child, child_key, key, text_value(element, child.tail) or None)
        self.nodes[key] = Node(shell, parent, children, (parent, canonical(shell), shell.tail))


def signature(node):
    return None if node is None else node.signature


def ordered_children(parent, members, documents):
    """Merge insertions and independent moves without comparing whole child lists."""
    sequences = []
    for document in documents:
        record = document.nodes.get(parent)
        sequences.append([key for key in record.children if key in members] if record else [])
    base, ours, theirs = sequences
    base_set = set(base)
    moved_by_side = []
    for side in (ours, theirs):
        present = set(side)
        previous = [key for key in base if key in present]
        current = [key for key in side if key in base_set]
        stable = set()
        for block in difflib.SequenceMatcher(a=previous, b=current, autojunk=False).get_matching_blocks():
            stable.update(previous[block.a:block.a + block.size])
        moved_by_side.append(set(current) - stable)
    moved = set.union(*moved_by_side)
    edges = {key: set() for key in members}
    incoming = dict.fromkeys(members, 0)

    def edge(a, b):
        if b not in edges[a]:
            edges[a].add(b)
            incoming[b] += 1

    stable = [key for key in base if key not in moved]
    for a, b in zip(stable, stable[1:]):
        edge(a, b)
    for sequence, changed in zip((ours, theirs), moved_by_side):
        for a, b in zip(sequence, sequence[1:]):
            if a in changed or b in changed or a not in base_set or b not in base_set:
                edge(a, b)
    position = {key: i for i, key in enumerate(base)}
    ready = [(position.get(key, len(base)), key) for key in members if not incoming[key]]
    heapq.heapify(ready)
    result = []
    while ready:
        _, key = heapq.heappop(ready)
        result.append(key)
        for child in sorted(edges[key]):
            incoming[child] -= 1
            if not incoming[child]:
                heapq.heappush(ready, (position.get(child, len(base)), child))
    if len(result) != len(members):
        raise SvgConflict([parent + ':order'])
    return result


def merge_svg(base, ours, theirs, overrides=None):
    overrides = overrides or {}
    documents = [Document(value) for value in (base, ours, theirs)]
    previous, left, right = (doc.nodes for doc in documents)
    merged, conflicts = {}, []
    for key in sorted(set(previous) | set(left) | set(right)):
        before, a, b = previous.get(key), left.get(key), right.get(key)
        if signature(a) == signature(b) or signature(b) == signature(before):
            chosen = a
        elif signature(a) == signature(before):
            chosen = b
        elif key in overrides:
            chosen = overrides[key]
        else:
            conflicts.append(key)
            continue
        if chosen is not None:
            merged[key] = chosen
    if conflicts:
        raise SvgConflict(conflicts)

    # Optional export layers disappear when their final item is removed. A new
    # independent item on the other branch keeps that container alive.
    for record in list(merged.values()):
        parent = record.parent
        visited = set()
        while parent is not None and parent not in merged:
            if parent in overrides and overrides[parent] is None:
                raise SvgConflict([parent + ":deleted-container"])
            if parent in visited:
                raise SvgConflict([parent + ':cycle'])
            visited.add(parent)
            candidate = left.get(parent) or right.get(parent) or previous.get(parent)
            if candidate is None or local(candidate.element.tag) not in CONTAINERS:
                raise SvgConflict([parent + ':missing-parent'])
            merged[parent] = candidate
            parent = candidate.parent
    if '@root' not in merged:
        raise SvgConflict(['@root'])
    children = {key: set() for key in merged}
    for key, record in merged.items():
        if record.parent is not None:
            children[record.parent].add(key)

    active, emitted = set(), set()
    def build(key):
        if key in active:
            raise SvgConflict([key + ':cycle'])
        active.add(key)
        result = copy.deepcopy(merged[key].element)
        for child in ordered_children(key, children[key], documents):
            result.append(build(child))
        active.remove(key)
        emitted.add(key)
        return result
    root = build('@root')
    if emitted != set(merged):
        raise SvgConflict([key + ':cycle' for key in set(merged) - emitted])
    # Keep prefixes required by the Unity importer (notably inkscape:label).
    namespaces = {}
    for document in documents:
        for prefix, uri in document.namespaces.items():
            if prefix == '' and uri in ('', SVG):
                continue
            if prefix in namespaces and namespaces[prefix] != uri:
                raise SvgConflict(['@namespaces:' + prefix])
            namespaces[prefix] = uri
    namespaces.update({'': SVG, 'inkscape': INKSCAPE})
    for prefix, uri in namespaces.items():
        if not re.fullmatch(r'ns\d+', prefix):
            ET.register_namespace(prefix, uri)
    # The SVG default namespace takes precedence over an optional svg: alias.
    ET.register_namespace('', SVG)
    output = ET.tostring(root, encoding='utf-8', xml_declaration=True)
    # Preserve unused declarations (metadata may contain QName-valued text).
    for prefix, uri in namespaces.items():
        declaration = 'xmlns' + (':' + prefix if prefix else '')
        if (declaration + '=').encode() not in output:
            root.set(declaration, uri)
    output = ET.tostring(root, encoding='utf-8', xml_declaration=True)
    # Validate generated identities as well as XML syntax before committing.
    Document(output)
    return output
