"""SVG geometry used by inspection, terrain baking and previews."""
import math
import re

import numpy as np

NUMBER = r"[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?"


def finite(value):
    result = float(value)
    if not math.isfinite(result):
        raise ValueError("Numbers must be finite")
    return result


def fmt(value):
    return format(finite(value), ".9g")


def transform(text):
    matrix = np.eye(3)
    rest = re.sub(r"([a-zA-Z]+)\s*\(([^)]*)\)", "", text)
    if rest.strip(" ,\t\r\n"):
        raise ValueError(f"Invalid SVG transform: {text}")
    for kind, body in re.findall(r"([a-zA-Z]+)\s*\(([^)]*)\)", text):
        args = [finite(n) for n in re.findall(NUMBER, body)]
        step = np.eye(3)
        if kind == "matrix" and len(args) == 6:
            a, b, c, d, e, f = args
            step = np.array([[a, c, e], [b, d, f], [0, 0, 1]])
        elif kind == "translate" and len(args) in (1, 2):
            step[:2, 2] = args if len(args) == 2 else [args[0], 0]
        elif kind == "scale" and len(args) in (1, 2):
            step[0, 0], step[1, 1] = args if len(args) == 2 else args * 2
        elif kind == "rotate" and len(args) in (1, 3):
            angle = math.radians(args[0])
            c, s = math.cos(angle), math.sin(angle)
            step[:2, :2] = [[c, -s], [s, c]]
            if len(args) == 3:
                pivot = np.array(args[1:])
                step[:2, 2] = pivot - step[:2, :2] @ pivot
        else:
            raise ValueError(f"Unsupported SVG transform: {kind}({body})")
        matrix = matrix @ step
    return matrix


def apply(matrix, points):
    points = np.asarray(points, dtype=float).reshape(-1, 2)
    return points @ matrix[:2, :2].T + matrix[:2, 2]


def rectangle(element, matrix):
    x, y, w, h = [finite(element.get(k, 0)) for k in ("x", "y", "width", "height")]
    return apply(matrix, [(x, y), (x + w, y), (x + w, y + h), (x, y + h)])


def path_points(data, samples=24):
    """Flatten cubics at rwrme's 24 samples/segment. Reject unknown commands."""
    tokens = re.findall(r"[a-zA-Z]|" + NUMBER, data)
    if re.sub(r"[a-zA-Z]|" + NUMBER, "", data).strip(" ,\t\r\n"):
        raise ValueError("Invalid SVG path data")
    result = []
    current = np.zeros(2)
    start = current.copy()
    last_control = None
    previous = ""
    cmd = None
    i = 0
    while i < len(tokens):
        if tokens[i].isalpha():
            cmd = tokens[i]
            i += 1
            if cmd.upper() == "Z":
                result.append(start.copy())
                current = start.copy()
                previous, cmd = cmd, None
                continue
        if cmd is None or cmd.upper() not in "MLHVCSQT":
            raise ValueError(f"Unsupported SVG path command: {cmd}")
        op = cmd.upper()
        count = {"M": 2, "L": 2, "H": 1, "V": 1, "C": 6, "S": 4, "Q": 4, "T": 2}[op]
        if i + count > len(tokens) or any(t.isalpha() for t in tokens[i:i + count]):
            raise ValueError(f"Incomplete SVG path command {cmd}")
        values = np.array([finite(v) for v in tokens[i:i + count]])
        i += count
        relative = cmd.islower()
        if op in "HV":
            end = current.copy()
            axis = 0 if op == "H" else 1
            end[axis] = values[0] + (current[axis] if relative else 0)
        else:
            values = values.reshape(-1, 2) + (current if relative else 0)
            end = values[-1]
        if op == "M":
            start = end.copy()
            # rwrme combines subpaths into its MePath anchor list.
            result.append(end.copy())
            cmd = "l" if relative else "L"
        elif op in "CSQT":
            if op in "CS":
                c1 = values[0] if op == "C" else (
                    2 * current - last_control if previous.upper() in ("C", "S") else current)
                c2 = values[1] if op == "C" else values[0]
                last_control = c2.copy()
            else:
                q = values[0] if op == "Q" else (
                    2 * current - last_control if previous.upper() in ("Q", "T") else current)
                c1, c2 = current + (q - current) * 2 / 3, end + (q - end) * 2 / 3
                last_control = q.copy()
            for t in np.linspace(0, 1, samples + 1)[1:]:
                result.append((1-t)**3 * current + 3*(1-t)**2*t*c1 + 3*(1-t)*t*t*c2 + t**3*end)
        else:
            result.append(end.copy())
        current = end.copy()
        previous = op
    return np.asarray(result, dtype=float).reshape(-1, 2)


def polyline_data(points):
    if not isinstance(points, list) or len(points) < 2:
        raise ValueError("points requires at least two [x,y] coordinates")
    pts = []
    for point in points:
        if not isinstance(point, list) or len(point) != 2:
            raise ValueError("Each point must be [x,y]")
        pts.append([finite(n) for n in point])
    previous = np.zeros(2)
    parts = []
    for i, point in enumerate(pts):
        point = np.array(point)
        delta = point - previous
        parts.append(("m " if i == 0 else "l ") + fmt(delta[0]) + "," + fmt(delta[1]))
        previous = point
    return " ".join(parts)


def translate_path(data, dx, dy):
    """Translate raw path coordinates without replacing cubic control points."""
    path_points(data)  # Validate command coverage before mutation.
    tokens = re.findall(r"[a-zA-Z]|" + NUMBER, data)
    output, cmd, first, i = [], None, True, 0
    while i < len(tokens):
        if tokens[i].isalpha():
            cmd = tokens[i]
            output.append(cmd)
            i += 1
            if cmd.upper() == "Z":
                continue
        count = {"M": 2, "L": 2, "H": 1, "V": 1, "C": 6, "S": 4, "Q": 4, "T": 2}[cmd.upper()]
        values = [finite(v) for v in tokens[i:i+count]]
        if cmd.isupper() or (first and cmd == "m"):
            for j in range(count):
                values[j] += dy if cmd.upper() == "V" or (cmd.upper() != "H" and j % 2) else dx
        output.extend(fmt(v) for v in values)
        first = False
        if cmd == "m":
            cmd = "l"
        elif cmd == "M":
            cmd = "L"
        i += count
    return " ".join(output)


def stroke_polygon(points, half_width):
    """MetaMap.TerrainPathRasterizer.BuildHardStrokePolygon (butt caps, miter<=8)."""
    def unit(v):
        length = np.linalg.norm(v)
        return v / length if length > 1e-5 else np.zeros(2)

    def normal(v):
        return np.array([-v[1], v[0]])

    def cross(a, b):
        return a[0] * b[1] - a[1] * b[0]

    sides = [[], []]
    for i, point in enumerate(points):
        d1 = unit(points[min(i + 1, len(points)-1)] - point) if i == 0 else unit(point - points[i-1])
        d2 = d1 if i == len(points)-1 else unit(points[i+1] - point)
        for side, sign in zip(sides, (1, -1)):
            n1, n2 = sign * normal(d1), sign * normal(d2)
            p1, p2 = point + n1 * half_width, point + n2 * half_width
            det = cross(d1, d2)
            if abs(det) >= 1e-8:
                hit = p1 + d1 * (cross(p2-p1, d2) / det)
                if np.linalg.norm(hit-point) <= half_width * 8:
                    side.append(hit)
                    continue
            bis = n1 + n2
            side.append(point + (n1 if bis @ bis < 1e-10 else unit(bis)) * half_width)
    return np.array(sides[0] + sides[1][::-1])


def inside_polygon(x, y, polygon):
    inside = np.zeros(np.broadcast_shapes(x.shape, y.shape), dtype=bool)
    previous = polygon[-1]
    for point in polygon:
        inside ^= ((point[1] > y) != (previous[1] > y)) & (
            x < (previous[0]-point[0]) * (y-point[1]) / (previous[1]-point[1]+1e-10) + point[0])
        previous = point
    return inside


def distance_to_path(x, y, points):
    best = np.full(np.broadcast_shapes(x.shape, y.shape), np.inf)
    for a, b in zip(points, points[1:]):
        delta = b - a
        length = delta @ delta
        t = 0 if length < 1e-6 else np.clip(((x-a[0])*delta[0] + (y-a[1])*delta[1]) / length, 0, 1)
        best = np.minimum(best, np.hypot(x-a[0]-delta[0]*t, y-a[1]-delta[1]*t))
    return best
