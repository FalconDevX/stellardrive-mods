import bpy, bmesh, sys, struct, math, numpy as np
argv=sys.argv[sys.argv.index('--')+1:]
src,outdir=argv[0],argv[1]
BODY_TRIS=int(argv[2]) if len(argv)>2 else 20000
DRILL_TRIS=int(argv[3]) if len(argv)>3 else 1200
AX,AY=0.17,0.28; R=0.11; ZTOP=-0.36
FEET=-0.673; SCALE=1.15
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
obj=[o for o in bpy.context.scene.objects if o.type=='MESH'][0]
bpy.context.view_layer.objects.active=obj
# split drill faces
bm=bmesh.new(); bm.from_mesh(obj.data)
for f in bm.faces:
    f.select = all(math.hypot(v.co.x-AX,v.co.y-AY)<R and v.co.z<ZTOP+0.005 for v in f.verts)
bm.to_mesh(obj.data); bm.free()
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.separate(type='SELECTED'); bpy.ops.object.mode_set(mode='OBJECT')
objs=[o for o in bpy.context.scene.objects if o.type=='MESH']
body=max(objs,key=lambda o:len(o.data.polygons)); drill=[o for o in objs if o!=body][0]
print("split body",len(body.data.polygons),"drill",len(drill.data.polygons))
def decimate(o,target):
    n=len(o.data.polygons)
    m=o.modifiers.new("d",'DECIMATE'); m.decimate_type='COLLAPSE'; m.ratio=min(1.0,target/n); m.use_collapse_triangulate=True
    bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier="d")
    print(o.name,"tris",len(o.data.polygons))
decimate(body,BODY_TRIS); decimate(drill,DRILL_TRIS)
# export: unity coords: (x,z,y) with origin at feet center (body) / drill axis top (drill), scaled
def export(o,path,origin):
    bpy.context.view_layer.objects.active=o
    for x in bpy.context.scene.objects: x.select_set(x==o)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
    me=o.data
    cn=me.corner_normals
    uvl=me.uv_layers.active.data
    # flat-ish shading: normals per face for hard-surface look, smooth if angle small
    verts={}; vbuf=[]; ibuf=[]
    me.calc_loop_triangles()
    for tri in me.loop_triangles:
        for li,vi in zip(tri.loops,tri.vertices):
            fn=cn[li].vector
            co=me.vertices[vi].co
            p=((co.x-origin[0])*SCALE,(co.z-origin[2])*SCALE,(co.y-origin[1])*SCALE)
            n=(fn.x,fn.z,fn.y)
            uv=uvl[li].uv
            key=(round(p[0],5),round(p[1],5),round(p[2],5),round(n[0],3),round(n[1],3),round(n[2],3),round(uv.x,5),round(uv.y,5))
            idx=verts.get(key)
            if idx is None:
                idx=len(vbuf); verts[key]=idx; vbuf.append(p+n+(uv.x,uv.y))
            ibuf.append(idx)
    with open(path,'wb') as f:
        f.write(b'MSH1'); f.write(struct.pack('<ii',len(vbuf),len(ibuf)))
        for v in vbuf: f.write(struct.pack('<8f',*v))
        f.write(struct.pack('<%di'%len(ibuf),*ibuf))
    print(path,len(vbuf),"verts",len(ibuf)//3,"tris")
export(body,outdir+"/excavator_body.bin",(0,0,FEET))
export(drill,outdir+"/excavator_drill.bin",(AX,AY,ZTOP))
print("DRILL_PIVOT_UNITY",(AX*SCALE,(ZTOP-FEET)*SCALE,AY*SCALE))
# texture
for name,fn in [('Image_0','excavator_albedo.png'),('Image_1','excavator_mr.png'),('Image_2','excavator_normal.png')]:
    im=bpy.data.images[name]; im.filepath_raw=outdir+'/'+fn; im.file_format='PNG'; im.save()
# Stopy (œrodki najni¿szych wierzcho³ków w æwiartkach) i punkty na œcianach korpusu pod gniazda pr¹du - w uk³adzie Unity.
import mathutils
me=body.data
lows={}
for v in me.vertices:
    if v.co.z < FEET+0.04:
        q=(v.co.x>0, v.co.y>0); lows.setdefault(q,[]).append(v.co.copy())
for q,pts in sorted(lows.items()):
    c=sum(pts,mathutils.Vector())/len(pts)
    print('FOOT_UNITY', round(c.x*SCALE,3), round(c.y*SCALE,3))
depsgraph=bpy.context.evaluated_depsgraph_get()
def hit(origin, direction):
    ok,loc,nrm,idx=body.ray_cast(mathutils.Vector(origin), mathutils.Vector(direction))
    if ok: print('SOCKET_UNITY', round(loc.x*SCALE,3), round((loc.z-FEET)*SCALE,3), round(loc.y*SCALE,3), 'normal', round(nrm.x,2), round(nrm.z,2), round(nrm.y,2))
    else: print('SOCKET_MISS', origin)
for z in (0.0, 0.1, 0.2):
    hit((3.0, 0.05, z), (-1,0,0))    # prawy bok (Blender +X)
    hit((0.2, 3.0, z), (0,-1,0))     # ty³ (Blender +Y = Unity +Z)

print("images",[(i.name,i.size[:]) for i in bpy.data.images])
bpy.ops.wm.save_as_mainfile(filepath=outdir+"/excavator.blend")
