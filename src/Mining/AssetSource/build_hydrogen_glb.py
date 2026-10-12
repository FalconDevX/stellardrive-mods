import bpy, math, os, struct, sys
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:]
SRC, OUT = argv[0], argv[1]
TARGET_TRIS = int(argv[2]) if len(argv) > 2 else 16000
HEIGHT = 1.96
TEX_SIZE = 1024

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

high = obj
bpy.ops.object.duplicate()
obj = bpy.context.view_layer.objects.active
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=0.0005)
bpy.ops.object.mode_set(mode='OBJECT')
src_tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
mod = obj.modifiers.new('dec', 'DECIMATE')
mod.ratio = min(1.0, TARGET_TRIS / src_tris)
mod.use_collapse_triangulate = True
bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
me = obj.data
while me.uv_layers:
    me.uv_layers.remove(me.uv_layers[0])
me.uv_layers.new(name='bake')
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(55), island_margin=0.004)
bpy.ops.uv.pack_islands(margin=0.004, rotate=True)
bpy.ops.object.mode_set(mode='OBJECT')

baked = bpy.data.images.new('baked', TEX_SIZE, TEX_SIZE)
mat = bpy.data.materials.new('low')
mat.use_nodes = True
tex_node = mat.node_tree.nodes.new('ShaderNodeTexImage')
tex_node.image = baked
mat.node_tree.nodes.active = tex_node
me.materials.clear()
me.materials.append(mat)
scn = bpy.context.scene
scn.render.engine = 'CYCLES'
scn.cycles.samples = 4
scn.render.bake.use_selected_to_active = True
scn.render.bake.cage_extrusion = 0.02
scn.render.bake.max_ray_distance = 0.08
scn.render.bake.margin = 8
bpy.ops.object.select_all(action='DESELECT')
high.select_set(True)
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'})
me.calc_loop_triangles()

xs = [v.co.x for v in me.vertices]
ys = [v.co.y for v in me.vertices]
zs = [v.co.z for v in me.vertices]
x0, x1 = min(xs), max(xs)
cy, cz = (min(ys) + max(ys)) / 2, (min(zs) + max(zs)) / 2

def end_radius(near):
    return max((math.hypot(v.co.y - cy, v.co.z - cz) for v in me.vertices if abs(v.co.x - near) < 0.04 * (x1 - x0)), default=0)

nozzle_at_max = end_radius(x1) > end_radius(x0)
sx = 1 if nozzle_at_max else -1
s = HEIGHT / (x1 - x0)
pad = (2.0 - HEIGHT) / 2

def to_unity(p):
    return ((p.y - cy) * s * sx, ((p.x - x0) if nozzle_at_max else (x1 - p.x)) * s + pad, (p.z - cz) * s)

def normal_to_unity(n):
    return tuple(Vector((n.y * sx, n.x * sx, n.z)).normalized())

uv_layer = me.uv_layers.active.data
verts, index, lookup = [], [], {}
for tri in me.loop_triangles:
    pts = []
    for li in tri.loops:
        uv = uv_layer[li].uv
        pts.append((to_unity(me.vertices[me.loops[li].vertex_index].co), normal_to_unity(Vector(me.loops[li].normal)), (uv.x, uv.y)))
    a, b, c = (Vector(q[0]) for q in pts)
    if (b - a).cross(c - a).dot(Vector(normal_to_unity(tri.normal))) < 0:
        pts = [pts[0], pts[2], pts[1]]
    for p, n, uv in pts:
        key = tuple(round(x, 5) for x in p + n + uv)
        if key not in lookup:
            lookup[key] = len(verts)
            verts.append(p + n + uv)
        index.append(lookup[key])

with open(os.path.join(OUT, 'hydrogen_thruster.bin'), 'wb') as f:
    f.write(b'MSH1')
    f.write(struct.pack('<ii', len(verts), len(index)))
    for v in verts:
        f.write(struct.pack('<8f', *v))
    f.write(struct.pack('<%di' % len(index), *index))
print('hydrogen_thruster.bin', len(verts), 'vertices', len(index) // 3, 'triangles')

img = baked
scn.render.image_settings.file_format = 'JPEG'
scn.render.image_settings.quality = 90
scn.view_settings.view_transform = 'Standard'
img.save_render(os.path.join(OUT, 'hydrogen_thruster_albedo.jpg'), scene=scn)

bounds = [(min(v[k] for v in verts), max(v[k] for v in verts)) for k in range(3)]
print('BOUNDS', bounds)
print('NOZZLE_TOP', (0, round(bounds[1][1], 3), 0))
