"""Close-ups of a creature's face, from the front and the side, and of its paws (PIP-03).

    blender -b --factory-startup --python art/blender/closeup.py -- <model.glb> <out prefix>

Writes <prefix>_face.png, <prefix>_face_side.png and <prefix>_paws.png. A creature seen whole
hides its faults; its eyes, mouth and feet are what the player looks at when it bites.
"""

import math
import sys

import bpy
from mathutils import Vector

glb, out = sys.argv[sys.argv.index("--") + 1:][:2]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
for obj in list(bpy.data.objects):
    if obj.type == "MESH" and not obj.parent:
        bpy.data.objects.remove(obj)

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = scene.render.resolution_y = 420
scene.view_settings.view_transform = "AgX"
world = bpy.data.worlds.new("Sky")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.42, 0.5, 0.58, 1)
scene.world = world
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 3.2
sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(35))
scene.collection.objects.link(sun)
bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0, 0))
ground = bpy.context.active_object
grass = bpy.data.materials.new("ground")
grass.use_nodes = True
grass.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.3, 0.34, 0.24, 1)
ground.data.materials.append(grass)

cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
scene.collection.objects.link(cam)
scene.camera = cam


def middle(bone):
    b = arm.data.bones[bone]
    return arm.matrix_world @ ((b.head_local + b.tail_local) / 2)


zs = [(o.matrix_world @ Vector(c)).z for o in bpy.data.objects if o.type == "MESH" and o.parent for c in o.bound_box]
reach = (max(zs) - min(zs)) * 0.32

for label, centre, distance, yaw, pitch in (
    ("face", middle("head"), reach * 2.4, 25, 8),
    ("face_side", middle("head"), reach * 2.4, 90, 5),
    ("paws", middle("front.L.paw"), reach * 2.2, 40, 15),
):
    a, p = math.radians(yaw), math.radians(pitch)
    cam.location = centre + Vector((math.sin(a) * math.cos(p), -math.cos(a) * math.cos(p), math.sin(p))) * distance
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = f"{out}_{label}.png"
    bpy.ops.render.render(write_still=True)
