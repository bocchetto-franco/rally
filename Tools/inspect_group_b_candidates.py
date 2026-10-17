"""Inspect downloaded FBX candidates without changing game assets."""
import bpy
import json
from pathlib import Path
from mathutils import Vector

root = Path(__file__).resolve().parents[1]
base = root / 'Logs/GroupBCandidates'
sources = [base/'Delta/source/Lancia Delta S4.fbx', base/'Pack/source/Expanded/Cars/Peugeot 205.fbx', base/'Pack/source/Expanded/Cars/Lancia S4.fbx']
results = []
for source in sources:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source))
    objects = []
    for o in bpy.context.scene.objects:
        if o.type != 'MESH':
            continue
        points = [o.matrix_world @ Vector(p) for p in o.bound_box]
        lo = [min(p[i] for p in points) for i in range(3)]
        hi = [max(p[i] for p in points) for i in range(3)]
        objects.append(dict(name=o.name, size=[hi[i]-lo[i] for i in range(3)], center=[(hi[i]+lo[i])/2 for i in range(3)], vertices=len(o.data.vertices), materials=[m.name for m in o.data.materials if m]))
    results.append(dict(source=str(source), objects=objects))
(base/'blender-inspection.json').write_text(json.dumps(results, indent=2))
print(json.dumps(results, indent=2))
