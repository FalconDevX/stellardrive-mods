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
POST = 0.45
TOP = 0.955


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
    w, slant, step = height * 0.75, height * 0.55, height * 1.6
    x = -length / 2 + 0.006
    while x + w + slant < length / 2 - 0.004:
        h = height / 2 - 0.002
        m.prism([(x, -h), (x + w, -h), (x + w + slant, h), (x + slant, h)], o + n * 0.0045, r, u, 0.003, 'black')
        x += step


def bolt(m, center, normal, r=0.0085, length=0.008):
    c, n = Vector(center), Vector(normal).normalized()
    m.cyl(tuple(c), tuple(c + n * length), r, 'grey', seg=6)


def build_cage(m):
    for sx in (-1, 1):
        for sz in (-1, 1):
            x, z = sx * POST, sz * POST
            m.box((x, 0.475, z), (0.05, 0.93, 0.05), 'steel', bevel=0.006)
            m.box((x, 0.02, z), (0.085, 0.04, 0.085), 'dark', bevel=0.005)
            for bx in (-1, 1):
                for bz in (-1, 1):
                    bolt(m, (x + bx * 0.029, 0.04, z + bz * 0.029), (0, 1, 0), 0.0065, 0.006)
            m.box((x, 0.86, z), (0.056, 0.05, 0.056), 'yellow')
            m.box((x, 0.83, z), (0.057, 0.01, 0.057), 'black')
            m.box((x, 0.985, z), (0.07, 0.03, 0.07), 'dark', bevel=0.006)

    for sz in (-1, 1):
        z = sz * POST
        m.box((0, TOP, z), (0.85, 0.05, 0.05), 'steel', bevel=0.005)
        hazard(m, (0, TOP, z + sz * 0.025), (1, 0, 0), (0, 1, 0), (0, 0, sz), 0.78, 0.034)
    for sx in (-1, 1):
        x = sx * POST
        m.box((x, TOP, 0), (0.05, 0.05, 0.85), 'steel', bevel=0.005)
        m.box((x, 0.405, 0), (0.042, 0.036, 0.85), 'steel', bevel=0.004)
        m.cyl((x, 0.43, -0.415), (x, 0.93, 0.415), 0.014, 'grey', seg=10)
        m.cyl((x, 0.43, 0.415), (x, 0.93, -0.415), 0.014, 'grey', seg=10)
        m.box((x, 0.68, 0), (0.05, 0.05, 0.05), 'dark', bevel=0.004)
        for sz in (-1, 1):
            m.prism([(0, 0), (0.09, 0), (0, -0.09)], (x + sz * 0.01, TOP - 0.025, sz * (POST - 0.025)), (0, 0, -sz), (0, 1, 0), 0.02, 'dark')

    for sx in (-1, 1):
        for sz in (-1, 1):
            m.prism([(0, 0), (0.08, 0), (0, -0.08)], (sx * (POST - 0.025), TOP - 0.025, sz * POST - sx * 0.01), (-sx, 0, 0), (0, 1, 0), 0.02, 'dark')


def build_bearings(m):
    for sz in (-1, 1):
        z = sz * 0.479
        center = (0, PIVOT_Y, z)
        profile = [(0.052, -0.014), (0.086, -0.014), (0.093, -0.008), (0.093, 0.008), (0.086, 0.014), (0.052, 0.014), (0.052, -0.014)]
        m.lathe(profile, 'dark', center, (0, 0, 1), seg=40)
        m.lathe([(0.0525, -0.004), (0.06, -0.004), (0.06, 0.016), (0.0525, 0.016), (0.0525, -0.004)], 'yellow', center, (0, 0, 1), seg=40)
        for i in range(6):
            a = math.radians(30 + 60 * i)
            bolt(m, (0.073 * math.cos(a), PIVOT_Y + 0.073 * math.sin(a), z + sz * 0.012), (0, 0, sz), 0.0075, 0.007)
        m.box((0, PIVOT_Y - 0.135, z - sz * 0.004), (0.12, 0.09, 0.02), 'dark', bevel=0.004)
        hazard(m, (0, PIVOT_Y - 0.135, z + sz * 0.006), (1, 0, 0), (0, 1, 0), (0, 0, sz), 0.105, 0.03)


def build_lamps(m):
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.box((sx * POST, 0.994, sz * POST), (0.036, 0.008, 0.036), 'glow')
    for sz in (-1, 1):
        m.box((0, TOP + 0.026, sz * POST), (0.3, 0.004, 0.012), 'glow')


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
    clearance = min((math.hypot(v[0], v[1] - PIVOT_Y) for v in verts if abs(v[2]) < 0.42), default=1.0)
    print(os.path.basename(path), len(verts), 'vertices', len(index) // 3, 'triangles', 'bounds', bounds, 'clearance', round(clearance, 3))


bpy.ops.wm.read_factory_settings(use_empty=True)
m = Builder()
build_cage(m)
build_bearings(m)
build_lamps(m)
detail = m.to_object('HeavyGyroDetail')
export(detail, os.path.join(OUT, 'heavy_gyro_frame.bin'), False)
export(detail, os.path.join(OUT, 'heavy_gyro_glow.bin'), True)
if PREVIEW:
    bpy.ops.wm.save_as_mainfile(filepath=PREVIEW)
