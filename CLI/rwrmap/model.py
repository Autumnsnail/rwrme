"""rwrme SVG/template model. Preserve unknown XML instead of rebuilding the map."""
import copy
import re
import uuid
import xml.etree.ElementTree as ET
from collections import Counter
from dataclasses import dataclass

import numpy as np

from .geometry import apply, finite, fmt, path_points, polyline_data, rectangle, transform

SVG = "http://www.w3.org/2000/svg"
INK = "http://www.inkscape.org/namespaces/inkscape"
ET.register_namespace("", SVG)
for prefix, uri in [("inkscape", INK), ("sodipodi", "http://sodipodi.sourceforge.net/DTD/sodipodi-0.dtd"),
                    ("xlink", "http://www.w3.org/1999/xlink"), ("rdf", "http://www.w3.org/1999/02/22-rdf-syntax-ns#"),
                    ("dc", "http://purl.org/dc/elements/1.1/"), ("cc", "http://creativecommons.org/ns#")]:
    ET.register_namespace(prefix, uri)

TEMPLATE_TYPES = ("mesh", "building", "wall", "decal", "post", "terrain")
SUBGROUPS = {"mesh": "meshs", "building": "buildings", "wall": "walls", "decal": "decals", "post": "posts"}
DEFAULT_ALPHA = {1: "terrain5_alpha_sand.png", 2: "terrain5_alpha_grass.png",
                 3: "terrain5_alpha_asphalt.png", 4: "terrain5_alpha_road.png"}


def local(element):
    return element.tag.split("}")[-1] if isinstance(element.tag, str) else ""


def label(element):
    return element.get("{" + INK + "}label", element.get("label", ""))


def xml(data):
    if b"<!ENTITY" in data.upper():
        raise ValueError("XML entity declarations are not supported")
    return ET.fromstring(data, parser=ET.XMLParser(target=ET.TreeBuilder(insert_comments=True, insert_pis=True)))


def serialize(root):
    return ET.tostring(root, encoding="utf-8", xml_declaration=True)


def child(parent, tag, attributes=None):
    ns = parent.tag.rsplit("}", 1)[0] + "}" if "}" in parent.tag else ""
    return ET.SubElement(parent, ns + tag, attributes or {})


def properties(element):
    result = {}
    for desc in element:
        if local(desc) not in ("desc", "title"):
            continue
        for part in "".join(desc.itertext()).split(";"):
            if "=" not in part:
                continue
            key, value = part.split("=", 1)
            key = key.strip()
            if key == "#offset":
                key = "offset"
            if not key.startswith("#"):
                result[key] = value.strip()
    return result


def set_properties(element, props):
    desc = next((c for c in element if local(c) == "desc"), None)
    if desc is None:
        desc = child(element, "desc")
    for key, value in props.items():
        if not re.fullmatch(r"[\w.:-]+", key) or any(c in str(value) for c in ";\r\n"):
            raise ValueError("Property keys/values cannot contain description delimiters")
    desc.text = "".join(f"{key} = {value};" for key, value in props.items())


def materials(root):
    return next((e for e in root if local(e) == "g" and label(e) == "materials"), None)


class Templates:
    def __init__(self, data):
        self.root = xml(data)
        self.layer = materials(self.root)
        if self.layer is None:
            raise ValueError('Template requires a root g with inkscape:label="materials"')
        self.entries = []
        self.warnings = []
        self.alpha_names = DEFAULT_ALPHA.copy()
        for rect in self.layer:
            if local(rect) != "rect" or label(rect).startswith("#general"):
                continue
            props = properties(rect)
            name = props.get("name")
            kind = None
            if label(rect).startswith("#mesh"):
                kind = "mesh"
            elif label(rect).startswith("#material") and "type" not in props:
                kind = "building"
            elif label(rect).startswith("#wall_template"):
                kind = "wall"
            elif label(rect).startswith("#terrain_layer") and "alpha" in props:
                kind = "terrain"
                index = int(props["index"])
                name = name or str(index)
                self.alpha_names[index] = props["alpha"]
            elif rect.get("id", "").startswith("decal_template"):
                kind = "decal"
            elif rect.get("id", "").startswith("post_template") and "mesh_template" in props:
                kind = "post"
            if not kind or not name:
                continue
            size = [finite(rect.get(k, 0)) for k in ("width", "height")]
            resolved = props.copy()
            if kind == "mesh" and "base_template" in props:
                base = next((t for t in self.entries if t["type"] == "mesh" and t["name"] == props["base_template"]), None)
                if base:
                    resolved = {**base["resolved"], **props}
                    size = base["size"].copy()
                else:
                    self.warnings.append(f"{name}: base_template {props['base_template']} was not defined earlier; rwrme does not resolve forward references")
            if any(t["type"] == kind and t["name"] == name for t in self.entries):
                self.warnings.append(f"Duplicate {kind} template {name}; first definition wins")
            self.entries.append({"type": kind, "name": name, "size": size, "properties": props,
                                 "resolved": resolved, "source_id": rect.get("id", "")})

    def find(self, kind, name):
        result = next((t for t in self.entries if t["type"] == kind and t["name"] == name), None)
        if result is None:
            raise ValueError(f"No loaded {kind} template named {name!r}")
        return result


@dataclass
class Item:
    element: ET.Element
    parent: ET.Element
    kind: str
    layer: str
    matrix: np.ndarray
    container: ET.Element | None = None
    partner: ET.Element | None = None

    @property
    def id(self):
        return self.element.get("id", "")

    def info(self):
        data = {"id": self.id, "type": self.kind, "layer": self.layer,
                "properties": properties(self.element), "attributes": dict(self.element.attrib)}
        if local(self.element) == "rect":
            corners = rectangle(self.element, self.matrix)
            data.update(position=corners[0].tolist(), corners=corners.tolist())
        elif local(self.element) == "path":
            data["path"] = self.element.get("d", "")
        if self.partner is not None:
            data["partner_id"] = self.partner.get("id")
            data["partner_path"] = self.partner.get("d", "")
        data["transform_matrix"] = self.matrix[:2].tolist()
        return data


class MapDocument:
    def __init__(self, data):
        self.root = xml(data)
        if local(self.root) != "svg":
            raise ValueError("objects.svg must have an svg root")

    def items(self):
        result = []

        def walk(parent, layer, matrix):
            for element in parent:
                tag, lab, eid = local(element), label(element), element.get("id", "")
                combined = matrix @ transform(element.get("transform", ""))
                if tag == "g":
                    paths = [p for p in element if local(p) == "path"]
                    start = next((p for p in paths if properties(p).get("type") in ("start", "deck_start")), None)
                    end = next((p for p in paths if properties(p).get("type") in ("end", "deck_end")), None)
                    if start is not None and end is not None:
                        # rwrme imports platform ID from the first/start path, not the group/end.
                        result.append(Item(start, element, "platform", layer, combined @ transform(start.get("transform", "")), parent, end))
                    else:
                        walk(element, layer, combined)
                    continue
                kind = None
                if tag == "rect":
                    if layer.startswith("bases"):
                        kind = "base"
                    elif eid.startswith("building"):
                        kind = "building"
                    elif eid.startswith("item_supply"):
                        kind = "item_supply"
                    elif eid.startswith("crate"):
                        kind = "crate"
                    elif eid.startswith("decal"):
                        kind = "decal"
                    elif lab.startswith("#mesh"):
                        kind = "mesh"
                    elif lab.startswith("#spawnrect"):
                        kind = "spawnpoint"
                    elif eid.startswith("spawn"):
                        kind = "vehicle" if properties(element).get("type") == "vehicle" else "spawnpoint"
                    elif lab.startswith("rock"):
                        kind = "rock"
                    elif lab.startswith("tree"):
                        kind = "tree"
                    elif lab.startswith("#ladder"):
                        kind = "ladder"
                elif tag == "path":
                    if layer.startswith("height_paths"):
                        kind = "height_path"
                    elif layer.startswith("material_paths"):
                        kind = "material_path"
                    elif layer.startswith("offroad"):
                        kind = "offroad"
                    elif eid.startswith("wall"):
                        kind = "wall"
                    elif lab.startswith("post"):
                        kind = "post"
                if kind:
                    # MapImporter.ImportHeightPathsFromGroup/ImportMaterialPathsFromGroup
                    # apply the group transform only (individual path transforms are ignored).
                    effective = matrix if kind in ("height_path", "material_path") else combined
                    result.append(Item(element, parent, kind, layer, effective))
                elif tag in ("path", "rect", "circle", "ellipse", "polygon", "polyline", "use"):
                    # Expose unrecognized objects for inspection; preserve all their XML.
                    result.append(Item(element, parent, "unknown", layer, combined))

        for group in self.root:
            lab = label(group)
            if local(group) == "g" and (lab.startswith(("bases", "offroad", "height_paths", "material_paths")) or re.fullmatch(r"layer(?:\d+)?(?:\.[\w]+)?", lab)):
                walk(group, lab, transform(group.get("transform", "")))
        return result

    def find(self, item_id):
        found = [i for i in self.items() if i.id == item_id or (i.partner is not None and i.partner.get("id") == item_id)]
        if len(found) != 1:
            raise ValueError(f"Expected one item with ID {item_id!r}; found {len(found)}")
        return found[0]

    def delete(self, item_id):
        item = self.find(item_id)
        if item.container is not None:
            item.container.remove(item.parent)
        else:
            item.parent.remove(item.element)
        return {"deleted": item.id, "type": item.kind}

    def counts(self):
        return dict(Counter(i.kind for i in self.items()))

    def new_id(self, prefix):
        used = {e.get("id") for e in self.root.iter()}
        while True:
            candidate = prefix + "_cli_" + uuid.uuid4().hex[:12]
            if candidate not in used:
                return candidate

    def add(self, templates, kind, name, x=None, y=None, width=None, depth=None, rotation=0, layer=1, points=None, props=None, item_id=None):
        if kind not in SUBGROUPS:
            raise ValueError("Template creation supports mesh, building, wall, decal, post")
        template = templates.find(kind, name)
        if layer < 1:
            raise ValueError("layer must be >= 1")
        item_id = item_id or self.new_id(kind)
        if not re.fullmatch(r"[#A-Za-z_][\w.#:-]*", item_id):
            raise ValueError("Invalid SVG item ID")
        if not item_id.startswith(kind):
            raise ValueError(f"Use an ID beginning with {kind} to avoid rwrme's prefix-based type detection")
        if any(e.get("id") == item_id for e in self.root.iter()):
            raise ValueError(f"ID already exists: {item_id}")
        # Use an untransformed group, so CLI coordinates always remain absolute SVG coordinates.
        layer_label = "layer" + str(layer)
        top = next((e for e in self.root if local(e) == "g" and label(e) == layer_label and not e.get("transform")), None)
        if top is None:
            top = child(self.root, "g", {"id": self.new_id("layer"), "{"+INK+"}label": layer_label, "{"+INK+"}groupmode": "layer"})
        group_label = SUBGROUPS[kind]
        group = next((e for e in top if local(e) == "g" and label(e) == group_label and not e.get("transform")), None)
        if group is None:
            group = child(top, "g", {"id": self.new_id(group_label), "{"+INK+"}label": group_label})
        values = dict(props or {})
        key = {"building": "material", "post": "post_template"}.get(kind, "template")
        if key in values and str(values[key]) != name:
            raise ValueError(f"Use --template to set {key}")
        values[key] = name
        if kind == "building":
            if width is None or depth is None:
                raise ValueError("Building templates are materials, not footprints; specify --width and --depth")
            values.setdefault("height", "1")
            values.setdefault("roof_type", "flat")
            h = finite(values["height"])
            if h < 0 or h != int(h):
                raise ValueError("Building height must be a nonnegative integer")
        if kind in ("wall", "post"):
            element = child(group, "path", {"id": item_id, "d": polyline_data(points), "style": "fill:none;stroke:#008000;stroke-width:2"})
            if kind == "post":
                element.set("{"+INK+"}label", "post")
        else:
            if x is None or y is None:
                raise ValueError("Rect items require --x and --y")
            w = template["size"][0] if width is None else finite(width)
            h = template["size"][1] if depth is None else finite(depth)
            if w <= 0 or h <= 0:
                raise ValueError("Rect dimensions must be positive; specify --width/--depth")
            angle = np.deg2rad(-finite(rotation))
            c, s = np.cos(angle), np.sin(angle)
            element = child(group, "rect", {"id": item_id, "x": "0", "y": "0", "width": fmt(w), "height": fmt(h),
                            "transform": "matrix(" + ",".join(fmt(v) for v in (c, s, -s, c, x, y)) + ")",
                            "{"+INK+"}label": "#mesh" if kind == "mesh" else "#"+kind,
                            "style": "fill:#808080;stroke:#000000;stroke-width:1"})
        set_properties(element, values)
        return self.find(item_id).info()

    def embed_templates(self, templates):
        existing = materials(self.root)
        general = [] if existing is None else [copy.deepcopy(e) for e in existing if local(e) == "rect" and label(e).startswith("#general")]
        replacement = copy.deepcopy(templates.layer)
        for element in list(replacement):
            if local(element) == "rect" and label(element).startswith("#general"):
                replacement.remove(element)
        replacement.extend(general)
        if existing is not None:
            index = list(self.root).index(existing)
            self.root.remove(existing)
        else:
            index = 0
        self.root.insert(index, replacement)

    def paths(self, kind):
        for item in self.items():
            if item.kind == kind:
                points = apply(item.matrix, path_points(item.element.get("d", "")))
                # MePathCurve.Append skips coincident samples (<1e-8 squared distance).
                filtered = []
                for point in points:
                    if not filtered or np.sum((point-filtered[-1])**2) >= 1e-8:
                        filtered.append(point)
                yield item, np.asarray(filtered).reshape(-1, 2)
