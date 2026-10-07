# Industrial companion cube, gel sprayer, long-fall boots. Original 7DTD art, not Valve meshes.
# blender --background --factory-startup --python hsportal_props.py -- <fbx_out_dir> [<blend_out>]

import bpy
import bmesh
import math
import os
import shutil
import sys
from mathutils import Vector, Euler, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.abspath(argv[0]) if argv else os.path.abspath(os.path.join(HERE, "..", "_unity", "Assets", "HSPortal", "Models"))
BLEND_OUT = os.path.abspath(argv[1]) if len(argv) > 1 else os.path.join(HERE, "hsportal_props.blend")
TEX_DIR = os.path.abspath(os.path.join(HERE, "..", "UIAtlases"))
TEX_DIFFUSE = os.path.join(TEX_DIR, "jack-jederstrom-bergman-comp-cube-texture-diffuse.jpg")
TEX_DISP = os.path.join(TEX_DIR, "jack-jederstrom-bergman-comp-cube-texture-displacement.jpg")
TEX_GLOW = os.path.join(TEX_DIR, "jack-jederstrom-bergman-comp-cube-texture-glow.jpg")
UNITY_TEX = os.path.abspath(os.path.join(HERE, "..", "_unity", "Assets", "HSPortal", "Textures"))

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
    col = MATS.get(name, (0.5, 0.5, 0.5, 1.0))
    m.diffuse_color = col
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    if "Base Color" in bsdf.inputs:
        bsdf.inputs["Base Color"].default_value = col
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = 0.45
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    out.location = (280, 0)
    bsdf.location = (0, 0)
    return m


def load_img(path, non_color=False):
    img = bpy.data.images.load(path, check_existing=True)
    try:
        img.colorspace_settings.name = "Non-Color" if non_color else "sRGB"
    except Exception:
        pass
    return img


def cube_mat():
    m = bpy.data.materials.get("HSCube_Body")
    if m is None:
        m = bpy.data.materials.new("HSCube_Body")
    m.use_nodes = True
    m.diffuse_color = (0.55, 0.55, 0.56, 1)
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tex_d = nt.nodes.new("ShaderNodeTexImage")
    tex_d.image = load_img(TEX_DIFFUSE, False)
    tex_e = nt.nodes.new("ShaderNodeTexImage")
    tex_e.image = load_img(TEX_GLOW, False)
    nt.links.new(tex_d.outputs["Color"], bsdf.inputs["Base Color"])
    if "Emission Color" in bsdf.inputs:
        nt.links.new(tex_e.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 2.4
    elif "Emission" in bsdf.inputs:
        nt.links.new(tex_e.outputs["Color"], bsdf.inputs["Emission"])
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = 0.38
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = 0.35
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    out.location = (360, 0)
    bsdf.location = (80, 0)
    tex_d.location = (-240, 80)
    tex_e.location = (-240, -160)
    return m


def face_uv(n, co, half):
    hx = max(half, 1e-6)
    ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
    if az >= ax and az >= ay:
        u = co.x / (2.0 * hx) + 0.5
        v = co.y / (2.0 * hx) + 0.5
        if n.z < 0.0:
            v = 1.0 - v
    elif ax >= ay:
        u = ((-co.y) if n.x > 0.0 else co.y) / (2.0 * hx) + 0.5
        v = co.z / (2.0 * hx) + 0.5
    else:
        u = (co.x if n.y > 0.0 else -co.x) / (2.0 * hx) + 0.5
        v = co.z / (2.0 * hx) + 0.5
    return u, v


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
    # One UV-mapped cube. Displacement becomes the corner blocks; diffuse + glow are the face look.
    root = new_empty("CompanionCube", None)
    mesh = bpy.data.meshes.new("CubeBody")
    ob = bpy.data.objects.new("CubeBody", mesh)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = root
    ob.rotation_mode = "XYZ"
    ob.data.materials.append(cube_mat())

    side = 0.86
    half = side * 0.5
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=side)
    bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=28, use_grid_fill=True)
    uv_layer = bm.loops.layers.uv.new("UVMap")
    for face in bm.faces:
        n = face.normal.copy()
        n.normalize()
        for loop in face.loops:
            u, v = face_uv(n, loop.vert.co, half)
            loop[uv_layer].uv = (u, v)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

    img = load_img(TEX_DISP, True)
    tex = bpy.data.textures.get("HSCubeDisp")
    if tex is None:
        tex = bpy.data.textures.new("HSCubeDisp", "IMAGE")
    tex.image = img
    md = ob.modifiers.new("HSCubeDisp", "DISPLACE")
    md.texture = tex
    md.texture_coords = "UV"
    md.mid_level = 0.32
    md.strength = 0.09
    md.direction = "NORMAL"
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    baked = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    ob.modifiers.clear()
    old = ob.data
    ob.data = baked
    bpy.data.meshes.remove(old)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    try:
        bpy.ops.object.shade_auto_smooth(angle=math.radians(42.0))
    except Exception:
        try:
            bpy.ops.object.shade_smooth()
        except Exception:
            pass

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
    # Standing boot: +Y toes, -Y heel, +Z up, sole on the ground.
    sole = new_mesh("BootSole", parent, "HSBoot_Sole")
    add_box(sole, (x, 0.03, 0.016), (0.088, 0.26, 0.032))
    add_box(sole, (x, 0.13, 0.028), (0.088, 0.07, 0.028))
    leather = new_mesh("BootLeather", parent, "HSBoot_Leather")
    add_box(leather, (x, 0.03, 0.07), (0.082, 0.20, 0.09))
    add_box(leather, (x, 0.11, 0.085), (0.08, 0.07, 0.07))
    add_box(leather, (x, -0.05, 0.16), (0.08, 0.09, 0.14))
    shaft = new_mesh("BootShaft", parent, "HSBoot_Leather")
    add_box(shaft, (x, -0.055, 0.32), (0.082, 0.095, 0.20))
    pad = new_mesh("BootShin", parent, "HSBoot_Orange")
    add_box(pad, (x, 0.005, 0.33), (0.09, 0.038, 0.18))
    metal = new_mesh("BootPlate", parent, "HSBoot_Metal")
    add_box(metal, (x, 0.135, 0.055), (0.09, 0.04, 0.05))
    add_box(metal, (x, -0.085, 0.05), (0.086, 0.035, 0.055))


def build_goo():
    # Paint-can of mixed conversion slime — reads at HUD ammo size.
    root = new_empty("GooCan", None)
    body = new_mesh("GooCanBody", root, "HSGun_Metal")
    add_cyl(body, (0, 0, 0.09), 0.07, 0.18, 20, "Z")
    rim = new_mesh("GooCanRim", root, "HSGun_MetalDark")
    add_cyl(rim, (0, 0, 0.18), 0.074, 0.024, 20, "Z")
    add_cyl(rim, (0, 0, 0.02), 0.074, 0.024, 20, "Z")
    stripe = new_mesh("GooCanStripe", root, "HSCube_Stripe")
    add_box(stripe, (0, 0, 0.09), (0.148, 0.04, 0.03))
    blue = new_mesh("GooLabelBlue", root, "HSGun_Blue")
    add_box(blue, (-0.04, 0.068, 0.10), (0.055, 0.012, 0.09))
    orange = new_mesh("GooLabelOrange", root, "HSGun_Orange")
    add_box(orange, (0.04, 0.068, 0.10), (0.055, 0.012, 0.09))
    slime = new_mesh("GooSlimeBlue", root, "HSGun_Blue")
    add_cyl(slime, (-0.018, 0.0, 0.205), 0.042, 0.05, 14, "Z")
    drip = new_mesh("GooSlimeOrange", root, "HSGun_Orange")
    add_cyl(drip, (0.02, 0.01, 0.22), 0.032, 0.04, 12, "Z")
    add_box(drip, (0.045, 0.0, 0.14), (0.035, 0.035, 0.10))
    lid = new_mesh("GooHandle", root, "HSGun_MetalLight")
    add_box(lid, (0.0, 0.0, 0.24), (0.018, 0.09, 0.018))
    add_box(lid, (-0.04, 0.0, 0.22), (0.018, 0.018, 0.04))
    add_box(lid, (0.04, 0.0, 0.22), (0.018, 0.018, 0.04))
    return root


def build_boots():
    root = new_empty("LongFallBoots", None)
    left = new_empty("BootL", root)
    right = new_empty("BootR", root)
    boot(left, 0.0)
    boot(right, 0.0)
    left.location.x = -0.09
    right.location.x = 0.09
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
    os.makedirs(UNITY_TEX, exist_ok=True)
    for src, name in ((TEX_DIFFUSE, "Cube_Diffuse.jpg"), (TEX_DISP, "Cube_Displacement.jpg"), (TEX_GLOW, "Cube_Glow.jpg")):
        if os.path.isfile(src):
            shutil.copy2(src, os.path.join(UNITY_TEX, name))
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    cube = build_cube()
    gun = build_gel_gun()
    boots = build_boots()
    goo = build_goo()
    gun.location = (2.0, 0, 0)
    boots.location = (4.0, 0, 0)
    goo.location = (6.0, 0, 0)
    bpy.context.view_layer.update()
    cube.location = (0, 0, 0)
    gun.location = (0, 0, 0)
    boots.location = (0, 0, 0)
    goo.location = (0, 0, 0)
    bpy.context.view_layer.update()
    export_fbx(cube, os.path.join(OUT_DIR, "CompanionCube.fbx"))
    export_fbx(gun, os.path.join(OUT_DIR, "GelGun.fbx"))
    export_fbx(boots, os.path.join(OUT_DIR, "LongFallBoots.fbx"))
    gun.location = (2.0, 0, 0)
    boots.location = (4.0, 0, 0)
    goo.location = (6.0, 0, 0)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)
    with open(os.path.join(OUT_DIR, "_props_log.txt"), "w") as f:
        f.write("CompanionCube GelGun LongFallBoots GooCan -> %s\nDONE\n" % OUT_DIR)


if __name__ == "__main__" or True:
    try:
        main()
    except Exception:
        import traceback
        os.makedirs(OUT_DIR, exist_ok=True)
        with open(os.path.join(OUT_DIR, "_props_log.txt"), "w") as f:
            f.write("FAILED\n" + traceback.format_exc())
        raise
