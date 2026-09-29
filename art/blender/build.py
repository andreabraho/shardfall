"""Builds one model headless, exports it for the game and renders it to be judged (PIP-01).

    blender -b --factory-startup --python art/blender/build.py -- <model> [--render <folder>]

<model> is a file in art/blender/models without .py. Its build() makes the pieces in an empty
scene and returns where the model goes: {"folder": "weapons", "name": "fang_blade"}. The .glb
lands in game/assets/temins/<folder>/<name>.glb. With --render, four views go to <folder> as
<name>_0.png ... <name>_3.png, lit like a sunny map, so a model can be judged without Godot.
"""

import importlib
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "models"))

import temins  # noqa: E402


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not args:
        raise SystemExit("usage: build.py -- <model> [--render <folder>]")

    model = args[0]
    render = args[args.index("--render") + 1] if "--render" in args else None

    temins.reset()
    where = importlib.import_module(model).build()

    out_dir = os.path.join(ROOT, "game", "assets", "temins", where["folder"])
    os.makedirs(out_dir, exist_ok=True)
    out = os.path.join(out_dir, where["name"] + ".glb")
    temins.export_glb(out)

    size = bounds()[0]
    print(f"[blender] {model}: {temins.triangles()} triangles, "
          f"{size[0]:.2f} x {size[1]:.2f} x {size[2]:.2f} m -> {os.path.relpath(out, ROOT)}")

    if render:
        turntable(render, where["name"], where.get("views", 4))


def bounds():
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        for corner in obj.bound_box:
            world = obj.matrix_world @ Vector(corner)
            lo = Vector(map(min, lo, world))
            hi = Vector(map(max, hi, world))
    return hi - lo, lo, hi


def turntable(folder, name, views):
    os.makedirs(folder, exist_ok=True)
    size, lo, hi = bounds()
    centre = (lo + hi) / 2
    reach = max(size) * 0.62

    scene = bpy.context.scene
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue

    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.film_transparent = False
    # AgX rolls bright colours off the way the game's tonemapper does, instead of clipping them.
    scene.view_settings.view_transform = "AgX"

    world = bpy.data.worlds.new("Sky")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.42, 0.50, 0.58, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9
    scene.world = world

    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.2
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(35))
    scene.collection.objects.link(sun)

    # A ground to catch the shadow, so size and footing read.
    bpy.ops.mesh.primitive_plane_add(size=reach * 6, location=(centre.x, centre.y, lo.z))
    ground = bpy.context.active_object
    ground.data.materials.append(temins.material("ground", (0.30, 0.34, 0.24), rough=1.0))

    cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    cam.data.lens = 50
    scene.collection.objects.link(cam)
    scene.camera = cam

    for i in range(views):
        angle = math.radians(35 + i * 360 / views)
        cam.location = centre + Vector((math.sin(angle), -math.cos(angle), 0.35)).normalized() * reach * 2.6
        cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(folder, f"{name}_{i}.png")
        bpy.ops.render.render(write_still=True)


main()
