"""Prepare accepted CC-BY FBX bodies; preserve geometry, UVs and uniform proportions."""
import bpy
import shutil
from pathlib import Path
from mathutils import Vector

root = Path(__file__).resolve().parents[1]
source = root/'Logs/GroupBCandidates/Pack/source/Expanded/Cars'
for model, file, texture, scale in [
    ('LanciaS4', 'Lancia S4', 'lancia_delta_S4_alta.png', 1.0),
    ('Peugeot205T16', 'Peugeot 205', 'peugeot_205_T16_Evo2_alta.png', 1.8/1.851588249206543),
]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source/(file+'.fbx')))
    objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    assert len(objects) == 5
    for o in objects:
        points = [o.matrix_world @ Vector(v) for v in o.bound_box]
        center = sum(points, Vector())/8
        if 'wheel' in o.name:
            o.name = 'wheel_' + ('F' if center.y < 0 else 'B') + ('L' if center.x < 0 else 'R')
        else:
            o.name = model+' Body'
        o.location *= scale
        o.scale *= scale
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    folder = root/'Assets/Art/Vehicles'/model
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source/'Texture2D'/texture, folder/'Body.png')
    bpy.ops.export_scene.fbx(filepath=str(folder/(model+'.fbx')), use_selection=True,
        object_types={'MESH'}, axis_forward='-Z', axis_up='Y', bake_anim=False,
        add_leaf_bones=False, path_mode='STRIP', use_mesh_modifiers=False)
    print('EXPORTED',model,scale,[(o.name,tuple(o.dimensions)) for o in objects])
