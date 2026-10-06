# Industrial companion cube, gel sprayer, long-fall boots. Original 7DTD art, not Valve meshes.
# blender --background --factory-startup --python hsportal_props.py -- <fbx_out_dir> [<blend_out>]

import bpy
import bmesh
import math
import os
import sys
from mathutils import Vector, Euler, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.abspath(argv[0]) if argv else os.path.abspath(os.path.join(HERE, "..", "_unity", "Assets", "HSPortal", "Models"))
BLEND_OUT = os.path.abspath(argv[1]) if len(argv) > 1 else os.path.join(HERE, "hsportal_props.blend")

MATS = {
    "HSCube_Metal": (0.28, 0.27, 0.25, 1),
    "HSCube_MetalDark": (0.10, 0.10, 0.11, 1),
    "HSCube_Heart": (0.72, 0.16, 0.22, 1),
    "HSCube_Stripe": (0.85, 0.68, 0.08, 1),
    "HSGun_Metal": (0.32, 0.30, 0.27, 1),
    "HSGun_MetalDark": (0.12, 0.12, 0.12, 1),
    "HSGun_MetalLight": (0.55, 0.53, 0.48, 1),
    "HSGun_Grip": (0.09, 0.08, 0.07, 1),
    "HSGun_Blue": (0.20, 0.55, 1.0, 1),
    "HSGun_Orange": (1.0, 0.40, 0.06, 1),
    "HSGun_Copper": (0.70, 0.36, 0.12, 1),
    "HSBoot_Leather": (0.18, 0.11, 0.07, 1),
    "HSBoot_Metal": (0.38, 0.38, 0.36, 1),
    "HSBoot_Orange": (0.95, 0.45, 0.08, 1),
    "HSBoot_Sole": (0.08, 0.08, 0.09, 1),
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
    res = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=radius, radius2=radius, depth=depth)
    if axis == "Y":
        am = Matrix.Rotation(math.radians(90.0), 4, "X")
    elif axis == "X":
        am = Matrix.Rotation(math.radians(-90.0), 4, "Y")
    else:
        am = Matrix.Identity(4)
    extra = rot.to_matrix().to_4x4() if rot is not None else Matrix.Identity(4)
    m = Matrix.Translation(Vector(center)) @ extra @ am
    bmesh.ops.transform(bm, matrix=m, verts=res["verts"])
    bm.to_mesh(ob.data)
    bm.free()


def heart_boxes(ob, face_off, axis):
    # Crude stencil heart from boxes, not the Valve circle-dot motif.
    if axis == "x":
        def p(a, b, c):
            return (face_off, a, b)
        sx, sy, sz = 0.02, 0.07, 0.07
    else:
        def p(a, b, c):
            return (a, face_off, b)
        sx, sy, sz = 0.07, 0.02, 0.07
    add_box(ob, p(-0.04, 0.05, 0), (sx if axis == "x" else 0.055, sy if axis != "x" else 0.055, 0.055))
    add_box(ob, p(0.04, 0.05, 0), (sx if axis == "x" else 0.055, sy if axis != "x" else 0.055, 0.055))
    add_box(ob, p(0.0, -0.02, 0), (sx if axis == "x" else 0.10, sy if axis != "x" else 0.10, 0.09))
    add_box(ob, p(0.0, -0.10, 0), (sx if axis == "x" else 0.05, sy if axis != "x" else 0.05, 0.05))


def build_cube():
    root = new_empty("CompanionCube", None)
    body = new_mesh("CubeBody", root, "HSCube_Metal")
    add_box(body, (0, 0, 0), (0.92, 0.92, 0.92))
    frame = new_mesh("CubeFrame", root, "HSCube_MetalDark")
    for s in (-1, 1):
        add_box(frame, (s * 0.46, 0, 0), (0.04, 0.96, 0.96))
        add_box(frame, (0, s * 0.46, 0), (0.96, 0.04, 0.96))
        add_box(frame, (0, 0, s * 0.46), (0.96, 0.96, 0.04))
    stripe = new_mesh("CubeStripe", root, "HSCube_Stripe")
    add_box(stripe, (0, 0.48, 0), (0.7, 0.02, 0.08))
    add_box(stripe, (0, -0.48, 0), (0.7, 0.02, 0.08))
    heart = new_mesh("CubeHeart", root, "HSCube_Heart")
    heart_boxes(heart, 0.48, "y")
    heart_boxes(heart, -0.48, "y")
    heart_boxes(heart, 0.48, "x")
    heart_boxes(heart, -0.48, "x")
    col = new_mesh("COL_Cube", root, "HSCube_Metal")
    add_box(col, (0, 0, 0), (1.0, 1.0, 1.0))
    return root


def build_gel_gun():
    root = new_empty("GelGun", None)
    body = new_mesh("GelBody", root, "HSGun_Metal")
    add_box(body, (0.0, 0.04, 0.03), (0.07, 0.18, 0.07))
    add_cyl(body, (0.0, 0.16, 0.03), 0.028, 0.08, 14, "Y")
    nozzle = new_mesh("GelNozzle", root, "HSGun_MetalDark")
    add_cyl(nozzle, (0.0, 0.22, 0.03), 0.018, 0.06, 12, "Y")
    add_cyl(nozzle, (0.0, 0.26, 0.03), 0.012, 0.03, 10, "Y")
    grip = new_mesh("GelGrip", root, "HSGun_Grip")
    add_box(grip, (0.0, -0.06, -0.06), (0.034, 0.05, 0.12), Euler((math.radians(14), 0, 0)))
    guard = new_mesh("GelGuard", root, "HSGun_MetalLight")
    add_box(guard, (0.0, -0.03, -0.02), (0.018, 0.04, 0.008))
    add_box(guard, (0.0, -0.015, -0.07), (0.018, 0.008, 0.09))
    blue = new_mesh("GelTankBlue", root, "HSGun_Blue")
    add_cyl(blue, (-0.045, 0.02, 0.05), 0.028, 0.11, 16, "Y")
    orange = new_mesh("GelTankOrange", root, "HSGun_Orange")
    add_cyl(orange, (0.045, 0.02, 0.05), 0.028, 0.11, 16, "Y")
    hose = new_mesh("GelHose", root, "HSGun_Copper")
    add_cyl(hose, (-0.045, 0.09, 0.05), 0.008, 0.06, 8, "Y")
    add_cyl(hose, (0.045, 0.09, 0.05), 0.008, 0.06, 8, "Y")
    add_box(hose, (0.0, 0.12, 0.05), (0.09, 0.02, 0.02))
    return root


def boot(parent, x):
    leather = new_mesh("BootLeather", parent, "HSBoot_Leather")
    add_box(leather, (x, 0.0, 0.06), (0.09, 0.22, 0.10))
    add_box(leather, (x, 0.06, 0.16), (0.085, 0.10, 0.12))
    shin = new_mesh("BootShin", parent, "HSBoot_Orange")
    add_box(shin, (x, -0.02, 0.18), (0.095, 0.16, 0.08))
    metal = new_mesh("BootPlate", parent, "HSBoot_Metal")
    add_box(metal, (x, 0.08, 0.07), (0.1, 0.04, 0.08))
    sole = new_mesh("BootSole", parent, "HSBoot_Sole")
    add_box(sole, (x, 0.02, 0.0), (0.1, 0.24, 0.04))
    add_box(sole, (x, 0.08, 0.02), (0.1, 0.08, 0.03))


def build_boots():
    root = new_empty("LongFallBoots", None)
    boot(root, -0.08)
    boot(root, 0.08)
    return root


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
        bake_anim=False,
    )


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    cube = build_cube()
    gun = build_gel_gun()
    boots = build_boots()
    gun.location = (2.0, 0, 0)
    boots.location = (4.0, 0, 0)
    bpy.context.view_layer.update()
    ol, gl, bl = Vector(cube.location), Vector(gun.location), Vector(boots.location)
    cube.location = (0, 0, 0)
    gun.location = (0, 0, 0)
    boots.location = (0, 0, 0)
    bpy.context.view_layer.update()
    export_fbx(cube, os.path.join(OUT_DIR, "CompanionCube.fbx"))
    export_fbx(gun, os.path.join(OUT_DIR, "GelGun.fbx"))
    export_fbx(boots, os.path.join(OUT_DIR, "LongFallBoots.fbx"))
    cube.location = ol
    gun.location = (2.0, 0, 0)
    boots.location = (4.0, 0, 0)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)
    with open(os.path.join(OUT_DIR, "_props_log.txt"), "w") as f:
        f.write("CompanionCube GelGun LongFallBoots -> %s\nDONE\n" % OUT_DIR)


if __name__ == "__main__" or True:
    try:
        main()
    except Exception:
        import traceback
        os.makedirs(OUT_DIR, exist_ok=True)
        with open(os.path.join(OUT_DIR, "_props_log.txt"), "w") as f:
            f.write("FAILED\n" + traceback.format_exc())
        raise
