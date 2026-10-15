import bpy, math, os, struct, sys
import numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:]
SRC, OUT, NAME, MODE = argv[0], argv[1], argv[2], argv[3]
TARGET_TRIS = int(argv[4])
FIT = float(argv[5])
TEX_SIZE = int(argv[6]) if len(argv) > 6 else 1024

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
high = bpy.context.view_layer.objects.active
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

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
bpy.ops.uv.smart_project(angle_limit=math.radians(55), island_margin=0.003)
bpy.ops.uv.pack_islands(margin=0.003, rotate=True)
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
scn.render.bake.cage_extrusion = 0.03
scn.render.bake.max_ray_distance = 0.15
scn.render.bake.margin = 8
bpy.ops.object.select_all(action='DESELECT')
high.select_set(True)
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'})
me.calc_loop_triangles()

pts = np.array([(v.co.y, v.co.z, -v.co.x) for v in me.vertices])
lo, hi = pts.min(axis=0), pts.max(axis=0)
rot = np.eye(3)
if MODE == 'tool':
    ext = hi - lo
    axis = 0 if ext[0] >= ext[2] else 2
    mid = (lo[axis] + hi[axis]) / 2
    low_y = pts[:, 1] < lo[1] + 0.25 * ext[1]
    grip_side = np.sign(np.mean(pts[low_y, axis]) - mid) or 1.0
    if axis == 0:
        rot = np.array([[0, 0, grip_side], [0, 1, 0], [-grip_side, 0, 0]], dtype=float)
    elif grip_side > 0:
        rot = np.array([[-1, 0, 0], [0, 1, 0], [0, 0, -1]], dtype=float)
    pts = pts @ rot.T
    lo, hi = pts.min(axis=0), pts.max(axis=0)
    center = (lo + hi) / 2
    scale = FIT / (hi - lo).max()
else:
    center = np.array([(lo[0] + hi[0]) / 2, lo[1], (lo[2] + hi[2]) / 2])
    scale = FIT / max(hi[0] - lo[0], hi[2] - lo[2])

def to_unity(p):
    q = rot @ np.array([p.y, p.z, -p.x])
    return tuple(((q - center) * scale).tolist())

def normal_to_unity(n):
    q = rot @ np.array([n.y, n.z, -n.x])
    return tuple(Vector(q.tolist()).normalized())

uv_layer = me.uv_layers.active.data
verts, index, lookup = [], [], {}
for tri in me.loop_triangles:
    tri_pts = []
    for li in tri.loops:
        uv = uv_layer[li].uv
        tri_pts.append((to_unity(me.vertices[me.loops[li].vertex_index].co), normal_to_unity(Vector(me.loops[li].normal)), (uv.x, uv.y)))
    a, b_, c = (Vector(q[0]) for q in tri_pts)
    if (b_ - a).cross(c - a).dot(Vector(normal_to_unity(tri.normal))) < 0:
        tri_pts = [tri_pts[0], tri_pts[2], tri_pts[1]]
    for p, n, uv in tri_pts:
        key = tuple(round(x, 5) for x in p + n + uv)
        if key not in lookup:
            lookup[key] = len(verts)
            verts.append(p + n + uv)
        index.append(lookup[key])

with open(os.path.join(OUT, NAME + '_body.bin'), 'wb') as f:
    f.write(b'MSH1')
    f.write(struct.pack('<ii', len(verts), len(index)))
    for v in verts:
        f.write(struct.pack('<8f', *v))
    f.write(struct.pack('<%di' % len(index), *index))
print(NAME + '_body.bin', len(verts), 'vertices', len(index) // 3, 'triangles')

scn.view_settings.view_transform = 'Standard'
scn.render.image_settings.file_format = 'JPEG'
scn.render.image_settings.quality = 88
baked.save_render(os.path.join(OUT, NAME + '_albedo.jpg'), scene=scn)
bounds = [(min(v[k] for v in verts), max(v[k] for v in verts)) for k in range(3)]
print('BOUNDS', [(round(a, 3), round(b, 3)) for a, b in bounds])
