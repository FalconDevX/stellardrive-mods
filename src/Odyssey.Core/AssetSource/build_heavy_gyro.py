import bpy, bmesh, math, os, struct, sys
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
CELLS = 4

C = Matrix(((0, 0, -1), (1, 0, 0), (0, 1, 0)))

PIVOT_Y = 0.5306


def B(v):
    return C @ Vector(v)


def frame_for(axis_up):
    up = Vector(axis_up).normalized()
    ref = Vector((1, 0, 0)) if abs(up.x) < 0.9 else Vector((0, 0, 1))
    x = up.cross(ref).normalized()
    z = x.cross(up).normalized()
    return Matrix((x, up, z)).transposed()


class Builder:
    def __init__(self):
        self.bm = bmesh.new()

    def _place(self, verts, m3, center):
        mb = C @ m3 @ C.transposed()
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation(B(center)) @ mb.to_4x4(), verts=verts)

    def _paint(self, verts, mat):
        faces = set()
        for v in verts:
            faces.update(v.link_faces)
        for f in faces:
            f.material_index = NAMES.index(mat)

    def box(self, center, size, mat, rot=None, bevel=0.0):
        verts = bmesh.ops.create_cube(self.bm, size=1.0)['verts']
        self._place(verts, (rot or Matrix.Identity(3)) @ Matrix.Diagonal(Vector(size)), center)
        self._paint(verts, mat)
        if bevel > 0:
            edges = list({e for v in verts for e in v.link_edges})
            bmesh.ops.bevel(self.bm, geom=edges + verts, offset=bevel, segments=2, profile=0.5, affect='EDGES', clamp_overlap=True)
        return self

    def lathe(self, profile, mat, center, axis_up, seg=40):
        rings = [[self.bm.verts.new(B((r * math.cos(a), y, r * math.sin(a)))) for a in (i * 2 * math.pi / seg for i in range(seg))] for r, y in profile]
        for a, b in zip(rings, rings[1:]):
            for i in range(seg):
                j = (i + 1) % seg
                try:
                    self.bm.faces.new([a[i], a[j], b[j], b[i]])
                except ValueError:
                    pass
        verts = [v for ring in rings for v in ring]
        self._place(verts, frame_for(axis_up), center)
        self._paint(verts, mat)
        return self

    def cyl(self, a, b, r, mat, seg=16):
        a, b = Vector(a), Vector(b)
        verts = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r, depth=(b - a).length)['verts']
        self._place(verts, frame_for(b - a), (a + b) / 2)
        self._paint(verts, mat)
        return self

    def pipe(self, points, r, mat, seg=12):
        for a, b in zip(points, points[1:]):
            self.cyl(a, b, r, mat, seg)
        for p in points[1:-1]:
            self.lathe([(r * math.sin(math.pi * i / 6), -r * math.cos(math.pi * i / 6)) for i in range(7)], mat, p, (0, 1, 0), seg)
        return self

    def prism(self, pts2d, origin, u, v, depth, mat):
        o, u, v = Vector(origin), Vector(u), Vector(v)
        n = u.cross(v).normalized()
        front = [self.bm.verts.new(B(o + u * x + v * y)) for x, y in pts2d]
        back = [self.bm.verts.new(B(o + u * x + v * y - n * depth)) for x, y in pts2d]
        k = len(pts2d)
        for f in [front, list(reversed(back))] + [[front[(i + 1) % k], front[i], back[i], back[(i + 1) % k]] for i in range(k)]:
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


def hazard(m, center, right, up, outward, length, height):
    o, r, u, n = Vector(center), Vector(right), Vector(up), Vector(outward)
    m.box(tuple(o + n * 0.002), tuple(abs(c) for c in (r * length + u * height + n * 0.004)), 'yellow')
    stripe = min(height, 0.034)
    w, slant, step = stripe * 0.75, stripe * 0.55, stripe * 1.6
    x = -length / 2 + 0.006
    while x + w + slant < length / 2 - 0.004:
        h = height / 2 - 0.002
        m.prism([(x, -h), (x + w, -h), (x + w + slant, h), (x + slant, h)], o + n * 0.0045, r, u, 0.003, 'black')
        x += step


def bolt(m, center, normal, r=0.0085, length=0.008):
    c, n = Vector(center), Vector(normal).normalized()
    m.cyl(tuple(c), tuple(c + n * length), r, 'grey', seg=6)


def build_aframes(m):
    for sz in (-1, 1):
        face = sz * 0.461
        foot = sz * 0.469
        z = sz * 0.476
        center = (0, PIVOT_Y, z)
        m.lathe([(0.052, -0.015), (0.086, -0.015), (0.094, -0.009), (0.094, 0.009), (0.086, 0.015), (0.052, 0.015), (0.052, -0.015)], 'dark', center, (0, 0, 1), seg=40)
        m.lathe([(0.0525, -0.004), (0.061, -0.004), (0.061, 0.018), (0.0525, 0.018), (0.0525, -0.004)], 'yellow', center, (0, 0, 1), seg=40)
        m.lathe([(0.079, 0.0145), (0.084, 0.0145), (0.084, 0.0175), (0.079, 0.0175), (0.079, 0.0145)], 'glow', center, (0, 0, 1), seg=40)
        for i in range(8):
            a = math.radians(22.5 + 45 * i)
            bolt(m, (0.069 * math.cos(a), PIVOT_Y + 0.069 * math.sin(a), z + sz * 0.015), (0, 0, sz), 0.0065, 0.006)
        for sx in (-1, 1):
            for y in (0.31, 0.37):
                x = sx * (0.22 - (y - 0.3) * 0.9)
                m.box((x, y, face + sz * 0.003), (0.034, 0.034, 0.006), 'dark', bevel=0.002)
                bolt(m, (x, y, face + sz * 0.006), (0, 0, sz), 0.0075, 0.006)
            m.box((sx * 0.335, 0.075, foot + sz * 0.004), (0.2, 0.09, 0.008), 'dark', bevel=0.002)
            hazard(m, (sx * 0.335, 0.075, foot + sz * 0.008), (1, 0, 0), (0, 1, 0), (0, 0, sz), 0.18, 0.07)
            for bx in (-1, 1):
                bolt(m, (sx * 0.335 + bx * 0.075, 0.165, foot), (0, 0, sz), 0.0075, 0.008)


def build_cables(m):
    for sx in (-1, 1):
        z = -0.476
        path = [(sx * 0.42, 0.17, -0.46), (sx * 0.42, 0.17, z), (sx * 0.30, 0.17, z), (sx * 0.265, 0.27, z), (sx * 0.12, 0.45, z), (sx * 0.092, 0.488, z)]
        m.pipe(path, 0.0075, 'rubber', seg=10)
        for t in (0.35, 0.65):
            a, b = Vector(path[3]), Vector(path[4])
            c = a + (b - a) * t
            m.box(tuple(c + Vector((0, 0, 0.004))), (0.026, 0.026, 0.012), 'dark', rot=Matrix.Rotation(math.atan2(b.y - a.y, b.x - a.x), 3, 'Z'), bevel=0.002)


def build_conditioners(m):
    for sx in (-1, 1):
        x = sx * 0.42
        z0 = -0.375
        m.box((x, 0.215, z0), (0.1, 0.13, 0.12), 'grey', bevel=0.008)
        m.box((x, 0.285, z0), (0.104, 0.012, 0.124), 'dark', bevel=0.003)
        for i in range(5):
            m.box((x + sx * 0.051, 0.175 + i * 0.019, z0), (0.006, 0.008, 0.09), 'dark')
        m.box((x, 0.215, z0 + 0.061), (0.07, 0.07, 0.004), 'white')
        m.box((x - sx * 0.012, 0.228, z0 + 0.0635), (0.03, 0.006, 0.002), 'black')
        m.box((x - sx * 0.012, 0.212, z0 + 0.0635), (0.03, 0.006, 0.002), 'black')
        m.box((x + sx * 0.02, 0.2, z0 + 0.0635), (0.012, 0.012, 0.002), 'yellow')
        m.cyl((x, 0.215, z0 - 0.06), (x, 0.215, z0 - 0.085), 0.016, 'dark', seg=12)
        m.cyl((x, 0.215, z0 - 0.085), (x, 0.17, z0 - 0.085), 0.009, 'rubber', seg=10)
        m.box((x - sx * 0.025, 0.2925, z0 - 0.03), (0.02, 0.006, 0.02), 'glow')
        m.box((x - sx * 0.025, 0.2925, z0 + 0.0), (0.02, 0.006, 0.02), 'glow')
        m.box((x - sx * 0.025, 0.2925, z0 + 0.03), (0.02, 0.006, 0.02), 'glow')
        for bz in (-1, 1):
            bolt(m, (x + sx * 0.03, 0.291, z0 + bz * 0.045), (0, 1, 0), 0.0055, 0.004)


def build_bar(m):
    for sx in (-1, 1):
        x = sx * 0.332
        m.lathe([(0.056, 0.0), (0.084, 0.0), (0.088, 0.004), (0.088, 0.011), (0.082, 0.015), (0.056, 0.015), (0.056, 0.0)], 'steel', (x, 0, 0), (sx, 0, 0), seg=32)
        for i in range(8):
            a = math.radians(22.5 + 45 * i)
            bolt(m, (x, 0.118 * math.sin(a), 0.118 * math.cos(a)), (sx, 0, 0), 0.0095, 0.009)
        for sy in (-1, 1):
            m.box((x + sx * 0.002, sy * 0.14, 0), (0.004, 0.012, 0.11), 'yellow')
    for sz in (-1, 1):
        for sy in (-1, 1):
            m.box((0, sy * 0.1235, sz * 0.366), (0.1, 0.019, 0.032), 'steel', bevel=0.003)
            for bx in (-1, 1):
                bolt(m, (bx * 0.035, sy * 0.133, sz * 0.366), (0, sy, 0), 0.0055, 0.004)


def export(obj, path, glow):
    me = obj.data
    me.calc_loop_triangles()
    verts, index, lookup = [], [], {}
    for tri in me.loop_triangles:
        if (NAMES[tri.material_index] in GLOW) != glow:
            continue
        cx, cy = tri.material_index % CELLS, tri.material_index // CELLS
        uv = ((cx + 0.5) / CELLS, 1.0 - (cy + 0.5) / CELLS)
        pts = []
        for li in tri.loops:
            co = obj.matrix_world @ me.vertices[me.loops[li].vertex_index].co
            n = me.corner_normals[li].vector if hasattr(me, 'corner_normals') else me.loops[li].normal
            pts.append(((co.y, co.z, -co.x), tuple(Vector((n.y, n.z, -n.x)).normalized())))
        a, b, c = (Vector(q[0]) for q in pts)
        if (b - a).cross(c - a).dot(Vector((tri.normal.y, tri.normal.z, -tri.normal.x))) < 0:
            pts = [pts[0], pts[2], pts[1]]
        for p, n in pts:
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
    clearance = min((math.hypot(v[0], v[1] - PIVOT_Y) for v in verts if abs(v[2]) < 0.395), default=1.0)
    reach = max(math.hypot(v[0], v[1]) for v in verts)
    print(os.path.basename(path), len(verts), 'vertices', len(index) // 3, 'triangles', 'bounds', bounds, 'clearance', round(clearance, 3), 'reach', round(reach, 3))


bpy.ops.wm.read_factory_settings(use_empty=True)
m = Builder()
build_aframes(m)
build_conditioners(m)
build_cables(m)
detail = m.to_object('HeavyGyroDetail')
export(detail, os.path.join(OUT, 'heavy_gyro_frame.bin'), False)
export(detail, os.path.join(OUT, 'heavy_gyro_glow.bin'), True)
b = Builder()
build_bar(b)
export(b.to_object('HeavyGyroBar'), os.path.join(OUT, 'heavy_gyro_bar.bin'), False)
if PREVIEW:
    bpy.ops.wm.save_as_mainfile(filepath=PREVIEW)
