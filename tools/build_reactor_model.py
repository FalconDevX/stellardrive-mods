import math
import os
import struct
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy.spatial import ConvexHull

SCALE = 0.64
TILE = 2.0
CORE = (-0.18, 0.28)
CORE_Y = 1.55


def rot(rx=0.0, ry=0.0, rz=0.0):
    cx, sx, cy, sy, cz, sz = math.cos(rx), math.sin(rx), math.cos(ry), math.sin(ry), math.cos(rz), math.sin(rz)
    mx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    my = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    mz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    return mx @ my @ mz


class Mesh:
    def __init__(self, origin=(0.0, 0.0, 0.0)):
        self.pos, self.nrm, self.uv, self.idx = [], [], [], []
        self.origin = np.array(origin, dtype=float)

    def add(self, tris, offset=(0, 0, 0), r=None, uv_mode="box", tile=TILE):
        r = np.eye(3) if r is None else r
        offset = np.array(offset, dtype=float)
        for corners in tris:
            pts = [r @ np.array(c[0]) + offset for c in corners]
            nrms = [r @ np.array(c[1]) for c in corners]
            uvs = [c[2] for c in corners]
            pts = [np.array([-p[0], p[1], p[2]]) for p in pts]
            nrms = [np.array([-n[0], n[1], n[2]]) for n in nrms]
            face = np.cross(pts[1] - pts[0], pts[2] - pts[0])
            if np.linalg.norm(face) < 1e-12:
                continue
            if uv_mode == "box":
                uvs = [box_uv(p, -face, tile) for p in pts]
            base = len(self.pos)
            for p, n, t in zip(pts, nrms, uvs):
                self.pos.append((p - np.array([-self.origin[0], self.origin[1], self.origin[2]])) * SCALE)
                self.nrm.append(n / max(1e-9, np.linalg.norm(n)))
                self.uv.append(t)
            self.idx += [base, base + 2, base + 1]

    def write(self, path):
        with open(path, "wb") as f:
            f.write(b"MSH1")
            f.write(struct.pack("<ii", len(self.pos), len(self.idx)))
            for p, n, t in zip(self.pos, self.nrm, self.uv):
                f.write(struct.pack("<8f", p[0], p[1], p[2], n[0], n[1], n[2], t[0], t[1]))
            f.write(struct.pack(f"<{len(self.idx)}i", *self.idx))
        return len(self.idx) // 3


def box_uv(p, n, tile):
    a = np.abs(n)
    if a[1] >= a[0] and a[1] >= a[2]:
        return (p[0] / tile if n[1] > 0 else -p[0] / tile, p[2] / tile)
    if a[0] >= a[2]:
        return (p[2] / tile if n[0] > 0 else -p[2] / tile, p[1] / tile)
    return (-p[0] / tile if n[2] > 0 else p[0] / tile, p[1] / tile)


def chamfer_box(w, h, d, b, uv=None):
    hx, hy, hz = w / 2, h / 2, d / 2
    b = min(b, hx * 0.45, hy * 0.45, hz * 0.45)
    pts = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                pts += [(sx * hx, sy * (hy - b), sz * (hz - b)), (sx * (hx - b), sy * hy, sz * (hz - b)), (sx * (hx - b), sy * (hy - b), sz * hz)]
    pts = np.array(pts)
    hull = ConvexHull(pts)
    tris = []
    for simplex, eq in zip(hull.simplices, hull.equations):
        a, c, e = pts[simplex[0]], pts[simplex[1]], pts[simplex[2]]
        n = eq[:3]
        if np.dot(np.cross(c - a, e - a), n) < 0:
            c, e = e, c
        tris.append([(p, n, uv(p, n, h) if uv else (0, 0)) for p in (a, c, e)])
    return tris


def band_uv():
    def f(p, n, h):
        length_tile = 4 * h
        if abs(n[1]) > 0.7:
            return (p[0] / length_tile, 0.03)
        along = p[0] if abs(n[0]) < abs(n[2]) else p[2]
        return (along / length_tile, min(1.0, max(0.0, (p[1] + h / 2) / h)))
    return f


def fixed_uv(u):
    return lambda p, n, h: (u, min(1.0, max(0.0, (p[1] + h / 2) / h)))


def lathe(profile, seg, uv_mode="world", tile=TILE, repeats=1, v_range=None):
    tris = []
    total = 0.0
    lengths = [0.0]
    for i in range(len(profile) - 1):
        total += math.dist(profile[i], profile[i + 1])
        lengths.append(total)
    y0 = min(p[1] for p in profile)
    y1 = max(p[1] for p in profile)
    for i in range(len(profile) - 1):
        (r0, a0), (r1, a1) = profile[i], profile[i + 1]
        dr, dy = r1 - r0, a1 - a0
        if abs(dr) < 1e-9 and abs(dy) < 1e-9:
            continue
        flat = abs(dy) < 1e-9
        nr, ny = dy, -dr
        ln = math.hypot(nr, ny)
        nr, ny = nr / ln, ny / ln
        for k in range(seg):
            t0, t1 = 2 * math.pi * k / seg, 2 * math.pi * (k + 1) / seg
            quad = []
            for (r, y, along), t in (((r0, a0, lengths[i]), t0), ((r1, a1, lengths[i + 1]), t0), ((r1, a1, lengths[i + 1]), t1), ((r0, a0, lengths[i]), t1)):
                p = (r * math.cos(t), y, r * math.sin(t))
                n = (nr * math.cos(t), ny, nr * math.sin(t))
                if flat:
                    uv = (p[0] / tile, p[2] / tile) if uv_mode != "band" else (p[0] / tile, 0.03)
                elif uv_mode == "core":
                    lo, hi = v_range or (y0, y1)
                    uv = (t / (2 * math.pi) * repeats, (y - lo) / (hi - lo))
                elif uv_mode == "band":
                    uv = (t * (r0 + r1) / 2 / tile, (y - y0) / (y1 - y0))
                else:
                    uv = (t * (r0 + r1) / 2 / tile, along / tile)
                quad.append((p, n, uv))
            tris.append([quad[0], quad[1], quad[2]])
            tris.append([quad[0], quad[2], quad[3]])
    return tris


def cylinder(rt, rb, h, seg=32, bevel=0.0, **kw):
    y0, y1 = -h / 2, h / 2
    b = min(bevel, h * 0.3, rt * 0.3, rb * 0.3)
    if b > 0:
        profile = [(0, y0), (rb - b, y0), (rb, y0 + b), (rt, y1 - b), (rt - b, y1), (0, y1)]
    else:
        profile = [(0, y0), (rb, y0), (rt, y1), (0, y1)]
    return lathe(profile, seg, **kw)


def tube(points, radius, seg=10, samples=8):
    pts = [np.array(p, dtype=float) for p in points]
    ext = [2 * pts[0] - pts[1]] + pts + [2 * pts[-1] - pts[-2]]
    curve = []
    for i in range(1, len(ext) - 2):
        p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
        for s in range(samples):
            t = s / samples
            curve.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    curve.append(pts[-1])
    tangents = [curve[min(i + 1, len(curve) - 1)] - curve[max(i - 1, 0)] for i in range(len(curve))]
    tangents = [t / np.linalg.norm(t) for t in tangents]
    up = np.array([0.0, 1.0, 0.0]) if abs(tangents[0][1]) < 0.9 else np.array([1.0, 0.0, 0.0])
    normal = np.cross(tangents[0], up)
    normal /= np.linalg.norm(normal)
    frames = []
    for t in tangents:
        normal = normal - t * np.dot(normal, t)
        normal /= np.linalg.norm(normal)
        frames.append((normal, np.cross(t, normal)))
    along = [0.0]
    for i in range(1, len(curve)):
        along.append(along[-1] + np.linalg.norm(curve[i] - curve[i - 1]))
    rings = []
    for c, (n, bn), a in zip(curve, frames, along):
        ring = []
        for k in range(seg + 1):
            ang = 2 * math.pi * k / seg
            d = n * math.cos(ang) + bn * math.sin(ang)
            ring.append((c + d * radius, d, (k / seg * 0.5, a / TILE)))
        rings.append(ring)
    tris = []
    for i in range(len(rings) - 1):
        for k in range(seg):
            a, b, c, d = rings[i][k], rings[i][k + 1], rings[i + 1][k + 1], rings[i + 1][k]
            tris.append([a, b, c])
            tris.append([a, c, d])
    return tris


def quad(center, normal, w, h, uv_rect):
    c, n = np.array(center, dtype=float), np.array(normal, dtype=float)
    u = np.array([0.0, 1.0, 0.0])
    r = np.cross(u, n)
    u0, v0, u1, v1 = uv_rect
    p = [c - r * w / 2 - u * h / 2, c + r * w / 2 - u * h / 2, c + r * w / 2 + u * h / 2, c - r * w / 2 + u * h / 2]
    t = [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
    return [[(p[0], n, t[0]), (p[1], n, t[1]), (p[2], n, t[2])], [(p[0], n, t[0]), (p[2], n, t[2]), (p[3], n, t[3])]]


def build(out):
    dark, light, hazard, pipes, copper, screens = Mesh(), Mesh(), Mesh(), Mesh(), Mesh(), Mesh()
    core = Mesh(origin=(CORE[0], CORE_Y, CORE[1]))
    cx, cz = CORE

    dark.add(chamfer_box(3.0, 0.36, 3.0, 0.08), (0, 0.18, 0))
    hazard.add(chamfer_box(2.76, 0.14, 2.76, 0.03, band_uv()), (0, 0.42, 0), uv_mode="explicit")
    light.add(chamfer_box(2.58, 0.10, 2.58, 0.03), (0, 0.52, 0))
    for px in (-0.62, 0.62):
        for pz in (-0.62, 0.62):
            dark.add(chamfer_box(1.08, 0.02, 1.08, 0.008), (px, 0.578, pz))

    for sx in (-1, 1):
        for sz in (-1, 1):
            light.add(chamfer_box(0.40, 0.80, 0.40, 0.05), (sx * 1.30, 0.40, sz * 1.30))
            dark.add(chamfer_box(0.44, 0.06, 0.44, 0.02), (sx * 1.30, 0.81, sz * 1.30))
            hazard.add(chamfer_box(0.41, 0.10, 0.41, 0.01, band_uv()), (sx * 1.30, 0.66, sz * 1.30), uv_mode="explicit")

    for side in (-1, 1):
        for y in (0.13, 0.25):
            pipes.add(cylinder(0.04, 0.04, 2.0, 16), (side * 1.50, y, 0), rot(math.pi / 2, 0, 0), uv_mode="explicit")
            for z in (-0.95, 0.95):
                pipes.add(cylinder(0.06, 0.06, 0.05, 16, 0.01), (side * 1.50, y, z), rot(math.pi / 2, 0, 0), uv_mode="explicit")

    def screen(center, normal, w, h, depth, region, frame=dark):
        c, n = np.array(center, dtype=float), np.array(normal, dtype=float)
        yaw = math.atan2(n[0], n[2])
        frame.add(chamfer_box(w + 0.06, h + 0.06, depth, 0.015), tuple(c), rot(0, yaw, 0))
        screens.add(quad(c + n * (depth / 2 + 0.0015), n, w, h, region), uv_mode="explicit")

    screen((0.0, 0.20, 1.52), (0, 0, 1), 0.62, 0.20, 0.06, WIDE)
    screen((1.52, 0.20, 0.0), (1, 0, 0), 0.62, 0.20, 0.06, WIDE)

    dark.add(chamfer_box(2.10, 0.40, 0.24, 0.04), (0, 0.77, 1.42))
    light.add(chamfer_box(2.16, 0.05, 0.28, 0.015), (0, 0.995, 1.42))
    for x in (-0.84, -0.28, 0.28, 0.84):
        pipes.add(chamfer_box(0.40, 0.34, 0.02, 0.01), (x, 0.76, 1.545))

    light.add(cylinder(0.84, 0.94, 0.26, 40, 0.03), (cx, 0.68, cz))
    hazard.add(cylinder(0.76, 0.76, 0.14, 40, 0.0, uv_mode="band", tile=0.56), (cx, 0.86, cz), uv_mode="explicit")
    core.add(cylinder(0.58, 0.58, 1.35, 40, 0.0, uv_mode="core", repeats=3), (cx, CORE_Y, cz), uv_mode="explicit")
    for i in range(8):
        a = i / 8 * 2 * math.pi
        core.add(chamfer_box(0.05, 1.25, 0.05, 0.012, fixed_uv(0.008)), (cx + math.cos(a) * 0.585, CORE_Y, cz + math.sin(a) * 0.585), rot(0, -a, 0), uv_mode="explicit")
    core.add(cylinder(0.30, 0.30, 0.06, 32, 0.0, uv_mode="core", repeats=2), (cx, 2.57, cz), uv_mode="explicit")
    for y in (0.98, 1.36, 1.74, 2.12):
        dark.add(cylinder(0.67, 0.67, 0.10, 40, 0.02), (cx, y, cz))
        copper.add(cylinder(0.69, 0.69, 0.04, 40, 0.008), (cx, y, cz))
    light.add(cylinder(0.52, 0.74, 0.26, 40, 0.03), (cx, 2.30, cz))
    dark.add(cylinder(0.42, 0.48, 0.16, 40, 0.02), (cx, 2.50, cz))

    for i in range(6):
        a = i / 6 * 2 * math.pi + 0.26
        light.add(chamfer_box(0.14, 1.32, 0.16, 0.03), (cx + math.cos(a) * 0.74, 1.55, cz + math.sin(a) * 0.74), rot(0, -a, 0))
        dark.add(chamfer_box(0.18, 0.32, 0.26, 0.035), (cx + math.cos(a) * 0.86, 0.72, cz + math.sin(a) * 0.86), rot(0, -a, -0.2))
        p1 = (cx + math.cos(a) * 0.74, 2.18, cz + math.sin(a) * 0.74)
        p2 = (cx + math.cos(a) * 0.68, 2.44, cz + math.sin(a) * 0.68)
        p3 = (cx + math.cos(a) * 0.40, 2.48, cz + math.sin(a) * 0.40)
        pipes.add(tube([p1, p2, p3], 0.042), uv_mode="explicit")
    for i in range(4):
        a = i / 6 * 2 * math.pi + 0.78
        bx, bz = cx + math.cos(a) * 0.70, cz + math.sin(a) * 0.70
        for by in (1.18, 1.56, 1.94):
            dark.add(chamfer_box(0.035, 0.44, 0.035, 0.008), (bx, by, bz), rot(0.62, -a, 0))
            dark.add(chamfer_box(0.035, 0.44, 0.035, 0.008), (bx, by, bz), rot(-0.62, -a, 0))

    dark.add(chamfer_box(1.68, 2.00, 1.05, 0.08), (0.38, 1.57, -0.68))
    light.add(chamfer_box(1.48, 0.22, 0.88, 0.05), (0.38, 2.66, -0.68))
    for y in (1.0, 1.8):
        hazard.add(chamfer_box(1.70, 0.08, 1.07, 0.01, band_uv()), (0.38, y, -0.68), uv_mode="explicit")
    for rz in (-0.44, -0.90):
        light.add(chamfer_box(0.34, 1.65, 0.38, 0.05), (1.26, 1.48, rz))
        fy = 0.85
        while fy <= 2.06:
            copper.add(chamfer_box(0.36, 0.035, 0.30, 0.008), (1.30, fy, rz))
            fy += 0.15
    screen((0.92, 1.52, -0.11), (0, 0, 1), 0.40, 0.50, 0.10, TALL, light)

    for vx in (-0.02, 0.74):
        light.add(chamfer_box(0.56, 0.24, 0.56, 0.04), (vx, 2.88, -0.68))
        hazard.add(chamfer_box(0.46, 0.10, 0.46, 0.015, band_uv()), (vx, 3.02, -0.68), uv_mode="explicit")
        dark.add(chamfer_box(0.40, 0.03, 0.40, 0.01), (vx, 3.08, -0.68))
        for k in range(5):
            pipes.add(chamfer_box(0.34, 0.03, 0.035, 0.008), (vx, 3.10, -0.68 - 0.14 + k * 0.07))

    dark.add(chamfer_box(0.76, 1.08, 0.88, 0.06), (-0.96, 1.08, -0.18))
    light.add(chamfer_box(0.66, 0.16, 0.76, 0.03), (-0.96, 1.68, -0.18))
    screen((-1.345, 1.12, -0.18), (-1, 0, 0), 0.42, 0.42, 0.04, SQUARE, light)
    for z in (-0.40, 0.04):
        for y in (0.75, 1.45):
            pipes.add(cylinder(0.05, 0.05, 0.06, 16, 0.01), (-1.36, y, z), rot(0, 0, math.pi / 2), uv_mode="explicit")
    pipes.add(tube([(-0.96, 1.72, -0.05), (-0.96, 2.25, -0.05), (-0.96, 2.35, -0.55), (-0.38, 2.35, -0.55)], 0.065, 14), uv_mode="explicit")
    pipes.add(tube([(-0.78, 1.72, -0.32), (-0.78, 2.08, -0.32), (-0.78, 2.15, -0.78), (-0.38, 2.15, -0.78)], 0.065, 14), uv_mode="explicit")

    beacon = Mesh()
    beacon.add(cylinder(0.15, 0.16, 0.06, 32, 0.015, uv_mode="core", v_range=(-0.03, 0.27)), (0, 0.03, 0), uv_mode="explicit")
    dome = [(0.0, 0.06), (0.115, 0.06), (0.12, 0.08), (0.12, 0.20)]
    for k in range(1, 9):
        a = k / 8 * math.pi / 2
        dome.append((0.12 * math.cos(a), 0.20 + 0.10 * math.sin(a)))
    beacon.add(lathe(dome, 40, uv_mode="core", v_range=(-0.02, 0.30)), uv_mode="explicit")
    beacon.add(cylinder(0.135, 0.135, 0.025, 32, 0.006, uv_mode="core", v_range=(-0.0125, 0.16)), (0, 0.075, 0), uv_mode="explicit")

    counts = {}
    for name, mesh in (("reactor_beacon", beacon), ("reactor_armor_dark", dark), ("reactor_armor_light", light), ("reactor_hazard", hazard), ("reactor_pipes", pipes),
                       ("reactor_copper", copper), ("reactor_screens", screens), ("reactor_core", core)):
        counts[name] = mesh.write(os.path.join(out, name + ".bin"))
    allpos = np.array(dark.pos + light.pos + hazard.pos + pipes.pos + copper.pos + screens.pos)
    print("triangles", counts, "total", sum(counts.values()))
    print("bounds", allpos.min(0).round(3), allpos.max(0).round(3))
    print("rotor position", (-CORE[0] * SCALE, CORE_Y * SCALE, CORE[1] * SCALE))


WIDE_PX = (0, 0, 1024, 330)
TALL_PX = (0, 334, 400, 834)
SQUARE_PX = (404, 334, 804, 734)


def region(px):
    x0, y0, x1, y1 = px
    return (x0 / 1024, 1 - y1 / 1024, x1 / 1024, 1 - y0 / 1024)


WIDE, TALL, SQUARE = region(WIDE_PX), region(TALL_PX), region(SQUARE_PX)


def noise_layer(rng, size, blobs, alpha_light, alpha_dark):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(blobs):
        s = rng.uniform(2, 14)
        x, y = rng.uniform(0, size), rng.uniform(0, size)
        c = (255, 255, 255, alpha_light) if rng.random() > 0.5 else (0, 0, 0, alpha_dark)
        for ox in (-size, 0, size):
            for oy in (-size, 0, size):
                d.rectangle([x + ox, y + oy, x + ox + s, y + oy + s], fill=c)
    return img


def armor_texture(base, panel, rng):
    size = 1024
    img = Image.new("RGBA", (size, size), base)
    d = ImageDraw.Draw(img)
    for x, y, w, h in [(24, 24, 464, 464), (512, 24, 488, 220), (512, 268, 488, 220), (24, 512, 300, 488), (348, 512, 652, 488)]:
        d.rectangle([x, y, x + w, y + h], fill=panel)
        d.rectangle([x, y, x + w, y + h], outline=(13, 15, 19, 255), width=8)
        d.rectangle([x + 6, y + 6, x + w - 6, y + h - 6], outline=(180, 205, 235, 60), width=3)
        for rx, ry in ((x + 22, y + 22), (x + w - 22, y + 22), (x + 22, y + h - 22), (x + w - 22, y + h - 22)):
            d.ellipse([rx - 7, ry - 7, rx + 7, ry + 7], fill=(17, 20, 26, 255))
            d.ellipse([rx - 5.5, ry - 5.5, rx + 2.5, ry + 2.5], fill=(104, 116, 136, 255))
    img = Image.alpha_composite(img, noise_layer(rng, size, 6500, 6, 13))
    scratches = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    sd = ImageDraw.Draw(scratches)
    for _ in range(220):
        x, y, a, l = rng.uniform(0, size), rng.uniform(0, size), rng.uniform(0, math.pi), rng.uniform(8, 44)
        sd.line([x, y, x + math.cos(a) * l, y + math.sin(a) * l], fill=(200, 215, 235, 30), width=1)
    img = Image.alpha_composite(img, scratches)
    return img.convert("RGB")


def hazard_texture(rng):
    w, h = 512, 128
    img = Image.new("RGB", (w, h), (70, 76, 86))
    d = ImageDraw.Draw(img)
    top, bottom = 16, h - 16
    d.rectangle([0, top, w, bottom], fill=(234, 179, 8))
    for i in range(-4, w // 64 + 4):
        x = i * 64
        d.polygon([(x, top), (x + 32, top), (x + 32 - 64, bottom), (x - 64, bottom)], fill=(34, 37, 42))
    d.rectangle([0, top - 3, w, top], fill=(75, 85, 99))
    d.rectangle([0, bottom, w, bottom + 3], fill=(75, 85, 99))
    img = Image.alpha_composite(img.convert("RGBA"), noise_layer(rng, 512, 900, 4, 20).crop((0, 0, w, h)))
    return img.convert("RGB")


def metal_texture(color, rng, brushed):
    size = 256
    arr = np.ones((size, size, 3)) * np.array(color, dtype=float)
    n = rng.normal(0, 1, (size, size))
    if brushed:
        n = (n + np.roll(n, 1, 0) + np.roll(n, 2, 0) + np.roll(n, -1, 0)) / 4
    arr += n[..., None] * 7
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))


def wrapped_noise(rng, h, w, octaves):
    v = np.linspace(0, 1, h, endpoint=False)[:, None]
    u = np.linspace(0, 1, w, endpoint=False)[None, :]
    total = np.zeros((h, w))
    for fu, fv, amp in octaves:
        for _ in range(4):
            ku, kv = rng.integers(1, fu + 1), rng.uniform(0.5, fv)
            total += amp * np.sin(2 * math.pi * (ku * u + kv * v) + rng.uniform(0, 2 * math.pi))
    return total


def core_texture():
    h, w = 512, 512
    rng = np.random.default_rng(7)
    v = np.linspace(0, 1, h)[:, None]
    u = np.linspace(0, 1, w, endpoint=False)[None, :]
    wobble = wrapped_noise(rng, h, w, [(3, 4, 0.02), (6, 9, 0.008)])
    field = np.zeros((h, w))
    for k in range(5):
        phase = k / 5 + rng.uniform(-0.04, 0.04)
        twist = rng.choice([-1, 1]) * rng.uniform(0.35, 0.6)
        center = phase + v * twist + wobble * (1 + k * 0.3)
        d = np.abs(((u - center) + 0.5) % 1.0 - 0.5)
        width = 0.018 + 0.008 * np.sin(2 * math.pi * (v * 3 + k * 0.37)) ** 2
        field += np.exp(-(d / width) ** 2) * (0.75 + 0.25 * (k % 2))
        field += 0.35 * np.exp(-(d / (width * 3.5)) ** 2)
    turb = wrapped_noise(rng, h, w, [(8, 6, 0.5), (16, 12, 0.25), (32, 20, 0.12)])
    field += 0.18 * (turb - turb.min()) / (turb.max() - turb.min())
    field *= 0.55 + 0.45 * np.sin(np.pi * v) ** 0.6
    field = np.clip(field, 0, 1.6)
    stops = np.array([0.0, 0.25, 0.6, 1.0, 1.6])
    colors = np.array([(4, 14, 40), (8, 60, 150), (20, 170, 255), (150, 235, 255), (255, 255, 255)], dtype=float)
    rgb = np.stack([np.interp(field, stops, colors[:, c]) for c in range(3)], -1)
    return Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)[::-1])


def beacon_texture():
    h, w = 128, 512
    v = 1 - (np.arange(h)[:, None] + 0.5) / h
    u = (np.arange(w)[None, :] + 0.5) / w
    d = np.abs(u - 0.5)
    spot = np.exp(-(d / 0.07) ** 2) + 0.35 * np.exp(-(d / 0.18) ** 2)
    amber = np.array([255, 120, 10], dtype=float)
    hot = np.array([255, 235, 170], dtype=float)
    glass = amber * (0.45 + 0.55 * np.clip(spot, 0, 1))[..., None]
    glass = glass + (hot - amber) * np.clip(spot - 0.6, 0, 1)[..., None]
    ribs = 0.85 + 0.15 * (np.sin(2 * math.pi * u * 24) > 0.6)
    glass *= ribs[..., None]
    metal = np.ones((h, w, 3)) * np.array([70, 76, 86], dtype=float)
    rgb = np.where((v < 0.2)[..., None], metal, glass)
    return Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8))


def screens_texture():
    size = 1024
    img = Image.new("RGB", (size, size), (24, 28, 34))
    d = ImageDraw.Draw(img)

    def frame(px):
        d.rectangle(px, fill=(4, 30, 43))
        d.rectangle([px[0] + 6, px[1] + 6, px[2] - 7, px[3] - 7], outline=(0, 210, 255), width=5)

    x0, y0, x1, y1 = WIDE_PX
    frame(WIDE_PX)
    for i, b in enumerate([0.55, 0.72, 0.64, 0.86, 0.80, 0.68, 0.92, 0.76, 0.6, 0.83, 0.7]):
        x = x0 + 40 + i * 64
        d.rectangle([x, int(y1 - 36 - b * 230), x + 42, y1 - 36], fill=(0, 240, 255))
    for i, c in enumerate([(34, 255, 85), (34, 255, 85), (255, 170, 0), (255, 34, 51)]):
        d.rectangle([x1 - 230, y0 + 40 + i * 66, x1 - 50, y0 + 84 + i * 66], fill=c)

    x0, y0, x1, y1 = TALL_PX
    frame(TALL_PX)
    for i, b in enumerate([0.5, 0.7, 0.62, 0.84, 0.76, 0.66]):
        x = x0 + 32 + i * 58
        d.rectangle([x, int(y0 + 40 + (1 - b) * 220), x + 38, y0 + 260], fill=(0, 240, 255))
    for i, c in enumerate([(34, 255, 85), (34, 255, 85), (255, 170, 0), (255, 34, 51), (0, 229, 255), (34, 255, 85)]):
        x = x0 + 32 + i * 58
        d.rectangle([x, y0 + 290, x + 38, y0 + 328], fill=c)
        d.rectangle([x, y0 + 350, x + 38, y0 + 470], fill=(45, 53, 66))
        level = [0.3, 0.6, 0.45, 0.8, 0.5, 0.7][i]
        d.rectangle([x, int(y0 + 350 + (1 - level) * 120), x + 38, y0 + 470], fill=(0, 160, 200))

    x0, y0, x1, y1 = SQUARE_PX
    frame(SQUARE_PX)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    for rr in (150, 110, 70):
        d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], outline=(0, 200, 255), width=4)
    d.arc([cx - 150, cy - 150, cx + 150, cy + 150], -90, 160, fill=(0, 255, 255), width=12)
    d.ellipse([cx - 30, cy - 30, cx + 30, cy + 30], fill=(0, 240, 255))
    return img


def textures(out):
    rng = np.random.default_rng(1337)
    armor_texture((74, 82, 94, 255), (88, 98, 112, 255), rng).save(os.path.join(out, "reactor_armor_dark.png"))
    armor_texture((132, 142, 156, 255), (152, 164, 180, 255), rng).save(os.path.join(out, "reactor_armor_light.png"))
    hazard_texture(rng).save(os.path.join(out, "reactor_hazard.png"))
    metal_texture((104, 112, 122), rng, True).save(os.path.join(out, "reactor_pipes.png"))
    metal_texture((214, 140, 70), rng, True).save(os.path.join(out, "reactor_copper.png"))
    core_texture().save(os.path.join(out, "reactor_core.png"))
    beacon_texture().save(os.path.join(out, "reactor_beacon.png"))
    screens_texture().save(os.path.join(out, "reactor_screens.png"))


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    textures(out)
    build(out)
