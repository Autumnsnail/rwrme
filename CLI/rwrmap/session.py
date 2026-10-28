"""Portable session snapshots, atomic commits, and non-destructive export."""
import contextlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import tempfile
import zipfile

from PIL import ImageOps

from .geometry import finite
from .model import MapDocument, Templates, serialize
from .terrain import FINAL_ALPHA, PRE_ALPHA, PRE_HEIGHT, bake, decode, encode, parse_config


def safe_name(name):
    name = name.replace("\\", "/")
    path = PurePosixPath(name)
    if not name or path.is_absolute() or any(part in ("", ".", "..") or ":" in part for part in name.split("/")):
        raise ValueError(f"Unsafe map-relative filename: {name!r}")
    if any(part.endswith((".", " ")) for part in path.parts):
        raise ValueError(f"Unsafe map-relative filename: {name!r}")
    return name


def is_within(path, parent):
    return path == parent or parent in path.parents


@contextlib.contextmanager
def lock(path):
    path.parent.mkdir(parents=True, exist_ok=True)
    lockfile = Path(str(path) + ".lock")
    try:
        fd = os.open(lockfile, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    except FileExistsError:
        raise ValueError(f"Session busy: {lockfile}. Use one session per agent; remove a stale lock only after checking its PID") from None
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as stream:
            json.dump({"pid": os.getpid(), "session": str(path)}, stream)
        yield
    finally:
        lockfile.unlink(missing_ok=True)


def atomic_bytes(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, name = tempfile.mkstemp(prefix=".rwrmap-", suffix=".tmp", dir=path.parent)
    try:
        with os.fdopen(fd, "wb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(name, path)
    finally:
        Path(name).unlink(missing_ok=True)


class Session:
    def __init__(self, path):
        self.path = Path(path).resolve()
        self.meta = {"version": 1, "revision": 0, "map_source": None, "template_source": None}
        self.files = {}
        self.template_data = None
        if self.path.exists():
            with zipfile.ZipFile(self.path) as archive:
                if sum(i.file_size for i in archive.infolist()) > 512 * 1024 * 1024:
                    raise ValueError("Session exceeds 512 MiB uncompressed limit")
                self.meta = json.loads(archive.read("session.json"))
                if self.meta.get("version") != 1:
                    raise ValueError("Unsupported session version")
                for name in archive.namelist():
                    if name.startswith("map/") and not name.endswith("/"):
                        self.files[safe_name(name[4:])] = archive.read(name)
                if "template.svg" in archive.namelist():
                    self.template_data = archive.read("template.svg")

    def save(self):
        self.meta["revision"] += 1
        buffer = io.BytesIO()
        with zipfile.ZipFile(buffer, "w", zipfile.ZIP_STORED) as archive:
            archive.writestr("session.json", json.dumps(self.meta, ensure_ascii=False))
            for name, data in sorted(self.files.items()):
                archive.writestr("map/" + safe_name(name), data)
            if self.template_data is not None:
                archive.writestr("template.svg", self.template_data)
        atomic_bytes(self.path, buffer.getvalue())

    def require_map(self):
        if not self.meta.get("map_source") or "objects.svg" not in self.files:
            raise ValueError("MAP_REQUIRED: run map-read first")

    def require_templates(self):
        if self.template_data is None:
            raise ValueError("TEMPLATE_REQUIRED: run template-read first")

    def require_editable(self):
        self.require_map()
        self.require_templates()

    @property
    def document(self):
        self.require_map()
        return MapDocument(self.files["objects.svg"])

    @property
    def templates(self):
        self.require_templates()
        return Templates(self.template_data)

    @property
    def config(self):
        self.require_map()
        return parse_config(self.files["terrain.cfg"])

    def set_document(self, doc):
        self.files["objects.svg"] = serialize(doc.root)

    def read_map(self, path, replace=False):
        if self.meta.get("map_source") and not replace:
            raise ValueError("A map is already loaded; use a new --session or map-read --replace")
        directory = Path(path).resolve()
        if directory.is_file() and directory.name == "objects.svg":
            directory = directory.parent
        if not directory.is_dir():
            raise ValueError(f"Map directory not found: {directory}")
        if is_within(self.path, directory):
            raise ValueError("Session must be outside the source map directory")
        files = {}
        total = 0
        for file in sorted(directory.rglob("*")):
            if file.is_symlink():
                raise ValueError(f"Map snapshot cannot include symbolic links: {file}")
            if not file.is_file() or file.suffix == ".meta" or any(part.startswith(".") for part in file.relative_to(directory).parts):
                continue
            total += file.stat().st_size
            if total > 512 * 1024 * 1024:
                raise ValueError("Map snapshot exceeds 512 MiB; choose the map directory, not the project")
            files[safe_name(file.relative_to(directory).as_posix())] = file.read_bytes()
        if not {"terrain.cfg", "objects.svg"} <= files.keys():
            raise ValueError("Map needs terrain.cfg and objects.svg")
        config = parse_config(files["terrain.cfg"])
        height_name = safe_name(config.get("Heightmap.image", "terrain5_heightmap.png"))
        config_height = finite(config.get("MaxHeight", 25))
        if config_height <= 0:
            raise ValueError("MaxHeight must be positive")
        for key in ("PageWorldX", "PageWorldZ"):
            if finite(config.get(key, 1024)) <= 0:
                raise ValueError(f"{key} must be positive")
        if height_name not in files:
            raise ValueError(f"Missing configured heightmap: {height_name}")
        if FINAL_ALPHA not in files:
            raise ValueError(f"Missing {FINAL_ALPHA}; rwrme requires the combined RGBA map")
        height = decode(files[height_name], "L")
        alpha = decode(files[FINAL_ALPHA], "RGBA")
        if height.width != height.height:
            raise ValueError("rwrme requires a square heightmap")
        doc = MapDocument(files["objects.svg"])
        # Parse every item before committing; source bytes remain intact.
        counts = doc.counts()
        for pre_name, ref_name, mode in ((PRE_HEIGHT, height_name, "L"), (PRE_ALPHA, FINAL_ALPHA, "RGBA")):
            files.setdefault(pre_name, files[ref_name])
            if decode(files[pre_name], mode).size != decode(files[ref_name], mode).size:
                raise ValueError(f"{pre_name} dimensions differ from {ref_name}")
        self.files = files
        self.meta["map_source"] = str(directory)
        return {"source": str(directory), "file_count": len(files), "items": counts,
                "heightmap": {"file": height_name, "size": list(height.size)}, "alpha_size": list(alpha.size)}

    def read_templates(self, path):
        source = Path(path).resolve()
        if source.is_dir():
            candidates = sorted((p for p in source.iterdir() if p.suffix.lower() in (".xml", ".svg") and p.is_file()), key=lambda p: p.name.lower())
            if not candidates:
                raise ValueError("No .xml/.svg template in directory")
            source = candidates[0]
        data = source.read_bytes()
        template = Templates(data)
        if not template.entries:
            raise ValueError("Template contains no supported definitions")
        names = []
        for index in range(1, 5):
            name = safe_name(template.alpha_names[index])
            if not name.lower().endswith(".png") or name in (PRE_HEIGHT, PRE_ALPHA, FINAL_ALPHA, "terrain5_heightmap.png"):
                raise ValueError(f"Invalid split alpha filename: {name}")
            names.append(name.lower())
        if len(set(names)) != 4:
            raise ValueError("Split alpha filenames must be distinct")
        self.template_data = data
        self.meta["template_source"] = str(source)
        return {"source": str(source), "counts": {kind: sum(t["type"] == kind for t in template.entries) for kind in sorted({t['type'] for t in template.entries})},
                "warnings": template.warnings}

    def status(self):
        return {**self.meta, "session": str(self.path), "editable": bool(self.meta.get("map_source") and self.template_data is not None),
                "items": self.document.counts() if self.meta.get("map_source") else {}, "file_count": len(self.files)}

    def image(self, target, stage="pre"):
        self.require_map()
        if stage == "baked":
            images = bake(self.files, self.config, self.document)
            return images[0 if target == "height" else 1]
        if target == "height":
            name = PRE_HEIGHT if stage == "pre" else self.config.get("Heightmap.image", "terrain5_heightmap.png")
        else:
            name = PRE_ALPHA if stage == "pre" else FINAL_ALPHA
        return decode(self.files[name], "L" if target == "height" else "RGBA")

    def protect_output(self, path):
        output = Path(path).resolve()
        for source in (self.meta.get("map_source"), self.meta.get("template_source")):
            if source and is_within(output, Path(source).resolve()):
                raise ValueError("Output cannot overwrite the source map/template")
        if output == self.path or is_within(self.path, output):
            raise ValueError("Output cannot contain or replace the session")
        # This CLI edits data files outside Assets. Unity asset writes belong to the Editor API.
        for ancestor in (output, *output.parents):
            if ancestor.name == "Assets" and (ancestor.parent / "ProjectSettings").is_dir():
                raise ValueError("Export outside Unity Assets, then import via the Unity Editor")
        return output

    def export(self, path, with_preview=False, preview_size=2048):
        self.require_editable()
        output = self.protect_output(path)
        if output.exists():
            raise ValueError("Export destination already exists; choose a new directory")
        template, doc = self.templates, self.document
        if any(safe_name(template.alpha_names[i]).lower() == self.config.get("Heightmap.image", "terrain5_heightmap.png").lower() for i in range(1, 5)):
            raise ValueError("Split alpha filenames cannot overwrite the configured heightmap")
        height, alpha = bake(self.files, self.config, doc)
        doc.embed_templates(template)
        exported = dict(self.files)
        svg = serialize(doc.root)
        # Same Unity VectorGraphics guard used by MapExporter.SanitizeSvgForUnityImport.
        import re
        svg = re.sub(rb'stroke-miterlimit:([0-9.]+)', lambda m: b'stroke-miterlimit:1' if float(m[1]) < 1 else m[0], svg)
        exported["objects.svg"] = svg
        height_bytes = encode(height)
        exported[self.config.get("Heightmap.image", "terrain5_heightmap.png")] = height_bytes
        exported["terrain5_heightmap.png"] = height_bytes
        exported[FINAL_ALPHA] = encode(alpha)
        for index in range(1, 5):
            name = safe_name(template.alpha_names[index])
            channel = alpha.getchannel("RGBA"[index-1])
            exported[name] = encode(ImageOps.invert(channel) if index == 4 else channel)
        preview_warnings = []
        if with_preview:
            from .preview import render
            preview, preview_warnings = render(doc, height, alpha, self.config, preview_size)
            exported["map_view_ls.png"] = encode(preview)
        output.parent.mkdir(parents=True, exist_ok=True)
        staging = Path(tempfile.mkdtemp(prefix=".rwrmap-export-", dir=output.parent))
        try:
            for name, data in exported.items():
                file = staging / safe_name(name)
                file.parent.mkdir(parents=True, exist_ok=True)
                file.write_bytes(data)
            # Destination is new and fully built before publishing it.
            if output.exists():
                raise ValueError("Export destination was created by another process")
            staging.rename(output)
        finally:
            if staging.exists():
                shutil.rmtree(staging)
        return {"directory": str(output), "file_count": len(exported), "item_count": sum(doc.counts().values()),
                "height_size": list(height.size), "alpha_size": list(alpha.size), "preview_generated": with_preview,
                "warnings": preview_warnings if with_preview else ["Existing preview files are copied unchanged; use --with-preview or preview to regenerate map_view_ls.png"]}
