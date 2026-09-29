"""Run with Blender --background --python Tools/prepare_classic_mini.py.
Adapt the attributed, downloaded Mini, not a procedurally generated car.
"""
import bpy
from pathlib import Path
from mathutils import Vector

root = Path(__file__).resolve().parents[1]
asset = root / 'Assets/Art/Vehicles/ClassicMini'
asset.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(asset / 'Source~/mini.glb'))
# The original artist's display plinth is not part of the drivable car.
bpy.data.objects.remove(bpy.data.objects['Cylinder_Cylinder.001'], do_unlink=True)
car = bpy.data.objects['Plane']
bpy.context.view_layer.objects.active = car
car.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.separate(type='LOOSE')
bpy.ops.object.mode_set(mode='OBJECT')

def bounds(obj):
    vs = [obj.matrix_world @ Vector(v) for v in obj.bound_box]
    return (Vector(tuple(min(v[i] for v in vs) for i in range(3))),
            Vector(tuple(max(v[i] for v in vs) for i in range(3))))

wheels, body = [], []
for obj in list(bpy.context.scene.objects):
    lo, hi = bounds(obj)
    center, size = (lo + hi) / 2, hi - lo
    # The source has four disconnected, complete tyre + hub assemblies.
    if center.z < -.5 and .65 < size.y < .75 and .65 < size.z < .75 and size.x < .3:
        obj.name = 'wheel_' + ('F' if center.y < 0 else 'B') + ('L' if center.x < 0 else 'R')
        wheels.append(obj)
    else:
        body.append(obj)
assert len(wheels) == 4, 'Source topology changed: expected four complete wheels.'
bpy.ops.object.select_all(action='DESELECT')
for obj in body:
    obj.select_set(True)
bpy.context.view_layer.objects.active = body[0]
bpy.ops.object.join()
body = bpy.context.object
body.name = 'Body'

# 3.10 m long; grounded, centred on wheelbase. Preserve the source silhouette.
scale = 3.10 / 4.125172138214111
lowest = min(bounds(o)[0].z for o in wheels)
for obj in [body] + wheels:
    for vertex in obj.data.vertices:
        vertex.co = Vector((vertex.co.x * scale, (vertex.co.y + .0065) * scale,
                            (vertex.co.z - lowest) * scale))
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(asset / 'ClassicMini.fbx'), use_selection=True,
    object_types={'MESH'}, axis_forward='-Z', axis_up='Y', bake_anim=False,
    add_leaf_bones=False, apply_unit_scale=True)
print('CLASSIC_MINI_EXPORT_PASS', [(o.name, tuple(o.location), tuple(o.dimensions)) for o in wheels])
