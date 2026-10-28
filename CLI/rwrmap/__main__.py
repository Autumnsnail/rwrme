"""JSON-first command line interface for other agents."""
import argparse
import json
import sys
from pathlib import Path

import numpy as np

from . import __version__
from .geometry import finite, fmt, transform, translate_path
from .model import TEMPLATE_TYPES, properties, serialize, set_properties
from .session import Session, atomic_bytes, lock
from .terrain import PRE_ALPHA, PRE_HEIGHT, edit_pixels, encode, pixel


class Parser(argparse.ArgumentParser):
    def error(self, message):
        raise ValueError(message)


def parser():
    root = Parser(description="rwrme map CLI. JSON output; load both map and templates before editing.")
    root.add_argument("--version", action="version", version=__version__)
    root.add_argument("--session", default=str(Path(__file__).resolve().parents[1] / ".work" / "default.rwrmap"))
    subs = root.add_subparsers(dest="command", required=True)
    subs.add_parser("status", help="Report session readiness and item counts")
    p = subs.add_parser("map-read", help="Snapshot a map directory; never edit source files")
    p.add_argument("path")
    p.add_argument("--replace", action="store_true")
    p = subs.add_parser("template-read", help="Load an XML/SVG template or choose first file in a directory")
    p.add_argument("path")
    p = subs.add_parser("templates-list", help="List definitions from the loaded template")
    p.add_argument("--type", choices=TEMPLATE_TYPES)
    p.add_argument("--search", default="")
    pagination(p)
    p = subs.add_parser("items-list", help="List every current instance, with stable source IDs")
    p.add_argument("--type")
    p.add_argument("--layer")
    p.add_argument("--search", default="")
    pagination(p)
    p = subs.add_parser("item-get")
    p.add_argument("id")
    p = subs.add_parser("item-add", help="Create from a loaded mesh/building/wall/decal/post template")
    p.add_argument("--type", required=True, choices=TEMPLATE_TYPES[:-1])
    p.add_argument("--template", required=True)
    p.add_argument("--id")
    p.add_argument("--x", type=finite)
    p.add_argument("--y", type=finite)
    p.add_argument("--width", type=finite)
    p.add_argument("--depth", type=finite)
    p.add_argument("--rotation", type=finite, default=0)
    p.add_argument("--layer", type=int, default=1)
    p.add_argument("--points", help="JSON [[x,y],...] or @file.json")
    p.add_argument("--set", action="append", default=[], metavar="KEY=VALUE")
    p = subs.add_parser("item-delete", help="Delete by ID; paired platform edges are deleted together")
    p.add_argument("id")
    p = subs.add_parser("item-edit", help="Translate in absolute SVG axes and edit desc properties")
    p.add_argument("id")
    p.add_argument("--dx", type=finite, default=0)
    p.add_argument("--dy", type=finite, default=0)
    p.add_argument("--set", action="append", default=[], metavar="KEY=VALUE")
    p = subs.add_parser("pixel-get", help="Read a PNG pixel; pre is the editable baseline")
    raster_args(p)
    p.add_argument("--stage", choices=("pre", "reference", "baked"), default="pre")
    p.add_argument("--x", type=int, required=True)
    p.add_argument("--y", type=int, required=True)
    p = subs.add_parser("pixel-set", help="Set a pixel or solid rectangle in the editable pre image")
    raster_args(p)
    p.add_argument("--x", type=int, required=True)
    p.add_argument("--y", type=int, required=True)
    p.add_argument("--width", type=int, default=1)
    p.add_argument("--height", type=int, default=1)
    group = p.add_mutually_exclusive_group(required=True)
    group.add_argument("--value", help="0..255 height, [r,g,b,a] alpha, or a single channel byte")
    group.add_argument("--meters", type=finite, help="Height in world meters, quantized through MaxHeight")
    p.add_argument("--channel", choices=("r", "g", "b", "a", "road"))
    p = subs.add_parser("pixel-patch", help="Apply a JSON list of {x,y,value,channel?} atomically")
    raster_args(p)
    p.add_argument("file")
    p = subs.add_parser("export", help="Bake pre+paths and export to a NEW directory")
    p.add_argument("directory")
    p.add_argument("--with-preview", action="store_true")
    p.add_argument("--preview-size", type=int, default=2048)
    p = subs.add_parser("preview", help="Render PNG from the current baked session")
    p.add_argument("output")
    p.add_argument("--size", type=int, default=2048)
    p.add_argument("--water-level", type=finite, default=2)
    p.add_argument("--overlay-items", action="store_true", help="Outline additional rect instances in orange")
    p = subs.add_parser("batch", help="Atomic session transaction: JSON array of command argument arrays")
    p.add_argument("file")
    return root


def pagination(p):
    p.add_argument("--offset", type=int, default=0)
    p.add_argument("--limit", type=int, default=100, help="0 means all")


def raster_args(p):
    p.add_argument("--target", choices=("height", "alpha"), required=True)
    p.add_argument("--origin", choices=("top-left", "bottom-left"), default="top-left")


def json_value(text):
    if text.startswith("@"):
        text = Path(text[1:]).read_text(encoding="utf-8-sig")
    return json.loads(text)


def prop_args(values):
    result = {}
    for value in values:
        if "=" not in value:
            raise ValueError("--set requires KEY=VALUE")
        key, value = value.split("=", 1)
        result[key.strip()] = value.strip()
    return result


def page(values, args):
    if args.offset < 0 or args.limit < 0:
        raise ValueError("Pagination offset/limit must be nonnegative")
    chunk = values[args.offset:None if args.limit == 0 else args.offset+args.limit]
    next_offset = args.offset+len(chunk)
    return {"total": len(values), "offset": args.offset, "items": chunk,
            "next_offset": next_offset if next_offset < len(values) else None}


MUTATIONS = {"map-read", "template-read", "item-add", "item-delete", "item-edit", "pixel-set", "pixel-patch"}


def execute(session, args):
    cmd = args.command
    if cmd == "status":
        return session.status()
    if cmd == "map-read":
        return session.read_map(args.path, args.replace)
    if cmd == "template-read":
        return session.read_templates(args.path)
    if cmd == "templates-list":
        template = session.templates
        values = [t for t in template.entries if (not args.type or t["type"] == args.type) and args.search.lower() in t["name"].lower()]
        return {**page(values, args), "warnings": template.warnings, "alpha_files": template.alpha_names}
    if cmd == "items-list":
        values = [i.info() for i in session.document.items() if (not args.type or i.kind == args.type) and (not args.layer or i.layer == args.layer) and args.search.lower() in i.id.lower()]
        return page(values, args)
    if cmd == "item-get":
        return session.document.find(args.id).info()
    if cmd in ("item-add", "item-delete", "item-edit", "pixel-set", "pixel-patch"):
        session.require_editable()
    if cmd == "item-add":
        doc = session.document
        result = doc.add(session.templates, args.type, args.template, x=args.x, y=args.y,
                         width=args.width, depth=args.depth, rotation=args.rotation, layer=args.layer,
                         points=json_value(args.points) if args.points else None, props=prop_args(args.set), item_id=args.id)
        session.set_document(doc)
        return result
    if cmd == "item-delete":
        doc = session.document
        result = doc.delete(args.id)
        session.set_document(doc)
        return result
    if cmd == "item-edit":
        doc = session.document
        item = doc.find(args.id)
        element = item.partner if item.partner is not None else item.element
        props = {**properties(element), **prop_args(args.set)}
        reference_key = {"building": "material", "mesh": "template", "wall": "template", "decal": "template", "post": "post_template"}.get(item.kind)
        if reference_key and reference_key in props:
            session.templates.find(item.kind, props[reference_key])
        if item.kind == "building" and "height" in props:
            h = finite(props["height"])
            if h < 0 or h != int(h):
                raise ValueError("Building height must be a nonnegative integer")
        set_properties(element, props)
        if args.dx or args.dy:
            parent_matrix = item.matrix @ np.linalg.inv(transform(item.element.get("transform", "")))
            local_delta = np.linalg.solve(parent_matrix[:2, :2], np.array([args.dx, args.dy]))
            for el in (item.element,) if item.partner is None else (item.element, item.partner):
                if item.kind in ("height_path", "material_path"):
                    if el.get("transform"):
                        raise ValueError("rwrme ignores individual terrain path transforms; remove the transform before translating")
                    el.set("d", translate_path(el.get("d", ""), *local_delta))
                    continue
                matrix = transform(el.get("transform", ""))
                matrix[:2, 2] += local_delta
                el.set("transform", "matrix(" + ",".join(fmt(v) for v in (matrix[0, 0], matrix[1, 0], matrix[0, 1], matrix[1, 1], matrix[0, 2], matrix[1, 2])) + ")")
        session.set_document(doc)
        return doc.find(args.id).info()
    if cmd == "pixel-get":
        image = session.image(args.target, args.stage)
        value = image.getpixel(pixel(image, args.x, args.y, args.origin))
        result = {"target": args.target, "stage": args.stage, "size": list(image.size), "origin": args.origin,
                  "x": args.x, "y": args.y, "value": value}
        if args.target == "height":
            result["normalized"] = value/255
            result["meters"] = value/255*finite(session.config.get("MaxHeight", 25))
        else:
            result["road"] = 255-value[3]
        return result
    if cmd in ("pixel-set", "pixel-patch"):
        image = session.image(args.target)
        if cmd == "pixel-patch":
            edits = json_value("@" + args.file)
            if not isinstance(edits, list):
                raise ValueError("Pixel patch must be a JSON array")
            result = edit_pixels(image, edits, args.origin)
            count = len(edits)
        else:
            if args.width < 1 or args.height < 1:
                raise ValueError("Pixel rectangle dimensions must be positive")
            if args.meters is not None:
                maximum = finite(session.config.get("MaxHeight", 25))
                if args.target != "height" or args.channel or not 0 <= args.meters <= maximum:
                    raise ValueError("--meters requires height target and a value in 0..MaxHeight")
                value = round(args.meters/maximum*255)
            else:
                value = json_value(args.value)
            start = pixel(image, args.x, args.y, args.origin)
            end = pixel(image, args.x+args.width-1, args.y+args.height-1, args.origin)
            probe = edit_pixels(image, [{"x": args.x, "y": args.y, "value": value, "channel": args.channel}], args.origin)
            box = (min(start[0], end[0]), min(start[1], end[1]), max(start[0], end[0])+1, max(start[1], end[1])+1)
            if args.channel:
                channels = list(image.split())
                index = 3 if args.channel == "road" else "rgba".index(args.channel)
                channels[index].paste(255-value if args.channel == "road" else value, box)
                from PIL import Image
                result = Image.merge("RGBA", channels)
            else:
                result = image.copy()
                result.paste(probe.getpixel(start), box)
            count = args.width*args.height
        session.files[PRE_HEIGHT if args.target == "height" else PRE_ALPHA] = encode(result)
        return {"target": args.target, "stage": "pre", "edited_pixels": count, "size": list(result.size)}
    if cmd == "export":
        return session.export(args.directory, args.with_preview, args.preview_size)
    if cmd == "preview":
        session.require_map()
        output = session.protect_output(args.output)
        if output.exists():
            raise ValueError("Preview output already exists; choose a new filename")
        from .preview import render
        from .terrain import bake
        height, alpha = bake(session.files, session.config, session.document)
        image, warnings = render(session.document, height, alpha, session.config, args.size, args.water_level, args.overlay_items)
        atomic_bytes(output, encode(image))
        return {"path": str(output), "size": list(image.size), "warnings": warnings}
    if cmd == "batch":
        commands = json_value("@" + args.file)
        if not isinstance(commands, list) or not commands:
            raise ValueError("Batch requires a nonempty JSON array of argument arrays")
        results = []
        for index, command in enumerate(commands):
            if not isinstance(command, list) or not command or not all(isinstance(x, str) for x in command):
                raise ValueError(f"Batch step {index}: expected string argument array")
            if any(x in ("--session", "--help", "-h", "--version") or x.startswith("--session=") for x in command):
                raise ValueError("Batch steps cannot select another session or exit the parser")
            step = parser().parse_args(command)
            if step.command in ("batch", "export", "preview"):
                raise ValueError("Batch does not allow nested batches or filesystem outputs; export after commit")
            try:
                results.append({"command": step.command, "result": execute(session, step)})
            except (ValueError, KeyError, OSError) as error:
                raise ValueError(f"Batch step {index} ({step.command}) failed; nothing committed: {error}") from error
        return {"steps": results}
    raise ValueError(f"Unknown command: {cmd}")


def main(argv=None):
    # JSON must survive Chinese paths/IDs on Windows consoles and pipes.
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8")
    try:
        args = parser().parse_args(argv)
        path = Path(args.session).resolve()
        if path.suffix != ".rwrmap":
            raise ValueError("Session path must end in .rwrmap")
        for parent in path.parents:
            if parent.name == "Assets" and (parent.parent / "ProjectSettings").is_dir():
                raise ValueError("Keep CLI sessions outside Unity Assets")
        with lock(path):
            session = Session(path)
            result = execute(session, args)
            if args.command in MUTATIONS or args.command == "batch":
                session.save()
            response = {"ok": True, "command": args.command, "revision": session.meta["revision"], "result": result}
        print(json.dumps(response, ensure_ascii=False, allow_nan=False))
        return 0
    except KeyboardInterrupt:
        print(json.dumps({"ok": False, "error": {"code": "INTERRUPTED", "message": "Interrupted; no partial session commit"}}))
        return 130
    except Exception as error:
        print(json.dumps({"ok": False, "error": {"code": type(error).__name__, "message": str(error)}}, ensure_ascii=False))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
