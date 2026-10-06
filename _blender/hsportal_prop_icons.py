# blender --background hsportal_props.blend --python hsportal_prop_icons.py -- <out_dir>
import bpy
import math
import os
import sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0]) if argv else os.path.join(os.path.dirname(bpy.data.filepath), "..", "UIAtlases", "ItemIconAtlas")
os.makedirs(OUT, exist_ok=True)

scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.render.resolution_x = 232
scene.render.resolution_y = 160
scene.render.film_transparent = True
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"

cam_data = bpy.data.cameras.new("icon_cam")
cam_data.lens = 50
cam = bpy.data.objects.new("icon_cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam


def mesh_objects(root):
    out = []
    stack = [root]
    while stack:
        ob = stack.pop()
        if ob.type == "MESH" and not ob.name.startswith("COL_"):
            out.append(ob)
        stack.extend(list(ob.children))
    return out


def world_bounds(obs):
    mn = Vector((1e9, 1e9, 1e9))
    mx = Vector((-1e9, -1e9, -1e9))
    anyv = False
    for ob in obs:
        for corner in ob.bound_box:
            w = ob.matrix_world @ Vector(corner)
            mn.x, mn.y, mn.z = min(mn.x, w.x), min(mn.y, w.y), min(mn.z, w.z)
            mx.x, mx.y, mx.z = max(mx.x, w.x), max(mx.y, w.y), max(mx.z, w.z)
            anyv = True
    if not anyv:
        return Vector((0, 0, 0)), 1.0
    center = (mn + mx) * 0.5
    size = (mx - mn).length
    return center, max(size, 0.20)


def hide_all():
    for ob in bpy.data.objects:
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


def render_one(name, file_name, yaw=-42, pitch=22, dist_mul=1.35):
    root = bpy.data.objects.get(name)
    log.append(name + (" OK" if root else " MISSING"))
    if root is None:
        return
    hide_all()
    show_tree(root)
    cam.hide_render = False
    cam.hide_set(False)
    meshes = mesh_objects(root)
    center, size = world_bounds(meshes)
    dist = size * dist_mul
    y, p = math.radians(yaw), math.radians(pitch)
    d = Vector((math.cos(p) * math.cos(y), math.cos(p) * math.sin(y), math.sin(p)))
    cam.location = center + d * dist
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    path = os.path.join(OUT, file_name)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


log = []
render_one("CompanionCube", "hsportalCube.png", yaw=-48, pitch=28, dist_mul=1.22)
render_one("GelGun", "hsportalGelGun.png", yaw=-42, pitch=18, dist_mul=1.28)
render_one("LongFallBoots", "hsportalBoots.png", yaw=-50, pitch=25, dist_mul=1.3)
with open(os.path.join(OUT, "_props_icons_log.txt"), "w") as f:
    f.write("\n".join(log) + "\nDONE\n")
