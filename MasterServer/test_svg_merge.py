import copy
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path
from svg_merge import Document, INKSCAPE, SvgConflict, merge_svg


def svg(body, attributes=''):
    return ('<svg xmlns="http://www.w3.org/2000/svg" xmlns:inkscape="' + INKSCAPE + '" ' + attributes + '>' + body + '</svg>').encode()


def indexed(content):
    return {node.get('id'): node for node in ET.fromstring(content).iter() if node.get('id')}


def merge(base, left, right):
    return indexed(merge_svg(base, left, right))


class SvgMergeTests(unittest.TestCase):
    def test_different_elements_merge(self):
        before = '<g id="layer"><rect id="a" x="0"/><rect id="b" x="0"/></g>'
        result = merge(svg(before), svg(before.replace('id="a" x="0"', 'id="a" x="10"')),
                       svg(before.replace('id="b" x="0"', 'id="b" x="20"')))
        self.assertEqual('10', result['a'].get('x'))
        self.assertEqual('20', result['b'].get('x'))

    def test_same_element_conflict_including_disjoint_attributes(self):
        with self.assertRaises(SvgConflict) as caught:
            merge(svg('<rect id="a" x="0" y="0"/>'), svg('<rect id="a" x="1" y="0"/>'), svg('<rect id="a" x="0" y="2"/>'))
        self.assertEqual(['a'], caught.exception.elements)

    def test_identical_edit_is_not_conflict(self):
        result = merge(svg('<rect id="a" x="0"/>'), svg('<rect id="a" x="1"/>'), svg('<rect id="a" x="1"/>'))
        self.assertEqual('1', result['a'].get('x'))

    def test_different_additions_same_layer(self):
        base = svg('<g id="layer"/>')
        result = merge(base, svg('<g id="layer"><rect id="a"/></g>'), svg('<g id="layer"><rect id="b"/></g>'))
        self.assertEqual({'a', 'b'}, {node.get('id') for node in result['layer']})

    def test_same_new_id_different_content_conflicts(self):
        with self.assertRaises(SvgConflict) as caught:
            merge(svg(''), svg('<rect id="new" x="1"/>'), svg('<rect id="new" x="2"/>'))
        self.assertEqual(['new'], caught.exception.elements)

    def test_delete_one_edit_another(self):
        result = merge(svg('<rect id="a"/><rect id="b" x="0"/>'), svg('<rect id="b" x="0"/>'),
                       svg('<rect id="a"/><rect id="b" x="3"/>'))
        self.assertNotIn('a', result)
        self.assertEqual('3', result['b'].get('x'))

    def test_delete_edit_same_element_conflicts(self):
        with self.assertRaises(SvgConflict) as caught:
            merge(svg('<rect id="a" x="0"/>'), svg(''), svg('<rect id="a" x="1"/>'))
        self.assertEqual(['a'], caught.exception.elements)

    def test_both_delete(self):
        self.assertNotIn('a', merge(svg('<rect id="a"/>'), svg(''), svg('')))

    def test_deleted_optional_layer_survives_independent_addition(self):
        base = svg('<g id="layer1"><g id="rocks"><rect id="a"/></g></g>')
        result = merge(base, svg(''), svg('<g id="layer1"><g id="rocks"><rect id="a"/><rect id="b"/></g></g>'))
        self.assertNotIn('a', result)
        self.assertEqual('b', result['rocks'][0].get('id'))

    def test_concurrent_new_layer(self):
        result = merge(svg(''), svg('<g id="layer2"><rect id="a"/></g>'), svg('<g id="layer2"><rect id="b"/></g>'))
        self.assertEqual({'a', 'b'}, {node.get('id') for node in result['layer2']})

    def test_move_and_edit_different_elements(self):
        base = svg('<g id="one"><rect id="a"/><rect id="b" x="0"/></g><g id="two"/>')
        left = svg('<g id="one"><rect id="b" x="0"/></g><g id="two"><rect id="a"/></g>')
        right = svg('<g id="one"><rect id="a"/><rect id="b" x="9"/></g><g id="two"/>')
        result = merge(base, left, right)
        self.assertEqual('a', result['two'][0].get('id'))
        self.assertEqual('9', result['b'].get('x'))

    def test_move_and_edit_same_element_conflicts(self):
        base = svg('<g id="one"><rect id="a" x="0"/></g><g id="two"/>')
        left = svg('<g id="one"/><g id="two"><rect id="a" x="0"/></g>')
        right = svg('<g id="one"><rect id="a" x="1"/></g><g id="two"/>')
        with self.assertRaises(SvgConflict) as caught:
            merge(base, left, right)
        self.assertEqual(['a'], caught.exception.elements)

    def test_parent_group_and_child_edits_are_independent(self):
        result = merge(svg('<g id="layer"><rect id="a" x="0"/></g>'),
                       svg('<g id="layer" transform="translate(1)"><rect id="a" x="0"/></g>'),
                       svg('<g id="layer"><rect id="a" x="4"/></g>'))
        self.assertEqual('translate(1)', result['layer'].get('transform'))
        self.assertEqual('4', result['a'].get('x'))

    def test_old_generated_labels_and_desc_ids_do_not_conflict(self):
        base = '<rect id="buildingA" x="0" inkscape:label="#rect64060"><desc id="desc0">height=1;</desc></rect>'
        base += '<path id="wallB" d="m 0,0"><desc id="desc20">material=stone;</desc></path>'
        left = base.replace('x="0"', 'x="5"').replace('desc20', 'desc21')
        right = base.replace('#rect64060', '#rect64061').replace('desc0', 'desc1').replace('m 0,0', 'm 3,3')
        result = merge(svg(base), svg(left), svg(right))
        self.assertEqual('5', result['buildingA'].get('x'))
        self.assertEqual('m 3,3', result['wallB'].get('d'))
        self.assertEqual('desc_buildingA', result['buildingA'][0].get('id'))

    def test_desc_content_changes_conflict_at_owner(self):
        base = '<rect id="buildingA"><desc id="desc0">height=1;</desc></rect>'
        with self.assertRaises(SvgConflict) as caught:
            merge(svg(base), svg(base.replace('height=1', 'height=2')), svg(base.replace('height=1', 'height=3')))
        self.assertEqual(['buildingA'], caught.exception.elements)

    def test_platform_group_index_churn(self):
        base = '<g id="platforms"><g id="gp10"><path id="platformA_s" d="m0,0"><desc id="desc10">start;</desc></path><path id="platformA" d="m1,1"><desc id="desc10">end;</desc></path></g></g><rect id="buildingB" x="0"/>'
        left = base.replace('m0,0', 'm2,2')
        right = base.replace('gp10', 'gp20').replace('desc10', 'desc20').replace('x="0"', 'x="3"')
        result = merge(svg(base), svg(left), svg(right))
        self.assertEqual('m2,2', result['platformA_s'].get('d'))
        self.assertEqual('3', result['buildingB'].get('x'))
        self.assertIn('group_platformA', result)
        self.assertEqual(2, len(result['group_platformA']))

    def test_platform_start_and_end_remain_in_order(self):
        base = '<g id="gp10"><path id="platformA_s" d="m0,0"/><path id="platformA" d="m1,1"/></g>'
        result = merge(svg(base), svg(base.replace('m0,0', 'm2,2')), svg(base.replace('m1,1', 'm3,3')))
        self.assertEqual(['platformA_s', 'platformA'], [child.get('id') for child in result['group_platformA']])

    def test_namespace_prefix_and_indentation_are_not_edits(self):
        base = svg('<g id="layer"><rect id="a" x="0"/><rect id="b" x="0"/></g>')
        left = base.replace(b'id="a" x="0"', b'x="1" id="a"')
        right = b'<s:svg xmlns:s="http://www.w3.org/2000/svg"><s:g id="layer">\n<s:rect id="a" x="0"/>\n<s:rect id="b" x="2"/>\n</s:g></s:svg>'
        output = merge_svg(base, left, right)
        result = indexed(output)
        self.assertEqual('1', result['a'].get('x'))
        self.assertEqual('2', result['b'].get('x'))
        self.assertIn(b'xmlns="http://www.w3.org/2000/svg"', output)

    def test_unused_empty_default_namespace_does_not_conflict(self):
        base = svg('<rect id="a" x="0"/><rect id="b" x="0"/>')
        right = b'<s:svg xmlns:s="http://www.w3.org/2000/svg" xmlns=""><s:rect id="a" x="0"/><s:rect id="b" x="2"/></s:svg>'
        result = merge(base, base.replace(b'id="a" x="0"', b'id="a" x="1"'), right)
        self.assertEqual('1', result['a'].get('x'))
        self.assertEqual('2', result['b'].get('x'))

    def test_inkscape_prefix_and_xlink_preserved(self):
        body = '<g id="layer" inkscape:label="layer1"><rect id="a" x="0"/><rect id="b" x="0"/></g>'
        output = merge_svg(svg(body), svg(body.replace('id="a" x="0"', 'id="a" x="1"')), svg(body.replace('id="b" x="0"', 'id="b" x="2"')))
        self.assertIn(b'inkscape:label="layer1"', output)
        self.assertEqual('layer1', indexed(output)['layer'].get('{' + INKSCAPE + '}label'))

    def test_text_tails_and_inline_content_are_preserved(self):
        base = svg('<text id="label">hello <tspan id="span">world</tspan> tail</text><rect id="a"/><rect id="b"/>')
        output = merge_svg(base, base.replace(b'id="a"', b'id="a" x="1"'), base.replace(b'id="b"', b'id="b" x="2"'))
        self.assertEqual('hello world tail', ''.join(indexed(output)['label'].itertext()))

    def test_append_order_with_concurrent_additions(self):
        result = merge(svg('<g id="layer"><rect id="a"/><rect id="z"/></g>'),
                       svg('<g id="layer"><rect id="a"/><rect id="b"/><rect id="z"/></g>'),
                       svg('<g id="layer"><rect id="a"/><rect id="c"/><rect id="z"/></g>'))
        self.assertEqual(['a', 'b', 'c', 'z'], [node.get('id') for node in result['layer']])

    def test_one_sided_reorder_preserved(self):
        body = '<g id="layer">' + ''.join('<rect id="' + key + '"/>' for key in 'abcd') + '</g>'
        left = '<g id="layer">' + ''.join('<rect id="' + key + '"/>' for key in 'bacd') + '</g>'
        result = merge(svg(body), svg(left), svg(body.replace('id="d"', 'id="d" x="1"')))
        self.assertEqual(list('bacd'), [node.get('id') for node in result['layer']])

    def test_duplicate_geometry_ids_rejected(self):
        with self.assertRaises(SvgConflict) as caught:
            Document(svg('<rect id="a"/><path id="a"/>'))
        self.assertEqual(['a'], caught.exception.elements)

    def test_invalid_and_entity_documents_rejected(self):
        for content in [b'not XML', b'<svg>', b'<!DOCTYPE svg [<!ENTITY x "secret">]><svg>&x;</svg>', b'<html/>']:
            with self.assertRaises(SvgConflict):
                Document(content)

    def test_parent_cycle_rejected(self):
        base = svg('<g id="a"/><g id="b"/>')
        left = svg('<g id="b"><g id="a"/></g>')
        right = svg('<g id="a"><g id="b"/></g>')
        with self.assertRaises(SvgConflict):
            merge(base, left, right)

    def test_real_project_map_independent_edits(self):
        path = Path(__file__).resolve().parent.parent / 'Assets' / 'map' / 'objects.svg'
        if not path.exists():
            self.skipTest('Local map fixture not installed')
        original = path.read_bytes()
        base = ET.fromstring(original)
        identities = [node.get('id') for node in base.iter() if node.get('id', '').startswith('building') and node.tag.endswith('rect')][:2]
        self.assertEqual(2, len(identities))
        variants = []
        for number, identity in enumerate(identities):
            changed = copy.deepcopy(base)
            next(node for node in changed.iter() if node.get('id') == identity).set('data-smoke', str(number))
            variants.append(ET.tostring(changed))
        result = indexed(merge_svg(original, *variants))
        self.assertEqual('0', result[identities[0]].get('data-smoke'))
        self.assertEqual('1', result[identities[1]].get('data-smoke'))
        self.assertEqual(len(Document(original).nodes), len(Document(merge_svg(original, *variants)).nodes))


if __name__ == '__main__':
    unittest.main()
