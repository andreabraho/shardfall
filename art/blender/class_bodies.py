"""The plain looks of the Archer, the Asura and the Dragonseer (CLS-04, 05, 06 — 2026-10-02).

    blender -b --factory-startup --python art/blender/class_bodies.py

"Take fitting models and change their look after how the original describes its characters"
(your call; the analysis is docs/09-archer-asura-dragonseer.md). All KayKit Adventurers 2.0, the
rig everything else wears, recoloured by their swatch sheets as the Ninja and the Mental Warrior
were:

- the Archer: dressed as the Ninja (ninja_body.py) but for a detail, as the Mental Warrior is the
  Body Warrior but for one (your call, 2026-10-02: in the original the two are one class) — the
  hood, the hems and the bands ochre where the Ninja's are green, the leather and her hair
  chestnut — and the Ranger's quiver on the back;
- the Asura, dark and arcane (your call, 2026-10-02: "much more arcane and darker"): ashen skin,
  white hair and violet eyes; black cloth and black leather with deep violet at the neck and the
  hems; the left arm — the mark — violet and lit from within. The man the Knight's face on the
  Ranger's body, the woman the Rogue;
- the Dragonseer: the Mage without his hat, the robe ivory and jade with a gold belt. The woman
  was the Rogue's face on the same robe; at your call ("the women a little more uncovered",
  2026-10-02) she is the lightened Rogue now, as the Ninja woman is: an ivory bodice cut short over a
  bare midriff, bare shoulders and arms, a jade wrap round the hips over bare legs, her hair dark.
  The Asura woman's bodice is cut short the same way.

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

import ninja_body as nb  # noqa: E402
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
    "K": (0.10, 0.09, 0.13),  # black cloth
    "R": (0.66, 0.10, 0.14),  # crimson
    "P": (0.36, 0.13, 0.54),  # deep violet
    "B": (0.13, 0.11, 0.15),  # black leather
    "D": (0.22, 0.21, 0.27),  # dark linen
    "A": (0.80, 0.76, 0.84),  # ashen skin
    "E": (0.80, 0.50, 1.00),  # violet eyes
    "W": (0.95, 0.95, 0.98),  # white hair
    "V": (0.42, 0.26, 0.62),  # the marked arm
    "I": (0.93, 0.92, 0.86),  # ivory
    "J": (0.16, 0.58, 0.50),  # jade
    "D": (0.10, 0.34, 0.32),  # dark jade
    "G": (0.95, 0.74, 0.25),  # gold
    "N": (0.20, 0.24, 0.40),  # blue-black hair
    "S": (0.62, 0.42, 0.16),  # soft gold leather
}

# Each texture is a sheet of swatches, 8 across and 4 down; "." stays as it is.
SHEETS = {
    # The Ranger: the scarf's blue, the tunic's cream.
    "archer_cloth": [". . . . . O O .", "F . . . . . . .", ". F O . . . . .", ". . . . . . . ."],
    # The Asura on the Ranger: the scarf violet, the tunic black, every leather black, the skin ashen.
    "asura_cloth": ["A B . . . K K B", "P B D . B B B B", "D P K B . . . B", ". . . . . . . ."],
    "asura_arm": ["V B . . . K K B", "P B D . B B B B", "D P K B . . . B", ". . . . . . . ."],
    # The Knight's head: its hair white, its skin ashen, its eyes violet.
    "asura_head_knight": ["A W E . . . . .", ". . . . . . . .", ". . . . . . . .", ". . . . . . . ."],
    # The Rogue: the teal bodice and skirt, the green hems and bands; every brown on her head is hair.
    "archer_woman_cloth": [". . . . . . . .", "M O . . . . . .", ". O . . . . . .", ". . . . . . . ."],
    "asura_woman_cloth": ["A B . . . B B B", "K P . . . B B B", ". P . B B B B B", ". . . . . . . ."],
    "asura_woman_arm": ["V B . . . B B B", "K P . . . B B B", ". P . B B B B B", ". . . . . . . ."],
    "asura_woman_head": ["A W E . . W W W", ". . . . . W W W", ". . . W W W W W", ". . . . . . . ."],
    "fair_hair": [". B . . . B B B", ". . . . . B B B", ". . . B B B B B", ". . . . . . . ."],
    "white_hair": [". W . . . W W W", ". . . . . W W W", ". . . W W W W W", ". . . . . . . ."],
    "dark_hair": [". N . . . N N N", ". . . . . N N N", ". . . N N N N N", ". . . . . . . ."],
    # The Mage: the purple robe, its dark lining, the magenta, the orange belt.
    "seer_cloth": [". . . . . G . .", "I I J . . . . D", ". J . . . . . .", ". . . . . . . ."],
    # The Rogue as the Dragonseer woman: the bodice and the wrap ivory, the hems jade, the leathers gold.
    "seer_woman_cloth": [". S . . . S S I", "I J . . . S S I", ". J . S S S I S", ". . . . . . . ."],
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


def wear(names, sheet, glow=0.0):
    """The objects `names` in one material of their own, their texture dyed by `sheet`; with
    `glow`, lit from within in violet (the Asura's marked arm)."""
    first = bpy.data.objects[names[0]]
    material = first.data.materials[0].copy()
    material.name = sheet
    image = dye(image_of(first), sheet)
    for node in material.node_tree.nodes:
        if node.type == "TEX_IMAGE":
            node.image = image
    if glow > 0:
        bsdf = next(n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        bsdf.inputs["Emission Color"].default_value = (0.55, 0.25, 1.0, 1.0)
        bsdf.inputs["Emission Strength"].default_value = glow
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


# The Ninja's dyes with the Archer's detail: ochre for green, chestnut for dark leather.
ARCHER = {**nb.DYES, "G": (0.66, 0.44, 0.14), "L": (0.42, 0.25, 0.14)}


def quiver(arm):
    """The Ranger's quiver on the back."""
    take("Ranger.glb", ["Ranger_Quiver"], arm)


def asura_man():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    arm = take("Ranger.glb", RANGER)
    take("Knight.glb", ["Knight_Head"], arm)
    bpy.data.objects["Knight_Head"].name = "Asura_Head"
    wear(["Asura_Head"], "asura_head_knight")
    wear([n for n in RANGER if n != "Ranger_ArmLeft"], "asura_cloth")
    wear(["Ranger_ArmLeft"], "asura_arm", glow=0.9)
    export("asura_man_body")


def seer_man():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    take("Mage.glb", MAGE + ["Mage_Head"])
    bpy.data.objects["Mage_Head"].name = "Seer_Head"
    wear(MAGE, "seer_cloth")
    export("seer_man_body")


def seer_woman_style(arm, image):
    nb.bare_legs(arm, image)
    wear([n for n in w.KEEP if n != "Rogue_Head"], "seer_woman_cloth")
    wear(["Rogue_Head"], "dark_hair")


def asura_woman_style(arm, image):
    wear([n for n in w.KEEP if n not in ("Rogue_Head", "Rogue_ArmLeft")], "asura_woman_cloth")
    wear(["Rogue_ArmLeft"], "asura_woman_arm", glow=0.9)
    wear(["Rogue_Head"], "asura_woman_head")


asura_man()
seer_man()
nb.man("archer_man_body", ARCHER, quiver)
nb.woman("archer_woman_body", ARCHER, quiver)
w.build(os.path.join(OUT, "asura_woman_body.glb"), crop=True, style=asura_woman_style)
w.build(os.path.join(OUT, "seer_woman_body.glb"), crop=True, style=seer_woman_style)
