"""The player's portraits for the new-game page (CLS-00, 2026-09-30): a man and a woman, head
and shoulders, as the game dresses them — the Knight's head on the Ranger's plain body, and the
Rogue's head and body.

    blender -b --factory-startup --python art/blender/portraits.py

Writes game/assets/ui/portrait_man.png and portrait_woman.png, 256 px, on a clear ground.
"""

import math
import os

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
KIT = os.path.join(ROOT, "game", "assets", "kaykit", "adventurers2")
OUT = os.path.join(ROOT, "game", "assets", "ui")

# Who is drawn: (file, parts kept) — the same pieces visuals.json grafts on the player.
LOOKS = {
    "man": [("Knight.glb", ["Knight_Head"]),
            ("Ranger.glb", ["Ranger_Body", "Ranger_ArmLeft", "Ranger_ArmRight"])],
    "woman": [("Rogue.glb", ["Rogue_Head", "Rogue_Body", "Rogue_ArmLeft", "Rogue_ArmRight"])],
}


def portrait(name, pieces):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    keep = []
    for file, parts in pieces:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=os.path.join(KIT, file))
        for obj in set(bpy.data.objects) - before:
            if obj.type == "MESH":
                if obj.name in parts:
                    keep.append(obj)
                else:
                    bpy.data.objects.remove(obj, do_unlink=True)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = 256
    scene.render.film_transparent = True
    scene.view_settings.view_transform = "Standard"

    world = bpy.data.worlds.new("Sky")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.6, 0.62, 0.66, 1)
    scene.world = world
    for energy, rot in ((3.0, (55, 5, 25)), (1.0, (60, -15, -150))):
        sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
        sun.data.energy = energy
        sun.rotation_euler = tuple(math.radians(a) for a in rot)
        scene.collection.objects.link(sun)

    # Head and shoulders, a little from the side, as a face turned to the player.
    zs = [(o.matrix_world @ Vector(c)).z for o in keep for c in o.bound_box]
    top = max(zs)
    centre = Vector((0, 0, top - 0.62))
    cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 1.8
    scene.collection.objects.link(cam)
    scene.camera = cam
    angle = math.radians(20)
    cam.location = centre + Vector((math.sin(angle), -math.cos(angle), 0.12)).normalized() * 6
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()

    os.makedirs(OUT, exist_ok=True)
    scene.render.filepath = os.path.join(OUT, f"portrait_{name}.png")
    bpy.ops.render.render(write_still=True)
    print(f"[portrait] {name}")


for look, pieces in LOOKS.items():
    portrait(look, pieces)
