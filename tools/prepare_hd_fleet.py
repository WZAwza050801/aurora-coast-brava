#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""把 3DAssets.dev「Car Park and Road Vehicle Fleet」的高模整车转成 Unity 契约。

这一批比上一批（Road Car Showroom Lineup）高一个数量级：3.0–4.9 万三角面，
而不是 0.75 万。材质命名也不同（paintB / paintC / paintE / interior /
lightWhite / lightAmber / lightRed / alloy / trim / glass），而且每台车都带
可开关的 bonnet / door 节点。

统一契约（与 AuroraProjectBuilder.CreateCar 对齐）：
  * 车长 4.53 m、车头 +Z、地面 y = 0
  * 四个独立车轮 pivot：wheel_fl / wheel_fr / wheel_rl / wheel_rr
  * 网格按材质分类改名：body* / glass* / rim* / tire* / trim*

用法：
  blender --background --python tools/prepare_hd_fleet.py -- --probe-only
  blender --background --python tools/prepare_hd_fleet.py
"""

import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "assets", "vehicles", "fleet_hd")
DST = os.path.join(ROOT, "Assets", "Vehicles", "FleetHD")
REPORT = os.path.join(ROOT, "docs", "hd_fleet_inspection.json")

TARGET_LEN = 4.53

WHEEL_MAP = {
    "wheel-front-left": "wheel_fl",
    "wheel-front-right": "wheel_fr",
    "wheel-rear-left": "wheel_rl",
    "wheel-rear-right": "wheel_rr",
}

# asset id -> (short name, display label, era)
CARS = {
    "32495": ("SportsCarHD", "HD MID-ENGINE SPORTS CAR", "modern / wedge GT"),
    "32515": ("MuscleCoupeHD", "HD CLASSIC MUSCLE COUPE", "1970s / V8"),
    "32488": ("TouringCoupeHD", "HD GRAND TOURING COUPE", "modern / GT"),
    "32514": ("SportsSaloonHD", "HD SPORTS SALOON", "modern / four-door"),
    "32499": ("ClassicSaloonHD", "HD CLASSIC SALOON", "1960s / classic"),
    "32494": ("HotHatchHD", "HD HOT HATCH", "1980s / hot hatch"),
    "32493": ("ThreeDoorHD", "HD THREE-DOOR HATCHBACK", "modern / compact"),
    "32492": ("ExecutiveHD", "HD EXECUTIVE SALOON", "modern / luxury"),
    "32487": ("CityCarHD", "HD CITY CAR", "modern / city"),
    "32496": ("ElectricHatchHD", "HD ELECTRIC HATCHBACK", "2020s / EV"),
}


def make_material(name, color, metallic, roughness):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
    mat.diffuse_color = (color[0], color[1], color[2], 1.0)
    return mat


def world_bounds(objects):
    if not objects:
        return None
    lows = [1e9] * 3
    highs = [-1e9] * 3
    for o in objects:
        for corner in o.bound_box:
            w = o.matrix_world @ __import__("mathutils").Vector(corner)
            for i in range(3):
                lows[i] = min(lows[i], w[i])
                highs[i] = max(highs[i], w[i])
    return {"min": [round(v, 4) for v in lows], "max": [round(v, 4) for v in highs]}


def classify(mat_names):
    """This pack uses paintB / paintC / paintE / interior / lightWhite /
    lightAmber / lightRed / alloy / trim / glass. The first slot wins."""
    src = (mat_names[0] if mat_names else "").lower()
    if src.startswith("paint"):
        return "body"
    if "glass" in src:
        return "glass"
    if "alloy" in src:
        return "rim"
    if "lightwhite" in src:
        return "lightwhite"
    if "lightamber" in src:
        return "lightamber"
    if "lightred" in src:
        return "lightred"
    if "lightgreen" in src:
        return "lightgreen"
    if "interior" in src:
        return "interior"
    if "trim" in src:
        return "trim"
    return "body"


def probe(glb_path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb_path)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    empties = [o for o in bpy.context.scene.objects if o.type == "EMPTY"]
    pivots = [o.name for o in empties
              if "wheel-" in o.name.lower() and "/" not in o.name]
    info = {
        "bounds": world_bounds(meshes),
        "meshes": len(meshes),
        "polygons": sum(len(o.data.polygons) for o in meshes),
        "materials": sorted({m.name for o in meshes for m in o.data.materials if m}),
        "wheel_pivots": sorted(pivots),
        "hinged": sorted({o.name for o in empties if "/" not in o.name
                          and any(k in o.name.lower() for k in ("door", "bonnet", "boot"))}),
    }
    return info


def panel_tilt(emp, child):
    """Return the tilt of a hinged panel's long axis, in degrees.

    The hinge empty sits on the hinge line; the panel mesh extends away from
    it. A closed panel runs level (tilt 0). The bonnet in this pack is
    modelled flipped, so its tilt is near 180 degrees.

    The far end is whichever end of the panel is furthest from the hinge
    along the panel's own axis, so the sign works out the same way for a
    forward-opening bonnet and a rear-hinged boot.
    """
    hinge = emp.matrix_world.translation
    pts = [child.matrix_world @ Vector(c) for c in child.bound_box]
    axis = (emp.matrix_world.to_3x3() @ Vector((0.0, 1.0, 0.0))).normalized()
    projected = [((p - hinge).dot(axis), p) for p in pts]
    values = [v for v, _ in projected]
    extreme = min(values) if values[0] < 0 else max(values)
    band = [p for v, p in projected if abs(v - extreme) < 0.03]
    far_z = sum(p.z for p in band) / len(band)
    far_p = band[0]
    dy = far_p.y - hinge.y
    dz = far_z - hinge.z
    if abs(dy) < 1e-6:
        return 0.0
    return math.degrees(math.atan2(dz, dy))


def fix_flipped_panels(meshes):
    """Counteract the FBX axis conversion on the panels that extend along -Y.

    The bonnet's mesh sits at local (0, -0.84, -0.08) -- it extends along
    -Y from its hinge, while the boot extends along +Y. Blender's FBX
    exporter converts the scene for axis_forward="-Z" / axis_up="Y", and the
    negative branch ends up mirrored in Unity: the bonnet's local rotation
    reads as identity but its world rotation as (321.7, 116, 180) instead of
    the body's (270.3, 296, 0), which stands the hood up like an open lid.

    Baking a 180-degree turn about the panel's own Z axis before export
    cancels the mirror, so the panel lands flat like the boot does.
    """
    fixed = []
    for o in meshes:
        parent = o.parent
        if parent is None:
            continue
        pname = parent.name.lower()
        if pname not in ("bonnet", "boot") and not pname.startswith("door-"):
            continue
        # Only the panel that reaches into negative Y needs the correction.
        if o.location.y >= -1e-6:
            continue
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        try:
            bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
            o.rotation_euler = (0.0, 0.0, math.pi)
            o.select_set(False)
            bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
            fixed.append(o.name)
        except RuntimeError:
            o.select_set(False)
    bpy.context.view_layer.update()
    return fixed


def flatten_panels(meshes):
    """Move the hinged panels up to the car root, with world-space offsets.

    The FBX writer mishandles a child that sits at a NEGATIVE offset along
    an axis its parent is rotated on: the bonnet's local rotation comes out
    as 308 deg instead of the body's 270 deg, which stands the hood up like
    an open lid. The boot, which extends along +Y, exports correctly.

    Re-parenting the panel meshes to the car root with world-space offsets
    removes the intermediate hinge node from the export, so the axis
    conversion has nothing to mirror. The wheels keep their pivots, since
    the shared WheelColliders drive those.
    """
    moved = []
    for o in list(bpy.context.scene.objects):
        if o.type != "EMPTY":
            continue
        pname = o.name.lower()
        if pname not in ("bonnet", "boot") and not pname.startswith("door-"):
            continue
        root = None
        anc = o.parent
        while anc is not None:
            if anc.type == "EMPTY":
                root = anc
                break
            anc = anc.parent
        if root is None:
            continue
        for child in list(o.children):
            if child.type != "MESH":
                continue
            world = child.matrix_world.copy()
            child.parent = root
            child.matrix_world = world
            moved.append(child.name)
    # The hinge empties are now empty and their transforms are meaningless
    # (the bonnet hinge exports with a stray 308 deg rotation), so drop them.
    for o in list(bpy.context.scene.objects):
        if o.type != "EMPTY":
            continue
        pname = o.name.lower()
        if pname not in ("bonnet", "boot") and not pname.startswith("door-"):
            continue
        if any(c.type == "MESH" for c in o.children):
            continue
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.context.view_layer.update()
    return moved


def group_world_verts(objects):
    """World-space vertex positions of a set of meshes."""
    pts = []
    for o in objects:
        for v in o.data.vertices:
            pts.append(o.matrix_world @ v.co)
    return pts


def bbox_of(points, pad=0.0):
    xs = [p.x for p in points]
    ys = [p.y for p in points]
    zs = [p.z for p in points]
    return {
        "x": (min(xs) - pad, max(xs) + pad),
        "y": (min(ys) - pad, max(ys) + pad),
        "z": (min(zs) - pad, max(zs) + pad),
    }


def envelope_excess(point, env):
    """How far a point sticks out of an axis-aligned envelope."""
    over = 0.0
    for axis, (lo, hi) in env.items():
        v = getattr(point, axis)
        if v < lo:
            over += lo - v
        elif v > hi:
            over += v - hi
    return over


def check_tailgates():
    """Verify that no hinged panel hangs outside the body shell.

    This used to try to SWING an open tailgate shut by searching for a
    rotation around the hinge. That was solving the wrong problem: the
    tailgates in this pack are modelled closed, and the panel had been
    pushed behind the car by a transform bug, not by the art. A search-based
    "fix" then swung it into the cabin. So the check stays read-only and
    fails the build instead of silently moving geometry.
    """
    problems = {}
    for emp in list(bpy.context.scene.objects):
        if emp.type != "EMPTY" or emp.name.lower() != "boot":
            continue
        group = [c for c in emp.children if c.type == "MESH"]
        if not group:
            continue
        body = [o for o in bpy.context.scene.objects
                if o.type == "MESH" and o.name.lower().startswith("body_") and o not in group]
        if not body:
            continue
        body_pts = group_world_verts(body)
        env = bbox_of(body_pts, pad=0.10)
        group_pts = group_world_verts(group)
        over = envelope_excess_max(group_pts, env)
        if over > 0.35:
            problems[emp.name] = round(over, 3)
    return problems


def envelope_excess_max(points, env):
    return max(envelope_excess(p, env) for p in points)


def clip_interior_to_roof():
    """Cut the seat backs off where they poke through the roof.

    The interior meshes carry the headrests up to 12 cm above the shell, so
    three black blocks appear sitting on the roof of every coupe in the
    garage. Anything above the body's own roof line is trimmed away; it is
    inside the roof anyway, so the cut is never visible.
    """
    import bmesh
    trimmed = {}
    body_meshes = [o for o in bpy.context.scene.objects
                   if o.type == "MESH" and o.name.lower().startswith("body_")]
    if not body_meshes:
        return trimmed
    roof = max(p.z for p in group_world_verts(body_meshes))
    limit = roof - 0.02
    for o in list(bpy.context.scene.objects):
        if o.type != "MESH" or not o.name.lower().startswith("interior_"):
            continue
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bm.verts.ensure_lookup_table()
        kill = [v for v in bm.verts if (o.matrix_world @ v.co).z > limit]
        if kill:
            bmesh.ops.delete(bm, geom=kill, context="VERTS")
            bm.to_mesh(o.data)
            trimmed[o.name] = len(kill)
        bm.free()
    bpy.context.view_layer.update()
    return trimmed


def bake_basis(obj):
    """Fold an object's local rotation and scale into its vertices.

    bpy.ops.object.transform_apply is not usable here: on a parented child
    it folds the parent's transform into the result, and it acts on the whole
    selection, so the outcome depends on what happened to be selected. Both
    effects were visible in this pipeline -- the hinge children ended up
    about 0.9 m behind the car, and the wheel meshes collapsed onto the
    origin. Doing the maths on the vertices is exact and order-independent.
    """
    import bmesh
    m = obj.matrix_basis.copy()
    m.translation = Vector((0.0, 0.0, 0.0))
    lin = m.to_3x3()
    ident = all(abs(lin[r][c] - (1.0 if r == c else 0.0)) < 1e-9
                for r in range(3) for c in range(3))
    if ident:
        return False
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.transform(bm, matrix=lin, verts=bm.verts)
    bm.to_mesh(obj.data)
    bm.free()
    obj.rotation_mode = "XYZ"
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)
    return True


def close_hinged_panels():
    """Report the hinged panels without touching them.

    Verified on 2026-11-16: the bonnet mesh in this pack is already flat
    (world bounds z = 0.61..0.81 against a hinge at z = 0.79, a 2.8 deg
    slope). An earlier version of this script measured a "-177 deg tilt" by
    taking the panel's far end along -Y, mis-read the atan2 sign, and rotated
    the bonnet 177 deg -- which is what made the hood stand up in the game.
    The panels are correct as modelled; the only fix needed is the naming
    and material dispatch.
    """
    results = {}
    for emp in list(bpy.context.scene.objects):
        if emp.type != "EMPTY":
            continue
        name = emp.name.lower()
        if name not in ("bonnet", "boot") and not name.startswith("door-"):
            continue
        for child in list(emp.children):
            if child.type != "MESH":
                continue
            results["%s/%s" % (emp.name, child.name)] = round(panel_tilt(emp, child), 2)
    return results


def convert(glb_path, out_name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb_path)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bounds = world_bounds(meshes)
    if not bounds:
        raise RuntimeError("no meshes in " + glb_path)

    # glTF is +Y up with the nose along +Z; Blender imports it Z-up, so the
    # glTF long axis becomes Blender Y. Normalise that extent to 4.53 m.
    span_x = bounds["max"][0] - bounds["min"][0]
    span_y = bounds["max"][1] - bounds["min"][1]
    long_span = span_y if span_y > span_x else span_x
    scale = TARGET_LEN / long_span

    min_z = bounds["min"][2]
    cx = (bounds["min"][0] + bounds["max"][0]) * 0.5
    cy = (bounds["min"][1] + bounds["max"][1]) * 0.5
    # Normalise into metres and bake the factor into the mesh vertices.
    #
    # Why bake instead of scaling the objects: Blender's FBX writer stores
    # each node's local transform, and a scale that sits on some nodes but
    # not others produces an inconsistent hierarchy in Unity. The symptom was
    # the root ending up at scale 100 while the bonnet hinge stayed at 1,
    # which also turned the bonnet's local rotation from the body's 270 deg
    # into 308 deg -- and stood the hood up like an open lid.
    #
    # Plan: scale every mesh's geometry, reset all object scales to 1, and
    # put the scaled positions on the empties.
    for o in meshes:
        o.scale = (o.scale.x * scale, o.scale.y * scale, o.scale.z * scale)
    bpy.context.view_layer.update()
    baked_basis = 0
    for o in meshes:
        if bake_basis(o):
            baked_basis += 1
        # The offset is expressed in the parent's (still unscaled) space, so
        # it needs the same factor applied.
        o.location = Vector((o.location.x * scale,
                             o.location.y * scale,
                             o.location.z * scale))
    bpy.context.view_layer.update()

    # With the geometry scaled, the empties must sit at the scaled positions
    # and carry no scale of their own. A child's `location` is relative to
    # its parent, so it only needs the factor applied; only the root needs
    # the recentring. Only the empties are touched here -- the meshes were
    # already scaled above, and running them through this loop a second time
    # pushed the hinge panels off the back of the car.
    for o in bpy.context.scene.objects:
        if o.type != "EMPTY":
            continue
        if o.parent is None:
            o.location = Vector(((o.location.x - cx) * scale,
                                 (o.location.y - cy) * scale,
                                 (o.location.z - min_z) * scale))
        else:
            o.location = Vector((o.location.x * scale,
                                 o.location.y * scale,
                                 o.location.z * scale))
        o.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()

    material_slots = {
        "body": make_material("HD Fleet paint", (0.72, 0.16, 0.05), 0.78, 0.24),
        "glass": make_material("HD Fleet glass", (0.03, 0.06, 0.09), 0.25, 0.08),
        "rim": make_material("HD Fleet alloy", (0.68, 0.71, 0.76), 0.92, 0.22),
        "tire": make_material("HD Fleet rubber", (0.014, 0.015, 0.019), 0.0, 0.72),
        "trim": make_material("HD Fleet dark trim", (0.019, 0.021, 0.027), 0.40, 0.38),
        "interior": make_material("HD Fleet interior", (0.045, 0.043, 0.042), 0.05, 0.62),
        "lightwhite": make_material("HD Fleet lamp white", (0.72, 0.78, 0.86), 0.05, 0.12),
        "lightamber": make_material("HD Fleet lamp amber", (0.78, 0.34, 0.04), 0.05, 0.14),
        "lightred": make_material("HD Fleet lamp red", (0.42, 0.012, 0.014), 0.12, 0.16),
        "lightgreen": make_material("HD Fleet lamp green", (0.08, 0.5, 0.16), 0.05, 0.14),
    }

    for o in meshes:
        cls = classify([m.name for m in o.data.materials])
        parent_name = o.parent.name if o.parent else ""
        suffix = o.name.replace("/", "_").replace(" ", "_")
        o.name = cls + "_" + suffix
        o.data.materials.clear()
        o.data.materials.append(material_slots[cls])

    # Verify that no hinged panel sticks out past the body shell.
    tailgates = check_tailgates()
    if tailgates:
        raise RuntimeError("panel protrudes outside the body shell: %s in %s"
                           % (tailgates, os.path.basename(glb_path)))

    # The seat backs poke through the roof on several bodies; trim them.
    trimmed = clip_interior_to_roof()

    # This pack models the bonnet in a raised pose: the panel is modelled
    # around the hinge but flipped, so it sticks up like an open hood. The
    # boot and the doors are already level. Measure the deviation per panel
    # instead of hard-coding an angle, because every model differs slightly.
    closed = close_hinged_panels()


    # Move the hinged panels up to the car root so the FBX writer does not
    # mirror a child that sits at a negative offset.
    flattened = flatten_panels(meshes)

    # The wheel meshes in this pack live under "wheel-front-left" style
    # pivots with their own children; rename the pivots to the contract.
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

    # Wheels in this pack are children of the pivot empties; the visual wheels
    # must end up as direct children of the body root so CreateCar can find
    # them by name and drive them from the shared WheelColliders.
    for o in list(bpy.context.scene.objects):
        if o.type == "MESH" and o.parent and o.parent.name.lower() in WHEEL_MAP.values():
            # Clearing the parent drops the object onto its own local
            # transform, which for these wheel meshes is the identity: the
            # placement lives in the pivot. The world matrix has to be
            # written back explicitly or the wheel collapses onto the origin.
            world = o.matrix_world.copy()
            o.parent = None
            o.matrix_parent_inverse = Matrix.Identity(4)
            o.matrix_world = world

    os.makedirs(DST, exist_ok=True)
    fbx_path = os.path.join(DST, out_name + ".fbx")
    # apply_unit_scale must stay off. With it on, Blender writes the scene's
    # unit scale into the FBX root (x100 for these GLB files) but not into
    # the child nodes, so Unity ends up with a root at scale 100 and the
    # bonnet hinge at scale 1. The mismatched hierarchy also turns the
    # bonnet's local rotation into 308 deg instead of the body's 270 deg,
    # which stands the hood up like an open lid. The metre normalisation is
    # already applied to the geometry above, so no unit scale is needed.
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=False,
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
        "closed_panels": closed,
        "baked_basis": baked_basis,
        "flattened_panels": len(flattened),
        "closed_tailgates": tailgates,
        "trimmed_interior": trimmed,
    }


def main():
    probe_only = "--probe-only" in sys.argv
    report = {
        "source": "https://3dassets.dev/packs/car-park-and-road-vehicle-fleet (CC0 1.0)",
        "generated": "tools/prepare_hd_fleet.py",
        "cars": {},
    }
    for cid, (name, label, era) in CARS.items():
        path = os.path.join(SRC, cid + ".glb")
        if not os.path.exists(path):
            print("MISSING", path, flush=True)
            continue
        print("PROCESS", name, cid, flush=True)
        if probe_only:
            info = probe(path)
        else:
            info = convert(path, name)
        info["label"] = label
        info["era"] = era
        info["source_id"] = cid
        report["cars"][name] = info
        print("  ", name, "tris", info.get("polygons"), "bounds", info.get("bounds"), flush=True)

    with open(REPORT, "w", encoding="utf-8") as fh:
        json.dump(report, fh, ensure_ascii=False, indent=1)
    print("WROTE", REPORT)


if __name__ == "__main__":
    main()
