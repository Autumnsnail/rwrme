"""End-to-end acceptance run on this checkout's actual map and templates.

All generated files stay under CLI/.work/verification-*. Input hashes are checked.
"""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import time

import numpy as np
from PIL import Image

CLI = Path(__file__).resolve().parents[1]
PROJECT = CLI.parent
sys.path.insert(0, str(CLI))
from rwrmap.session import Session


def hashes(directory):
    return {p.relative_to(directory).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in directory.rglob("*") if p.is_file()}


def main():
    source = PROJECT / "Assets/map"
    template_dir = PROJECT / "Assets/templates"
    if not source.is_dir() or not template_dir.is_dir():
        raise SystemExit("This acceptance run requires Assets/map and Assets/templates")
    (CLI / ".work").mkdir(exist_ok=True)
    output = Path(tempfile.mkdtemp(prefix="verification-", dir=CLI / ".work"))
    session_path = output / "agent.rwrmap"
    source_before = hashes(source)
    templates_before = hashes(template_dir)
    started = time.monotonic()
    calls = []

    def run(*args, expected=0, session=session_path):
        process = subprocess.run([sys.executable, str(CLI / "rwrmap.py"), "--session", str(session), *map(str, args)],
                                 cwd=PROJECT, capture_output=True, encoding="utf-8")
        if process.returncode != expected:
            raise AssertionError(f"{args}: {process.stdout}\n{process.stderr}")
        value = json.loads(process.stdout)
        calls.append({"command": args[0], "exit_code": process.returncode})
        if value["ok"] != (expected == 0):
            raise AssertionError(value)
        return value.get("result", value.get("error"))

    try:
        run("item-delete", "missing", expected=2)
        imported = run("map-read", source)
        loaded = run("template-read", template_dir)
        initial_items = run("items-list", "--limit", "0")["items"]
        template_counts = loaded["counts"]
        created = []
        for kind in ("mesh", "building", "wall", "decal", "post"):
            templates = run("templates-list", "--type", kind, "--limit", "0")["items"]
            if not templates:
                raise AssertionError(f"No real template for {kind}")
            template = templates[0]
            item_id = kind+"_cli_verification"
            args = ["item-add", "--type", kind, "--template", template["name"], "--id", item_id, "--layer", "1"]
            if kind in ("wall", "post"):
                args += ["--points", "[[1000,1000],[1030,1000],[1030,1030]]"]
            else:
                args += ["--x", "1000", "--y", "1000", "--width", "20", "--depth", "15"]
            created.append(run(*args))
        platform = next(i for i in initial_items if i["type"] == "platform")
        run("item-delete", platform["id"])
        run("item-get", platform["id"], expected=2)
        job = output / "batch.json"
        job.write_text(json.dumps([
            ["item-edit", "building_cli_verification", "--dx", "10", "--dy", "-5", "--set", "height=2"],
            ["pixel-set", "--target", "height", "--x", "2", "--y", "3", "--value", "123"],
            ["pixel-set", "--target", "alpha", "--x", "2", "--y", "3", "--channel", "road", "--value", "201"],
        ]), encoding="utf-8")
        run("batch", job)
        assert run("pixel-get", "--target", "height", "--x", "2", "--y", "3")["value"] == 123
        assert run("pixel-get", "--target", "alpha", "--x", "2", "--y", "3")["road"] == 201
        exported = run("export", output / "map", "--with-preview", "--preview-size", "2048")
        reloaded = run("map-read", output / "map", session=output / "roundtrip.rwrmap")
        final_items = run("items-list", "--limit", "0", session=output / "roundtrip.rwrmap")["items"]
        final_by_id = {i["id"]: i for i in final_items}
        assert len(final_items) == len(initial_items)+5-1
        assert platform["id"] not in final_by_id
        for item in created:
            assert final_by_id[item["id"]]["type"] == item["type"]
        assert final_by_id["building_cli_verification"]["position"] == [1010, 995]
        assert final_by_id["building_cli_verification"]["properties"]["height"] == "2"
        for item in initial_items:
            if item["id"] != platform["id"]:
                assert final_by_id[item["id"]] == item, item["id"]
        session = Session(session_path)
        with Image.open(output / "map/terrain5_combined_alpha.png") as image:
            alpha = np.array(image)
        for index in range(1, 5):
            filename = session.templates.alpha_names[index]
            with Image.open(output / "map" / filename) as image:
                actual = np.array(image)
            expected = alpha[:, :, index-1] if index != 4 else 255-alpha[:, :, 3]
            np.testing.assert_array_equal(actual, expected)
        with Image.open(output / "map/pre_terrain5_heightmap.png") as image:
            assert image.getpixel((2, 3)) == 123
        with Image.open(output / "map/pre_terrain5_combined_alpha.png") as image:
            assert image.getpixel((2, 3))[3] == 54
        with Image.open(output / "map/map_view_ls.png") as image:
            assert image.size == (2048, 2048)
            assert np.array(image).std() > 5
        assert (output / "map/map_config.xml").read_bytes() == (source / "map_config.xml").read_bytes()
        report = {"ok": True, "source_items": len(initial_items), "source_counts": imported["items"],
                  "template_counts": template_counts, "template_warning_count": len(loaded["warnings"]),
                  "exported_items": len(final_items), "created_types": [i["type"] for i in created],
                  "deleted_platform": platform["id"], "source_files_unchanged": True,
                  "all_untouched_instances_equal_after_roundtrip": True, "split_channels_verified": "R,G,B,255-A",
                  "preview_size": [2048, 2048], "preview": str(output / "map/map_view_ls.png"),
                  "elapsed_seconds": round(time.monotonic()-started, 2), "calls": calls, "export": exported}
    finally:
        assert hashes(source) == source_before, "Source map changed during the acceptance run"
        assert hashes(template_dir) == templates_before, "Source templates changed during the acceptance run"
    (output / "report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"ok": True, "report": str(output / "report.json"), "source_items": report["source_items"],
                      "template_counts": template_counts, "preview": report["preview"], "seconds": report["elapsed_seconds"]}, ensure_ascii=False))


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    main()
