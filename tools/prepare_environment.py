import bpy
import json
import os

ROOT = r"D:\ai_for_games\赛车游戏复刻\Alsphalt8"
ASSETS = [
    ("island_tree_01", "IslandTree.fbx", 0.01),
    ("coast_land_rocks_02", "CoastRocks.fbx", 0.02),
    ("shrub_01", "Shrub.fbx", 0.08),
]
report = {}
os.makedirs(os.path.join(ROOT, "Assets", "Vegetation"), exist_ok=True)

for asset_id, output_name, ratio in ASSETS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    source = os.path.join(ROOT, "assets", "models", asset_id, asset_id + ".gltf")
    bpy.ops.import_scene.gltf(filepath=source)
    before = 0
    after = 0
    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH":
            continue
        before += len(obj.data.polygons)
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        modifier = obj.modifiers.new(name="Real time LOD", type="DECIMATE")
        modifier.ratio = ratio
        modifier.use_collapse_triangulate = True
        try:
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        except Exception as exc:
            print("DECIMATE_WARNING", asset_id, obj.name, exc)
        after += len(obj.data.polygons)
        obj.select_set(False)
    output = os.path.join(ROOT, "Assets", "Vegetation", output_name)
    bpy.ops.export_scene.fbx(
        filepath=output,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        use_mesh_modifiers=True,
        path_mode="COPY",
        embed_textures=True,
        add_leaf_bones=False,
    )
    report[asset_id] = {
        "source": source,
        "output": output,
        "source_polygons": before,
        "optimized_polygons": after,
        "decimate_ratio": ratio,
        "bytes": os.path.getsize(output),
    }
    print("ENVIRONMENT_PREPARED", asset_id, before, after, output)

with open(os.path.join(ROOT, "docs", "vegetation_inspection.json"), "w", encoding="utf-8") as f:
    json.dump(report, f, ensure_ascii=False, indent=2)
