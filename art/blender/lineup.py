"""Renders built models side by side, flat on, to compare a family of them (PIP-02).

    blender -b --factory-startup --python art/blender/lineup.py -- <out.png> <a.glb> <b.glb> ...
"""

import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import temins  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
out, files = args[0], args[1:]

temins.reset()

x = 0.0
gap = 0.25
lo_z, hi_z = 1e9, -1e9

for path in files:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]

    xs = [(o.matrix_world @ Vector(c)).x for o in new for c in o.bound_box]
    zs = [(o.matrix_world @ Vector(c)).z for o in new for c in o.bound_box]
    width = max(xs) - min(xs)

    for o in new:
        o.location.x += x - min(xs)

    x += width + gap
    lo_z, hi_z = min(lo_z, min(zs)), max(hi_z, max(zs))

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.view_settings.view_transform = "Standard"
scene.render.resolution_x = 1800
scene.render.resolution_y = 900

world = bpy.data.worlds.new("Sky")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.42, 0.50, 0.58, 1)
scene.world = world

sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 3.0
sun.rotation_euler = (math.radians(40), math.radians(25), math.radians(-20))
scene.collection.objects.link(sun)

span = x - gap
cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
cam.data.type = "ORTHO"
cam.data.ortho_scale = max(span, (hi_z - lo_z) * 2) * 1.08
cam.location = (span / 2, -10, (lo_z + hi_z) / 2)
cam.rotation_euler = (math.radians(90), 0, 0)
scene.collection.objects.link(cam)
scene.camera = cam

scene.render.filepath = out
bpy.ops.render.render(write_still=True)
