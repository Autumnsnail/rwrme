"""CPU tactical preview following MapExporter.exportMapViewPic's draw order."""
from collections import defaultdict
import re

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

from .geometry import apply, finite, path_points, rectangle, transform
from .model import local, properties


def bilinear(array, width, height):
    xs, ys = np.linspace(0, array.shape[1]-1, width), np.linspace(0, array.shape[0]-1, height)
    x0, y0 = xs.astype(int), ys.astype(int)
    x1, y1 = np.minimum(x0+1, array.shape[1]-1), np.minimum(y0+1, array.shape[0]-1)
    tx, ty = (xs-x0)[None, :], (ys-y0)[:, None]
    return ((array[y0[:, None], x0]*(1-tx)+array[y0[:, None], x1]*tx)*(1-ty)
            + (array[y1[:, None], x0]*(1-tx)+array[y1[:, None], x1]*tx)*ty).astype(np.float32)


def render(document, height, alpha, config, size=2048, water_level=2, overlay_items=False):
    if not 64 <= size <= 2048:
        raise ValueError("Preview size must be between 64 and 2048")
    draw_size = size * 2
    span = finite(config.get("PageWorldX", 1024))
    water_level = finite(water_level)
    max_height = finite(config.get("MaxHeight", 25))
    warnings = []
    items = document.items()

    def project(points):
        pts = np.asarray(points).reshape(-1, 2).copy()
        pts[:, 0] = pts[:, 0] / 2 / span * (draw_size-1)
        pts[:, 1] = (draw_size-1) - (2048-pts[:, 1]) / 2 / span * (draw_size-1)
        return [tuple(p) for p in pts]

    pixels = np.full((draw_size, draw_size, 3), 255, dtype=np.uint8)
    density = np.zeros((64, 64), dtype=float)
    cell = max(1, (draw_size+63)//64)
    for item in items:
        name = properties(item.element).get("template", "").lower()
        if item.kind == "tree" or (item.kind == "mesh" and "tree" in name):
            center = rectangle(item.element, item.matrix).mean(axis=0)
            x, y = project([center])[0]
            # Grid is stored top-down here; reverse rows before smoothing is unnecessary.
            density[min(63, max(0, int(y/cell))), min(63, max(0, int(x/cell)))] += 1
    for _ in range(2):
        padded = np.pad(density, 1, mode="edge")
        density = sum(padded[y:y+64, x:x+64] for y in range(3) for x in range(3))/9
    flat = np.sort(density.ravel())
    if flat[-1] > 0:
        lo, hi = flat[int(4095*.33)], flat[int(4095*.66)]
        if hi <= lo:
            hi = min(flat[-1], lo+1)
        dense = bilinear(density, draw_size, draw_size)
        stripe = pixels[:, ::7]
        sampled = dense[:, ::7]
        stripe[sampled > lo] = (225, 225, 225)
        stripe[sampled > hi] = (152, 152, 152)
        del dense
    a = np.array(alpha)
    xs = np.rint(np.linspace(0, alpha.width-1, draw_size)).astype(int)
    ys = np.rint(np.linspace(0, alpha.height-1, draw_size)).astype(int)
    pixels[a[ys[:, None], xs, 2] >= 128] = (213, 213, 213)
    pixels[a[ys[:, None], xs, 3] < 128] = (72, 72, 72)
    image = Image.fromarray(pixels)
    draw = ImageDraw.Draw(image)
    for item in items:
        if item.kind != "base":
            continue
        corners = project(rectangle(item.element, item.matrix))
        phase = 0
        for start, end in zip(corners, corners[1:] + corners[:1]):
            start, end = np.array(start), np.array(end)
            steps = max(1, int(np.linalg.norm(end-start)))
            # Fixed draw-size dash width mirrors rwrme's supersampled base outline.
            for i in range(steps):
                if (phase+i) % 8 < 4:
                    p = start + (end-start)*i/steps
                    draw.rectangle((p[0]-1, p[1]-1, p[0]+1, p[1]+1), fill=(127, 127, 127))
            phase += steps
    heights = bilinear(np.array(height, dtype=np.float32)/255, draw_size, draw_size)*max_height
    pixels = np.array(image)
    water = heights < water_level
    pixels[water] = (119, 174, 213)
    bands = np.floor(heights/10)
    contour = np.zeros(water.shape, dtype=bool)
    contour[:, :-1] |= (water[:, :-1] != water[:, 1:]) | (~water[:, :-1] & ~water[:, 1:] & (bands[:, :-1] != bands[:, 1:]))
    contour[1:, :] |= (water[1:, :] != water[:-1, :]) | (~water[1:, :] & ~water[:-1, :] & (bands[1:, :] != bands[:-1, :]))
    pixels[contour] = 0
    image = Image.fromarray(pixels)
    del pixels, heights, bands, contour, water
    draw = ImageDraw.Draw(image)
    for item in items:
        if item.kind != "platform":
            continue
        try:
            right = project(apply(item.matrix, path_points(item.element.get("d", ""))))
            parent_matrix = item.matrix @ np.linalg.inv(transform(item.element.get("transform", "")))
            left = project(apply(parent_matrix @ transform(item.partner.get("transform", "")), path_points(item.partner.get("d", ""))))
            props = properties(item.partner)
            n = min(len(left), len(right))
            if n < 2:
                continue
            if props.get("top_material", "terrain").lower() != "terrain":
                for i in range(n-1):
                    draw.polygon([left[i], right[i], right[i+1], left[i+1]], fill=(230, 230, 230))
            draw.line([left[0], right[0]], fill=0, width=2)
            draw.line([left[n-1], right[n-1]], fill=0, width=2)
            if props.get("mode") != "bridge":
                draw.line(left, fill=0, width=2)
                if props.get("type", "").startswith("deck"):
                    draw.line(right, fill=0, width=2)
        except ValueError as error:
            warnings.append(f"Skipped platform {item.id}: {error}")
    buildings = defaultdict(list)
    for item in items:
        if item.kind == "building":
            match = re.match(r"layer(\d+)", item.layer)
            buildings[(int(match[1]) if match else 1, finite(properties(item.element).get("height", 1)))].append(item)
    for key in sorted(buildings):
        mask = Image.new("L", image.size)
        md = ImageDraw.Draw(mask)
        for item in buildings[key]:
            md.polygon(project(rectangle(item.element, item.matrix)), fill=255)
        image.paste((128, 128, 128), mask=mask)
        array = np.array(mask) > 0
        interior = np.pad(array, 1)
        inside = interior[1:-1, :-2] & interior[1:-1, 2:] & interior[:-2, 1:-1] & interior[2:, 1:-1]
        image.paste((0, 0, 0), mask=Image.fromarray((array & ~inside).astype("uint8")*255))
    draw = ImageDraw.Draw(image)
    for item in items:
        name = properties(item.element).get("template", "").lower()
        if item.kind == "wall":
            try:
                points = project(apply(item.matrix, path_points(item.element.get("d", ""))))
                if len(points) >= 2:
                    draw.line(points, fill=0, width=2)
            except ValueError as error:
                warnings.append(f"Skipped wall {item.id}: {error}")
        elif item.kind == "rock" or (item.kind == "mesh" and name.startswith("rock")):
            x, y = project([rectangle(item.element, item.matrix).mean(axis=0)])[0]
            draw.ellipse((x-3, y-3, x+3, y+3), fill=(145, 145, 145))
        elif overlay_items and local(item.element) == "rect":
            points = project(rectangle(item.element, item.matrix))
            draw.line(points+points[:1], fill=(190, 70, 30), width=2)
    image = image.resize((size, size), Image.Resampling.BOX)
    # Same five-tap binomial kernel and two passes; Pillow handles edge pixels differently.
    kernel = [a*b for a in (1, 4, 6, 4, 1) for b in (1, 4, 6, 4, 1)]
    blurred = image
    for _ in range(2):
        blurred = blurred.filter(ImageFilter.Kernel((5, 5), kernel, scale=256))
    return Image.blend(image, blurred, .42), warnings
