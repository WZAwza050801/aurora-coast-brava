import bpy
import json
import os
from mathutils import Vector

ROOT = r"D:\ai_for_games\赛车游戏复刻\Alsphalt8"
SOURCE = os.path.join(ROOT, "assets", "vehicles", "bmw_cc0", "bmw.obj")
OUTPUT = os.path.join(ROOT, "Assets", "Vehicles", "AuroraGT.fbx")
REPORT = os.path.join(ROOT, "docs", "bmw_cc0_inspection.json")

if bpy.app.background:
    bpy.ops.wm.read_factory_settings(use_empty=True)
else:
    # Keep the MCP add-on and its socket alive when running inside a live Blender.
    for existing in list(bpy.data.objects):
        bpy.data.objects.remove(existing, do_unlink=True)

bpy.ops.wm.obj_import(filepath=SOURCE, forward_axis="NEGATIVE_Z", up_axis="Y")

for obj in list(bpy.context.scene.objects):
    if obj.name == "Platform":
        bpy.data.objects.remove(obj, do_unlink=True)


def world_center(obj):
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return sum(points, Vector()) / 8.0


def set_input(node, names, value):
    for name in names:
        if name in node.inputs:
            node.inputs[name].default_value = value
            return


def material(name, color, metallic, roughness, coat=0.0):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    set_input(bsdf, ["Base Color"], (*color, 1.0))
    set_input(bsdf, ["Metallic"], metallic)
    set_input(bsdf, ["Roughness"], roughness)
    set_input(bsdf, ["Coat Weight", "Clearcoat"], coat)
    set_input(bsdf, ["Coat Roughness", "Clearcoat Roughness"], 0.08)
    return mat

paint = material("Aurora midnight blue", (0.012, 0.055, 0.19), 0.88, 0.22, 0.75)
alloy = material("Forged aluminium", (0.44, 0.48, 0.55), 0.94, 0.2, 0.15)
rubber = material("Performance rubber", (0.01, 0.012, 0.016), 0.0, 0.74)
glass = material("Smoked glass", (0.022, 0.045, 0.06), 0.25, 0.09, 0.5)
carbon = material("Dark trim", (0.012, 0.016, 0.022), 0.35, 0.34, 0.2)
chrome = material("Polished metal", (0.58, 0.62, 0.68), 0.96, 0.16)
tail = material("Tail optics", (0.35, 0.004, 0.006), 0.25, 0.16, 0.6)

meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
body = next(o for o in meshes if o.name == "carShell")
body_center = world_center(body)
light = next((o for o in meshes if o.name == "lightsGlass"), body)
light_center = world_center(light)
dims = body.dimensions
length_axis = 0 if dims.x > dims.y else 1
width_axis = 1 - length_axis
front_sign = 1 if (light_center[length_axis] - body_center[length_axis]) > 0 else -1

wheel_tires = [o for o in meshes if o.name.startswith("Tire3_TireRubber")]
if len(wheel_tires) != 4:
    raise RuntimeError(f"Expected four tires, found {len(wheel_tires)}")
centers = [world_center(o) for o in wheel_tires]
wheel_info = []
for tire, center in zip(wheel_tires, centers):
    front = (center[length_axis] - body_center[length_axis]) * front_sign > 0
    right = center[width_axis] < body_center[width_axis]
    code = ("f" if front else "r") + ("r" if right else "l")
    wheel_info.append((code, tire, center))

roots = {}
for code, tire, center in wheel_info:
    root = bpy.data.objects.new("wheel_" + code, None)
    bpy.context.collection.objects.link(root)
    root.location = center
    bpy.context.view_layer.update()
    roots[code] = (root, center)

wheel_prefixes = ("Tire1_TireRim", "Tire3_TireBolts", "Tire3_TireRoundel", "Tire3_TireRubber", "Brake4_brakeAssembly", "Brake3_Cube")
for obj in meshes:
    if not obj.name.startswith(wheel_prefixes):
        continue
    center = world_center(obj)
    code, (root, wheel_center) = min(roots.items(), key=lambda item: (center - item[1][1]).length_squared)
    matrix = obj.matrix_world.copy()
    obj.parent = root
    obj.matrix_world = matrix
    if obj.name.startswith("Tire3_TireRubber"):
        obj.name = "tire_" + code
        obj.data.materials.clear(); obj.data.materials.append(rubber)
    elif obj.name.startswith("Tire1_TireRim"):
        obj.name = "rim_" + code
        obj.data.materials.clear(); obj.data.materials.append(alloy)

for obj in meshes:
    if obj.name.startswith("wheel_"):
        continue
    name = obj.name.lower()
    if obj == body:
        obj.name = "body"
        obj.data.materials.clear(); obj.data.materials.append(paint)
    elif "glass" in name or name == "window":
        obj.data.materials.clear(); obj.data.materials.append(glass)
    elif "taillight" in name:
        obj.data.materials.clear(); obj.data.materials.append(tail)
    elif "chrome" in name:
        for slot in obj.material_slots:
            slot.material = chrome
    elif any(part in name for part in ("plastic", "trim", "diffuser", "intake")):
        for slot in obj.material_slots:
            if slot.material and ("Black" in slot.material.name or "Matte" in slot.material.name):
                slot.material = carbon

# The imported FBX still faces Unity -X. AuroraProjectBuilder.CreateCar applies
# a +90 degree yaw to the visual root, matching the +Z WheelCollider forward.
# Normalize from the archive's centimetre-like units to a 2.76 m wheelbase.
wheelbase = abs(roots["fl"][1][length_axis] - roots["rl"][1][length_axis])
unit_scale = 2.76 / wheelbase
for obj in bpy.context.scene.objects:
    if obj.parent is None:
        obj.location *= unit_scale
        obj.scale *= unit_scale
bpy.context.view_layer.update()
scaled_points = [o.matrix_world @ Vector(corner) for o in bpy.context.scene.objects if o.type == "MESH" for corner in o.bound_box]
min_z = min(p.z for p in scaled_points)
for obj in bpy.context.scene.objects:
    if obj.parent is None:
        obj.location.z -= min_z
bpy.context.view_layer.update()
points = [o.matrix_world @ Vector(corner) for o in bpy.context.scene.objects if o.type == "MESH" for corner in o.bound_box]
report = {
    "source": SOURCE,
    "license": "CC0 / Public Domain (McGuire Computer Graphics Archive)",
    "authors": ["Mike Pan", "Morgan McGuire"],
    "source_url": "https://casual-effects.com/g3d/data10/research/model/bmw/bmw.zip",
    "objects": len([o for o in bpy.context.scene.objects if o.type == "MESH"]),
    "polygons": sum(len(o.data.polygons) for o in bpy.context.scene.objects if o.type == "MESH"),
    "wheel_roots": {code: list(pair[0].location) for code, pair in roots.items()},
    "wheel_dimensions": {code: list(tire.dimensions) for code, tire, center in wheel_info},
    "bounds": {
        "min": [min(p[i] for p in points) for i in range(3)],
        "max": [max(p[i] for p in points) for i in range(3)],
    },
    "length_axis_in_blender": length_axis,
    "front_sign": front_sign,
    "unity_builder_yaw_degrees": 90,
    "unit_scale": unit_scale,
    "ground_offset": -min_z,
}
with open(REPORT, "w", encoding="utf-8") as f:
    json.dump(report, f, ensure_ascii=False, indent=2)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "assets", "vehicles", "aurora_bmw_cc0.blend"))
bpy.ops.export_scene.fbx(filepath=OUTPUT, axis_forward="-Z", axis_up="Y", apply_unit_scale=True, use_mesh_modifiers=True, add_leaf_bones=False)
print("BMW_PREPARED", OUTPUT)
print("WHEELS", list(roots.keys()))
print("BOUNDS", report["bounds"])
