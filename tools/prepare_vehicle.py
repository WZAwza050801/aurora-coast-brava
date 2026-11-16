import bpy
import json
import os
from mathutils import Vector

ROOT = r"D:\ai_for_games\赛车游戏复刻\Alsphalt8"
SOURCE = os.path.join(ROOT, "assets", "vehicles", "ferrari_source.glb")
OUTPUT = os.path.join(ROOT, "assets", "vehicles", "aurora_gt.glb")
REPORT = os.path.join(ROOT, "docs", "vehicle_inspection.json")

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SOURCE)

report = {"source": SOURCE, "objects": [], "materials": []}
for obj in bpy.context.scene.objects:
    item = {
        "name": obj.name,
        "type": obj.type,
        "location": list(obj.location),
        "dimensions": list(obj.dimensions),
        "parent": obj.parent.name if obj.parent else None,
    }
    if obj.type == "MESH":
        item["vertices"] = len(obj.data.vertices)
        item["polygons"] = len(obj.data.polygons)
        item["materials"] = [m.name if m else "" for m in obj.data.materials]
    report["objects"].append(item)


def set_input(node, names, value):
    for name in names:
        if name in node.inputs:
            node.inputs[name].default_value = value
            return


def make_material(name, color, metallic, roughness, coat=0.0, transmission=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    set_input(bsdf, ["Base Color"], (*color, 1.0))
    set_input(bsdf, ["Metallic"], metallic)
    set_input(bsdf, ["Roughness"], roughness)
    set_input(bsdf, ["Coat Weight", "Clearcoat"], coat)
    set_input(bsdf, ["Coat Roughness", "Clearcoat Roughness"], 0.08)
    set_input(bsdf, ["Transmission Weight", "Transmission"], transmission)
    set_input(bsdf, ["IOR"], 1.46)
    return mat

paint = make_material("Aurora Deep Blue - metallic clearcoat", (0.012, 0.055, 0.19), 0.88, 0.23, 0.72)
alloy = make_material("Forged satin aluminium", (0.42, 0.46, 0.52), 0.94, 0.2, 0.18)
glass = make_material("Smoked laminated glass", (0.025, 0.055, 0.075), 0.18, 0.08, 0.5, 0.22)
carbon = make_material("Satin carbon and dark trim", (0.012, 0.015, 0.022), 0.45, 0.31, 0.25)
rubber = make_material("Performance tyre rubber", (0.012, 0.014, 0.018), 0.0, 0.72)
light = make_material("Optical lamp details", (0.72, 0.86, 1.0), 0.35, 0.12, 0.7)

for obj in bpy.context.scene.objects:
    if obj.type != "MESH":
        continue
    n = obj.name.lower()
    if n == "body" or n.startswith("body_"):
        obj.data.materials.clear()
        obj.data.materials.append(paint)
    elif n.startswith("rim_"):
        obj.data.materials.clear()
        obj.data.materials.append(alloy)
    elif "glass" in n:
        obj.data.materials.clear()
        obj.data.materials.append(glass)
    elif "trim" in n or "grill" in n or "vent" in n:
        obj.data.materials.clear()
        obj.data.materials.append(carbon)
    elif "tire" in n or "tyre" in n:
        obj.data.materials.clear()
        obj.data.materials.append(rubber)
    elif "light" in n or "lamp" in n:
        obj.data.materials.clear()
        obj.data.materials.append(light)

report["materials"] = [m.name for m in bpy.data.materials]
report["wheel_nodes"] = [o.name for o in bpy.context.scene.objects if o.name.lower().startswith("rim_")]
report["bounds"] = {}
mesh_objects = [o for o in bpy.context.scene.objects if o.type == "MESH"]
if mesh_objects:
    points = [o.matrix_world @ Vector(corner) for o in mesh_objects for corner in o.bound_box]
    report["bounds"] = {
        "min": [min(p[i] for p in points) for i in range(3)],
        "max": [max(p[i] for p in points) for i in range(3)],
    }

with open(REPORT, "w", encoding="utf-8") as f:
    json.dump(report, f, ensure_ascii=False, indent=2)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "assets", "vehicles", "aurora_gt.blend"))
bpy.ops.export_scene.gltf(
    filepath=OUTPUT,
    export_format="GLB",
    export_yup=True,
    export_apply=True,
    export_draco_mesh_compression_enable=False,
    export_animations=False,
    export_cameras=False,
    export_lights=False,
)
FBX_OUTPUT = os.path.join(ROOT, "assets", "vehicles", "aurora_gt.fbx")
bpy.ops.export_scene.fbx(
    filepath=FBX_OUTPUT,
    axis_forward="-Z",
    axis_up="Y",
    apply_unit_scale=True,
    bake_space_transform=False,
    use_mesh_modifiers=True,
    add_leaf_bones=False,
)
print("VEHICLE_PREPARED", OUTPUT)
print("FBX_PREPARED", FBX_OUTPUT)
print("WHEELS", report["wheel_nodes"])
print("BOUNDS", report["bounds"])
