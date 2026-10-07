# blender --background hsportal_props.blend --python hsportal_prop_icons.py -- <out_dir>
import bpy
import math
import os
import sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0]) if argv else os.path.join(os.path.dirname(bpy.data.filepath), "..", "UIAtlases", "ItemIconAtlas")
os.makedirs(OUT, exist_ok=True)

W, H = 232, 160
ASPECT = float(W) / float(H)

scene = bpy.context.scene
for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try:
        scene.render.engine = eng
        break
    except Exception:
        pass
scene.render.resolution_x = W
scene.render.resolution_y = H
scene.render.film_transparent = True
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"
if hasattr(scene, "eevee"):
    try:
        scene.eevee.use_bloom = False
    except Exception:
        pass

world = scene.world
if world is None:
    world = bpy.data.worlds.new("icon_world")
    scene.world = world
world.use_nodes = True
bg = None
for n in world.node_tree.nodes:
    if n.type == "BACKGROUND":
        bg = n
        break
if bg is not None:
    bg.inputs[0].default_value = (0.18, 0.19, 0.21, 1)
    bg.inputs[1].default_value = 0.35

cam_data = bpy.data.cameras.new("icon_cam")
cam_data.type = "ORTHO"
cam_data.clip_start = 0.01
cam_data.clip_end = 80.0
cam = bpy.data.objects.new("icon_cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam


def make_light(name, energy, size, loc, look_at=(0, 0, 0.1)):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.size = size
    ob = bpy.data.objects.new(name, data)
    ob.location = loc
    ob.rotation_euler = (Vector(look_at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(ob)
    return ob


make_light("icon_key", 420, 2.4, (1.6, -1.8, 2.1))
make_light("icon_fill", 140, 3.2, (-2.0, 0.6, 1.4))
make_light("icon_rim", 180, 2.0, (-0.4, 2.2, 1.6))


def mesh_objects(root):
    out = []
    stack = [root]
    while stack:
        ob = stack.pop()
        if ob.type == "MESH" and not ob.name.startswith("COL_"):
            out.append(ob)
        stack.extend(list(ob.children))
    return out


def world_verts(obs):
    dg = bpy.context.evaluated_depsgraph_get()
    pts = []
    for ob in obs:
        ev = ob.evaluated_get(dg)
        try:
            mesh = ev.to_mesh()
        except Exception:
            continue
        mw = ev.matrix_world
        for v in mesh.vertices:
            pts.append(mw @ v.co)
        try:
            ev.to_mesh_clear()
        except Exception:
            pass
    return pts


def hide_all():
    for ob in bpy.data.objects:
        if ob.type in ("LIGHT", "CAMERA"):
            ob.hide_render = False
            ob.hide_set(False)
            continue
        ob.hide_render = True
        ob.hide_set(True)


def show_tree(root):
    stack = [root]
    while stack:
        ob = stack.pop()
        hide = ob.name.startswith("COL_")
        ob.hide_render = hide
        ob.hide_set(hide)
        stack.extend(list(ob.children))


def fit_ortho(meshes, yaw, pitch, pad=1.18):
    pts = world_verts(meshes)
    if not pts:
        cam.location = (2, -2, 2)
        cam.rotation_euler = (Vector((0, 0, 0)) - cam.location).to_track_quat("-Z", "Y").to_euler()
        cam_data.ortho_scale = 1.0
        return
    center = Vector((0, 0, 0))
    for p in pts:
        center += p
    center /= float(len(pts))
    y, p = math.radians(yaw), math.radians(pitch)
    d = Vector((math.cos(p) * math.cos(y), math.cos(p) * math.sin(y), math.sin(p)))
    cam.location = center + d * 6.0
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.view_layer.update()
    right = cam.matrix_world.to_quaternion() @ Vector((1, 0, 0))
    up = cam.matrix_world.to_quaternion() @ Vector((0, 1, 0))
    xs = [(p - center).dot(right) for p in pts]
    ys = [(p - center).dot(up) for p in pts]
    minx, maxx = min(xs), max(xs)
    miny, maxy = min(ys), max(ys)
    mid_x = (minx + maxx) * 0.5
    mid_y = (miny + maxy) * 0.5
    cam.location = cam.location + right * mid_x + up * mid_y
    w = (maxx - minx)
    h = (maxy - miny)
    # ortho_scale is the larger rendered axis in world units (landscape → width).
    scale = max(w * pad, h * pad * ASPECT, 0.12)
    cam_data.ortho_scale = scale
    bpy.context.view_layer.update()


def render_one(name, file_name, yaw=-42, pitch=22, pad=1.18):
    root = bpy.data.objects.get(name)
    log.append(name + (" OK" if root else " MISSING"))
    if root is None:
        return
    hide_all()
    show_tree(root)
    cam.hide_render = False
    cam.hide_set(False)
    meshes = mesh_objects(root)
    fit_ortho(meshes, yaw, pitch, pad)
    path = os.path.join(OUT, file_name)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


log = []
render_one("CompanionCube", "hsportalCube.png", yaw=-48, pitch=28, pad=1.16)
render_one("GelGun", "hsportalGelGun.png", yaw=-42, pitch=18, pad=1.16)
render_one("LongFallBoots", "hsportalBoots.png", yaw=-18, pitch=16, pad=1.16)
render_one("GooCan", "hsportalGoo.png", yaw=-38, pitch=24, pad=1.16)
with open(os.path.join(OUT, "_props_icons_log.txt"), "w") as f:
    f.write("\n".join(log) + "\nDONE\n")
