"""Renders an item's bag icon from the model it is worn as (2026-09-30).

    blender -b --factory-startup --python art/blender/icons.py -- [item ids...]

With no ids, every weapon, armour and helmet whose visual is a model of ours. Each icon is a
PNG on a clear ground in game/assets/icons/items/<item id>.png, shaped like the item's cell in
the bag: a sword tall and narrow (1 x 3), an armour 2 x 3, a helmet square. It is drawn in its
own colours; the bag shows the rarity round it.
"""

import json
import math
import os
import re
import sys

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)

import temins  # noqa: E402
import trinkets  # noqa: E402
DATA = os.path.join(ROOT, "game", "data")
OUT = os.path.join(ROOT, "game", "assets", "icons", "items")

# Pixels per bag cell: crisp at the bag's size with room to spare.
CELL = 128

# The height, as the icon camera sees it, of the longest great sword: every weapon is drawn
# at the scale that fits this one.
WEAPON_HEIGHT = 2.62


def load_json(path):
    """The content files carry // comments, which JSON does not."""
    with open(path, encoding="utf-8") as f:
        text = f.read()
    return json.loads(re.sub(r"^\s*//.*$", "", text, flags=re.M))


def items():
    visuals = {v["id"]: v for v in load_json(os.path.join(DATA, "tables", "visuals.json"))}
    found = []
    for name in ("weapons.json", "armor.json", "accessories.json"):
        for item in load_json(os.path.join(DATA, "items", name)):
            # Gear never drawn on the character is built for its icon alone.
            if item["id"] in trinkets.BUILDERS:
                found.append((item["id"], None, item.get("grid_size", [1, 1])))
                continue
            look = visuals.get(item.get("visual", ""))
            if not look or look.get("primitive") != "model" or "/temins/" not in look.get("model_path", ""):
                continue
            path = os.path.join(ROOT, "game", look["model_path"].replace("res://", ""))
            found.append((item["id"], path, item.get("grid_size", [1, 1])))
    return found


def clear():
    # Through the kit, so its cache of materials is emptied with the scene.
    temins.reset()


def meshes():
    return [o for o in bpy.data.objects if o.type == "MESH"]


def bounds(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def render(item_id, path, cell):
    clear()
    if path is None:
        trinkets.build(item_id)
    else:
        bpy.ops.import_scene.gltf(filepath=path)

    # Forearm bracers stand out sideways in the rest pose: an armour's icon is its body.
    for o in list(bpy.data.objects):
        if o.name.startswith("bone__lowerarm"):
            bpy.data.objects.remove(o, do_unlink=True)

    kind = item_id.split("_")[0]

    # A bow lies along Y, as the hand holds it (bowkit.py): stood up for its picture, as a sword stands.
    if item_id.endswith("_bow"):
        for o in meshes():
            o.matrix_world = Matrix.Rotation(math.radians(90), 4, "X") @ o.matrix_world
        bpy.context.view_layer.update()

    # A fan is held with its leaf facing forward (fankit.py): turned flat on for its picture.
    if item_id.endswith("_fan"):
        for o in meshes():
            o.matrix_world = Matrix.Rotation(math.radians(-90), 4, "Z") @ o.matrix_world
        bpy.context.view_layer.update()

    objs = meshes()
    lo, hi = bounds(objs)
    centre = (lo + hi) / 2

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.view_settings.view_transform = "Standard"
    scene.render.film_transparent = True
    scene.render.resolution_x = CELL * cell[0]
    scene.render.resolution_y = CELL * cell[1]

    world = bpy.data.worlds.new("Sky")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.58, 0.62, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
    scene.world = world

    for energy, rot in ((3.2, (50, 10, 30)), (1.2, (60, -20, -140))):
        sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
        sun.data.energy = energy
        sun.rotation_euler = tuple(math.radians(a) for a in rot)
        scene.collection.objects.link(sun)

    cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    cam.data.type = "ORTHO"
    scene.collection.objects.link(cam)
    scene.camera = cam

    # A sword seen on the flat of its blade, turned a little so it has depth; worn gear from a
    # little to the right of the front and a little above.
    yaw, pitch = {
        "wpn": (18, 4), "rng": (20, 32), "ear": (20, 10), "nck": (8, 6),
        "shd": (22, 8), "bts": (35, 16), "brc": (22, 18),
    }.get(kind, (24, 5))
    direction = Vector((math.sin(math.radians(yaw)), -math.cos(math.radians(yaw)), math.tan(math.radians(pitch)))).normalized()
    cam.location = centre + direction * 10
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()

    # Fit the ortho frame to the item as the camera sees it, with a margin, and centre it on
    # what it sees. The camera's matrix only follows its new place after an update.
    bpy.context.view_layer.update()
    view = cam.matrix_world.inverted()
    pts = [view @ (o.matrix_world @ Vector(c)) for o in objs for c in o.bound_box]
    width = max(p.x for p in pts) - min(p.x for p in pts)
    height = max(p.y for p in pts) - min(p.y for p in pts)
    middle = Vector(((max(p.x for p in pts) + min(p.x for p in pts)) / 2, (max(p.y for p in pts) + min(p.y for p in pts)) / 2, 0))
    cam.location += cam.matrix_world.to_3x3() @ middle
    aspect = cell[0] / cell[1]
    if kind == "wpn":
        # Every weapon at one scale, the longest great sword filling the cell: a sword is then
        # plainly shorter than a great sword in the bag (2026-09-30, at your call).
        cam.data.sensor_fit = "VERTICAL"
        # A shorter cell shows less of that height: a dagger, two cells tall, at the same scale.
        cam.data.ortho_scale = max(WEAPON_HEIGHT * cell[1] / 3, width / aspect) * 1.06
    elif aspect < 1:
        cam.data.sensor_fit = "VERTICAL"
        cam.data.ortho_scale = max(height, width / aspect) * 1.1
    else:
        cam.data.sensor_fit = "AUTO"
        cam.data.ortho_scale = max(width, height) * 1.1

    os.makedirs(OUT, exist_ok=True)
    scene.render.filepath = os.path.join(OUT, item_id + ".png")
    bpy.ops.render.render(write_still=True)
    source = os.path.relpath(path, ROOT) if path else "built for the icon"
    print(f"[icon] {item_id} {cell[0]}x{cell[1]} <- {source}")


def main():
    wanted = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for item_id, path, cell in items():
        if not wanted or item_id in wanted:
            render(item_id, path, cell)


main()
