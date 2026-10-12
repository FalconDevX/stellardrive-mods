import bpy, math, os, struct, sys
import numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:]
SRC, OUT = argv[0], argv[1]
TARGET_TRIS = int(argv[2]) if len(argv) > 2 else 30000
SIZE = 1.96
TEX_SIZE = 2048

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

px = np.empty(TEX_SIZE * TEX_SIZE * 4, dtype=np.float32)
baked.pixels.foreach_get(px)
px = px.reshape(TEX_SIZE, TEX_SIZE, 4)
r, g, b = px[..., 0], px[..., 1], px[..., 2]
glow = (b > 0.45) & (g > 0.4) & (b - r > 0.22) & (g - r > 0.15)
mask = bpy.data.images.new('mask', TEX_SIZE // 4, TEX_SIZE // 4)
small = glow.reshape(TEX_SIZE // 4, 4, TEX_SIZE // 4, 4).mean(axis=(1, 3)).astype(np.float32)
mask_px = np.stack([small, small, small, np.ones_like(small)], axis=-1)
mask.pixels.foreach_set(mask_px.ravel())

xs = [v.co.x for v in me.vertices]
ys = [v.co.y for v in me.vertices]
zs = [v.co.z for v in me.vertices]
cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
z0 = min(zs)
s = SIZE / max(max(xs) - min(xs), max(ys) - min(ys))

def to_unity(p):
    return ((p.y - cy) * s, (p.z - z0) * s, -(p.x - cx) * s)

def normal_to_unity(n):
    return tuple(Vector((n.y, n.z, -n.x)).normalized())

uv_layer = me.uv_layers.active.data
verts, index, lookup = [], [], {}
glow_pts = []
for tri in me.loop_triangles:
    pts = []
    for li in tri.loops:
        uv = uv_layer[li].uv
        pts.append((to_unity(me.vertices[me.loops[li].vertex_index].co), normal_to_unity(Vector(me.loops[li].normal)), (uv.x, uv.y)))
    a, b_, c = (Vector(q[0]) for q in pts)
    if (b_ - a).cross(c - a).dot(Vector(normal_to_unity(tri.normal))) < 0:
        pts = [pts[0], pts[2], pts[1]]
    u = sum(q[2][0] for q in pts) / 3
    v = sum(q[2][1] for q in pts) / 3
    if glow[min(TEX_SIZE - 1, int(v * TEX_SIZE)), min(TEX_SIZE - 1, int(u * TEX_SIZE))]:
        glow_pts.append(tuple((a + b_ + c) / 3))
    for p, n, uv in pts:
        key = tuple(round(x, 5) for x in p + n + uv)
        if key not in lookup:
            lookup[key] = len(verts)
            verts.append(p + n + uv)
        index.append(lookup[key])

with open(os.path.join(OUT, 'reactor_body.bin'), 'wb') as f:
    f.write(b'MSH1')
    f.write(struct.pack('<ii', len(verts), len(index)))
    for v in verts:
        f.write(struct.pack('<8f', *v))
    f.write(struct.pack('<%di' % len(index), *index))
print('reactor_body.bin', len(verts), 'vertices', len(index) // 3, 'triangles')

scn.view_settings.view_transform = 'Standard'
scn.render.image_settings.file_format = 'JPEG'
scn.render.image_settings.quality = 88
baked.save_render(os.path.join(OUT, 'reactor_albedo.jpg'), scene=scn)
scn.render.image_settings.file_format = 'PNG'
scn.render.image_settings.color_mode = 'BW'
mask.save_render(os.path.join(OUT, 'reactor_emission.png'), scene=scn)

bounds = [(min(v[k] for v in verts), max(v[k] for v in verts)) for k in range(3)]
print('BOUNDS', [(round(a, 3), round(b, 3)) for a, b in bounds])
pts = np.array(glow_pts)
print('GLOW TRIS', len(pts))
if len(argv) > 3:
    np.save(argv[3], pts)
if len(pts):
    for name, sel in (('-X', pts[:, 0] < -0.3), ('+X', pts[:, 0] > 0.3), ('-Z', pts[:, 2] < -0.3), ('+Z', pts[:, 2] > 0.3), ('top', pts[:, 1] > 1.2)):
        q = pts[sel]
        if len(q):
            print('TUBE', name, len(q), 'min', np.round(q.min(axis=0), 3).tolist(), 'max', np.round(q.max(axis=0), 3).tolist())
