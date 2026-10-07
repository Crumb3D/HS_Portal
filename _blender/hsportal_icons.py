# Inventory icons: blender --background hsportal_gun.blend --python hsportal_icons.py -- <out_dir>
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
scene.frame_set(1)

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
        if ob.type == "MESH":
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


root = bpy.data.objects.get("PortalGun")
log = []
if root is None:
    log.append("MISSING PortalGun")
else:
    meshes = mesh_objects(root)
    center, size = world_bounds(meshes)
    dist = size * 1.22
    # Front-left 3/4 so claws sit in the foreground like the concept sheet
    yaw, pitch = math.radians(128), math.radians(16)
    d = Vector((
        math.cos(pitch) * math.cos(yaw),
        math.cos(pitch) * math.sin(yaw),
        math.sin(pitch),
    ))
    cam.location = center + d * dist
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    path = os.path.join(OUT, "hsportalGun.png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    log.append("hsportalGun -> " + path)

with open(os.path.join(OUT, "_icons_log.txt"), "w") as f:
    f.write("\n".join(log) + "\nDONE\n")
