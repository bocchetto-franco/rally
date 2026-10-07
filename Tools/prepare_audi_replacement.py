"""Import TonyWony's original CC-BY Collada mesh without executing asset code."""
import bpy, bmesh, xml.etree.ElementTree as ET
from pathlib import Path
from mathutils import Vector, Matrix
root=Path(__file__).resolve().parents[1]
source=root/'Logs/VehicleReplacements/Audi/model/model.dae'
ns={'c':'http://www.collada.org/2005/11/COLLADASchema'}
doc=ET.parse(source).getroot()
mesh=doc.find('.//c:geometry/c:mesh',ns)
arrays={}
for s in mesh.findall('c:source',ns):
    vals=list(map(float,s.find('c:float_array',ns).text.split()))
    stride=int(s.find('c:technique_common/c:accessor',ns).get('stride','1'))
    arrays[s.get('id')]=[vals[i:i+stride] for i in range(0,len(vals),stride)]
vertex_source=mesh.find('c:vertices/c:input',ns).get('source')[1:]
positions=arrays[vertex_source]
poly=mesh.find('c:polylist',ns)
inputs={i.get('semantic'):(int(i.get('offset','0')),i.get('source')[1:]) for i in poly.findall('c:input',ns)}
stride=max(i[0] for i in inputs.values())+1
indices=list(map(int,poly.find('c:p',ns).text.split()))
counts=list(map(int,poly.find('c:vcount',ns).text.split()))
matrix_vals=list(map(float,doc.find('.//c:visual_scene/c:node/c:matrix',ns).text.split()))
matrix=Matrix([matrix_vals[i:i+4] for i in range(0,16,4)])
points=[]
for p in positions:
    v=matrix@Vector(p[:3]); points.append(Vector((v.x,-v.z,v.y)))
lo=Vector(tuple(min(v[j] for v in points) for j in range(3)))
hi=Vector(tuple(max(v[j] for v in points) for j in range(3)))
print('SOURCE_BOUNDS',tuple(lo),tuple(hi),'SIZE',tuple(hi-lo),flush=True)
scale=4.25/max(hi-lo)
points=[(v-Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z)))*scale for v in points]
faces=[];uvs=[];cursor=0
for n in counts:
    face=[]
    for j in range(n):
        at=cursor+j*stride;face.append(indices[at+inputs['VERTEX'][0]])
        uvs.append(arrays[inputs['TEXCOORD'][1]][indices[at+inputs['TEXCOORD'][0]]][:2])
    faces.append(face);cursor+=n*stride
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
data=bpy.data.meshes.new('Audi original');data.from_pydata(points,[],faces);data.update()
uv=data.uv_layers.new(name='UVMap')
for l,v in zip(uv.data,uvs):l.uv=v
for p in data.polygons:p.use_smooth=True
obj=bpy.data.objects.new('Audi original',data);bpy.context.collection.objects.link(obj)
bpy.context.view_layer.objects.active=obj;obj.select_set(True)
# Weld duplicated UV seam vertices only; UV data remains per-loop.
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=.00001);bpy.ops.mesh.separate(type='LOOSE');bpy.ops.object.mode_set(mode='OBJECT')
parts=[]
for o in list(bpy.data.objects):
    if o.type!='MESH':continue
    v=[o.matrix_world@Vector(b) for b in o.bound_box]
    low=Vector(tuple(min(p[j] for p in v) for j in range(3)));high=Vector(tuple(max(p[j] for p in v) for j in range(3)))
    parts.append((o.name,len(o.data.polygons),tuple((low+high)/2),tuple(high-low)))
print('PARTS',sorted(parts,key=lambda a:-a[1]),flush=True)
folder=root/'Assets/Art/Vehicles/AudiQuattro';folder.mkdir(parents=True,exist_ok=True)
groups={k:[] for k in ['wheel_FL','wheel_FR','wheel_BL','wheel_BR']};body=[]
for name,_,centre,size in parts:
    x,y,z=centre
    if abs(x)>.62 and abs(abs(y)-1.11664)<.32 and z<.62 and size[1]<.7 and size[2]<.7:
        groups['wheel_'+('F' if y>0 else 'B')+('L' if x<0 else 'R')].append(bpy.data.objects[name])
    else:body.append(bpy.data.objects[name])
material=bpy.data.materials.new('Audi Rally PBR')
for name,objects in list(groups.items())+[('Audi Body',body)]:
    assert objects,name
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join()
    o=bpy.context.object;o.name=name;o.data.materials.clear();o.data.materials.append(material)
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY',center='BOUNDS')
    if name.startswith('wheel_'):
        assert .59<o.dimensions.y<.62 and .59<o.dimensions.z<.62,(name,tuple(o.dimensions))
        print('WHEEL',name,'parts',len(objects),'center',tuple(o.location),'bounds',tuple(o.dimensions),flush=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(folder/'AudiQuattro.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
# Source roughness/metallic are linear data. Pack Unity's smoothness into alpha.
import numpy as np
textures=folder/'Textures';textures.mkdir(exist_ok=True)
rough=bpy.data.images.load(str(root/'Logs/VehicleReplacements/Audi/textures/Audi_Quttor_S1_roughness.jpeg'));rough.colorspace_settings.name='Non-Color';rough.scale(2048,2048)
metal=bpy.data.images.load(str(root/'Logs/VehicleReplacements/Audi/textures/Audi_Quttor_S1_metallic.jpeg'));metal.colorspace_settings.name='Non-Color';metal.scale(2048,2048)
a=np.empty(2048*2048*4,dtype=np.float32);b=np.empty_like(a);rough.pixels.foreach_get(a);metal.pixels.foreach_get(b)
b[3::4]=1-a[0::4]
packed=bpy.data.images.new('MetallicSmoothness',2048,2048,alpha=True);packed.colorspace_settings.name='Non-Color';packed.pixels.foreach_set(b);packed.filepath_raw=str(textures/'MetallicSmoothness.png');packed.file_format='PNG';packed.save()
print('AUDI_EXPORT_PASS length=4.25m triangles='+str(sum(len(o.data.polygons) for o in bpy.data.objects if o.type=='MESH')),flush=True)
