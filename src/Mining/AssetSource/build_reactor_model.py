import bpy, bmesh, math, os, struct, sys, zlib
import numpy as np
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = argv[0] if argv else os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets')
PREVIEW = argv[1] if len(argv) > 1 else None

PALETTE = [
    ('dark', (0.10, 0.11, 0.12)), ('grey', (0.38, 0.40, 0.42)), ('steel', (0.60, 0.62, 0.64)), ('light', (0.82, 0.83, 0.82)),
    ('yellow', (0.96, 0.74, 0.10)), ('black', (0.04, 0.04, 0.04)), ('blue', (0.12, 0.38, 0.86)), ('white', (0.93, 0.93, 0.93)),
    ('red', (0.82, 0.12, 0.10)), ('green', (0.18, 0.78, 0.28)), ('orange', (0.94, 0.42, 0.08)), ('glow', (0.30, 0.80, 0.90)),
    ('screen', (0.08, 0.30, 0.36)), ('copper', (0.74, 0.44, 0.24)), ('rubber', (0.06, 0.06, 0.06)), ('olive', (0.32, 0.35, 0.27)),
]
GLOW = {'glow', 'screen'}
NAMES = [n for n, _ in PALETTE]
CELLS, CELL_PX = 4, 256
MARGIN = 6.0 / CELL_PX
SOCKETS = [(-0.70, 0.45, 'yellow'), (-0.25, 0.45, 'yellow'), (0.25, 0.45, 'blue'), (0.70, 0.45, 'white')]

C = Matrix(((0, 0, -1), (1, 0, 0), (0, 1, 0)))


def B(v):
    return C @ Vector(v)


def axis_rot(axis, deg):
    return Matrix.Rotation(math.radians(deg), 3, Vector(axis))


class Builder:
    def __init__(self):
        self.bm = bmesh.new()

    def _place(self, geom_verts, mat, m3, center):
        mb = C @ m3 @ C.transposed()
        full = Matrix.Translation(B(center)) @ mb.to_4x4()
        bmesh.ops.transform(self.bm, matrix=full, verts=geom_verts)

    def _faces_of(self, verts):
        faces = set()
        for v in verts:
            faces.update(v.link_faces)
        return faces

    def _paint(self, verts, mat):
        for f in self._faces_of(verts):
            f.material_index = NAMES.index(mat)

    def box(self, center, size, mat, rot=None, bevel=0.0):
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = res['verts']
        rot = rot or Matrix.Identity(3)
        m3 = rot @ Matrix.Diagonal(Vector((size[0], size[1], size[2])))
        self._place(verts, mat, m3, center)
        self._paint(verts, mat)
        if bevel > 0:
            edges = list({e for v in verts for e in v.link_edges})
            bmesh.ops.bevel(self.bm, geom=edges + verts, offset=bevel, segments=2, profile=0.5, affect='EDGES', clamp_overlap=True)
        return self

    def lathe(self, profile, mat, center=(0, 0, 0), seg=48, rot=None):
        rot = rot or Matrix.Identity(3)
        rings = []
        for r, y in profile:
            if r <= 1e-6:
                rings.append([self.bm.verts.new(B((0, y, 0)))])
                continue
            rings.append([self.bm.verts.new(B((r * math.cos(a), y, r * math.sin(a)))) for a in (i * 2 * math.pi / seg for i in range(seg))])
        new_verts = [v for ring in rings for v in ring]
        for a, b in zip(rings, rings[1:]):
            if len(a) == 1 and len(b) == 1:
                continue
            for i in range(seg):
                j = (i + 1) % seg
                if len(a) == 1:
                    quad = [a[0], b[j], b[i]]
                elif len(b) == 1:
                    quad = [a[i], a[j], b[0]]
                else:
                    quad = [a[i], a[j], b[j], b[i]]
                try:
                    self.bm.faces.new(quad)
                except ValueError:
                    pass
        self._place(new_verts, mat, rot, center)
        self._paint(new_verts, mat)
        return self

    def cyl(self, a, b, r, mat, seg=24, bevel=0.0):
        a, b = Vector(a), Vector(b)
        d = b - a
        length = d.length
        up = d.normalized()
        ref = Vector((1, 0, 0)) if abs(up.x) < 0.9 else Vector((0, 0, 1))
        x = up.cross(ref).normalized()
        z = x.cross(up).normalized()
        rot = Matrix((x, up, z)).transposed()
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r, depth=length)
        verts = res['verts']
        self._place(verts, mat, rot, (a + b) / 2)
        self._paint(verts, mat)
        if bevel > 0:
            caps = [e for e in {e for v in verts for e in v.link_edges} if len(e.link_faces) == 2 and abs(e.link_faces[0].normal.dot(e.link_faces[1].normal)) < 0.5]
            if caps:
                bmesh.ops.bevel(self.bm, geom=caps, offset=bevel, segments=2, profile=0.5, affect='EDGES', clamp_overlap=True)
        return self

    def torus(self, center, axis_up, radius, tube, mat, seg=40, tseg=10):
        profile = []
        for i in range(tseg + 1):
            a = i * 2 * math.pi / tseg
            profile.append((radius + tube * math.cos(a), tube * math.sin(a)))
        up = Vector(axis_up).normalized()
        ref = Vector((1, 0, 0)) if abs(up.x) < 0.9 else Vector((0, 0, 1))
        x = up.cross(ref).normalized()
        z = x.cross(up).normalized()
        return self.lathe(profile, mat, center, seg, Matrix((x, up, z)).transposed())

    def sphere(self, center, r, mat, seg=16):
        prof = [(r * math.sin(math.pi * i / seg), -r * math.cos(math.pi * i / seg)) for i in range(seg + 1)]
        prof[0] = (0, -r)
        prof[-1] = (0, r)
        return self.lathe(prof, mat, center, seg * 2)

    def pipe(self, points, r, mat, seg=16):
        for a, b in zip(points, points[1:]):
            self.cyl(a, b, r, mat, seg)
        for p in points[1:-1]:
            self.sphere(p, r, mat, seg // 2)
        return self

    def prism(self, pts2d, origin, u, v, depth, mat):
        o, u, v = Vector(origin), Vector(u), Vector(v)
        n = u.cross(v).normalized()
        front = [self.bm.verts.new(B(o + u * x + v * y)) for x, y in pts2d]
        back = [self.bm.verts.new(B(o + u * x + v * y - n * depth)) for x, y in pts2d]
        k = len(pts2d)
        faces = [front, list(reversed(back))]
        for i in range(k):
            j = (i + 1) % k
            faces.append([front[j], front[i], back[i], back[j]])
        for f in faces:
            try:
                self.bm.faces.new(f)
            except ValueError:
                pass
        self._paint(front + back, mat)
        return self

    def to_object(self, name):
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-6)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        obj = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(obj)
        for n, col in PALETTE:
            mat = bpy.data.materials.get('pal_' + n) or bpy.data.materials.new('pal_' + n)
            mat.diffuse_color = (*col, 1.0)
            me.materials.append(mat)
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
        obj.select_set(False)
        return obj


def ring_points(radius, count, offset=0.0):
    return [(radius * math.cos(offset + i * 2 * math.pi / count), radius * math.sin(offset + i * 2 * math.pi / count)) for i in range(count)]


def yaw(deg):
    return axis_rot((0, 1, 0), deg)


def hazard_band(m, face_center, right, up, outward, length):
    o = Vector(face_center)
    r, u, n = Vector(right), Vector(up), Vector(outward)
    m.box(tuple(o + n * 0.003), tuple(abs(c) for c in (r * length + u * 0.07 + n * 0.006)), 'yellow')
    step, w = 0.11, 0.05
    x = -length / 2 + 0.01
    while x + w + 0.035 < length / 2:
        m.prism([(x, -0.03), (x + w, -0.03), (x + w + 0.035, 0.03), (x + 0.035, 0.03)], o + n * 0.0065, r, u, 0.004, 'black')
        x += step


def build_body():
    m = Builder()
    m.box((0, 0.05, 0), (1.9, 0.10, 1.9), 'dark', bevel=0.012)
    hazard_band(m, (0, 0.05, -0.95), (1, 0, 0), (0, 1, 0), (0, 0, -1), 1.7)
    hazard_band(m, (-0.95, 0.05, 0), (0, 0, 1), (0, 1, 0), (-1, 0, 0), 1.7)
    hazard_band(m, (0.95, 0.05, 0), (0, 0, -1), (0, 1, 0), (1, 0, 0), 1.7)
    m.box((0, 0.115, 0), (1.84, 0.03, 1.84), 'grey', bevel=0.006)
    for i in range(-10, 11):
        z = i * 0.082
        m.box((0, 0.132, z), (1.76, 0.006, 0.012), 'dark')

    for sx in (-1, 1):
        for sz in (-1, 1):
            x, z = 0.86 * sx, 0.86 * sz
            m.box((x, 0.155, z), (0.2, 0.05, 0.2), 'dark', bevel=0.008)
            for bx, bz in ((-0.07, -0.07), (0.07, -0.07), (-0.07, 0.07), (0.07, 0.07)):
                m.cyl((x + bx, 0.18, z + bz), (x + bx, 0.195, z + bz), 0.014, 'steel', 6)
            m.box((x, 0.85, z), (0.13, 1.40, 0.13), 'steel', bevel=0.012)
            for y in (0.45, 0.85, 1.25):
                m.box((x, y, z), (0.15, 0.03, 0.15), 'grey', bevel=0.004)
            m.box((x, 1.575, z), (0.17, 0.05, 0.17), 'yellow', bevel=0.008)
            m.cyl((x, 1.6, z), (x, 1.645, z), 0.035, 'orange', 16)
            m.sphere((x, 1.645, z), 0.035, 'orange', 10)

    for s in (-1, 1):
        m.box((0, 1.53, 0.86 * s), (1.6, 0.08, 0.08), 'steel', bevel=0.008)
        m.box((0.86 * s, 1.53, 0), (0.08, 0.08, 1.6), 'steel', bevel=0.008)
    for deg in (45, 135, 225, 315):
        r = yaw(deg)
        mid = r @ Vector((0.66, 1.52, 0))
        m.box(tuple(mid), (0.42, 0.05, 0.06), 'grey', rot=r, bevel=0.006)

    m.lathe([(0, 0.13), (0.64, 0.13), (0.64, 0.19), (0.58, 0.21), (0.56, 0.24), (0, 0.24)], 'grey')
    m.lathe([(0.56, 0.24), (0.555, 0.25), (0.555, 1.10), (0.54, 1.13), (0.0, 1.13)], 'light', seg=64)
    m.lathe([(0.0, 1.13), (0.57, 1.13), (0.57, 1.18), (0.0, 1.18)], 'grey', seg=64)
    for x, z in ring_points(0.53, 32, math.pi / 32):
        m.cyl((x, 1.18, z), (x, 1.215, z), 0.012, 'steel', 6)
        m.cyl((x, 1.215, z), (x, 1.225, z), 0.018, 'dark', 6)
    dome = [(0.0, 1.18)]
    for i in range(13):
        a = math.radians(i * 90 / 12)
        dome.append((0.44 * math.cos(a), 1.18 + 0.2 * math.sin(a)))
    dome[-1] = (0.0, 1.38)
    m.lathe(dome[1:], 'steel', seg=64)
    for y in (0.36, 0.58, 0.80, 1.02):
        m.torus((0, y, 0), (0, 1, 0), 0.556, 0.016, 'steel', 64, 8)
    for y in (0.30, 0.55, 0.80, 1.05):
        for x, z in ring_points(0.557, 12, math.pi / 12):
            m.box((x, y, z), (0.03, 0.03, 0.03), 'dark')
    for x, z in ring_points(0.60, 24):
        m.cyl((x, 0.19, z), (x, 0.215, z), 0.013, 'steel', 6)

    rods = []
    for ix in range(-3, 4):
        for iz in range(-3, 4):
            x, z = ix * 0.075, iz * 0.075
            if math.hypot(x, z) < 0.27:
                rods.append((x, z))
    for x, z in rods:
        r = math.hypot(x, z)
        base = 1.18 + 0.2 * math.sqrt(max(0.0, 1 - (r / 0.44) ** 2))
        m.cyl((x, base - 0.01, z), (x, 1.40, z), 0.022, 'grey', 10)
        m.cyl((x, 1.40, z), (x, 1.415, z), 0.028, 'dark', 10)
        m.cyl((x, base + 0.05, z), (x, base + 0.07, z), 0.03, 'steel', 10)
    m.box((0, 1.42, 0), (0.62, 0.012, 0.62), 'dark')
    for k in (-1, 1):
        m.pipe([(0.30 * k, 1.42, 0.0), (0.42 * k, 1.42, 0.0), (0.48 * k, 1.20, 0.30)], 0.02, 'dark', 10)
    m.lathe([(0.40, 1.43), (0.43, 1.43), (0.43, 1.50), (0.40, 1.50)], 'steel', seg=64)
    for k in range(-4, 5):
        z = k * 0.085
        half = math.sqrt(max(0.0, 0.385 ** 2 - z ** 2))
        if half > 0.02:
            m.box((0, 1.56, z), (half * 2, 0.012, 0.018), 'dark')
    m.torus((0, 1.56, 0), (0, 1, 0), 0.40, 0.014, 'dark', 48, 8)
    for deg in (0, 90, 180, 270):
        r = yaw(deg)
        c = r @ Vector((0.415, 1.53, 0))
        m.box(tuple(c), (0.04, 0.06, 0.04), 'steel', rot=r)

    for deg in (45, 135, 225, 315):
        r = yaw(deg)
        out = r @ Vector((1, 0, 0))
        side = r @ Vector((0, 0, 1))
        c = r @ Vector((0.565, 0.70, 0))
        m.box(tuple(c), (0.04, 0.70, 0.17), 'dark', rot=r, bevel=0.008)
        m.box(tuple(c + out * 0.012), (0.03, 0.62, 0.11), 'glow', rot=r)
        for y in (0.48, 0.70, 0.92):
            m.box(tuple(Vector((c.x, y, c.z)) + out * 0.03), (0.016, 0.02, 0.15), 'steel', rot=r)

    for s in (-1, 1):
        sx, sz = 0.74 * s, 0.30
        m.cyl((sx, 0.13, sz), (sx, 0.25, sz), 0.12, 'dark', 24, 0.006)
        for a in range(4):
            leg = yaw(45 + a * 90) @ Vector((0.13, 0, 0))
            m.box((sx + leg.x, 0.19, sz + leg.z), (0.03, 0.12, 0.03), 'steel', rot=yaw(45 + a * 90))
        m.lathe([(0.0, 0.25), (0.15, 0.25), (0.15, 1.05), (0.16, 1.12), (0.16, 1.32), (0.13, 1.39), (0.07, 1.42), (0.0, 1.43)], 'light', center=(sx, 0, sz), seg=40)
        for y in (0.42, 0.66, 0.90, 1.12):
            m.torus((sx, y, sz), (0, 1, 0), 0.152 if y < 1.1 else 0.162, 0.009, 'steel', 40, 6)
        m.cyl((sx, 0.55, sz + 0.14), (sx, 0.55, sz + 0.175), 0.04, 'dark', 16)
        m.box((sx - 0.12 * s, 0.75, sz), (0.004, 0.12, 0.08), 'yellow', rot=yaw(0))
        m.pipe([(0.52 * s, 0.92, 0.22), (0.66 * s, 0.92, 0.26)], 0.055, 'orange', 18)
        m.cyl((0.53 * s, 0.92, 0.22), (0.57 * s, 0.92, 0.23), 0.075, 'dark', 18)
        m.cyl((0.62 * s, 0.92, 0.25), (0.585 * s, 0.92, 0.24), 0.075, 'dark', 18)
        m.pipe([(sx, 1.40, sz), (sx, 1.46, sz), (sx, 1.46, 0.58), (sx, 0.98, 0.58)], 0.03, 'steel', 12)
        m.cyl((sx, 0.98, 0.58), (sx, 0.94, 0.58), 0.05, 'dark', 14)

        px, pz = 0.74 * s, -0.36
        m.cyl((px, 0.13, pz), (px, 0.20, pz), 0.13, 'dark', 24, 0.006)
        m.lathe([(0.0, 0.20), (0.11, 0.20), (0.12, 0.26), (0.12, 0.40), (0.09, 0.45), (0.0, 0.45)], 'blue', center=(px, 0, pz), seg=32)
        m.cyl((px, 0.45, pz), (px, 0.52, pz), 0.06, 'steel', 16)
        m.cyl((px, 0.52, pz), (px, 0.80, pz), 0.085, 'olive', 24, 0.008)
        for y in (0.56, 0.61, 0.66, 0.71, 0.76):
            m.torus((px, y, pz), (0, 1, 0), 0.088, 0.008, 'grey', 24, 6)
        m.cyl((px, 0.80, pz), (px, 0.84, pz), 0.06, 'dark', 16)
        m.box((px, 0.30, pz + 0.125), (0.12, 0.08, 0.01), 'yellow')
        m.pipe([(sx, 0.30, sz - 0.14), (sx, 0.30, pz + 0.12)], 0.05, 'blue', 16)
        m.pipe([(px - 0.11 * s, 0.33, pz), (0.50 * s, 0.33, pz + 0.05), (0.43 * s, 0.33, -0.22)], 0.05, 'blue', 16)
        m.cyl((0.47 * s, 0.33, -0.25), (0.505 * s, 0.33, -0.27), 0.07, 'dark', 16)
        m.pipe([(px, 0.84, pz), (px, 0.95, pz), (0.86 * s, 0.95, pz)], 0.012, 'black', 8)

    pzx, pzz = -0.10, -0.68
    m.lathe([(0.0, 0.14), (0.09, 0.14), (0.09, 1.15), (0.07, 1.21), (0.0, 1.23)], 'light', center=(pzx, 0, pzz), seg=32)
    for y in (0.35, 0.65, 0.95):
        m.torus((pzx, y, pzz), (0, 1, 0), 0.092, 0.007, 'steel', 32, 6)
    m.box((pzx, 0.8, pzz - 0.09), (0.08, 0.16, 0.006), 'yellow')
    m.pipe([(pzx, 1.23, pzz), (pzx, 1.30, pzz), (pzx, 1.30, -0.40), (pzx * 0.5, 1.26, -0.25)], 0.02, 'steel', 10)
    m.pipe([(pzx, 0.18, pzz + 0.09), (pzx, 0.18, -0.50)], 0.025, 'orange', 10)

    m.box((0.42, 0.40, -0.80), (0.62, 0.54, 0.2), 'dark', bevel=0.012)
    tilt = axis_rot((1, 0, 0), -35)
    m.box((0.42, 0.70, -0.80), (0.64, 0.04, 0.30), 'grey', rot=tilt, bevel=0.006)
    m.box((0.42, 0.715, -0.79), (0.36, 0.012, 0.17), 'screen', rot=tilt)
    for i, col in enumerate(('red', 'green', 'yellow', 'green')):
        p = Vector((0.70, 0.705, -0.80)) + tilt @ Vector((0, 0.02, -0.09 + i * 0.06))
        m.cyl(tuple(p - tilt @ Vector((0, 0.01, 0))), tuple(p + tilt @ Vector((0, 0.012, 0))), 0.016, col, 12)
    for i in range(3):
        p = Vector((0.12 + i * 0.07, 0.30, -0.905))
        m.cyl(tuple(p), tuple(p + Vector((0, 0, -0.012))), 0.022, ('red', 'green', 'glow')[i], 14)
    m.box((0.42, 0.52, -0.905), (0.5, 0.06, 0.008), 'yellow')

    m.box((-0.40, 0.47, -0.82), (0.36, 0.42, 0.12), 'yellow', bevel=0.012)
    m.box((-0.40, 0.47, -0.64), (0.18, 0.18, 0.30), 'grey', bevel=0.01)
    o = Vector((-0.40, 0.49, -0.881))
    for k in range(3):
        a0 = math.radians(90 + k * 120 - 30)
        a1 = math.radians(90 + k * 120 + 30)
        pts = []
        for t in range(7):
            a = a0 + (a1 - a0) * t / 6
            pts.append((0.12 * math.cos(a), 0.12 * math.sin(a)))
        for t in range(6, -1, -1):
            a = a0 + (a1 - a0) * t / 6
            pts.append((0.035 * math.cos(a), 0.035 * math.sin(a)))
        m.prism(pts, o, (-1, 0, 0), (0, 1, 0), 0.004, 'black')
    m.cyl((-0.40, 0.49, -0.881), (-0.40, 0.49, -0.885), 0.022, 'black', 16)
    m.cyl((-0.52, 0.31, -0.89), (-0.28, 0.31, -0.89), 0.012, 'steel', 10)
    for x in (-0.52, -0.28):
        m.cyl((x, 0.31, -0.89), (x, 0.31, -0.875), 0.012, 'steel', 10)
    for x in (-0.56, -0.24):
        for y in (0.28, 0.66):
            m.cyl((x, y, -0.88), (x, y, -0.886), 0.011, 'steel', 8)

    m.box((0, 0.46, 0.90), (1.72, 0.62, 0.05), 'dark', bevel=0.01)
    m.box((0, 0.78, 0.905), (1.76, 0.03, 0.06), 'steel', bevel=0.004)
    m.box((0, 0.14, 0.905), (1.76, 0.03, 0.06), 'steel', bevel=0.004)
    for x, y, col in SOCKETS:
        m.cyl((x, y, 0.92), (x, y, 0.945), 0.115, 'grey', 32, 0.006)
        m.cyl((x, y, 0.945), (x, y, 0.952), 0.092, col, 32)
        m.cyl((x, y, 0.952), (x, y, 0.956), 0.062, 'black', 24)
        for bx, by in ring_points(0.1, 4, math.pi / 4):
            m.cyl((x + bx, y + by, 0.945), (x + bx, y + by, 0.952), 0.009, 'steel', 6)
        m.box((x, y + 0.17, 0.928), (0.22, 0.035, 0.006), col)
        m.cyl((x, y + 0.24, 0.925), (x, y + 0.24, 0.94), 0.018, 'glow', 12)
    m.box((0, 0.3, 0.928), (0.004, 0.24, 0.006), 'grey')

    for x in (-0.70, -0.25):
        m.pipe([(x, 0.45, 0.875), (x, 0.45, 0.72), (x * 0.5, 0.62, 0.50)], 0.04, 'copper', 14)
    m.pipe([(0.25, 0.45, 0.875), (0.25, 0.45, 0.72), (0.25, 0.30, 0.62), (0.18, 0.30, 0.52)], 0.045, 'blue', 16)
    m.pipe([(0.70, 0.45, 0.875), (0.70, 0.45, 0.76), (0.70, 1.02, 0.76), (0.30, 1.10, 0.52)], 0.014, 'white', 10)
    m.box((0, 1.02, 0.74), (0.46, 0.26, 0.14), 'grey', bevel=0.01)
    m.box((0, 1.05, 0.813), (0.30, 0.12, 0.006), 'screen')
    m.cyl((-0.17, 0.95, 0.81), (-0.17, 0.95, 0.825), 0.014, 'green', 10)
    m.cyl((0.17, 0.95, 0.81), (0.17, 0.95, 0.825), 0.014, 'red', 10)
    return m.to_object('ReactorBody')


def build_rotor():
    m = Builder()
    m.cyl((0, -0.03, 0), (0, 0.04, 0), 0.075, 'dark', 24, 0.008)
    m.sphere((0, 0.04, 0), 0.05, 'steel', 12)
    for k in range(9):
        r = yaw(k * 40) @ axis_rot((1, 0, 0), 32)
        c = yaw(k * 40) @ Vector((0.21, 0.005, 0))
        m.box(tuple(c), (0.27, 0.012, 0.075), 'steel', rot=r, bevel=0.003)
    m.torus((0, 0.005, 0), (0, 1, 0), 0.355, 0.012, 'grey', 48, 8)
    return m.to_object('ReactorRotor')


def cell_uv(index, u01=0.5, v01=0.5):
    cx, cy = index % CELLS, index // CELLS
    span = 1.0 - 2.0 * MARGIN
    return ((cx + MARGIN + u01 * span) / CELLS, 1.0 - (cy + MARGIN + (1.0 - v01) * span) / CELLS)


def projected_uv(index, p, fn, lo, hi):
    axis = max(range(3), key=lambda k: abs(fn[k]))
    a, b = {0: (2, 1), 1: (0, 2), 2: (0, 1)}[axis]
    u = (p[a] - lo[a]) / max(1e-6, hi[a] - lo[a])
    v = (p[b] - lo[b]) / max(1e-6, hi[b] - lo[b])
    return cell_uv(index, min(1.0, max(0.0, u)), min(1.0, max(0.0, v)))


def export(obj, path, glow=None):
    me = obj.data
    me.calc_loop_triangles()
    mw = obj.matrix_world
    verts, index, lookup = [], [], {}
    world = [mw @ v.co for v in me.vertices]
    conv = [(c.y, c.z, -c.x) for c in world]
    lo = [min(c[k] for c in conv) for k in range(3)]
    hi = [max(c[k] for c in conv) for k in range(3)]
    for tri in me.loop_triangles:
        if glow is not None and (NAMES[tri.material_index] in GLOW) != glow:
            continue
        pts = []
        for li in tri.loops:
            co = mw @ me.vertices[me.loops[li].vertex_index].co
            n = me.corner_normals[li].vector if hasattr(me, 'corner_normals') else me.loops[li].normal
            p = (co.y, co.z, -co.x)
            nn = Vector((n.y, n.z, -n.x)).normalized()
            pts.append((p, tuple(nn)))
        a, b, c = (Vector(q[0]) for q in pts)
        fn = Vector((tri.normal.y, tri.normal.z, -tri.normal.x))
        if (b - a).cross(c - a).dot(fn) < 0:
            pts = [pts[0], pts[2], pts[1]]
        for p, n in pts:
            uv = projected_uv(tri.material_index, p, fn, lo, hi)
            key = tuple(round(x, 5) for x in p + n + uv)
            if key not in lookup:
                lookup[key] = len(verts)
                verts.append(p + n + uv)
            index.append(lookup[key])
    with open(path, 'wb') as f:
        f.write(b'MSH1')
        f.write(struct.pack('<ii', len(verts), len(index)))
        for v in verts:
            f.write(struct.pack('<8f', *v))
        f.write(struct.pack('<%di' % len(index), *index))
    bounds = [(round(min(v[k] for v in verts), 3), round(max(v[k] for v in verts), 3)) for k in range(3)]
    print(os.path.basename(path), len(verts), 'vertices', len(index) // 3, 'triangles', 'bounds', bounds)


def value_noise(rng, n, cells):
    grid = rng.random((cells + 1, cells + 1))
    t = np.linspace(0, cells, n, endpoint=False)
    i = t.astype(int)
    f = t - i
    f = f * f * (3 - 2 * f)
    g00 = grid[i[:, None], i[None, :]]
    g10 = grid[i[:, None] + 1, i[None, :]]
    g01 = grid[i[:, None], i[None, :] + 1]
    g11 = grid[i[:, None] + 1, i[None, :] + 1]
    fy, fx = f[:, None], f[None, :]
    return (g00 * (1 - fy) + g10 * fy) * (1 - fx) + (g01 * (1 - fy) + g11 * fy) * fx


SEAMED = {'grey', 'steel', 'light', 'olive', 'white', 'dark'}
PAINTED = {'yellow', 'blue', 'red', 'green', 'orange', 'white', 'olive'}


def cell_texture(name, base, seed):
    n = CELL_PX
    rng = np.random.default_rng(seed)
    y = np.arange(n)[:, None] / n
    x = np.arange(n)[None, :] / n
    col = np.ones((n, n, 3)) * np.array(base)[None, None, :]
    if name in GLOW:
        glow = 0.85 + 0.15 * np.cos((x - 0.5) * math.pi) * np.cos((y - 0.5) * math.pi)
        if name == 'screen':
            glow = glow * (0.9 + 0.1 * (np.arange(n)[:, None] % 4 < 2))
        return np.clip(col * glow[..., None], 0, 1)
    brushed = rng.normal(0, 1, (n, 1)) * 0.025 + rng.normal(0, 1, (n, n)) * 0.012
    if name in ('steel', 'light', 'grey', 'copper'):
        brushed = brushed * 1.8
    big = value_noise(rng, n, 6)
    grime = (0.10 + 0.10 * y) * big + 0.05 * value_noise(rng, n, 24)
    shade = 1.0 + brushed - grime
    if name in SEAMED:
        step = n // 4
        yy = np.arange(n)[:, None] % step
        xx = np.arange(n)[None, :] % (step * 2)
        seam = ((yy < 2) | (xx < 2)).astype(float)
        high = ((yy == 2) | (xx == 2)).astype(float)
        shade = shade - 0.22 * seam + 0.08 * high
        ry = (np.arange(n)[:, None] % step) - step // 2
        rx = (np.arange(n)[None, :] % (step * 2))
        rivet = ((np.abs(ry) < 2) & ((np.abs(rx - 8) < 2) | (np.abs(rx - step * 2 + 8) < 2))).astype(float)
        shade = shade + 0.12 * rivet
    if name in PAINTED:
        chips = (value_noise(rng, n, 40) > 0.82).astype(float) * (value_noise(rng, n, 9) > 0.55)
        col = col * (1 - chips[..., None]) + np.array([0.33, 0.34, 0.35])[None, None, :] * chips[..., None]
    streak = (rng.random((1, n)) > 0.97) * (0.06 * y)
    shade = shade - streak
    return np.clip(col * shade[..., None], 0, 1)


def write_png(path, pixel=None):
    size = CELLS * CELL_PX
    img = np.zeros((size, size, 3))
    for i, (name, c) in enumerate(PALETTE):
        cy, cx = i // CELLS, i % CELLS
        img[cy * CELL_PX:(cy + 1) * CELL_PX, cx * CELL_PX:(cx + 1) * CELL_PX] = cell_texture(name, c, 1000 + i)
    data = (np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)
    raw = b''.join(bytes(1) + data[r].tobytes() for r in range(size))

    def chunk(tag, data):
        return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', zlib.crc32(tag + data) & 0xffffffff)
    with open(path, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n')
        f.write(chunk(b'IHDR', struct.pack('>IIBBBBB', size, size, 8, 2, 0, 0, 0)))
        f.write(chunk(b'IDAT', zlib.compress(raw, 9)))
        f.write(chunk(b'IEND', b''))


def srgb(c):
    return [max(0, min(255, int(round(v * 255)))) for v in c]


def render_previews(objs, path):
    scn = bpy.context.scene
    scn.render.engine = 'BLENDER_WORKBENCH'
    scn.display.shading.light = 'STUDIO'
    scn.display.shading.color_type = 'MATERIAL'
    scn.display.shading.show_cavity = True
    scn.display.shading.show_shadows = True
    scn.render.resolution_x = 1100
    scn.render.resolution_y = 900
    scn.world = bpy.data.worlds.new('w')
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    scn.collection.objects.link(cam)
    scn.camera = cam
    for i, (ux, uy, uz) in enumerate(((2.6, 2.2, 3.2), (-2.8, 1.8, -3.0), (0.0, 3.6, 0.6))):
        cam.location = B((ux, uy, uz))
        target = B((0, 0.75, 0))
        cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scn.render.filepath = path.replace('.png', '_%d.png' % i)
        bpy.ops.render.render(write_still=True)


bpy.ops.wm.read_factory_settings(use_empty=True)
body = build_body()
rotor = build_rotor()
export(body, os.path.join(OUT, 'reactor_body.bin'), False)
export(body, os.path.join(OUT, 'reactor_glow.bin'), True)
export(rotor, os.path.join(OUT, 'reactor_rotor.bin'))
write_png(os.path.join(OUT, 'reactor_palette.png'))
if PREVIEW:
    rotor.location = B((0, 1.44, 0))
    render_previews([body, rotor], PREVIEW)
