"""Render the wanderer as two flat-shaded sprite frames (idle, step) with a transparent background.

    blender -b -P blender/render_wanderer.py

Writes Assets/Resources/Sprites/wanderer_0.png and wanderer_1.png (48x64).
"""
import bpy
import math
import os

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   "unity", "PointOfOrigin", "Assets", "Resources", "Sprites")
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.film_transparent = True
scene.render.resolution_x = 48
scene.render.resolution_y = 64
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.display.render_aa = "OFF"
shading = scene.display.shading
shading.light = "FLAT"
shading.color_type = "OBJECT"
shading.show_shadows = False
shading.show_cavity = False
shading.show_object_outline = True
shading.object_outline_color = (0.05, 0.04, 0.06)
# plain colours out, no filmic or AgX curve (it greyed the first render)
scene.view_settings.view_transform = "Standard"
scene.view_settings.look = "None"


def add(op, color, name, **kw):
    op(**kw)
    ob = bpy.context.active_object
    ob.name = name
    ob.color = (*color, 1.0)
    return ob


BODY = (1.0, 0.96, 0.86)
HEAD = (1.0, 0.72, 0.34)
LAMP = (1.0, 0.85, 0.55)
CLOAK = (0.86, 0.80, 0.70)

body = add(bpy.ops.mesh.primitive_cylinder_add, BODY, "body", radius=0.21, depth=0.50, location=(0, 0, 0.25), vertices=16)
cloak = add(bpy.ops.mesh.primitive_cone_add, CLOAK, "cloak", radius1=0.27, radius2=0.19, depth=0.34, location=(0, 0, 0.17), vertices=16)
head = add(bpy.ops.mesh.primitive_uv_sphere_add, HEAD, "head", radius=0.175, location=(0, 0, 0.70), segments=16, ring_count=10)
lamp = add(bpy.ops.mesh.primitive_cube_add, LAMP, "lamp", size=0.14, location=(0.33, 0, 0.36))
handle = add(bpy.ops.mesh.primitive_cylinder_add, BODY, "handle", radius=0.02, depth=0.22, location=(0.28, 0, 0.44), vertices=8)
handle.rotation_euler = (0, math.radians(35), 0)

bpy.ops.object.camera_add(location=(0, -10, 0.42), rotation=(math.radians(90), 0, 0))
cam = bpy.context.active_object
cam.data.type = "ORTHO"
cam.data.ortho_scale = 1.15
scene.camera = cam

frames = [
    ("wanderer_0", 0.0, 0.0),     # idle
    ("wanderer_1", 0.05, 0.12),   # a step: the body lifts and the lantern swings forward
]
for name, lift, swing in frames:
    body.location.z = 0.25 + lift
    cloak.location.z = 0.17 + lift
    head.location.z = 0.70 + lift
    lamp.location = (0.33 + swing, 0, 0.36 + lift * 0.5)
    handle.location = (0.28 + swing * 0.6, 0, 0.44 + lift * 0.5)
    scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("rendered", scene.render.filepath)

# the ember and the seed, each in its own tiny frame
for ob in (body, cloak, head, lamp, handle):
    ob.hide_render = True

EMBER = (1.0, 0.36, 0.12)
EMBER_CORE = (1.0, 0.89, 0.66)
ember = add(bpy.ops.mesh.primitive_ico_sphere_add, EMBER, "ember", radius=0.30, location=(0, 0, 0.40), subdivisions=1)
core = add(bpy.ops.mesh.primitive_uv_sphere_add, EMBER_CORE, "ember_core", radius=0.13, location=(0.04, -0.2, 0.44), segments=12, ring_count=8)
scene.render.resolution_x = 24
scene.render.resolution_y = 24
cam.data.ortho_scale = 0.8
cam.location = (0, -10, 0.40)
scene.render.filepath = os.path.join(OUT, "ember.png")
bpy.ops.render.render(write_still=True)
print("rendered", scene.render.filepath)
ember.hide_render = True
core.hide_render = True

SEED = (0.44, 0.89, 1.0)
SEED_CORE = (0.85, 0.98, 1.0)
top = add(bpy.ops.mesh.primitive_cone_add, SEED, "seed_top", radius1=0.22, radius2=0.0, depth=0.42, location=(0, 0, 0.61), vertices=6)
bottom = add(bpy.ops.mesh.primitive_cone_add, SEED, "seed_bottom", radius1=0.22, radius2=0.0, depth=0.30, location=(0, 0, 0.25), vertices=6)
bottom.rotation_euler = (math.radians(180), 0, 0)
glint = add(bpy.ops.mesh.primitive_uv_sphere_add, SEED_CORE, "seed_glint", radius=0.06, location=(0.06, -0.2, 0.52), segments=8, ring_count=6)
scene.render.resolution_x = 20
scene.render.resolution_y = 28
cam.data.ortho_scale = 0.95
cam.location = (0, -10, 0.42)
scene.render.filepath = os.path.join(OUT, "seed.png")
bpy.ops.render.render(write_still=True)
print("rendered", scene.render.filepath)
