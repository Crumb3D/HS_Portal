# HS Portal Gun — production hard-surface model.
# Hierarchy and animation takes come from hsportal_gun_prototype.py.
# Geometry is built as named mechanical parts to match portal Gun Design.png.
#
# blender --background --factory-startup --python hsportal_gun.py -- <fbx_out_dir> [<blend_out>]

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
    "HSGun_MetalLight": (0.42, 0.40, 0.36, 1.0),
    "HSGun_Grip": (0.045, 0.04, 0.038, 1.0),
    "HSGun_Copper": (0.62, 0.30, 0.09, 1.0),
    "HSGun_Blue": (0.12, 0.48, 1.0, 1.0),
    "HSGun_Orange": (1.0, 0.36, 0.04, 1.0),
    "HSGun_Gauge": (0.05, 0.05, 0.05, 1.0),
    "HSGun_GaugeFace": (0.86, 0.74, 0.48, 1.0),
    "HSGun_Glass": (0.55, 0.78, 0.95, 0.28),
    "HSGun_Glow": (0.55, 0.88, 1.0, 1.0),
    "HSGun_Plate": (0.38, 0.34, 0.20, 1.0),
    "HSGun_Hazard": (0.82, 0.64, 0.08, 1.0),
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
    ob.empty_display_size = 0.03
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


def xform(center, rot=None, size=None):
    eul = rot if rot is not None else Euler((0, 0, 0))
    m = Matrix.Translation(Vector(center)) @ eul.to_matrix().to_4x4()
    if size is not None:
        m = m @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
    return m


def with_mesh(ob):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    return bm


def commit(ob, bm, bevel=0.0, segs=2):
    if bevel > 0.0:
        try:
            bm.edges.ensure_lookup_table()
            geom = []
            for e in bm.edges:
                if len(e.link_faces) != 2:
                    continue
                if e.calc_face_angle() > math.radians(28.0):
                    geom.append(e)
            if geom:
                bmesh.ops.bevel(
                    bm, geom=geom, offset=bevel, offset_type="OFFSET",
                    segments=segs, profile=0.7, affect="EDGES",
                )
        except Exception:
            pass
    try:
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.00012)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    except Exception:
        pass
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()
    for p in ob.data.polygons:
        p.use_smooth = True


def add_box(ob, center, size, rot=None):
    bm = with_mesh(ob)
    res = bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.transform(bm, matrix=xform(center, rot, size), verts=res["verts"])
    bm.to_mesh(ob.data)
    bm.free()


def add_cyl(ob, center, radius, depth, segs=16, axis="Y", rot=None):
    bm = with_mesh(ob)
    res = bmesh.ops.create_cone(
        bm, cap_ends=True, cap_tris=False, segments=segs,
        radius1=radius, radius2=radius, depth=depth,
    )
    extra = rot.to_matrix().to_4x4() if rot is not None else Matrix.Identity(4)
    m = Matrix.Translation(Vector(center)) @ extra @ axis_matrix(axis)
    bmesh.ops.transform(bm, matrix=m, verts=res["verts"])
    bm.to_mesh(ob.data)
    bm.free()


def add_cone(ob, center, r1, r2, depth, segs=16, axis="Y", rot=None):
    bm = with_mesh(ob)
    res = bmesh.ops.create_cone(
        bm, cap_ends=True, cap_tris=False, segments=segs,
        radius1=r1, radius2=r2, depth=depth,
    )
    extra = rot.to_matrix().to_4x4() if rot is not None else Matrix.Identity(4)
    m = Matrix.Translation(Vector(center)) @ extra @ axis_matrix(axis)
    bmesh.ops.transform(bm, matrix=m, verts=res["verts"])
    bm.to_mesh(ob.data)
    bm.free()


def add_bolt(ob, center, axis="Z", r=0.0026, h=0.0036):
    add_cyl(ob, center, r, h, 6, axis)
    off = {"X": (h * 0.45, 0, 0), "Y": (0, h * 0.45, 0), "Z": (0, 0, h * 0.45)}[axis]
    add_cyl(ob, (center[0] + off[0], center[1] + off[1], center[2] + off[2]), r * 0.62, h * 0.35, 8, axis)


def add_profile(ob, yz, thick, shift=(0.0, 0.0, 0.0), rot=None):
    pts = list(yz)
    if pts[0] != pts[-1]:
        pts.append(pts[0])
    n = len(pts) - 1
    if n < 3:
        return
    bm = with_mesh(ob)
    hx = thick * 0.5
    rings = []
    for s in (-hx, hx):
        ring = []
        for i in range(n):
            y, z = pts[i]
            ring.append(bm.verts.new(Vector((s, y, z))))
        rings.append(ring)
    bm.verts.ensure_lookup_table()
    for i in range(n):
        j = (i + 1) % n
        try:
            bm.faces.new((rings[0][i], rings[0][j], rings[1][j], rings[1][i]))
        except ValueError:
            pass
    try:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[1])
    except ValueError:
        pass
    extra = rot.to_matrix().to_4x4() if rot is not None else Matrix.Identity(4)
    m = Matrix.Translation(Vector(shift)) @ extra
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    bm.to_mesh(ob.data)
    bm.free()


def pipe_seg(ob, a, b, radius, segs=10):
    a = Vector(a)
    b = Vector(b)
    d = b - a
    length = d.length
    if length < 0.0001:
        return
    mid = (a + b) * 0.5
    quat = d.normalized().to_track_quat("Z", "Y")
    add_cyl(ob, mid, radius, length, segs, "Z", quat.to_euler())


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


def build_rear(recoil):
    rear = new_mesh("RearPowerBox", recoil, "HSGun_MetalDark")
    add_box(rear, (0.0, -0.118, 0.048), (0.128, 0.168, 0.118))
    add_box(rear, (0.0, -0.118, 0.110), (0.136, 0.176, 0.010))
    add_box(rear, (0.066, -0.118, 0.048), (0.010, 0.172, 0.116))
    add_box(rear, (-0.066, -0.118, 0.048), (0.010, 0.172, 0.116))
    add_box(rear, (0.0, -0.204, 0.048), (0.122, 0.010, 0.110))
    add_box(rear, (0.0, -0.208, 0.048), (0.046, 0.016, 0.118))
    add_box(rear, (0.0, -0.208, 0.048), (0.122, 0.016, 0.046))
    for x in (-0.052, 0.052):
        for y in (-0.188, -0.118, -0.048):
            for z in (0.004, 0.092):
                add_bolt(rear, (x, y, z), "Z", 0.0024, 0.0032)
    add_box(rear, (0.0, -0.118, 0.118), (0.070, 0.090, 0.006))
    add_box(rear, (0.0, -0.155, 0.118), (0.078, 0.018, 0.005), Euler((0, 0, 0)))
    commit(rear, with_mesh(rear), bevel=0.0016, segs=2)

    plate = new_mesh("RearLabelPlate", recoil, "HSGun_Plate")
    add_box(plate, (0.0, -0.116, 0.119), (0.062, 0.078, 0.004))
    add_box(plate, (0.0, -0.148, 0.121), (0.070, 0.012, 0.003))
    commit(plate, with_mesh(plate), bevel=0.0008, segs=1)

    stripe = new_mesh("RearHazard", recoil, "HSGun_Hazard")
    add_box(stripe, (0.0, -0.148, 0.123), (0.068, 0.008, 0.002))
    commit(stripe, with_mesh(stripe), bevel=0.0004, segs=1)

    gauge = new_mesh("AnalogGauge", recoil, "HSGun_Gauge")
    add_cyl(gauge, (-0.074, -0.092, 0.046), 0.030, 0.014, 24, "X")
    add_cyl(gauge, (-0.066, -0.092, 0.046), 0.034, 0.006, 24, "X")
    add_bolt(gauge, (-0.072, -0.092, 0.074), "X", 0.0022, 0.004)
    add_bolt(gauge, (-0.072, -0.092, 0.018), "X", 0.0022, 0.004)
    commit(gauge, with_mesh(gauge), bevel=0.0008, segs=1)

    face = new_mesh("GaugeFace", gauge, "HSGun_GaugeFace")
    add_cyl(face, (-0.082, -0.092, 0.046), 0.024, 0.002, 24, "X")
    commit(face, with_mesh(face), bevel=0.0003, segs=1)

    glass = new_mesh("GaugeGlass", gauge, "HSGun_Glass")
    add_cyl(glass, (-0.084, -0.092, 0.046), 0.025, 0.002, 20, "X")
    commit(glass, with_mesh(glass), bevel=0.0002, segs=1)

    needle = new_mesh("GaugeNeedle", gauge, "HSGun_Orange")
    add_box(needle, (-0.085, -0.092, 0.054), (0.0016, 0.0032, 0.018), Euler((0, math.radians(90), math.radians(38))))
    commit(needle, with_mesh(needle), bevel=0.0002, segs=1)
    return gauge


def build_chassis(recoil):
    mid = new_mesh("MidBody", recoil, "HSGun_Metal")
    add_box(mid, (0.0, 0.042, 0.040), (0.086, 0.168, 0.078))
    add_box(mid, (0.0, 0.042, 0.082), (0.072, 0.150, 0.012))
    add_box(mid, (0.0, 0.118, 0.040), (0.094, 0.028, 0.086))
    add_box(mid, (0.048, 0.040, 0.040), (0.010, 0.140, 0.070))
    add_box(mid, (-0.048, 0.040, 0.040), (0.010, 0.140, 0.070))
    for y in (-0.020, 0.040, 0.100):
        add_bolt(mid, (0.044, y, 0.082), "Z", 0.0022, 0.003)
        add_bolt(mid, (-0.044, y, 0.082), "Z", 0.0022, 0.003)
    commit(mid, with_mesh(mid), bevel=0.0015, segs=2)

    bracket = new_mesh("CoilBrackets", recoil, "HSGun_MetalLight")
    add_box(bracket, (0.0, -0.018, 0.086), (0.078, 0.012, 0.022))
    add_box(bracket, (0.0, 0.102, 0.086), (0.078, 0.012, 0.022))
    add_box(bracket, (0.0, -0.018, -0.008), (0.050, 0.012, 0.018))
    add_box(bracket, (0.0, 0.102, -0.008), (0.050, 0.012, 0.018))
    commit(bracket, with_mesh(bracket), bevel=0.0008, segs=1)


def build_coils(recoil):
    glass_b = new_mesh("CoilBlue_Glass", recoil, "HSGun_Glass")
    core_b = new_mesh("CoilBlue_Core", recoil, "HSGun_Blue")
    caps = new_mesh("CoilBlue_Caps", recoil, "HSGun_MetalLight")
    for x in (-0.024, 0.024):
        c = (x, 0.042, 0.092)
        add_cyl(glass_b, c, 0.0165, 0.122, 18, "Y")
        add_cyl(core_b, c, 0.0105, 0.112, 14, "Y")
        for k in range(9):
            y = -0.012 + k * 0.0135
            add_cyl(core_b, (x, y, 0.092), 0.0142, 0.0042, 12, "Y")
        add_cyl(caps, (x, -0.022, 0.092), 0.018, 0.010, 14, "Y")
        add_cyl(caps, (x, 0.106, 0.092), 0.018, 0.010, 14, "Y")
        add_cyl(caps, (x, -0.022, 0.092), 0.012, 0.014, 10, "Y")
        add_cyl(caps, (x, 0.106, 0.092), 0.012, 0.014, 10, "Y")
    commit(glass_b, with_mesh(glass_b), bevel=0.0004, segs=1)
    commit(core_b, with_mesh(core_b), bevel=0.0)
    commit(caps, with_mesh(caps), bevel=0.0007, segs=1)

    glass_o = new_mesh("CoilOrange_Glass", recoil, "HSGun_Glass")
    core_o = new_mesh("CoilOrange_Core", recoil, "HSGun_Orange")
    caps_o = new_mesh("CoilOrange_Caps", recoil, "HSGun_MetalLight")
    c = (0.0, 0.042, -0.018)
    add_cyl(glass_o, c, 0.0195, 0.122, 18, "Y")
    add_cyl(core_o, c, 0.0128, 0.112, 14, "Y")
    for k in range(9):
        y = -0.012 + k * 0.0135
        add_cyl(core_o, (0.0, y, -0.018), 0.0168, 0.0046, 12, "Y")
    add_cyl(caps_o, (0.0, -0.022, -0.018), 0.021, 0.010, 14, "Y")
    add_cyl(caps_o, (0.0, 0.106, -0.018), 0.021, 0.010, 14, "Y")
    commit(glass_o, with_mesh(glass_o), bevel=0.0004, segs=1)
    commit(core_o, with_mesh(core_o), bevel=0.0)
    commit(caps_o, with_mesh(caps_o), bevel=0.0007, segs=1)
    return core_b, core_o


def build_conduits(recoil):
    copper = new_mesh("HeavyConduits", recoil, "HSGun_Copper")
    pipe_seg(copper, (0.058, -0.175, 0.090), (0.058, -0.040, 0.090), 0.0055)
    pipe_seg(copper, (0.058, -0.040, 0.090), (0.040, 0.010, 0.092), 0.0050)
    pipe_seg(copper, (0.040, 0.010, 0.092), (0.024, 0.030, 0.092), 0.0046)
    pipe_seg(copper, (-0.058, -0.175, 0.090), (-0.058, -0.040, 0.090), 0.0055)
    pipe_seg(copper, (-0.058, -0.040, 0.090), (-0.040, 0.010, 0.092), 0.0050)
    pipe_seg(copper, (-0.040, 0.010, 0.092), (-0.024, 0.030, 0.092), 0.0046)
    pipe_seg(copper, (0.050, -0.160, 0.010), (0.050, 0.000, -0.018), 0.0048)
    pipe_seg(copper, (0.050, 0.000, -0.018), (0.018, 0.028, -0.018), 0.0044)
    pipe_seg(copper, (-0.050, -0.160, 0.010), (-0.050, 0.000, -0.018), 0.0048)
    pipe_seg(copper, (-0.050, 0.000, -0.018), (-0.018, 0.028, -0.018), 0.0044)
    add_cyl(copper, (0.058, -0.175, 0.090), 0.007, 0.010, 10, "Y")
    add_cyl(copper, (-0.058, -0.175, 0.090), 0.007, 0.010, 10, "Y")
    commit(copper, with_mesh(copper), bevel=0.0005, segs=1)


def build_grip(recoil):
    tilt = Euler((math.radians(14), 0, 0))
    grip = new_mesh("Grip", recoil, "HSGun_Grip")
    add_box(grip, (0.0, -0.078, -0.072), (0.038, 0.050, 0.128), tilt)
    add_box(grip, (0.0, -0.070, -0.018), (0.042, 0.056, 0.028), tilt)
    add_box(grip, (0.0, -0.086, -0.128), (0.034, 0.044, 0.016), tilt)
    for z in (-0.040, -0.058, -0.076, -0.094, -0.112):
        add_box(grip, (0.0, -0.056, z), (0.040, 0.010, 0.010), tilt)
    commit(grip, with_mesh(grip), bevel=0.0022, segs=2)

    pommel = new_mesh("GripPommel", recoil, "HSGun_MetalDark")
    add_box(pommel, (0.0, -0.090, -0.140), (0.036, 0.048, 0.012), tilt)
    commit(pommel, with_mesh(pommel), bevel=0.0010, segs=1)

    guard = new_mesh("TriggerGuard", recoil, "HSGun_MetalLight")
    add_box(guard, (0.0, -0.034, -0.018), (0.018, 0.052, 0.007))
    add_box(guard, (0.0, -0.012, -0.072), (0.018, 0.007, 0.108))
    add_box(guard, (0.0, -0.048, -0.124), (0.018, 0.058, 0.007))
    add_box(guard, (0.0, -0.058, -0.018), (0.018, 0.010, 0.010))
    commit(guard, with_mesh(guard), bevel=0.0012, segs=2)

    trigger = new_mesh("Trigger", recoil, "HSGun_MetalLight")
    add_box(trigger, (0.0, -0.042, -0.048), (0.008, 0.010, 0.028), Euler((math.radians(18), 0, 0)))
    add_box(trigger, (0.0, -0.038, -0.062), (0.007, 0.008, 0.016), Euler((math.radians(38), 0, 0)))
    add_cyl(trigger, (0.0, -0.040, -0.034), 0.005, 0.010, 10, "X")
    commit(trigger, with_mesh(trigger), bevel=0.0008, segs=1)

    dials = new_mesh("GripDials", recoil, "HSGun_Gauge")
    add_cyl(dials, (-0.024, -0.068, -0.006), 0.011, 0.008, 16, "X")
    add_cyl(dials, (-0.024, -0.090, -0.006), 0.009, 0.008, 16, "X")
    add_cyl(dials, (-0.028, -0.068, -0.006), 0.008, 0.002, 14, "X")
    add_cyl(dials, (-0.028, -0.090, -0.006), 0.0065, 0.002, 14, "X")
    commit(dials, with_mesh(dials), bevel=0.0005, segs=1)

    faces = new_mesh("GripDialFaces", recoil, "HSGun_GaugeFace")
    add_cyl(faces, (-0.029, -0.068, -0.006), 0.0075, 0.0015, 14, "X")
    add_cyl(faces, (-0.029, -0.090, -0.006), 0.0060, 0.0015, 14, "X")
    commit(faces, with_mesh(faces), bevel=0.0002, segs=1)
    return trigger


def build_emitter(recoil):
    hub_y = 0.150
    hub_z = 0.040
    hub_r = 0.046
    hub = new_mesh("EmitterHub", recoil, "HSGun_MetalDark")
    add_cyl(hub, (0.0, hub_y, hub_z), 0.050, 0.038, 24, "Y")
    add_cyl(hub, (0.0, hub_y + 0.022, hub_z), 0.040, 0.018, 20, "Y")
    add_cyl(hub, (0.0, hub_y + 0.032, hub_z), 0.030, 0.012, 18, "Y")
    add_cone(hub, (0.0, hub_y + 0.040, hub_z), 0.026, 0.015, 0.012, 16, "Y")
    for i in range(8):
        a = math.radians(i * 45.0 + 22.0)
        add_bolt(hub, (math.cos(a) * 0.042, hub_y + 0.020, hub_z + math.sin(a) * 0.042), "Y", 0.0024, 0.004)
    commit(hub, with_mesh(hub), bevel=0.0012, segs=2)

    iris = new_mesh("EmitterIris", hub, "HSGun_MetalLight")
    add_cyl(iris, (0.0, hub_y + 0.038, hub_z), 0.022, 0.004, 16, "Y")
    add_cyl(iris, (0.0, hub_y + 0.040, hub_z), 0.015, 0.004, 12, "Y")
    commit(iris, with_mesh(iris), bevel=0.0005, segs=1)

    muzzle = new_mesh("MuzzleGlow", hub, "HSGun_Glow")
    add_cyl(muzzle, (0.0, hub_y + 0.044, hub_z), 0.012, 0.008, 16, "Y")
    muzzle.scale = (0.22, 0.22, 0.22)

    claws = []
    tips = []
    rest_rot = []
    for i, angle in enumerate((0.0, 90.0, 180.0, 270.0)):
        rad = math.radians(angle)
        yaw = Euler((0.0, rad, 0.0))
        rim = Vector((0.0, hub_y + 0.008, hub_z)) + yaw.to_matrix() @ Vector((0.0, 0.0, hub_r))

        claw = new_mesh("Claw_%d" % (i + 1), hub, "HSGun_Metal")
        add_profile(claw, [
            (0.000, -0.006),
            (0.000, 0.016),
            (0.018, 0.030),
            (0.048, 0.046),
            (0.086, 0.054),
            (0.118, 0.048),
            (0.140, 0.032),
            (0.140, 0.018),
            (0.118, 0.028),
            (0.086, 0.034),
            (0.048, 0.028),
            (0.018, 0.012),
            (0.000, -0.006),
        ], 0.022)
        add_box(claw, (0.0, 0.004, 0.004), (0.028, 0.026, 0.022))
        add_cyl(claw, (0.0, 0.000, 0.004), 0.008, 0.030, 12, "X")
        add_bolt(claw, (0.014, 0.000, 0.004), "X", 0.0026, 0.007)
        add_bolt(claw, (-0.014, 0.000, 0.004), "X", 0.0026, 0.007)
        commit(claw, with_mesh(claw), bevel=0.0008, segs=1)

        plate = new_mesh("Claw_%d_Plate" % (i + 1), claw, "HSGun_MetalLight")
        add_profile(plate, [
            (0.020, 0.018),
            (0.048, 0.038),
            (0.090, 0.046),
            (0.118, 0.038),
            (0.118, 0.028),
            (0.090, 0.036),
            (0.048, 0.028),
            (0.020, 0.010),
        ], 0.012)
        add_bolt(plate, (0.0, 0.040, 0.036), "Z", 0.0020, 0.004)
        add_bolt(plate, (0.0, 0.090, 0.042), "Z", 0.0020, 0.004)
        commit(plate, with_mesh(plate), bevel=0.0004, segs=1)

        tip = new_mesh("Claw_%d_Tip" % (i + 1), claw, "HSGun_MetalDark")
        add_profile(tip, [
            (0.000, 0.006),
            (0.000, 0.018),
            (0.022, 0.010),
            (0.044, -0.004),
            (0.058, -0.018),
            (0.066, -0.028),
            (0.058, -0.032),
            (0.040, -0.018),
            (0.018, 0.000),
            (0.000, 0.006),
        ], 0.014)
        commit(tip, with_mesh(tip), bevel=0.0005, segs=1)
        tip.location = (0.0, 0.128, 0.028)
        tip.rotation_euler = Euler((0, 0, 0))

        piston = new_mesh("Piston_%d" % (i + 1), claw, "HSGun_MetalLight")
        add_cyl(piston, (0.0, 0.036, 0.006), 0.0044, 0.062, 10, "Y", Euler((math.radians(-18), 0, 0)))
        add_cyl(piston, (0.0, 0.012, 0.000), 0.0062, 0.014, 10, "Y", Euler((math.radians(-18), 0, 0)))
        commit(piston, with_mesh(piston), bevel=0.0003, segs=1)

        ram = new_mesh("Ram_%d" % (i + 1), piston, "HSGun_Copper")
        add_cyl(ram, (0.0, 0.038, 0.006), 0.0024, 0.044, 8, "Y", Euler((math.radians(-18), 0, 0)))
        commit(ram, with_mesh(ram), bevel=0.0002, segs=1)

        claw.location = rim
        claw.rotation_euler = Euler((math.radians(-6.0), rad, 0.0))
        claws.append(claw)
        tips.append(tip)
        rest_rot.append((claw.rotation_euler.x, claw.rotation_euler.y, claw.rotation_euler.z))
    return hub, muzzle, claws, tips, rest_rot, [(0.0, 0.0, 0.0)] * 4


def build_gun():
    root = new_empty("PortalGun", None)
    recoil = new_empty("Recoil", root)
    build_rear(recoil)
    build_chassis(recoil)
    core_b, core_o = build_coils(recoil)
    build_conduits(recoil)
    trigger = build_grip(recoil)
    hub, muzzle, claws, tips, rest_rot, tip_rest = build_emitter(recoil)

    scene = bpy.context.scene
    scene.render.fps = 30
    scene.frame_start = 1
    scene.frame_end = 24

    anim_objs = [recoil, trigger, muzzle, core_b, core_o] + claws + tips

    def rest_pose(frame):
        key(recoil, frame, loc=(0, 0, 0), rot=(0, 0, 0))
        key(trigger, frame, rot=(0, 0, 0))
        key(muzzle, frame, scl=(0.22, 0.22, 0.22))
        key(core_b, frame, scl=(1, 1, 1))
        key(core_o, frame, scl=(1, 1, 1))
        for c, r in zip(claws, rest_rot):
            key(c, frame, rot=r)
        for t in tips:
            key(t, frame, rot=(0, 0, 0))

    def start_take(take):
        for ob in anim_objs:
            act = bpy.data.actions.new(ob.name + "|" + take)
            act.use_fake_user = True
            assign_action(ob, act)

    start_take("Idle")
    rest_pose(1)
    key(core_b, 12, scl=(1.08, 1.0, 1.08))
    key(core_o, 12, scl=(1.08, 1.0, 1.08))
    rest_pose(24)

    start_take("Fire")
    rest_pose(1)
    key(trigger, 4, rot=(math.radians(18), 0, 0))
    key(trigger, 14, rot=(0, 0, 0))
    for c, r in zip(claws, rest_rot):
        key(c, 3, rot=r)
        key(c, 8, rot=(r[0] + math.radians(32), r[1], r[2]))
        key(c, 12, rot=(r[0] + math.radians(18), r[1], r[2]))
        key(c, 18, rot=r)
    for t in tips:
        key(t, 3, rot=(0, 0, 0))
        key(t, 8, rot=(math.radians(-16), 0, 0))
        key(t, 18, rot=(0, 0, 0))
    key(recoil, 6, loc=(0.0, -0.018, 0.006), rot=(math.radians(-6), 0, math.radians(1.5)))
    key(recoil, 16, loc=(0, 0, 0), rot=(0, 0, 0))
    key(core_b, 4, scl=(1.0, 1.0, 1.0))
    key(core_b, 9, scl=(1.22, 1.04, 1.22))
    key(core_b, 16, scl=(1.0, 1.0, 1.0))
    key(core_o, 4, scl=(1.0, 1.0, 1.0))
    key(core_o, 9, scl=(1.22, 1.04, 1.22))
    key(core_o, 16, scl=(1.0, 1.0, 1.0))
    key(muzzle, 5, scl=(0.35, 0.35, 0.35))
    key(muzzle, 9, scl=(2.4, 2.0, 2.4))
    key(muzzle, 14, scl=(0.22, 0.22, 0.22))

    start_take("Reject")
    rest_pose(1)
    key(trigger, 3, rot=(math.radians(10), 0, 0))
    key(trigger, 10, rot=(0, 0, 0))
    for c, r in zip(claws, rest_rot):
        key(c, 4, rot=(r[0] + math.radians(8), r[1], r[2]))
        key(c, 10, rot=r)
    key(recoil, 4, loc=(0.0, -0.006, 0.0), rot=(math.radians(-2), 0, math.radians(-4)))
    key(recoil, 10, loc=(0, 0, 0), rot=(0, 0, 0))
    key(core_b, 5, scl=(0.92, 1.0, 0.92))
    key(core_b, 12, scl=(1, 1, 1))
    key(core_o, 5, scl=(0.92, 1.0, 0.92))
    key(core_o, 12, scl=(1, 1, 1))

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


def setup_scene(root):
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    cam_data = bpy.data.cameras.new("ConceptCam")
    cam_data.lens = 50
    cam = bpy.data.objects.new("ConceptCam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (0.42, -0.36, 0.22)
    cam.rotation_euler = (math.radians(72), 0, math.radians(52))
    scene.camera = cam
    key = bpy.data.lights.new("Key", "AREA")
    key.energy = 80
    key.size = 0.4
    key_ob = bpy.data.objects.new("KeyLight", key)
    scene.collection.objects.link(key_ob)
    key_ob.location = (0.3, -0.2, 0.4)
    fill = bpy.data.lights.new("Fill", "AREA")
    fill.energy = 25
    fill.size = 0.5
    fill_ob = bpy.data.objects.new("FillLight", fill)
    scene.collection.objects.link(fill_ob)
    fill_ob.location = (-0.3, 0.2, 0.2)
    root["hsportal"] = "PortalGun"


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
    root, anim_objs = build_gun()
    stash_nla(anim_objs)
    setup_scene(root)
    bpy.context.view_layer.update()
    path = os.path.join(OUT_DIR, "PortalGun.fbx")
    export_fbx(root, path)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)
    names = sorted(ob.name for ob in bpy.data.objects if ob.type in {"MESH", "EMPTY"})
    with open(os.path.join(OUT_DIR, "_export_log.txt"), "w") as f:
        f.write("PortalGun -> %s\n" % path)
        f.write("objects:\n")
        for n in names:
            f.write("  %s\n" % n)
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
