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
    print(os.path.basename(path), len(verts), 'vertices', len(index) // 3, 'triangles', 'bounds', bounds)


def bolt(m, center, normal, r=0.0085, length=0.008):
    c, n = Vector(center), Vector(normal).normalized()
    m.cyl(tuple(c), tuple(c + n * length), r, 'grey', seg=6)


def knurl(m, center, axis, r, length, mat, ribs=14):
    c, a = Vector(center), Vector(axis).normalized()
    m.cyl(tuple(c - a * length / 2), tuple(c + a * length / 2), r, mat, seg=24)
    f = frame_for(a)
    side, up = f.col[0], f.col[2]
    for i in range(ribs):
        t = 2 * math.pi * i / ribs
        d = side * math.cos(t) + up * math.sin(t)
        m.cyl(tuple(c + d * r - a * length * 0.45), tuple(c + d * r + a * length * 0.45), 0.0018, mat, seg=6)


def build_head(m):
    m.box((0, 0.117, 0.06), (0.046, 0.058, 0.25), 'steel', bevel=0.012)
    m.cyl((0, 0.146, -0.05), (0, 0.146, 0.17), 0.021, 'light', seg=28)
    m.box((0, 0.168, 0.04), (0.012, 0.01, 0.15), 'blue', bevel=0.003)
    for sx in (-1, 1):
        m.box((sx * 0.0235, 0.118, 0.07), (0.003, 0.03, 0.12), 'blue', bevel=0.001)
        m.box((sx * 0.025, 0.118, 0.07), (0.002, 0.008, 0.09), 'white')
        for z in (-0.02, 0.15):
            bolt(m, (sx * 0.0235, 0.098, z), (sx, 0, 0), 0.004, 0.003)
    m.lathe([(0.0, 0.0), (0.027, 0.0), (0.027, 0.012), (0.024, 0.016), (0.024, 0.03), (0.02, 0.034), (0.0, 0.034)], 'grey', (0, 0.117, 0.185), (0, 0, 1), 32)
    knurl(m, (0, 0.117, 0.232), (0, 0, 1), 0.028, 0.02, 'dark', 18)
    m.lathe([(0.0, 0.0), (0.026, 0.0), (0.026, 0.004), (0.016, 0.012), (0.0, 0.014)], 'dark', (0, 0.117, 0.242), (0, 0, 1), 32)
    for sx in (-1, 1):
        m.cyl((sx * 0.02, 0.117, 0.24), (sx * 0.02, 0.117, 0.268), 0.0075, 'dark', seg=12)
        m.lathe([(0.0075, 0.0), (0.0075, 0.004), (0.0, 0.006)], 'dark', (sx * 0.02, 0.117, 0.268), (0, 0, 1), 12)
        m.cyl((sx * 0.017, 0.117, 0.262), (sx * 0.0155, 0.117, 0.2745), 0.0018, 'black', seg=6)
    m.cyl((0, 0.117, 0.254), (0, 0.117, 0.27), 0.0065, 'copper', seg=16)
    m.lathe([(0.0065, 0.0), (0.004, 0.006), (0.0015, 0.009), (0.0, 0.009)], 'copper', (0, 0.117, 0.27), (0, 0, 1), 16)


def build_rear(m):
    m.box((0, 0.13, -0.058), (0.104, 0.068, 0.026), 'dark', bevel=0.008)
    m.box((0, 0.139, -0.0725), (0.074, 0.074, 0.006), 'black', rot=Matrix.Rotation(math.radians(11.1), 3, 'X'), bevel=0.003)
    for sx in (-1, 1):
        for sy in (-1, 1):
            bolt(m, (sx * 0.044, 0.13 + sy * 0.026, -0.071), (0, 0, -1), 0.003, 0.003)
    m.box((0, 0.1, -0.0715), (0.05, 0.005, 0.004), 'yellow')
    knurl(m, (0, 0.09, -0.06), (0, 0, 1), 0.012, 0.03, 'black', 12)
    m.cyl((0, 0.09, -0.04), (0, 0.09, -0.03), 0.008, 'copper', seg=6)
    knurl(m, (0.04, 0.15, -0.025), (1, 0, 0), 0.011, 0.016, 'black', 12)
    m.cyl((0.023, 0.15, -0.025), (0.032, 0.15, -0.025), 0.007, 'copper', seg=6)
    m.box((0.0485, 0.15, -0.025), (0.002, 0.004, 0.012), 'white')


def build_trigger(m):
    pts = [(0.0, 0.0), (0.012, 0.0), (0.02, -0.03), (0.024, -0.07), (0.02, -0.11), (0.012, -0.135), (0.004, -0.14),
           (0.008, -0.12), (0.01, -0.09), (0.006, -0.05), (-0.002, -0.02)]
    m.prism(pts, (-0.019, 0.098, 0.058), (0, 0, 1), (0, 1, 0), 0.038, 'light')
    for sx in (-1, 1):
        m.cyl((sx * 0.019, 0.098, 0.064), (sx * 0.026, 0.098, 0.064), 0.005, 'grey', seg=12)
    m.cyl((-0.026, 0.098, 0.064), (0.026, 0.098, 0.064), 0.0025, 'steel', seg=8)
    m.cyl((0, 0.105, 0.05), (0, 0.105, 0.08), 0.0045, 'steel', seg=12)


def build_grip(m):
    profile = [(-0.05, 0.095), (-0.047, 0.04), (-0.04, -0.02), (-0.034, -0.07), (-0.03, -0.088), (-0.018, -0.096), (0.02, -0.096),
               (0.03, -0.088), (0.032, -0.06), (0.026, -0.045), (0.03, -0.03), (0.032, -0.012), (0.026, 0.004), (0.03, 0.02),
               (0.032, 0.04), (0.026, 0.058), (0.03, 0.075), (0.036, 0.095)]
    m.prism(profile, (-0.022, 0, 0), (0, 0, 1), (0, 1, 0), 0.044, 'dark')
    inlay = [(-0.04, 0.07), (-0.037, 0.02), (-0.031, -0.03), (-0.026, -0.07), (0.014, -0.075), (0.018, -0.03), (0.017, 0.02), (0.019, 0.07)]
    for sx in (-1, 1):
        m.prism(inlay, (0.0225 if sx > 0 else -0.025, 0, 0), (0, 0, 1), (0, 1, 0), 0.0025, 'rubber')
        for y in range(-60, 61, 12):
            m.box((sx * 0.0252, y / 1000, -0.012), (0.0015, 0.003, 0.045), 'black')
        m.box((sx * 0.0228, -0.083, -0.005), (0.002, 0.008, 0.04), 'blue')
    m.box((0, 0.093, -0.007), (0.05, 0.014, 0.09), 'steel', bevel=0.004)


def build_inlet(m):
    m.cyl((0, -0.096, -0.005), (0, -0.104, -0.005), 0.012, 'copper', seg=6)
    m.cyl((0, -0.104, -0.005), (0, -0.112, -0.005), 0.009, 'copper', seg=16)
    knurl(m, (0, -0.118, -0.005), (0, 1, 0), 0.011, 0.01, 'steel', 14)
    m.cyl((0, -0.123, -0.005), (0, -0.128, -0.005), 0.006, 'steel', seg=16)
    m.cyl((0, -0.075, 0.03), (0, -0.075, 0.042), 0.012, 'grey', seg=20)
    m.cyl((0, -0.075, 0.042), (0, -0.075, 0.045), 0.013, 'dark', seg=24)
    m.cyl((0, -0.075, 0.0445), (0, -0.075, 0.0458), 0.0105, 'white', seg=24)
    m.box((0.003, -0.072, 0.0462), (0.0012, 0.009, 0.0008), 'red', rot=Matrix.Rotation(math.radians(-35), 3, 'Z'))
    m.box((0, -0.0835, 0.0462), (0.008, 0.0015, 0.0008), 'green')


def build_cup(m):
    c = (0, 0, 0.165)
    m.lathe([(0.0, -0.075), (0.048, -0.075), (0.056, -0.07), (0.06, -0.06), (0.061, -0.04), (0.062, 0.03), (0.063, 0.045),
             (0.066, 0.048), (0.066, 0.054), (0.0, 0.054)], 'light', c, (0, 1, 0), 40)
    for y in (-0.05, -0.02, 0.01):
        m.lathe([(0.0618, y), (0.0638, y + 0.002), (0.0638, y + 0.006), (0.0618, y + 0.008)], 'steel', c, (0, 1, 0), 40)
    m.box((0, -0.035, 0.165 - 0.0625), (0.05, 0.03, 0.003), 'yellow')
    for k in range(3):
        m.box((-0.014 + k * 0.014, -0.035, 0.165 - 0.0642), (0.007, 0.02, 0.0012), 'black')
    m.box((0, -0.01, 0.165 + 0.0625), (0.012, 0.09, 0.002), 'grey')
    for y in (-0.05, -0.03, -0.01, 0.01, 0.03):
        m.box((0, y, 0.165 + 0.0637), (0.008, 0.0012, 0.0012), 'black')
    m.lathe([(0.0, 0.054), (0.068, 0.054), (0.068, 0.064), (0.05, 0.07), (0.022, 0.074), (0.0, 0.074)], 'grey', c, (0, 1, 0), 40)
    for i in range(12):
        t = 2 * math.pi * i / 12
        m.box((0.067 * math.cos(t), 0.059, 0.165 + 0.067 * math.sin(t)), (0.006, 0.01, 0.006), 'dark')
    m.cyl((0.035, 0.07, 0.15), (0.035, 0.086, 0.15), 0.004, 'black', seg=10)
    m.cyl((0.035, 0.086, 0.152), (0.035, 0.086, 0.132), 0.004, 'black', seg=10)
    m.cyl((0, 0.07, 0.165), (0, 0.08, 0.165), 0.014, 'copper', seg=6)
    m.cyl((0, 0.08, 0.165), (0, 0.092, 0.165), 0.009, 'copper', seg=16)
    m.box((-0.071, 0.06, 0.165), (0.01, 0.014, 0.03), 'steel', bevel=0.002)
    m.box((-0.077, 0.045, 0.165), (0.006, 0.03, 0.012), 'red', bevel=0.002)


bpy.ops.wm.read_factory_settings(use_empty=True)
m = Builder()
build_head(m)
build_rear(m)
build_trigger(m)
build_grip(m)
build_inlet(m)
build_cup(m)
obj = m.to_object('PaintTool')
export(obj, os.path.join(OUT, 'paint_tool.bin'), False)
if PREVIEW:
    bpy.ops.wm.save_as_mainfile(filepath=PREVIEW)
