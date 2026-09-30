"""Renders frames from a creature's clips, to judge its animation without Godot (PIP-03).

    blender -b --factory-startup --python art/blender/poses.py -- <model.glb> <out folder> <name>

Optional, in the environment:
    POSE_CLIPS  which clips and where in each, 0 to 1: "Idle:0,.5;Walk:0,.25,.5,.75;Run:0,.5;..."
    POSE_YAW    the camera's angle round the creature, in degrees (70 by default; 90 is side on)

Each frame goes to <out>/<name>_<nn>_<clip>.png. The file is imported the way the game will
read it, so this also proves the clips survived the export.
"""

import math
import os
import sys

import bpy
from mathutils import Vector

glb, out, name = sys.argv[sys.argv.index("--") + 1:][:3]
os.makedirs(out, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")

# The importer adds a sphere of its own to draw the bones with: not part of the creature.
meshes = [o for o in bpy.data.objects if o.type == "MESH" and o.parent]

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = scene.render.resolution_y = 360
scene.view_settings.view_transform = "AgX"
world = bpy.data.worlds.new("Sky")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.42, 0.5, 0.58, 1)
scene.world = world
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 3.2
sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(35))
scene.collection.objects.link(sun)

pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
lo = Vector([min(p[i] for p in pts) for i in range(3)])
hi = Vector([max(p[i] for p in pts) for i in range(3)])
centre, reach = (lo + hi) / 2, max(hi - lo) * 0.75

bpy.ops.mesh.primitive_plane_add(size=reach * 8, location=(centre.x, centre.y, lo.z))
ground = bpy.context.active_object
grass = bpy.data.materials.new("ground")
grass.use_nodes = True
grass.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.3, 0.34, 0.24, 1)
ground.data.materials.append(grass)

cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
scene.collection.objects.link(cam)
scene.camera = cam
angle = math.radians(float(os.environ.get("POSE_YAW", "70")))
cam.location = centre + Vector((math.sin(angle), -math.cos(angle), 0.3)).normalized() * reach * 2.6
cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()

arm.animation_data_create()
for track in list(arm.animation_data.nla_tracks):
    arm.animation_data.nla_tracks.remove(track)

wanted = os.environ.get("POSE_CLIPS", "Idle:0,.5;Walk:0,.5;Run:0,.25,.5,.75;Attack:.3,.45,.55;HitReact:.3;Death:.25,.7,1")
k = 0
for spec in wanted.split(";"):
    clip, times = spec.split(":")
    action = next((a for a in bpy.data.actions if a.name.split("|")[-1] == clip), None)
    if action is None:
        print(f"[poses] no clip called {clip}")
        continue
    arm.animation_data.action = action
    if hasattr(arm.animation_data, "action_slot") and action.slots:
        arm.animation_data.action_slot = action.slots[0]
    a, b = action.frame_range
    for u in times.split(","):
        scene.frame_set(int(round(a + (b - a) * float(u))))
        scene.render.filepath = os.path.join(out, f"{name}_{k:02d}_{clip}.png")
        k += 1
        bpy.ops.render.render(write_still=True)

print("[poses] clips:", sorted(a.name for a in bpy.data.actions))
