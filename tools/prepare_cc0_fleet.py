"""Batch-convert the CC0 "Road Car Showroom Lineup" GLB cars into Unity FBX.

Source : https://3dassets.dev/packs/road-car-showroom-lineup  (CC0 1.0)
Output : Assets/Vehicles/Fleet/<name>.fbx  +  docs/cc0_fleet_inspection.json

Normalisation contract (must match AuroraProjectBuilder.CreateCar):
  * car faces +Z in Unity (physics forward), up +Y
  * four independent wheel pivot nodes named wheel_fl / wheel_fr / wheel_rl / wheel_rr
  * ground plane at y = 0, body length ~4.5 m
  * mesh names kept stable so the builder's material override can find body/glass/rim

Run with:
  blender -b --python tools/prepare_cc0_fleet.py -- [--probe-only]
"""
import bpy
import json
import os
import sys
from mathutils import Vector

ROOT = r"D:\ai_for_games\赛车游戏复刻\Alsphalt8"
SRC = os.path.join(ROOT, "assets", "vehicles", "cc0")
DST = os.path.join(ROOT, "Assets", "Vehicles", "Fleet")
REPORT = os.path.join(ROOT, "docs", "cc0_fleet_inspection.json")

# id -> (unity asset name, era/style label). Only recognisable sports/GT shapes.
CARS = {
    "15113": ("MidEngineSupercar", "mid-engine supercar"),
    "15120": ("SportsCoupe90s", "1990s sports coupe"),
    "15118": ("MuscleCoupe70s", "1970s muscle coupe"),
    "15116": ("Roadster60s", "1960s open roadster"),
    "15112": ("Roadster", "two-seat roadster"),
    "15108": ("Coupe", "modern coupe"),
    "15107": ("Convertible", "convertible"),
}

# The CC0 pack uses glTF node names like "wheel-front-right" for the pivot and
# "wheel-front-right/rubber" for its submeshes.
WHEEL_MAP = {
    "wheel-front-left": "wheel_fl",
    "wheel-front-right": "wheel_fr",
    "wheel-rear-left": "wheel_rl",
    "wheel-rear-right": "wheel_rr",
}


def world_bounds(objects):
    pts = [o.matrix_world @ Vector(c) for o in objects if o.type == "MESH" for c in o.bound_box]
    if not pts:
        return None
    return {
        "min": [min(p[i] for p in pts) for i in range(3)],
        "max": [max(p[i] for p in pts) for i in range(3)],
    }


def probe(glb_path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb_path)
    info = {"objects": [], "bounds": world_bounds(bpy.context.scene.objects)}
    for o in bpy.context.scene.objects:
        info["objects"].append({
            "name": o.name,
            "type": o.type,
            "parent": o.parent.name if o.parent else None,
            "world_pos": [round(v, 4) for v in o.matrix_world.translation],
            "polygons": len(o.data.polygons) if o.type == "MESH" else 0,
            "materials": [m.name if m else "" for m in o.data.materials] if o.type == "MESH" else [],
        })
    return info


def make_material(name, color, metallic, roughness):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    for k in ("Base Color",):
        if k in bsdf.inputs:
            bsdf.inputs[k].default_value = (*color, 1.0)
    for k in ("Metallic",):
        if k in bsdf.inputs:
            bsdf.inputs[k].default_value = metallic
    for k in ("Roughness",):
        if k in bsdf.inputs:
            bsdf.inputs[k].default_value = roughness
    return mat


def convert(glb_path, out_name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb_path)

    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bounds = world_bounds(meshes)
    if not bounds:
        raise RuntimeError("no meshes in " + glb_path)

    # The pack is metre-sized, +Y up, nose +Z in glTF. Blender imports with
    # Z-up, so the glTF long axis Z becomes Blender Y. Normalise length by the
    # horizontal extent (Blender Y after import) to the project's 4.53 m.
    span_x = bounds["max"][0] - bounds["min"][0]
    span_y = bounds["max"][1] - bounds["min"][1]
    long_axis = 1 if span_y > span_x else 0
    long_span = span_y if long_axis == 1 else span_x
    target_len = 4.53
    scale = target_len / long_span

    # Ground + centre before scaling (all transforms are rigid node transforms).
    min_z = bounds["min"][2]
    cx = (bounds["min"][0] + bounds["max"][0]) * 0.5
    cy = (bounds["min"][1] + bounds["max"][1]) * 0.5
    for o in bpy.context.scene.objects:
        if o.parent is None:
            o.location.x = (o.location.x - cx) * scale
            o.location.y = (o.location.y - cy) * scale
            o.location.z = (o.location.z - min_z) * scale
            o.scale = (o.scale.x * scale, o.scale.y * scale, o.scale.z * scale)

    bpy.context.view_layer.update()

    # glTF keeps the original material names (paint / glass / alloy / trim /
    # white / accent / rubber / steel). Use them as the authoritative class,
    # then rename every mesh to the shared naming contract that
    # AuroraProjectBuilder.CreateCar dispatches on: body*, glass*, rim*, tire*,
    # trim*. This makes one material-mapping routine work for BMW, Ferrari and
    # every CC0 car without per-model exceptions.
    prefix_by_material = {
        "paint": "body",
        "glass": "glass",
        "rubber": "tire",
        "trim": "trim",
        "dark": "trim",
    }
    material_slots = {
        "body": make_material("Fleet paint", (0.72, 0.16, 0.05), 0.75, 0.28),
        "glass": make_material("Fleet glass", (0.03, 0.06, 0.09), 0.2, 0.12),
        "tire": make_material("Fleet rubber", (0.015, 0.016, 0.02), 0.0, 0.75),
        "rim": make_material("Fleet alloy", (0.62, 0.66, 0.72), 0.9, 0.25),
        "trim": make_material("Fleet dark trim", (0.02, 0.022, 0.028), 0.35, 0.4),
    }

    def classify(o):
        mats = [m.name.lower() if m else "" for m in o.data.materials]
        src = mats[0] if mats else ""
        if src in prefix_by_material:
            return prefix_by_material[src]
        if any(k in src for k in ("glass", "window")):
            return "glass"
        if any(k in src for k in ("rubber", "tire", "tyre")):
            return "tire"
        if any(k in src for k in ("alloy", "steel", "accent", "white", "chrome")):
            return "rim"
        if any(k in src for k in ("trim", "plastic", "grill")):
            return "trim"
        return "body"

    wheel_children = {}
    for o in meshes:
        cls = classify(o)
        # Keep the wheel-side suffix so wheel visuals remain identifiable.
        parent_name = o.parent.name if o.parent else ""
        suffix = o.name.replace("/", "_").replace(" ", "_")
        o.name = cls + "_" + suffix
        o.data.materials.clear()
        o.data.materials.append(material_slots[cls])
        if "wheel-" in parent_name:
            wheel_children.setdefault(parent_name, []).append(o)

    # Rename the four wheel pivots to the contract used by CreateCar.
    renamed = {}
    for o in list(bpy.context.scene.objects):
        key = o.name.lower().split("/")[0]
        if key in WHEEL_MAP:
            new = WHEEL_MAP[key]
            if new not in renamed:
                o.name = new
                renamed[new] = [round(v, 4) for v in o.matrix_world.translation]
    missing = [v for v in WHEEL_MAP.values() if v not in renamed]
    if missing:
        raise RuntimeError("missing wheel pivots " + str(missing) + " in " + glb_path)

    os.makedirs(DST, exist_ok=True)
    fbx_path = os.path.join(DST, out_name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        bake_space_transform=False,
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        object_types={"EMPTY", "MESH"},
    )
    return {
        "fbx": fbx_path,
        "wheels": renamed,
        "bounds": world_bounds(meshes),
        "scale": round(scale, 5),
        "meshes": len(meshes),
        "polygons": sum(len(o.data.polygons) for o in meshes),
    }


def main():
    probe_only = "--probe-only" in sys.argv
    report = {"source": "https://3dassets.dev/packs/road-car-showroom-lineup (CC0 1.0)", "cars": {}}
    if probe_only:
        for cid, (name, label) in CARS.items():
            p = os.path.join(SRC, cid + ".glb")
            info = probe(p)
            report["cars"][name] = {"label": label, "bounds": info["bounds"],
                                    "objects": info["objects"][:8],
                                    "wheel_pivots": [o["name"] for o in info["objects"]
                                                     if o["type"] == "EMPTY" and "wheel-" in o["name"] and "/" not in o["name"]]}
            print("PROBE", name, "bounds", info["bounds"],
                  "pivots", report["cars"][name]["wheel_pivots"])
    else:
        for cid, (name, label) in CARS.items():
            p = os.path.join(SRC, cid + ".glb")
            print("CONVERT", name, p, flush=True)
            report["cars"][name] = convert(p, name)
            report["cars"][name]["label"] = label
            report["cars"][name]["source_id"] = cid
            print("  ->", report["cars"][name]["fbx"], "polygons", report["cars"][name]["polygons"], flush=True)
    with open(REPORT, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
    print("FLEET_REPORT", REPORT, flush=True)


if __name__ == "__main__":
    main()
