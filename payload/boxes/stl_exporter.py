from __future__ import annotations

import io
import math
import re
import struct
import zipfile
from typing import Iterable

import shapely
import shapely.affinity
from shapely.geometry import LineString, MultiPolygon, Polygon
from shapely.geometry.polygon import orient
from shapely.ops import polygonize, triangulate, unary_union

from boxes.Color import Color
from boxes.drawing import Surface


def sanitize_part_filename(label: str, fallback_idx: int) -> str:
    """Turn a part label into a clean Windows/ZIP filename stem without extension."""
    text = (label or "").strip()
    if not text or text.lower() in ("default", "part", "invisible"):
        return f"Part_{fallback_idx}"
    # Replace newlines and unsafe path characters
    cleaned = re.sub(r'[<>:"/\\|?*\r\n\t]+', "_", text)
    cleaned = re.sub(r"\s+", "_", cleaned).strip("._ ")
    if not cleaned:
        return f"Part_{fallback_idx}"
    return cleaned[:80]


def _is_annotation_color(rgb) -> bool:
    """Return True if path color is the annotation color (used for labels/debug text)."""
    if rgb is None:
        return False
    ann = Color.ANNOTATIONS
    try:
        return (
            int(rgb.red) == int(ann.red)
            and int(rgb.green) == int(ann.green)
            and int(rgb.blue) == int(ann.blue)
        )
    except AttributeError:
        return False


def _sample_cubic_bezier(
    x0: float,
    y0: float,
    x1: float,
    y1: float,
    x2: float,
    y2: float,
    x3: float,
    y3: float,
    steps: int = 12,
) -> list[tuple[float, float]]:
    pts = []
    for s in range(1, steps + 1):
        t = s / float(steps)
        mt = 1.0 - t
        px = (
            (mt**3) * x0
            + 3 * (mt**2) * t * x1
            + 3 * mt * (t**2) * x2
            + (t**3) * x3
        )
        py = (
            (mt**3) * y0
            + 3 * (mt**2) * t * y1
            + 3 * mt * (t**2) * y2
            + (t**3) * y3
        )
        pts.append((round(px, 4), round(py, 4)))
    return pts


def part_to_polygons(part) -> list[Polygon]:
    """Extract clean 2D Shapely Polygons (with holes) from a Boxes.py Part, ignoring text."""
    lines: list[LineString] = []

    for path in getattr(part, "pathes", []):
        if _is_annotation_color(path.params.get("rgb")):
            continue

        path.faster_edges("corner")
        sub: list[tuple[float, float]] = []
        cx, cy = 0.0, 0.0

        for c in path.path:
            cmd = c[0]
            if cmd == "T":
                # Completely omit 2D text glyphs from 3D geometry
                continue
            elif cmd == "M":
                if len(sub) >= 2:
                    if math.hypot(sub[0][0] - sub[-1][0], sub[0][1] - sub[-1][1]) < 0.05:
                        sub[-1] = sub[0]
                    lines.append(LineString(sub))
                cx, cy = round(float(c[1]), 4), round(float(c[2]), 4)
                sub = [(cx, cy)]
            elif cmd == "L":
                nx, ny = round(float(c[1]), 4), round(float(c[2]), 4)
                if math.hypot(nx - cx, ny - cy) > 1e-4:
                    sub.append((nx, ny))
                    cx, cy = nx, ny
            elif cmd == "C":
                x3, y3, x1, y1, x2, y2 = [float(v) for v in c[1:7]]
                for px, py in _sample_cubic_bezier(cx, cy, x1, y1, x2, y2, x3, y3):
                    if not sub or math.hypot(px - sub[-1][0], py - sub[-1][1]) > 1e-4:
                        sub.append((px, py))
                cx, cy = round(x3, 4), round(y3, 4)

        if len(sub) >= 2:
            if math.hypot(sub[0][0] - sub[-1][0], sub[0][1] - sub[-1][1]) < 0.05:
                sub[-1] = sub[0]
            lines.append(LineString(sub))

    if not lines:
        return []

    closed_lines: list[LineString] = []
    open_slits: list[LineString] = []
    for ls in lines:
        coords = list(ls.coords)
        if len(coords) >= 4 and math.hypot(coords[0][0] - coords[-1][0], coords[0][1] - coords[-1][1]) < 1e-3:
            closed_lines.append(ls)
        else:
            open_slits.append(ls)

    # First try polygonizing all lines together so multi-segment contours close
    merged_all = shapely.set_precision(unary_union(lines), grid_size=1e-3)
    polys = list(polygonize(merged_all))

    if not polys and closed_lines:
        merged_closed = shapely.set_precision(unary_union(closed_lines), grid_size=1e-3)
        polys = list(polygonize(merged_closed))

    if not polys:
        return []

    # Even-odd containment nesting (outer solid vs inner cutout holes / islands)
    polys.sort(key=lambda p: p.area, reverse=True)
    result = None
    for p in polys:
        p_clean = p.buffer(0)
        if p_clean.is_empty or p_clean.area < 1e-2:
            continue
        if result is None:
            result = p_clean
        else:
            rep = p_clean.representative_point()
            if result.contains(rep):
                result = result.difference(p_clean)
            else:
                result = result.union(p_clean)

    if result is None or result.is_empty:
        return []

    # Cut open internal slits (e.g., living-hinge flex lines) through the solid
    boundary_geom = result.boundary
    filtered_slits = []
    for sl in open_slits:
        mid = sl.interpolate(0.5, normalized=True)
        if result.contains(mid) and boundary_geom.distance(mid) > 0.05:
            filtered_slits.append(sl.buffer(0.15, cap_style=2))
    if filtered_slits:
        try:
            result = result.difference(unary_union(filtered_slits))
        except Exception:
            pass

    if isinstance(result, Polygon):
        return [result] if result.area >= 1e-2 else []
    elif isinstance(result, MultiPolygon):
        return [g for g in result.geoms if g.area >= 1e-2]
    return [g for g in getattr(result, "geoms", []) if isinstance(g, Polygon) and g.area >= 1e-2]


def _triangulate_polygon(poly: Polygon) -> list[tuple[tuple[float, float], tuple[float, float], tuple[float, float]]]:
    """Triangulate a oriented 2D polygon (with optional holes) into CCW 2D triangles."""
    tris: list[tuple[tuple[float, float], tuple[float, float], tuple[float, float]]] = []
    tri_geoms: Iterable[Polygon] = ()

    try:
        coll = shapely.constrained_delaunay_triangles(poly)
        tri_geoms = getattr(coll, "geoms", ())
    except Exception:
        raw = triangulate(poly)
        tri_geoms = [t for t in raw if poly.contains(t.representative_point())]

    for tri in tri_geoms:
        coords = list(tri.exterior.coords)
        if len(coords) < 4:
            continue
        ax, ay = float(coords[0][0]), float(coords[0][1])
        bx, by = float(coords[1][0]), float(coords[1][1])
        cx, cy = float(coords[2][0]), float(coords[2][1])
        cross = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax)
        if abs(cross) < 1e-10:
            continue
        if cross < 0:
            bx, by, cx, cy = cx, cy, bx, by
        tris.append(((ax, ay), (bx, by), (cx, cy)))

    return tris


def _append_ring_walls(
    facets: list[bytes],
    ring_coords: Iterable[tuple[float, float]],
    thickness: float,
) -> None:
    pts = [(float(p[0]), float(p[1])) for p in ring_coords]
    for i in range(len(pts) - 1):
        x0, y0 = pts[i]
        x1, y1 = pts[i + 1]
        dx = x1 - x0
        dy = y1 - y0
        seg_len = math.hypot(dx, dy)
        if seg_len < 1e-7:
            continue
        nx = dy / seg_len
        ny = -dx / seg_len
        facets.append(
            struct.pack(
                "<12fH",
                nx, ny, 0.0,
                x0, y0, 0.0,
                x1, y1, 0.0,
                x1, y1, thickness,
                0,
            )
        )
        facets.append(
            struct.pack(
                "<12fH",
                nx, ny, 0.0,
                x0, y0, 0.0,
                x1, y1, thickness,
                x0, y0, thickness,
                0,
            )
        )


def polygons_to_binary_stl(polygons: list[Polygon], thickness: float, solid_name: str = "part") -> bytes:
    """Extrude a list of 2D Polygons along +Z by `thickness` mm and return Binary STL bytes."""
    if not polygons or thickness <= 0:
        return b""

    minx = min(p.bounds[0] for p in polygons)
    miny = min(p.bounds[1] for p in polygons)
    shifted_polys = [
        shapely.affinity.translate(orient(p, sign=1.0), xoff=-minx, yoff=-miny)
        for p in polygons
    ]

    facets: list[bytes] = []
    t = float(thickness)

    for poly in shifted_polys:
        tris = _triangulate_polygon(poly)
        if not tris:
            continue

        # Top (+Z) and Bottom (-Z) faces
        for (ax, ay), (bx, by), (cx, cy) in tris:
            facets.append(
                struct.pack(
                    "<12fH",
                    0.0, 0.0, 1.0,
                    ax, ay, t,
                    bx, by, t,
                    cx, cy, t,
                    0,
                )
            )
            facets.append(
                struct.pack(
                    "<12fH",
                    0.0, 0.0, -1.0,
                    ax, ay, 0.0,
                    cx, cy, 0.0,
                    bx, by, 0.0,
                    0,
                )
            )

        # Exterior walls & Interior hole walls
        _append_ring_walls(facets, poly.exterior.coords, t)
        for interior in poly.interiors:
            _append_ring_walls(facets, interior.coords, t)

    if not facets:
        return b""

    header_txt = f"Boxes.py Studio 3D STL ({solid_name})".encode("ascii", errors="replace")[:80]
    header = header_txt.ljust(80, b"\x00")
    out = bytearray(header)
    out.extend(struct.pack("<I", len(facets)))
    for f in facets:
        out.extend(f)
    return bytes(out)


class STLSurface(Surface):
    """Surface that extrudes each Boxes.py part into individual 3D STL files inside a ZIP."""

    scale = 1.0
    invert_y = False

    def __init__(self) -> None:
        super().__init__()
        self.thickness: float = 3.0
        self.box_name: str = "Box"

    def finish(self, inner_corners="corner"):
        zip_buf = io.BytesIO()
        used_names: dict[str, int] = {}
        part_idx = 0

        with zipfile.ZipFile(zip_buf, mode="w", compression=zipfile.ZIP_DEFLATED) as zf:
            for part in self.parts:
                if getattr(part, "is_reference", False):
                    continue
                if not getattr(part, "pathes", None):
                    continue

                polys = part_to_polygons(part)
                if not polys:
                    continue

                part_idx += 1
                raw_label = getattr(part, "label", "") or getattr(part, "name", "")
                stem = sanitize_part_filename(raw_label, part_idx)

                count = used_names.get(stem, 0) + 1
                used_names[stem] = count
                fname = f"{stem}.stl" if count == 1 else f"{stem}_{count}.stl"

                stl_bytes = polygons_to_binary_stl(polys, self.thickness, solid_name=stem)
                if stl_bytes:
                    zf.writestr(fname, stl_bytes)

        zip_buf.seek(0)
        return zip_buf
