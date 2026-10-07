import bpy, json
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=r'C:\Users\franc\Downloads\rally\Logs\VehicleReplacements\BMW\BMW E30_Final01\E30_Final01.blend', load_ui=False, use_scripts=False)
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    print('PART',o.name,'location',tuple(o.location),'size',tuple(o.dimensions),'polys',len(o.data.polygons),'hide',o.hide_render,'mods',[(m.type,m.show_render) for m in o.modifiers],'mats',[m.name for m in o.data.materials if m])
for m in bpy.data.materials:
    print('MAT',m.name)
    if m.node_tree:
        for n in m.node_tree.nodes:
            if n.type=='TEX_IMAGE' and n.image: print('TEX',n.image.name,n.image.filepath,tuple(n.image.size))
