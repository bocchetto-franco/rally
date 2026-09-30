"""Normalize TARANTULA's CC-BY 4.0 FBX; retain original meshes, UVs and materials."""
import bpy
from pathlib import Path
from mathutils import Vector

root = Path(__file__).resolve().parents[1]
source = root / 'Logs/VehicleCandidates/LanciaSource/Integrale HF.fbx'
output = root / 'Assets/Art/Vehicles/LanciaDelta/LanciaDelta.fbx'
output.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

def center(obj):
    return sum((obj.matrix_world @ Vector(v) for v in obj.bound_box), Vector()) / 8

# One rear rim object spans both sides in the source. Split its disconnected
# islands, then group each rim and tyre by axle/side. Do not generate replacement wheels.
for obj in list(bpy.data.objects):
    if not obj.name.lower().startswith(('rim', 'tire')):
        continue
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='LOOSE')
    bpy.ops.object.mode_set(mode='OBJECT')

groups = {n: [] for n in ('wheel_FL', 'wheel_FR', 'wheel_BL', 'wheel_BR')}
for obj in list(bpy.data.objects):
    if obj.type != 'MESH' or not obj.name.lower().startswith(('rim', 'tire')):
        continue
    p = center(obj)
    name = 'wheel_' + ('F' if p.y < 0 else 'B') + ('L' if p.x < 0 else 'R')
    groups[name].append(obj)
for name, objects in groups.items():
    assert len(objects) > 0, 'Missing wheel ' + name
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    print('DELTA_WHEEL', name, tuple(obj.location), tuple(obj.dimensions))

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(output), use_selection=True, object_types={'MESH'},
    axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False,
    apply_unit_scale=True, path_mode='STRIP')
print('LANCIA_EXPORT_PASS', len(bpy.data.objects), 'objects')
