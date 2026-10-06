# HS Portal Gun — modelled from portal Gun Design.png (industrial 7DTD turnaround).
# blender --background --factory-startup --python hsportal_gun.py -- <fbx_out_dir> [<blend_out>]
#
# Blender: +Y barrel, +Z up, origin in the grip (HoldType 1 palm).
# FBX bakes to Unity (+Z forward, +Y up). 1 unit = 1 m.

import bpy
import bmesh
import math
import os
import sys
from mathutils import Vector, Euler, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.abspath(argv[0]) if argv else os.path.abspath(os.path.join(HERE, "..", "_unity", "Assets", "HSPortal", "Models"))
BLEND_OUT = os.path.abspath(argv[1]) if len(argv) > 1 else os.path.join(HERE, "hsportal_gun.blend")

MATS = {
    "HSGun_Metal": (0.22, 0.21, 0.19, 1.0),
    "HSGun_MetalDark": (0.07, 0.07, 0.07, 1.0),
    "HSGun_MetalLight": (0.40, 0.39, 0.36, 1.0),
    "HSGun_Grip": (0.05, 0.045, 0.04, 1.0),
    "HSGun_Copper": (0.55, 0.27, 0.10, 1.0),
    "HSGun_Blue": (0.15, 0.45, 1.0, 1.0),
    "HSGun_Orange": (1.0, 0.34, 0.05, 1.0),
    "HSGun_Gauge": (0.04, 0.04, 0.045, 1.0),
    "HSGun_Plate": (0.32, 0.28, 0.16, 1.0),
    "HSGun_Glow": (0.55, 0.82, 1.0, 1.0),
    "HSGun_Hazard": (0.85, 0.68, 0.08, 1.0),
}


def mat(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
    m.diffuse_color = MATS.get(name, (0.5, 0.5, 0.5, 1.0))
    m.use_nodes = False
    return m


def new_mesh(name, parent, mat_name):
    mesh = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(ob)
    if parent is not None:
        ob.parent = parent
    ob.data.materials.append(mat(mat_name))
    ob.rotation_mode = "XYZ"
    return ob


def new_empty(name, parent):
    ob = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(ob)
    ob.empty_display_type = "ARROWS"
    ob.empty_display_size = 0.04
    ob.rotation_mode = "XYZ"
    if parent is not None:
        ob.parent = parent
    return ob


def axis_matrix(axis):
    if axis == "Y":
        return Matrix.Rotation(math.radians(90.0), 4, "X")
    if axis == "X":
        return Matrix.Rotation(math.radians(-90.0), 4, "Y")
    return Matrix.Identity(4)


def add_box(ob, center, size, rot=None):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    res = bmesh.ops.create_cube(bm, size=1.0)
    eul = rot if rot is not None else Euler((0, 0, 0))
    m = Matrix.Translation(Vector(center)) @ eul.to_matrix().to_4x4() @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
    bmesh.ops.transform(bm, matrix=m, verts=res["verts"])
    bm.to_mesh(ob.data)
    bm.free()


def add_cyl(ob, center, radius, depth, segs=16, axis="Y", rot=None):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    res = bmesh.ops.create_cone(
        bm, cap_ends=True, cap_tris=False, segments=segs,
        radius1=radius, radius2=radius, depth=depth,
    )
    extra = rot.to_matrix().to_4x4() if rot is not None else Matrix.Identity(4)
    m = Matrix.Translation(Vector(center)) @ extra @ axis_matrix(axis)
    bmesh.ops.transform(bm, matrix=m, verts=res["verts"])
    bm.to_mesh(ob.data)
    bm.free()


def add_cone(ob, center, r1, r2, depth, segs=12, axis="Y"):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    res = bmesh.ops.create_cone(
        bm, cap_ends=True, cap_tris=False, segments=segs,
        radius1=r1, radius2=r2, depth=depth,
    )
    m = Matrix.Translation(Vector(center)) @ axis_matrix(axis)
    bmesh.ops.transform(bm, matrix=m, verts=res["verts"])
    bm.to_mesh(ob.data)
    bm.free()


def ensure_ad(ob):
    if ob.animation_data is None:
        ob.animation_data_create()
    return ob.animation_data


def assign_action(ob, act):
    ad = ensure_ad(ob)
    ad.action = act
    if hasattr(ad, "action_slot") and hasattr(act, "slots"):
        slot = None
        if len(act.slots) > 0:
            slot = act.slots[0]
        else:
            try:
                slot = act.slots.new(id_type="OBJECT", name=ob.name)
            except Exception:
                slot = None
        if slot is not None:
            try:
                ad.action_slot = slot
            except Exception:
                pass
    return ad


def key(ob, frame, loc=None, rot=None, scl=None):
    if loc is not None:
        ob.location = Vector(loc)
        ob.keyframe_insert("location", frame=frame)
    if rot is not None:
        ob.rotation_euler = Euler(rot)
        ob.keyframe_insert("rotation_euler", frame=frame)
    if scl is not None:
        ob.scale = Vector(scl)
        ob.keyframe_insert("scale", frame=frame)


def build_gun():
    root = new_empty("PortalGun", None)
    recoil = new_empty("Recoil", root)

    # Receiver / barrel housing (chunky industrial, not a Valve replica)
    body = new_mesh("Body", recoil, "HSGun_Metal")
    add_box(body, (0.0, 0.040, 0.034), (0.078, 0.130, 0.062))
    add_box(body, (0.0, 0.092, 0.034), (0.066, 0.060, 0.054))
    add_box(body, (0.042, 0.018, 0.036), (0.014, 0.078, 0.038), Euler((0, 0, math.radians(7))))
    add_box(body, (-0.042, 0.018, 0.036), (0.014, 0.078, 0.038), Euler((0, 0, math.radians(-7))))
    add_box(body, (0.0, 0.058, 0.066), (0.044, 0.056, 0.012))
    add_cyl(body, (0.0, 0.108, 0.034), 0.030, 0.040, 16, "Y")

    # DANGER 7010 pack on the rear — the big crate from the turnaround
    rear = new_mesh("RearBox", recoil, "HSGun_MetalDark")
    add_box(rear, (0.0, -0.078, 0.058), (0.096, 0.100, 0.092))
    add_box(rear, (0.0, -0.078, 0.108), (0.074, 0.074, 0.014))
    add_box(rear, (0.050, -0.078, 0.058), (0.010, 0.078, 0.070))
    add_box(rear, (-0.050, -0.078, 0.058), (0.010, 0.078, 0.070))
    add_cyl(rear, (0.0, -0.078, 0.116), 0.024, 0.042, 16, "X")

    plate = new_mesh("Plate", rear, "HSGun_Hazard")
    add_box(plate, (0.0, -0.130, 0.058), (0.078, 0.006, 0.056))
    add_box(plate, (0.051, -0.078, 0.070), (0.004, 0.060, 0.036))
    stripes = new_mesh("Hazard", rear, "HSGun_MetalDark")
    for z in (0.040, 0.072, 0.104):
        add_box(stripes, (0.052, -0.078, z), (0.005, 0.062, 0.010))

    grip = new_mesh("Grip", recoil, "HSGun_Grip")
    add_box(grip, (0.0, 0.002, -0.062), (0.032, 0.044, 0.126), Euler((math.radians(10), 0, 0)))
    add_box(grip, (0.0, 0.000, -0.126), (0.036, 0.050, 0.018))
    for z in (-0.028, -0.050, -0.072, -0.094):
        add_box(grip, (0.018, 0.006, z), (0.007, 0.032, 0.014))

    guard = new_mesh("Guard", recoil, "HSGun_MetalLight")
    add_box(guard, (0.0, 0.030, -0.016), (0.018, 0.040, 0.007))
    add_box(guard, (0.0, 0.048, -0.036), (0.016, 0.007, 0.042))

    trigger = new_mesh("Trigger", recoil, "HSGun_MetalLight")
    add_box(trigger, (0.0, 0.028, -0.034), (0.008, 0.010, 0.030), Euler((math.radians(14), 0, 0)))

    hub = new_mesh("Hub", recoil, "HSGun_MetalLight")
    add_cyl(hub, (0.0, 0.138, 0.034), 0.036, 0.024, 18, "Y")
    add_cyl(hub, (0.0, 0.154, 0.034), 0.028, 0.014, 14, "Y")
    add_box(hub, (0.0, 0.146, 0.034), (0.010, 0.032, 0.010))
    add_box(hub, (0.0, 0.146, 0.034), (0.010, 0.032, 0.010), Euler((0, math.radians(90), 0)))

    claws = []
    rest_rot = []
    specs = [
        ("ClawA", math.radians(90), math.radians(-16)),
        ("ClawB", math.radians(210), math.radians(16)),
        ("ClawC", math.radians(330), 0.0),
    ]
    for name, around, twist in specs:
        claw = new_mesh(name, hub, "HSGun_Metal")
        add_box(claw, (0.0, 0.032, 0.048), (0.018, 0.062, 0.024), Euler((math.radians(-30), 0, 0)))
        add_box(claw, (0.0, 0.072, 0.072), (0.016, 0.056, 0.020), Euler((math.radians(-58), 0, 0)))
        add_box(claw, (0.0, 0.102, 0.092), (0.014, 0.036, 0.016), Euler((math.radians(-76), 0, 0)))
        add_box(claw, (0.0, 0.018, 0.032), (0.022, 0.020, 0.018))
        claw.location = (0.0, 0.138, 0.034)
        claw.rotation_euler = Euler((0.0, around, twist))
        claws.append(claw)
        rest_rot.append((claw.rotation_euler.x, claw.rotation_euler.y, claw.rotation_euler.z))

    def make_coil(name, parent, loc, color, length=0.064):
        coil = new_mesh(name, parent, color)
        add_cyl(coil, (0, 0, 0), 0.010, length, 12, "Y")
        rings = 8
        for i in range(rings):
            y = -length * 0.42 + i * (length * 0.84 / max(1, rings - 1))
            add_cyl(coil, (0, y, 0), 0.014, 0.006, 12, "Y")
        add_cyl(coil, (0, -length * 0.5, 0), 0.008, 0.010, 10, "Y")
        add_cyl(coil, (0, length * 0.5, 0), 0.008, 0.010, 10, "Y")
        coil.location = loc
        return coil

    coil_bl = make_coil("CoilBlueL", hub, (-0.018, 0.122, 0.058), "HSGun_Blue")
    coil_br = make_coil("CoilBlueR", hub, (0.018, 0.122, 0.058), "HSGun_Blue")
    coil_ol = make_coil("CoilOrangeL", hub, (-0.018, 0.122, 0.010), "HSGun_Orange")
    coil_or = make_coil("CoilOrangeR", hub, (0.018, 0.122, 0.010), "HSGun_Orange")
    coils_blue = [coil_bl, coil_br]
    coils_orange = [coil_ol, coil_or]

    wires = new_mesh("Wires", recoil, "HSGun_Copper")
    add_cyl(wires, (0.030, 0.018, 0.066), 0.0045, 0.100, 8, "Y", Euler((math.radians(-16), 0, math.radians(10))))
    add_cyl(wires, (-0.030, 0.018, 0.066), 0.0045, 0.100, 8, "Y", Euler((math.radians(-16), 0, math.radians(-10))))
    add_cyl(wires, (0.024, -0.028, 0.086), 0.005, 0.078, 8, "Y")
    add_cyl(wires, (-0.024, -0.028, 0.086), 0.005, 0.078, 8, "Y")
    add_cyl(wires, (0.0, -0.018, 0.090), 0.004, 0.060, 8, "X")

    gauge = new_mesh("Gauge", recoil, "HSGun_Gauge")
    add_cyl(gauge, (-0.044, 0.022, 0.042), 0.017, 0.012, 16, "X")
    add_cyl(gauge, (-0.051, 0.022, 0.042), 0.013, 0.004, 16, "X")
    needle = new_mesh("Needle", gauge, "HSGun_Orange")
    add_box(needle, (-0.052, 0.022, 0.049), (0.002, 0.003, 0.015), Euler((0, math.radians(90), math.radians(32))))

    # Rear grip dials from the handle turnaround
    dials = new_mesh("Dials", recoil, "HSGun_Gauge")
    add_cyl(dials, (0.0, -0.018, -0.006), 0.010, 0.006, 12, "Y")
    add_cyl(dials, (0.0, -0.030, -0.006), 0.008, 0.006, 12, "Y")
    dial_face = new_mesh("DialFace", recoil, "HSGun_MetalLight")
    add_cyl(dial_face, (0.0, -0.015, -0.006), 0.007, 0.002, 12, "Y")
    add_cyl(dial_face, (0.0, -0.027, -0.006), 0.005, 0.002, 12, "Y")

    latch = new_mesh("Latch", recoil, "HSGun_MetalLight")
    add_box(latch, (0.0, 0.078, 0.070), (0.026, 0.030, 0.010))
    add_box(latch, (0.0, 0.088, 0.078), (0.016, 0.012, 0.012))

    bolts = new_mesh("Bolts", recoil, "HSGun_MetalLight")
    for loc in [
        (-0.030, -0.046, 0.086), (0.030, -0.046, 0.086),
        (-0.030, 0.056, 0.058), (0.030, 0.056, 0.058),
        (0.0, -0.110, 0.092), (0.034, -0.098, 0.022), (-0.034, -0.098, 0.022),
    ]:
        add_cyl(bolts, loc, 0.0045, 0.007, 8, "Z")

    muzzle = new_mesh("MuzzleGlow", hub, "HSGun_Glow")
    add_cyl(muzzle, (0.0, 0.168, 0.034), 0.020, 0.010, 14, "Y")
    add_cyl(muzzle, (0.0, 0.176, 0.034), 0.011, 0.018, 12, "Y")
    muzzle.scale = (0.15, 0.15, 0.15)

    scene = bpy.context.scene
    scene.render.fps = 30
    scene.frame_start = 1
    scene.frame_end = 30

    anim_objs = [recoil, trigger, muzzle] + claws + coils_blue + coils_orange

    def rest_pose(frame):
        key(recoil, frame, loc=(0, 0, 0), rot=(0, 0, 0))
        key(trigger, frame, rot=(0, 0, 0))
        key(muzzle, frame, scl=(0.15, 0.15, 0.15))
        for c, r in zip(claws, rest_rot):
            key(c, frame, rot=r)
        for c in coils_blue + coils_orange:
            key(c, frame, scl=(1, 1, 1))

    def start_take(take):
        for ob in anim_objs:
            act = bpy.data.actions.new(ob.name + "|" + take)
            act.use_fake_user = True
            assign_action(ob, act)

    start_take("Idle")
    rest_pose(1)
    for c in coils_blue + coils_orange:
        key(c, 16, scl=(1.05, 1.0, 1.05))
        key(c, 30, scl=(1, 1, 1))
    rest_pose(30)

    start_take("Fire")
    rest_pose(1)
    key(trigger, 3, rot=(math.radians(18), 0, 0))
    key(trigger, 10, rot=(0, 0, 0))
    for c, r in zip(claws, rest_rot):
        key(c, 4, rot=(r[0] + math.radians(24), r[1], r[2]))
        key(c, 8, rot=(r[0] + math.radians(8), r[1], r[2]))
        key(c, 14, rot=r)
    key(recoil, 3, loc=(0.0, -0.012, 0.004), rot=(math.radians(-6), 0, math.radians(2)))
    key(recoil, 6, loc=(0.0, -0.018, 0.002), rot=(math.radians(-4), 0, 0))
    key(recoil, 14, loc=(0, 0, 0), rot=(0, 0, 0))
    for c in coils_blue + coils_orange:
        key(c, 3, scl=(1.28, 1.08, 1.28))
        key(c, 7, scl=(0.92, 1.0, 0.92))
        key(c, 14, scl=(1, 1, 1))
    key(muzzle, 2, scl=(0.25, 0.25, 0.25))
    key(muzzle, 4, scl=(1.9, 1.5, 1.9))
    key(muzzle, 7, scl=(0.55, 0.8, 0.55))
    key(muzzle, 12, scl=(0.15, 0.15, 0.15))

    start_take("Reject")
    rest_pose(1)
    key(trigger, 2, rot=(math.radians(10), 0, 0))
    key(trigger, 8, rot=(0, 0, 0))
    key(recoil, 3, loc=(0.0, -0.006, 0.0), rot=(math.radians(-2), 0, math.radians(-4)))
    key(recoil, 8, loc=(0, 0, 0), rot=(0, 0, 0))
    for c, r in zip(claws, rest_rot):
        key(c, 3, rot=(r[0] + math.radians(7), r[1], r[2]))
        key(c, 8, rot=r)
    key(muzzle, 3, scl=(0.4, 0.4, 0.4))
    key(muzzle, 8, scl=(0.15, 0.15, 0.15))

    return root, anim_objs


def stash_nla(anim_objs):
    takes = ["Idle", "Fire", "Reject"]
    for take in takes:
        for ob in anim_objs:
            act = bpy.data.actions.get(ob.name + "|" + take)
            if act is None:
                continue
            ad = ensure_ad(ob)
            track = ad.nla_tracks.new()
            track.name = take
            start = int(act.frame_range[0])
            try:
                strip = track.strips.new(take, start, act)
            except TypeError:
                slot = None
                if hasattr(ad, "action_slot"):
                    slot = ad.action_slot
                strip = track.strips.new(take, start, act, action_slot=slot)
            strip.name = take
            ad.action = None


def export_fbx(root, path):
    bpy.ops.object.select_all(action="DESELECT")

    def sel(ob):
        ob.select_set(True)
        for c in ob.children:
            sel(c)

    sel(root)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
    )


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    root, anim_objs = build_gun()
    stash_nla(anim_objs)
    bpy.context.view_layer.update()
    path = os.path.join(OUT_DIR, "PortalGun.fbx")
    export_fbx(root, path)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)
    with open(os.path.join(OUT_DIR, "_export_log.txt"), "w") as f:
        f.write("PortalGun -> %s\n" % path)
        f.write("actions:\n")
        for a in bpy.data.actions:
            f.write("  %s frames %s\n" % (a.name, tuple(a.frame_range)))
        f.write("DONE\n")


if __name__ == "__main__" or True:
    try:
        main()
    except Exception:
        import traceback
        os.makedirs(OUT_DIR, exist_ok=True)
        with open(os.path.join(OUT_DIR, "_export_log.txt"), "w") as f:
            f.write("FAILED\n" + traceback.format_exc())
        raise
