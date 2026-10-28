"""Pixel IO and the pre_ + paths bake used by MetaMap.TerrainPathRasterizer."""
import io

import numpy as np
from PIL import Image

from .geometry import distance_to_path, finite, inside_polygon, stroke_polygon
from .model import properties

PRE_HEIGHT = "pre_terrain5_heightmap.png"
PRE_ALPHA = "pre_terrain5_combined_alpha.png"
FINAL_ALPHA = "terrain5_combined_alpha.png"
COLORS = np.array([(0, 0, 0, 255), (255, 0, 0, 255), (0, 255, 0, 255), (0, 0, 255, 255), (0, 0, 0, 0)], dtype=float)
MATERIALS = ("base", "sand", "grass", "asphalt", "road")


def decode(data, mode):
    with Image.open(io.BytesIO(data)) as image:
        image.load()
        # rwrme LoadImage -> Color.grayscale is an 8-bit workflow.
        if mode == "L" and image.mode.startswith(("I", "F")):
            raise ValueError("16-bit/float heightmaps are not supported by this CLI; use an 8-bit PNG")
        return image.convert(mode)


def encode(image):
    stream = io.BytesIO()
    image.save(stream, format="PNG")
    return stream.getvalue()


def parse_config(data):
    result = {}
    for line in data.decode("utf-8-sig").splitlines():
        line = line.strip()
        if line.startswith(("//", "#", ";")) or "=" not in line:
            continue
        key, value = line.split("=", 1)
        result[key.strip()] = value.strip()
    if "Heightmap.image" in result:
        result["Heightmap.image"] = result["Heightmap.image"].replace("\\", "/")
    return result


def bake(files, config, document):
    """Return final height and RGBA images; leave the editable baseline untouched."""
    height_name = config.get("Heightmap.image", "terrain5_heightmap.png")
    base = np.array(decode(files.get(PRE_HEIGHT, files[height_name]), "L"), dtype=np.float32) / 255
    height = base.copy()
    h, w = height.shape
    if h != w:
        raise ValueError("rwrme height path baking requires a square heightmap")
    world_x, world_z = [finite(config.get(k, 1024)) for k in ("PageWorldX", "PageWorldZ")]
    if world_x <= 0 or world_z <= 0:
        raise ValueError("PageWorldX/PageWorldZ must be positive")
    for item, points in document.paths("height_path"):
        props = properties(item.element)
        width = finite(props.get("width", 20))
        if len(points) < 2 or width <= 0:
            continue
        polygon = stroke_polygon(points, width / 2)
        low, high = polygon.min(axis=0), polygon.max(axis=0)
        x0 = max(0, min(w-1, int(low[0] / 2 / world_x * w)))
        x1 = max(0, min(w-1, int(high[0] / 2 / world_x * w)))
        y0 = h - 1 - max(0, min(h-1, int((2048-low[1]) / 2 / world_z * w)))
        y1 = h - 1 - max(0, min(h-1, int((2048-high[1]) / 2 / world_z * w)))
        xs = (np.arange(x0, x1+1)[None, :] + .5) / w * world_x * 2
        ys = 2048 - (h-np.arange(y0, y1+1)[:, None]-.5) / w * world_z * 2
        mask = inside_polygon(xs, ys, polygon)
        current = height[y0:y1+1, x0:x1+1]
        value = finite(props.get("height_offset", props.get("height", props.get("height_delta", .03))))
        default_mode = "offset" if "height_offset" in props else "set" if "height" in props else "raise"
        mode = props.get("mode", default_mode)
        if mode == "set":
            next_value = np.full_like(current, value)
        elif mode == "offset":
            next_value = base[y0:y1+1, x0:x1+1] + value
        elif mode == "lower":
            next_value = current - value
        elif mode == "raise":
            next_value = current + value
        else:
            raise ValueError(f"Unknown height path mode: {mode}")
        current[mask] = np.clip(next_value, 0, 1)[mask]
    alpha = np.array(decode(files.get(PRE_ALPHA, files[FINAL_ALPHA]), "RGBA"), dtype=np.float32)
    ah, aw, _ = alpha.shape
    for item, points in document.paths("material_path"):
        props = properties(item.element)
        width = finite(props.get("width", 20)) / 2
        if len(points) < 2 or width <= 0:
            continue
        low, high = points.min(axis=0)-width, points.max(axis=0)+width
        # rwrme: Unity pixel=(svg.x/2, (2048-svg.y)/2+512)*textureSize/2048.
        # Pillow stores rows top-down, while Unity GetPixels uses bottom-up rows.
        x0, x1 = max(0, int(low[0]*aw/4096)), min(aw-1, int(high[0]*aw/4096))
        y0 = max(0, ah-1-int((3072-low[1])*ah/4096))
        y1 = min(ah-1, ah-1-int((3072-high[1])*ah/4096))
        if x1 < x0 or y1 < y0:
            continue
        xs = (np.arange(x0, x1+1)[None, :]+.5)*4096/aw
        ys = 3072-(ah-np.arange(y0, y1+1)[:, None]-.5)*4096/ah
        distance = distance_to_path(xs, ys, points)
        hardness = max(finite(props.get("hardness", 1)), 1e-4)
        weight = np.maximum(0, 1-distance/width)**hardness
        index = int(props["material_index"]) if "material_index" in props else (MATERIALS.index(props["material"].lower()) if props.get("material", "").lower() in MATERIALS else 2)
        paint = COLORS[index if 0 <= index <= 4 else 0]
        current = alpha[y0:y1+1, x0:x1+1]
        current[:] = np.rint(current + (paint-current) * weight[:, :, None])
    return Image.fromarray(np.rint(np.clip(height, 0, 1)*255).astype("uint8")), Image.fromarray(alpha.astype("uint8"))


def pixel(image, x, y, origin):
    if x < 0 or y < 0 or x >= image.width or y >= image.height:
        raise ValueError(f"Pixel ({x},{y}) outside {image.width}x{image.height}")
    return x, image.height-1-y if origin == "bottom-left" else y


def edit_pixels(image, edits, origin="top-left"):
    """Validate a complete patch before changing any pixel."""
    prepared = []
    for edit in edits:
        if not isinstance(edit, dict) or not {"x", "y", "value"} <= edit.keys():
            raise ValueError("Each pixel edit needs x, y and value")
        if type(edit["x"]) is not int or type(edit["y"]) is not int:
            raise ValueError("Pixel x/y must be integers")
        position = pixel(image, edit["x"], edit["y"], origin)
        value = edit["value"]
        channel = edit.get("channel")
        if channel is not None:
            if image.mode != "RGBA" or channel not in ("r", "g", "b", "a", "road"):
                raise ValueError("Alpha channels: r, g, b, a, road (255-A)")
            values = [value]
        else:
            values = [value] if image.mode == "L" else value
            if not isinstance(values, list) or len(values) != (1 if image.mode == "L" else 4):
                raise ValueError("Height requires a byte; alpha requires [r,g,b,a] or --channel")
        if any(type(v) is not int or not 0 <= v <= 255 for v in values):
            raise ValueError("Pixel values must be integers in 0..255")
        prepared.append((position, value, channel))
    result = image.copy()
    for position, value, channel in prepared:
        if channel:
            rgba = list(result.getpixel(position))
            rgba[3 if channel == "road" else "rgba".index(channel)] = 255-value if channel == "road" else value
            value = tuple(rgba)
        elif image.mode == "RGBA":
            value = tuple(value)
        result.putpixel(position, value)
    return result
