"""Regression tests against small, independently checkable map fixtures.

Run: python -m unittest discover -s CLI/tests -v
"""
import contextlib
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

import numpy as np
from PIL import Image

CLI = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(CLI))
from rwrmap.__main__ import main
from rwrmap.geometry import path_points, translate_path
from rwrmap.model import INK, MapDocument, Templates, properties
from rwrmap.session import Session, lock
from rwrmap.terrain import FINAL_ALPHA, PRE_ALPHA, PRE_HEIGHT, bake, decode, encode

HEADER = f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:i="{INK}" width="2048" height="2048">'
TEMPLATE = (HEADER + '''<g i:label="materials" id="materials">
<rect id="material1" i:label="#material" width="70" height="50"><desc>name=Brick;</desc></rect>
<rect id="materialSkip" i:label="#material"><desc>name=NotBuilding;type=road;</desc></rect>
<rect id="mesh0" i:label="#mesh_template" width="8" height="4"><desc>name=Box;mesh_name=box.mesh;texture0=a.png;</desc></rect>
<rect id="mesh1" i:label="#mesh_template" width="99" height="99"><desc>name=Child; base_template=Box;texture0=b.png;</desc></rect>
<rect id="wall_template1" i:label="#wall_template" width="1" height="1"><desc>name=Fence;depth=1;height=2;</desc></rect>
<rect id="decal_template1" width="10" height="6"><desc>name=Tile;texture0=tile.png;</desc></rect>
<rect id="post_template1"><desc>name=Posts;mesh_template=Box;</desc></rect>
<rect id="terrain1" i:label="#terrain_layer"><desc>index=4;alpha=custom_road.png;</desc></rect>
<rect id="template_general" i:label="#general"><desc>name=DoNotReplace;</desc></rect>
</g></svg>''').encode()

MAP = (HEADER + '''<!-- keep this comment --><metadata><custom version="7"/></metadata>
<g i:label="materials"><rect i:label="#general" id="general"><desc>name=Original;flip=1;</desc></rect></g>
<g i:label="layer1"><g i:label="buildings"><rect id="building_original" x="0" y="0" width="40" height="20" transform="matrix(0,1,-1,0,100,200)"><desc>height=2;material=Brick;custom=keep;</desc></rect></g>
<g i:label="platforms"><g id="pair"><path id="platform_start" d="m 0,0 20,0"><desc>type=start;</desc></path><path id="platform_end" d="m 0,10 20,0"><desc>type=end;top_material=terrain;</desc></path></g></g>
<g><circle id="unknown_circle" cx="80" cy="90" r="2"/></g></g>
<g i:label="offroad"><path id="offroad1" d="m 0,0 c 10,0 10,10 20,10 l 20,0"/></g>
</svg>''').encode()


class CliTests(unittest.TestCase):
    def setUp(self):
        (CLI / ".work").mkdir(exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix="tests-", dir=CLI / ".work")
        self.root = Path(self.temp.name)
        self.source = self.root / "source"
        self.source.mkdir()
        (self.source / "objects.svg").write_bytes(MAP)
        (self.source / "terrain.cfg").write_text("Heightmap.image=custom_height.png\nMaxHeight=25\nPageWorldX=1024\nPageWorldZ=1024\n", encoding="utf-8")
        Image.new("L", (8, 8), 51).save(self.source / "custom_height.png")
        Image.new("RGBA", (16, 16), (10, 20, 30, 255)).save(self.source / FINAL_ALPHA)
        (self.source / "map_config.xml").write_bytes(b'<map_config><custom keep="yes"/></map_config>')
        (self.source / "extra.bin").write_bytes(bytes(range(256)))
        (self.source / "objects.svg.meta").write_text("guid: preserve", encoding="utf-8")
        self.template = self.root / "模板.svg"
        self.template.write_bytes(TEMPLATE)
        self.session = self.root / "agent.rwrmap"

    def tearDown(self):
        self.temp.cleanup()

    def run_cli(self, *arguments, ok=True):
        buffer = io.StringIO()
        with contextlib.redirect_stdout(buffer):
            code = main(["--session", str(self.session), *map(str, arguments)])
        response = json.loads(buffer.getvalue())
        self.assertEqual(code, 0 if ok else 2, response)
        self.assertEqual(response["ok"], ok, response)
        return response.get("result", response.get("error"))

    def ready(self):
        self.run_cli("map-read", self.source)
        self.run_cli("template-read", self.template)

    def digest(self):
        return hashlib.sha256(self.session.read_bytes()).hexdigest()

    def test_requires_both_reads_for_every_edit(self):
        commands = [("item-delete", "building_original"), ("item-edit", "building_original", "--dx", "1"),
                    ("pixel-set", "--target", "height", "--x", "0", "--y", "0", "--value", "1"),
                    ("item-add", "--type", "mesh", "--template", "Box", "--x", "1", "--y", "1")]
        for command in commands:
            self.run_cli(*command, ok=False)
        self.run_cli("map-read", self.source)
        before = self.digest()
        for command in commands:
            self.run_cli(*command, ok=False)
        self.assertEqual(before, self.digest())
        self.run_cli("export", self.root / "export", ok=False)

    def test_templates_can_load_first_and_lookup_inherits_size(self):
        self.run_cli("template-read", self.template)
        self.assertFalse(self.run_cli("status")["editable"])
        found = self.run_cli("templates-list", "--type", "mesh", "--search", "Child")["items"][0]
        self.assertEqual(found["size"], [8, 4])
        self.assertEqual(found["resolved"]["mesh_name"], "box.mesh")
        self.assertEqual(found["resolved"]["texture0"], "b.png")
        self.assertEqual(self.run_cli("templates-list", "--type", "building")["total"], 1)
        self.run_cli("map-read", self.source)
        self.assertTrue(self.run_cli("status")["editable"])

    def test_pagination_and_all_instances(self):
        self.ready()
        all_items = self.run_cli("items-list", "--limit", "0")
        self.assertEqual(all_items["total"], 4)
        self.assertEqual({i["type"] for i in all_items["items"]}, {"building", "platform", "unknown", "offroad"})
        first = self.run_cli("items-list", "--limit", "2")
        second = self.run_cli("items-list", "--limit", "2", "--offset", first["next_offset"])
        self.assertEqual(first["items"]+second["items"], all_items["items"])
        self.assertIsNone(second["next_offset"])

    def test_template_creation_all_five_kinds_and_reload(self):
        self.ready()
        for kind, name in [("mesh", "Child"), ("building", "Brick"), ("decal", "Tile"), ("wall", "Fence"), ("post", "Posts")]:
            args = ["item-add", "--type", kind, "--template", name, "--id", kind+"_new"]
            if kind in ("wall", "post"):
                args += ["--points", "[[10,20],[30,40],[40,50]]"]
            else:
                args += ["--x", "100", "--y", "200", "--width", "8", "--depth", "4", "--rotation", "90"]
            result = self.run_cli(*args)
            self.assertEqual(result["type"], kind)
            if kind == "mesh":
                np.testing.assert_allclose(result["corners"][1], [100, 192], atol=1e-7)
        self.run_cli("export", self.root / "out")
        other = Session(self.root / "other.rwrmap")
        other.read_map(self.root / "out")
        for kind in ("mesh", "building", "decal", "wall", "post"):
            self.assertEqual(other.document.find(kind+"_new").kind, kind)

    def test_invalid_template_id_or_dimensions_never_commit(self):
        self.ready()
        before = self.digest()
        for args in [
            ["item-add", "--type", "mesh", "--template", "Missing", "--x", "0", "--y", "0"],
            ["item-add", "--type", "building", "--template", "Brick", "--x", "0", "--y", "0"],
            ["item-add", "--type", "building", "--template", "Brick", "--id", "bad", "--x", "0", "--y", "0", "--width", "8", "--depth", "4"],
            ["item-add", "--type", "mesh", "--template", "Box", "--id", "building_original", "--x", "0", "--y", "0"],
            ["item-add", "--type", "wall", "--template", "Fence", "--points", "[[0,0]]"],
            ["item-add", "--type", "mesh", "--template", "Box", "--x", "NaN", "--y", "0"],
        ]:
            self.run_cli(*args, ok=False)
            self.assertEqual(before, self.digest())

    def test_delete_platform_by_either_edge_and_preserve_others(self):
        self.ready()
        self.run_cli("item-delete", "platform_end")
        data = Session(self.session).files["objects.svg"]
        self.assertNotIn(b'platform_start', data)
        self.assertNotIn(b'platform_end', data)
        self.assertIn(b'unknown_circle', data)
        self.run_cli("item-delete", "does-not-exist", ok=False)

    def test_move_in_svg_axes_preserves_rotation(self):
        self.ready()
        before = self.run_cli("item-get", "building_original")
        after = self.run_cli("item-edit", "building_original", "--dx", "30", "--dy", "-10", "--set", "height=3")
        np.testing.assert_allclose(np.array(after["corners"])-np.array(before["corners"]), np.tile([30, -10], (4, 1)))
        self.assertEqual(after["properties"]["custom"], "keep")
        self.assertEqual(after["properties"]["height"], "3")

    def test_pixels_top_bottom_and_height_meters(self):
        self.ready()
        self.run_cli("pixel-set", "--target", "height", "--x", "1", "--y", "2", "--meters", "5")
        self.assertEqual(self.run_cli("pixel-get", "--target", "height", "--x", "1", "--y", "2")["value"], 51)
        self.run_cli("pixel-set", "--target", "height", "--x", "1", "--y", "2", "--value", "123")
        self.assertEqual(self.run_cli("pixel-get", "--target", "height", "--x", "1", "--y", "5", "--origin", "bottom-left")["value"], 123)
        self.assertEqual(self.run_cli("pixel-get", "--target", "height", "--x", "1", "--y", "2", "--stage", "reference")["value"], 51)

    def test_channel_rectangle_and_inverted_road_export(self):
        self.ready()
        self.run_cli("pixel-set", "--target", "alpha", "--x", "1", "--y", "2", "--width", "3", "--height", "2", "--channel", "road", "--value", "200")
        self.assertEqual(self.run_cli("pixel-get", "--target", "alpha", "--x", "3", "--y", "3")["value"], [10, 20, 30, 55])
        self.run_cli("export", self.root / "out")
        with Image.open(self.root / "out/custom_road.png") as image:
            self.assertEqual(image.getpixel((3, 3)), 200)
        with Image.open(self.root / "out/terrain5_alpha_sand.png") as image:
            self.assertEqual(image.getpixel((3, 3)), 10)

    def test_pixel_patch_is_atomic(self):
        self.ready()
        patch = self.root / "patch.json"
        patch.write_text(json.dumps([{"x": 0, "y": 0, "value": 220}, {"x": 100, "y": 0, "value": 0}]), encoding="utf-8")
        before = self.digest()
        self.run_cli("pixel-patch", "--target", "height", patch, ok=False)
        self.assertEqual(before, self.digest())
        patch.write_text('[{"x":0,"y":0,"value":220},{"x":1,"y":0,"value":77}]', encoding="utf-8")
        self.run_cli("pixel-patch", "--target", "height", patch)
        self.assertEqual(self.run_cli("pixel-get", "--target", "height", "--x", "1", "--y", "0")["value"], 77)

    def test_bad_pixels_and_nonfinite_numbers_rejected(self):
        self.ready()
        before = self.digest()
        for args in [("--x", "-1", "--y", "0", "--value", "1"), ("--x", "0", "--y", "0", "--value", "256"),
                     ("--x", "0", "--y", "0", "--value", "1.5"), ("--x", "0", "--y", "0", "--meters", "26"),
                     ("--x", "7", "--y", "0", "--value", "1", "--width", "2")]:
            self.run_cli("pixel-set", "--target", "height", *args, ok=False)
        self.assertEqual(before, self.digest())

    def test_export_preserves_unknown_files_settings_and_source_bytes(self):
        self.ready()
        before = {p.name: p.read_bytes() for p in self.source.iterdir()}
        output = self.root / "out"
        self.run_cli("export", output)
        self.assertEqual(before, {p.name: p.read_bytes() for p in self.source.iterdir()})
        self.assertEqual((output / "extra.bin").read_bytes(), before["extra.bin"])
        self.assertEqual((output / "map_config.xml").read_bytes(), before["map_config.xml"])
        svg = (output / "objects.svg").read_bytes()
        self.assertIn(b'keep this comment', svg)
        self.assertIn(b'Original', svg)
        self.assertNotIn(b'DoNotReplace', svg)
        self.assertIn(b'custom version="7"', svg)
        self.assertFalse((output / "objects.svg.meta").exists())
        self.assertEqual((output / "custom_height.png").read_bytes(), (output / "terrain5_heightmap.png").read_bytes())
        self.run_cli("export", output, ok=False)
        self.run_cli("export", self.source, ok=False)

    def test_map_replace_is_explicit_and_bad_read_does_not_clobber(self):
        self.ready()
        before = self.digest()
        self.run_cli("map-read", self.source, ok=False)
        (self.source / "terrain.cfg").write_text("Heightmap.image=../../escape.png", encoding="utf-8")
        self.run_cli("map-read", self.source, "--replace", ok=False)
        self.assertEqual(before, self.digest())

    def test_batch_rollback_and_single_commit(self):
        self.ready()
        job = self.root / "job.json"
        job.write_text(json.dumps([["item-delete", "building_original"], ["item-delete", "missing"]]), encoding="utf-8")
        before = self.digest()
        self.run_cli("batch", job, ok=False)
        self.assertEqual(before, self.digest())
        revision = self.run_cli("status")["revision"]
        job.write_text(json.dumps([["item-delete", "building_original"], ["item-delete", "platform_start"]]), encoding="utf-8")
        self.run_cli("batch", job)
        self.assertEqual(self.run_cli("status")["revision"], revision+1)
        self.assertEqual(self.run_cli("items-list", "--limit", "0")["total"], 2)

    def test_batch_disallows_external_outputs_and_alternate_sessions(self):
        self.ready()
        job = self.root / "job.json"
        for commands in [[["export", str(self.root / "out")]], [["--session="+str(self.root / "other.rwrmap"), "status"]]]:
            job.write_text(json.dumps(commands), encoding="utf-8")
            self.run_cli("batch", job, ok=False)
        self.assertFalse((self.root / "out").exists())

    def test_lock_rejects_concurrent_writer(self):
        self.ready()
        with lock(self.session):
            self.run_cli("item-delete", "building_original", ok=False)
        self.run_cli("item-get", "building_original")

    def test_preview_and_export_preview_are_valid_pngs(self):
        self.ready()
        output = self.root / "preview.png"
        self.run_cli("preview", output, "--size", "64", "--overlay-items")
        with Image.open(output) as image:
            self.assertEqual(image.size, (64, 64))
            self.assertGreater(len(image.getcolors(10000)), 1)
        self.run_cli("export", self.root / "out", "--with-preview", "--preview-size", "64")
        self.assertTrue((self.root / "out/map_view_ls.png").is_file())

    def test_entry_point_from_another_working_directory(self):
        result = subprocess.run([sys.executable, str(CLI / "rwrmap.py"), "--session", str(self.session), "status"], cwd=self.root, capture_output=True, encoding="utf-8")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue(json.loads(result.stdout)["ok"])

    def test_bad_arguments_are_json_and_return_nonzero(self):
        self.run_cli("not-a-command", ok=False)
        self.run_cli("pixel-set", "--target", "height", ok=False)


class RasterizerTests(unittest.TestCase):
    def data(self, paths):
        doc = MapDocument((HEADER+paths+"</svg>").encode())
        files = {"height.png": encode(Image.new("L", (8, 8), 51)), FINAL_ALPHA: encode(Image.new("RGBA", (16, 16), (0, 0, 0, 255)))}
        return files, {"Heightmap.image": "height.png", "PageWorldX": "1024", "PageWorldZ": "1024"}, doc

    def test_height_modes_use_normalized_values_and_offset_baseline(self):
        paths = '<g i:label="height_paths">'
        for n, desc in enumerate(("width=512;mode=raise;height_delta=0.3;", "width=512;mode=offset;height_offset=0.1;")):
            paths += f'<path id="hp{n}" d="M 0,1024 L 2048,1024"><desc>{desc}</desc></path>'
        files, cfg, doc = self.data(paths+'</g>')
        image, _ = bake(files, cfg, doc)
        self.assertIn(image.getpixel((4, 3)), (76, 77))  # .2 baseline + .1; NOT .2+.3+.1
        self.assertEqual(image.getpixel((4, 1)), 51)
        # Bake starts from the same baseline each time.
        np.testing.assert_array_equal(np.array(image), np.array(bake(files, cfg, doc)[0]))

    def test_height_set_lower_and_clamp(self):
        paths = '<g i:label="height_paths"><path id="hp" d="M 0,1024 L 2048,1024"><desc>width=512;height=0.8;</desc></path><path id="hp2" d="M 0,1024 L 2048,1024"><desc>width=512;height_delta=1;mode=lower;</desc></path></g>'
        image, _ = bake(*self.data(paths))
        self.assertEqual(image.getpixel((4, 3)), 0)
        self.assertEqual(image.getpixel((4, 0)), 51)

    def test_material_pixel_center_and_512_offset_match_source_formula(self):
        # (x=4,y=6) in a 16x16 top-down image maps to SVG (1152,640).
        paths = '<g i:label="material_paths"><path id="mp" d="M 0,640 L 2048,640"><desc>width=512;material_index=4;hardness=1;</desc></path></g>'
        _, image = bake(*self.data(paths))
        self.assertEqual(image.getpixel((4, 6)), (0, 0, 0, 0))
        self.assertEqual(image.getpixel((4, 5)), (0, 0, 0, 255))
        self.assertEqual(image.getpixel((12, 6)), (0, 0, 0, 255))

    def test_material_hardness_blends_all_channels(self):
        paths = '<g i:label="material_paths"><path id="mp" d="M 0,768 L 2048,768"><desc>width=512;material_index=2;hardness=2;</desc></path></g>'
        _, image = bake(*self.data(paths))
        # Distance 128 / halfwidth 256; (1-.5)^2=.25; round(.25*255)=64.
        self.assertEqual(image.getpixel((4, 6)), (0, 64, 0, 255))

    def test_curve_sampling_and_translation_preserve_cubics(self):
        data = "m 10,20 c 0,10 10,10 10,0 l 5,0 M 100,100 H 120 V 130 Z"
        points = path_points(data)
        self.assertEqual(len(points), 30)
        moved = translate_path(data, 7, -3)
        self.assertIn("c", moved)
        np.testing.assert_allclose(path_points(moved), points + [7, -3])

    def test_unknown_path_command_fails_instead_of_silent_bad_bake(self):
        with self.assertRaises(ValueError):
            path_points("M 0,0 A 10,10 0 0 0 20,20")

    def test_template_forward_inheritance_matches_import_order(self):
        data = (HEADER+'<g i:label="materials"><rect i:label="#mesh" width="9" height="7"><desc>name=Child;base_template=Later;</desc></rect><rect i:label="#mesh" width="2" height="3"><desc>name=Later;</desc></rect></g></svg>').encode()
        templates = Templates(data)
        self.assertEqual(templates.find("mesh", "Child")["size"], [9, 7])
        self.assertEqual(len(templates.warnings), 1)

    def test_xml_entities_rejected(self):
        with self.assertRaises(ValueError):
            MapDocument(b'<!DOCTYPE svg [<!ENTITY x "boom">]><svg>&x;</svg>')


if __name__ == "__main__":
    unittest.main()
