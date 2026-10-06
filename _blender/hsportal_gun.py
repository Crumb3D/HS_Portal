# 7 Days to Die Improvised Portal Gun
# Matched precisely to reference art: 4 front articulated claws, horizontal dual plasma coils,
# rear battery crate with side-mounted analog gauge, and heavy industrial grip assembly.
#
# Usage:
# blender --background --factory-startup --python hsportal_gun_v2.py -- <fbx_out_dir> [<blend_out>]

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
    "HSGun_Metal": (0.20, 0.19, 0.18, 1.0),
    "HSGun_MetalDark": (0.06, 0.06, 0.06, 1.0),
    "HSGun_MetalLight": (0.38, 0.36, 0.33, 1.0),
    "HSGun_Grip": (0.04, 0.04, 0.038, 1.0),
    "HSGun_Copper": (0.58, 0.28, 0.08, 1.0),
    "HSGun_Blue": (0.10, 0.50, 1.0, 1.0),
    "HSGun_Orange": (1.0, 0.38, 0.02, 1.0),
    "HSGun_Gauge": (0.05, 0.05, 0.05, 1.0),
    "HSGun_GaugeFace": (0.85, 0.75, 0.55, 1.0),
    "HSGun_Glass": (0.7, 0.85, 0.95, 0.3),
    "HSGun_Glow": (0.40, 0.80, 1.0, 1.0),
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


def ensure_ad(ob):
    if ob.animation_data is None:
        ob.animation_data_create()
    return ob.animation_data


def assign_action(ob, act):
    ad = ensure_ad(ob)
    ad.action = act
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

    # -------------------------------------------------------------------------
    # 1. MAIN REAR POWER BOX (SCAVENGED BATTERY/POWER MODULE)
    # -------------------------------------------------------------------------
    rear = new_mesh("RearPowerBox", recoil, "HSGun_MetalDark")
    # Main crate volume
    add_box(rear, (0.0, -0.12, 0.04), (0.13, 0.18, 0.12))
    # Top lid plate & reinforcement corner brackets
    add_box(rear, (0.0, -0.12, 0.103), (0.136, 0.186, 0.012))
    add_box(rear, (0.062, -0.12, 0.04), (0.01, 0.182, 0.122))
    add_box(rear, (-0.062, -0.12, 0.04), (0.01, 0.182, 0.122))

    # Side-Mounted Analog Dial (Left side, exactly like reference)
    gauge_body = new_mesh("AnalogGauge", recoil, "HSGun_Gauge")
    add_cyl(gauge_body, (-0.068, -0.10, 0.04), 0.032, 0.016, 24, "X")
    gauge_face = new_mesh("GaugeFace", gauge_body, "HSGun_GaugeFace")
    add_cyl(gauge_face, (-0.076, -0.10, 0.04), 0.028, 0.002, 24, "X")
    needle = new_mesh("GaugeNeedle", gauge_body, "HSGun_Orange")
    add_box(needle, (-0.078, -0.10, 0.048), (0.002, 0.004, 0.022), Euler((0, math.radians(90), math.radians(35))))

    # -------------------------------------------------------------------------
    # 2. MID CHASSIS & DUAL HORIZONTAL PLASMA TUBE CHAMBER
    # -------------------------------------------------------------------------
    mid_body = new_mesh("MidBody", recoil, "HSGun_Metal")
    # Central support structure extending forward
    add_box(mid_body, (0.0, 0.05, 0.04), (0.09, 0.16, 0.08))

    # Top Coil Assembly (Blue Mode) - Dual horizontal tubes
    top_glass = new_mesh("CoilBlue_Glass", recoil, "HSGun_Glass")
    add_cyl(top_glass, (-0.025, 0.05, 0.085), 0.018, 0.13, 16, "Y")
    add_cyl(top_glass, (0.025, 0.05, 0.085), 0.018, 0.13, 16, "Y")

    top_core = new_mesh("CoilBlue_Core", recoil, "HSGun_Blue")
    add_cyl(top_core, (-0.025, 0.05, 0.085), 0.012, 0.12, 16, "Y")
    add_cyl(top_core, (0.025, 0.05, 0.085), 0.012, 0.12, 16, "Y")
    # Filament rings along the tube length
    for y_pos in [-0.01, 0.01, 0.03, 0.05, 0.07, 0.09, 0.11]:
        add_cyl(top_core, (-0.025, y_pos, 0.085), 0.015, 0.005, 12, "Y")
        add_cyl(top_core, (0.025, y_pos, 0.085), 0.015, 0.005, 12, "Y")

    # Bottom Coil Assembly (Orange Mode) - Single larger horizontal tube bottom-centered
    bot_glass = new_mesh("CoilOrange_Glass", recoil, "HSGun_Glass")
    add_cyl(bot_glass, (0.0, 0.05, -0.005), 0.022, 0.13, 16, "Y")

    bot_core = new_mesh("CoilOrange_Core", recoil, "HSGun_Orange")
    add_cyl(bot_core, (0.0, 0.05, -0.005), 0.015, 0.12, 16, "Y")
    for y_pos in [-0.01, 0.01, 0.03, 0.05, 0.07, 0.09, 0.11]:
        add_cyl(bot_core, (0.0, y_pos, -0.005), 0.018, 0.005, 12, "Y")

    # Heavy power conduits and copper wiring wrapping the chassis
    conduits = new_mesh("HeavyConduits", recoil, "HSGun_Copper")
    add_cyl(conduits, (0.048, 0.02, 0.04), 0.007, 0.16, 12, "Y")
    add_cyl(conduits, (-0.048, 0.02, 0.04), 0.007, 0.16, 12, "Y")

    # -------------------------------------------------------------------------
    # 3. GRIP & CONTROL INTERFACE
    # -------------------------------------------------------------------------
    grip = new_mesh("Grip", recoil, "HSGun_Grip")
    # Ergonomic pistol grip angled back
    add_box(grip, (0.0, -0.08, -0.07), (0.036, 0.052, 0.13), Euler((math.radians(12), 0, 0)))
    # Grip texture ridges
    for z_off in [-0.03, -0.05, -0.07, -0.09, -0.11]:
        add_box(grip, (0.0, -0.062, z_off), (0.038, 0.012, 0.01), Euler((math.radians(12), 0, 0)))

    # Closed trigger guard loop
    guard = new_mesh("TriggerGuard", recoil, "HSGun_MetalLight")
    add_box(guard, (0.0, -0.04, -0.025), (0.02, 0.05, 0.008))
    add_box(guard, (0.0, -0.02, -0.075), (0.02, 0.008, 0.10))
    add_box(guard, (0.0, -0.05, -0.125), (0.02, 0.06, 0.008))

    trigger = new_mesh("Trigger", recoil, "HSGun_MetalLight")
    add_box(trigger, (0.0, -0.045, -0.05), (0.01, 0.012, 0.032), Euler((math.radians(15), 0, 0)))

    # Control dials on grip frame
    dials = new_mesh("GripDials", recoil, "HSGun_Gauge")
    add_cyl(dials, (-0.022, -0.07, -0.01), 0.011, 0.008, 14, "X")
    add_cyl(dials, (-0.022, -0.09, -0.01), 0.009, 0.008, 14, "X")

    # -------------------------------------------------------------------------
    # 4. FRONT EMITTER HUB & 4-CLAW ARTICULATED ASSEMBLY
    # -------------------------------------------------------------------------
    hub = new_mesh("EmitterHub", recoil, "HSGun_MetalDark")
    add_cyl(hub, (0.0, 0.14, 0.04), 0.045, 0.03, 20, "Y")
    add_cyl(hub, (0.0, 0.155, 0.04), 0.035, 0.02, 20, "Y")

    # Plasma core inner lens
    muzzle = new_mesh("MuzzleGlow", hub, "HSGun_Glow")
    add_cyl(muzzle, (0.0, 0.165, 0.04), 0.025, 0.01, 16, "Y")
    muzzle.scale = (0.2, 0.2, 0.2)

    claws = []
    rest_rot = []
    # 4 Radial Claws: Top (0°), Right (90°), Bottom (180°), Left (270°)
    claw_angles = [0.0, 90.0, 180.0, 270.0]

    for i, angle in enumerate(claw_angles):
        rad = math.radians(angle)
        claw = new_mesh(f"Claw_{i+1}", hub, "HSGun_Metal")

        # Base articulated mount block
        add_box(claw, (0.0, 0.01, 0.045), (0.018, 0.04, 0.022))
        # Mid prong arm (angled outwards)
        add_box(claw, (0.0, 0.05, 0.07), (0.016, 0.06, 0.020), Euler((math.radians(-28), 0, 0)))
        # Front curved claw tip (angled back inwards)
        add_box(claw, (0.0, 0.095, 0.082), (0.014, 0.05, 0.016), Euler((math.radians(-65), 0, 0)))

        # Hydraulic piston actuator rod connecting claw to hub base
        piston = new_mesh(f"Piston_{i+1}", claw, "HSGun_MetalLight")
        add_cyl(piston, (0.0, 0.02, 0.02), 0.005, 0.05, 10, "Y", Euler((math.radians(-15), 0, 0)))

        # Orient around central Y axis at 0, 90, 180, 270 degrees
        claw.location = (0.0, 0.15, 0.04)
        claw.rotation_euler = Euler((0.0, rad, 0.0))
        claws.append(claw)
        rest_rot.append((claw.rotation_euler.x, claw.rotation_euler.y, claw.rotation_euler.z))

    # -------------------------------------------------------------------------
    # 5. ANIMATIONS & TAKES (IDLE, FIRE, REJECT)
    # -------------------------------------------------------------------------
    scene = bpy.context.scene
    scene.render.fps = 30
    scene.frame_start = 1
    scene.frame_end = 30

    anim_objs = [recoil, trigger, muzzle, top_core, bot_core] + claws

    def rest_pose(frame):
        key(recoil, frame, loc=(0, 0, 0), rot=(0, 0, 0))
        key(trigger, frame, rot=(0, 0, 0))
        key(muzzle, frame, scl=(0.2, 0.2, 0.2))
        key(top_core, frame, scl=(1, 1, 1))
        key(bot_core, frame, scl=(1, 1, 1))
        for c, r in zip(claws, rest_rot):
            key(c, frame, rot=r)

    def start_take(take):
        for ob in anim_objs:
            act = bpy.data.actions.new(ob.name + "|" + take)
            act.use_fake_user = True
            assign_action(ob, act)

    # Take 1: Idle
    start_take("Idle")
    rest_pose(1)
    key(top_core, 15, scl=(1.06, 1.0, 1.06))
    key(bot_core, 15, scl=(1.06, 1.0, 1.06))
    rest_pose(30)

    # Take 2: Fire
    start_take("Fire")
    rest_pose(1)
    # Trigger pull
    key(trigger, 3, rot=(math.radians(16), 0, 0))
    key(trigger, 10, rot=(0, 0, 0))
    # Claw expansion (opening hydraulic mechanism)
    for c, r in zip(claws, rest_rot):
        key(c, 4, rot=(r[0] + math.radians(22), r[1], r[2]))
        key(c, 10, rot=(r[0] + math.radians(6), r[1], r[2]))
        key(c, 16, rot=r)
    # Body Recoil
    key(recoil, 3, loc=(0.0, -0.015, 0.005), rot=(math.radians(-5), 0, math.radians(1)))
    key(recoil, 14, loc=(0, 0, 0), rot=(0, 0, 0))
    # Energy flash
    key(muzzle, 2, scl=(0.4, 0.4, 0.4))
    key(muzzle, 5, scl=(2.2, 1.8, 2.2))
    key(muzzle, 12, scl=(0.2, 0.2, 0.2))

    # Take 3: Reject / Misfire
    start_take("Reject")
    rest_pose(1)
    key(trigger, 2, rot=(math.radians(8), 0, 0))
    key(trigger, 8, rot=(0, 0, 0))
    key(recoil, 3, loc=(0.0, -0.005, 0.0), rot=(math.radians(-2), 0, math.radians(-3)))
    key(recoil, 8, loc=(0, 0, 0), rot=(0, 0, 0))

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
                slot = getattr(ad, "action_slot", None)
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