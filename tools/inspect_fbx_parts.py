"""List every mesh in an FBX with its world-space bounds.

Run it through Blender to check where a part actually ended up after the
GLB -> FBX conversion:

    "path/to/blender.exe" --background \
        --factory-startup --python tools/inspect_fbx_parts.py -- \
        "Assets/Vehicles/FleetHD/CityCarHD.fbx"

It is the fastest way to tell a mis-placed part (a tailgate hanging 0.9 m
behind the car, a wheel collapsed onto the origin) from a modelling
quirk. Output is sorted by top height, so anything poking above the roof
shows up first.
"""

import bpy
import sys
import os
import json

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
fbx_path = argv[0]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx_path)

out = []
for o in bpy.context.scene.objects:
    if o.type != "MESH":
        continue
    pts = [o.matrix_world @ v.co for v in o.data.vertices]
    if not pts:
        continue
    mnx = min(p.x for p in pts); mxx = max(p.x for p in pts)
    mny = min(p.y for p in pts); mxy = max(p.y for p in pts)
    mnz = min(p.z for p in pts); mxz = max(p.z for p in pts)
    mats = [m.name for m in o.data.materials]
    out.append({
        "name": o.name,
        "mat": mats[0] if mats else "",
        "x": [round(mnx, 3), round(mxx, 3)],
        "y": [round(mny, 3), round(mxy, 3)],
        "z": [round(mnz, 3), round(mxz, 3)],
        "parent": o.parent.name if o.parent else "",
    })

out.sort(key=lambda r: -r["z"][1])
print("=== meshes by top height ===")
for r in out:
    print("%-28s mat=%-12s x=%-18s y=%-18s z=%-18s parent=%s"
          % (r["name"][:28], r["mat"][:12], r["x"], r["y"], r["z"], r["parent"]))
