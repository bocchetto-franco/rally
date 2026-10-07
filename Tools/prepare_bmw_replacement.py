"""Prepare Martin Trafas' CC-BY 4.0 BMW. Original geometry/UVs, no procedural car mesh."""
import bpy
from pathlib import Path
from mathutils import Vector, Matrix
root=Path(__file__).resolve().parents[1]
source=root/'Logs/VehicleReplacements/BMW/BMW E30_Final01/E30_Final01.blend'
folder=root/'Assets/Art/Vehicles/BmwE30'
folder.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False,use_scripts=False)
for o in list(bpy.data.objects):
    if o.type!='MESH' or o.name=='SCENE': bpy.data.objects.remove(o,do_unlink=True)
# Undo the source's display steering pose before baking object transforms.
def bounds(o): return [o.matrix_world@Vector(v) for v in o.bound_box]
def center(o): return sum(bounds(o),Vector())/8
front_tyres=[o for o in bpy.data.objects if o.name.startswith('BMW_E30_M3_TIRE') and center(o).y<0]
for tyre in front_tyres:
    pivot=center(tyre)
    straighten=Matrix.Translation(pivot)@Matrix.Rotation(-tyre.rotation_euler.z,4,'Z')@Matrix.Translation(-pivot)
    for part in bpy.data.objects:
        if part.name.startswith(('BMW_E30_M3_RIM','BMW_E30_M3_TIRE','Brake_disc','Brembo_Calipers','Logo_Plane')):
            p=center(part)
            if p.y<0 and (p.x<0)==(pivot.x<0): part.matrix_world=straighten@part.matrix_world
# The source's geometry-node modifiers only implement the artist's presentation.
# Export the authored mesh, not its studio or any scene scripting.
for o in bpy.data.objects:
    o.modifiers.clear()
    o.select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects['BMW_E30_M3_BODY']
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
def bounds(o): return [o.matrix_world@Vector(v) for v in o.bound_box]
def center(o): return sum(bounds(o),Vector())/8
groups={k:[] for k in ['wheel_FL','wheel_FR','wheel_BL','wheel_BR']}
for o in list(bpy.data.objects):
    if o.name.startswith(('BMW_E30_M3_RIM','BMW_E30_M3_TIRE','Brake_disc')):
        p=center(o); groups['wheel_'+('F' if p.y<0 else 'B')+('L' if p.x<0 else 'R')].append(o)
for name,objects in groups.items():
    assert len(objects)==3,(name,len(objects))
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join();o=bpy.context.object;o.name=name
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY',center='BOUNDS')
# Normalize uniformly to 4.30m, maintaining the body's proportions.
body=bpy.data.objects['BMW_E30_M3_BODY']; v=bounds(body)
scale=4.30/(max(p.y for p in v)-min(p.y for p in v))
for o in bpy.data.objects:
    o.location*=scale;o.scale*=scale
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
bpy.ops.export_scene.fbx(filepath=str(folder/'BmwE30.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='STRIP',use_mesh_modifiers=False)
print('BMW_EXPORT_PASS',scale,[(k,tuple(bpy.data.objects[k].location),tuple(bpy.data.objects[k].dimensions)) for k in groups])
