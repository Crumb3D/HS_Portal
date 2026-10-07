# blender --background hsportal_gun.blend --python hsportal_gun_preview.py -- <png_out>
import bpy
import math
import os
import sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0]) if argv else os.path.join(os.path.dirname(bpy.data.filepath), "gun_preview.png")

scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = "BOTH"
scene.render.resolution_x = 1280
scene.render.resolution_y = 720
scene.render.film_transparent = False
scene.world.color = (0.08, 0.09, 0.10)
scene.render.image_settings.file_format = "PNG"
scene.frame_set(1)

root = bpy.data.objects.get("PortalGun")
obs = []
stack = [root] if root else []
while stack:
    ob = stack.pop()
    if ob.type == "MESH":
        obs.append(ob)
    stack.extend(list(ob.children))

mn = Vector((1e9, 1e9, 1e9))
mx = Vector((-1e9, -1e9, -1e9))
for ob in obs:
    for corner in ob.bound_box:
        w = ob.matrix_world @ Vector(corner)
        mn.x, mn.y, mn.z = min(mn.x, w.x), min(mn.y, w.y), min(mn.z, w.z)
        mx.x, mx.y, mx.z = max(mx.x, w.x), max(mx.y, w.y), max(mx.z, w.z)
center = (mn + mx) * 0.5
size = max((mx - mn).length, 0.2)

cam_data = bpy.data.cameras.new("preview_cam")
cam_data.lens = 50
cam = bpy.data.objects.new("preview_cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
yaw, pitch = math.radians(128), math.radians(16)
d = Vector((
    math.cos(pitch) * math.cos(yaw),
    math.cos(pitch) * math.sin(yaw),
    math.sin(pitch),
))
cam.location = center + d * (size * 1.35)
cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
scene.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("PREVIEW " + OUT)
