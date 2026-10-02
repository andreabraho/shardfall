"""The plain looks of the Archer, the Asura and the Dragonseer (CLS-04, 05, 06 — 2026-10-02).

    blender -b --factory-startup --python art/blender/class_bodies.py

"Take fitting models and change their look after how the original describes its characters"
(your call; the analysis is docs/09-archer-asura-dragonseer.md). All KayKit Adventurers 2.0, the
rig everything else wears, recoloured by their swatch sheets as the Ninja and the Mental Warrior
were:

- the Archer: the Ranger, quiver and all, in forest green and tan; the woman the lightened Rogue
  (woman_body.py) in moss and tan, fair-haired, with the Ranger's quiver on her back;
- the Asura: the Knight's face, white-haired, on the Ranger's body in black and crimson, the
  left arm dark violet — the mark; the woman the Rogue the same, white-haired, the same arm;
- the Dragonseer: the Mage without his hat, the robe ivory and jade with a gold belt; the woman
  the Rogue's face, dark-haired, on the same robe.

Writes game/assets/temins/characters/<class>_<man|woman>_body.glb, which visuals.json grafts on
the player.
"""

import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)

import woman_body as w  # noqa: E402

KIT = os.path.join(ROOT, "game", "assets", "kaykit", "adventurers2")
OUT = os.path.join(ROOT, "game", "assets", "temins", "characters")

RANGER = ["Ranger_Body", "Ranger_ArmLeft", "Ranger_ArmRight", "Ranger_LegLeft", "Ranger_LegRight"]
MAGE = ["Mage_Body", "Mage_ArmLeft", "Mage_ArmRight", "Mage_LegLeft", "Mage_LegRight"]

# A dye is the colour a swatch's lightest shade becomes; the gradient under it is kept.
DYES = {
    "F": (0.14, 0.40, 0.22),  # forest green
    "O": (0.66, 0.58, 0.38),  # tan
    "M": (0.20, 0.42, 0.24),  # moss
    "B": (0.92, 0.76, 0.42),  # fair hair
    "K": (0.17, 0.16, 0.20),  # black cloth
    "R": (0.66, 0.10, 0.14),  # crimson
    "W": (0.95, 0.95, 0.98),  # white hair
    "V": (0.42, 0.26, 0.62),  # the marked arm
    "I": (0.93, 0.92, 0.86),  # ivory
    "J": (0.16, 0.58, 0.50),  # jade
    "D": (0.10, 0.34, 0.32),  # dark jade
    "G": (0.95, 0.74, 0.25),  # gold
    "N": (0.20, 0.24, 0.40),  # blue-black hair
}

# Each texture is a sheet of swatches, 8 across and 4 down; "." stays as it is.
SHEETS = {
    # The Ranger: the scarf's blue, the tunic's cream.
    "archer_cloth": [". . . . . O O .", "F . . . . . . .", ". F O . . . . .", ". . . . . . . ."],
    "asura_cloth": [". . . . . K K .", "R . . . . . . .", ". R K . . . . .", ". . . . . . . ."],
    "asura_arm": ["V . . . . K K .", "R . . . . . . .", ". R K . . . . .", ". . . . . . . ."],
    # The Knight's head: its hair.
    "white_hair_knight": [". W . . . . . .", ". . . . . . . .", ". . . . . . . .", ". . . . . . . ."],
    # The Rogue: the teal bodice and skirt, the green hems and bands; every brown on her head is hair.
    "archer_woman_cloth": [". . . . . . . .", "M O . . . . . .", ". O . . . . . .", ". . . . . . . ."],
    "asura_woman_cloth": [". . . . . . . .", "K R . . . . . .", ". R . . . . . .", ". . . . . . . ."],
    "asura_woman_arm": ["V . . . . . . .", "K R . . . . . .", ". R . . . . . .", ". . . . . . . ."],
    "fair_hair": [". B . . . B B B", ". . . . . B B B", ". . . B B B B B", ". . . . . . . ."],
    "white_hair": [". W . . . W W W", ". . . . . W W W", ". . . W W W W W", ". . . . . . . ."],
    "dark_hair": [". N . . . N N N", ". . . . . N N N", ". . . N N N N N", ". . . . . . . ."],
    # The Mage: the purple robe, its dark lining, the magenta, the orange belt.
    "seer_cloth": [". . . . . G . .", "I I J . . . . D", ". J . . . . . .", ". . . . . . . ."],
}


def dye(image, name):
    """A copy of `image` with the swatches SHEETS[name] names dyed."""
    sheet = SHEETS[name]
    width, height = image.size
    pixels = np.array(image.pixels[:], dtype=np.float32).reshape(height, width, 4)
    rows, columns = len(sheet), len(sheet[0].split())
    for row, line in enumerate(sheet):
        for column, key in enumerate(line.split()):
            if key == ".":
                continue
            # Pixels run from the bottom row up.
            y0, y1 = height - (row + 1) * height // rows, height - row * height // rows
            x0, x1 = column * width // columns, (column + 1) * width // columns
            block = pixels[y0:y1, x0:x1, :3]
            light = block.max(axis=2, keepdims=True)
            block[:] = np.array(DYES[key], dtype=np.float32) * (light / light.max())
    dyed = bpy.data.images.new(name, width, height, alpha=False)
    dyed.pixels = pixels.ravel()
    dyed.pack()
    return dyed


def image_of(obj):
    return next(n.image for n in obj.data.materials[0].node_tree.nodes if n.type == "TEX_IMAGE")


def wear(names, sheet):
    """The objects `names` in one material of their own, their texture dyed by `sheet`."""
    first = bpy.data.objects[names[0]]
    material = first.data.materials[0].copy()
    material.name = sheet
    image = dye(image_of(first), sheet)
    for node in material.node_tree.nodes:
        if node.type == "TEX_IMAGE":
            node.image = image
    for name in names:
        bpy.data.objects[name].data.materials[0] = material


def take(file, keep, arm=None):
    """Imports `file` and keeps the meshes `keep`. With `arm`, they are hung on that armature —
    every KayKit character has the same bones by name — and what came with them is dropped."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=file if os.path.isabs(file) else os.path.join(KIT, file))
    fresh = set(bpy.data.objects) - before
    for obj in list(fresh):
        if obj.type == "MESH" and obj.name not in keep:
            fresh.discard(obj)
            bpy.data.objects.remove(obj, do_unlink=True)
    if arm is None:
        return next(o for o in fresh if o.type == "ARMATURE")
    for obj in list(fresh):
        if obj.type != "MESH":
            continue
        place = obj.matrix_world.copy()
        obj.parent = arm
        obj.matrix_world = place
        for modifier in obj.modifiers:
            if modifier.type == "ARMATURE":
                modifier.object = arm
        fresh.discard(obj)
    for obj in fresh:
        bpy.data.objects.remove(obj, do_unlink=True)
    return arm


def export(name):
    out = os.path.join(OUT, name + ".glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=False,
                              export_animations=False, export_skins=True, export_yup=True)
    print(f"[class] -> {os.path.relpath(out, ROOT)}")


def archer_man():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    take("Ranger.glb", RANGER + ["Ranger_Head", "Ranger_Quiver"])
    # A name of its own: the Knight's head it replaces is still on the player's model.
    bpy.data.objects["Ranger_Head"].name = "Archer_Head"
    wear(RANGER, "archer_cloth")
    export("archer_man_body")


def asura_man():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    arm = take("Ranger.glb", RANGER)
    take("Knight.glb", ["Knight_Head"], arm)
    bpy.data.objects["Knight_Head"].name = "Asura_Head"
    wear(["Asura_Head"], "white_hair_knight")
    wear([n for n in RANGER if n != "Ranger_ArmLeft"], "asura_cloth")
    wear(["Ranger_ArmLeft"], "asura_arm")
    export("asura_man_body")


def seer_man():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    take("Mage.glb", MAGE + ["Mage_Head"])
    bpy.data.objects["Mage_Head"].name = "Seer_Head"
    wear(MAGE, "seer_cloth")
    export("seer_man_body")


def seer_woman():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    arm = take("Mage.glb", MAGE)
    take("Rogue.glb", ["Rogue_Head"], arm)
    bpy.data.objects["Rogue_Head"].name = "Seer_Head"
    wear(["Seer_Head"], "dark_hair")
    wear(MAGE, "seer_cloth")
    export("seer_woman_body")


def archer_woman_style(arm, image):
    """Run by woman_body.build once she is cut: moss and tan, fair hair, the Ranger's quiver."""
    wear([n for n in w.KEEP if n != "Rogue_Head"], "archer_woman_cloth")
    wear(["Rogue_Head"], "fair_hair")
    take("Ranger.glb", ["Ranger_Quiver"], arm)


def asura_woman_style(arm, image):
    wear([n for n in w.KEEP if n not in ("Rogue_Head", "Rogue_ArmLeft")], "asura_woman_cloth")
    wear(["Rogue_ArmLeft"], "asura_woman_arm")
    wear(["Rogue_Head"], "white_hair")


archer_man()
asura_man()
seer_man()
seer_woman()
w.build(os.path.join(OUT, "archer_woman_body.glb"), style=archer_woman_style)
w.build(os.path.join(OUT, "asura_woman_body.glb"), style=asura_woman_style)
