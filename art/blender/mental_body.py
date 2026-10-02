"""The Mental Warrior's plain looks (CLS-03b, 2026-10-02): the Warrior's own bodies, recoloured.

    blender -b --factory-startup --python art/blender/mental_body.py

"A slightly different base look for the Mental Warrior, man and woman: the colour of the hair
perhaps, and a little something of the clothes" (your call). Nothing is reshaped: the man is the
Knight's head on the Ranger's body and the woman the lightened Rogue of woman_body.py, as the
Body Warrior wears them, with black hair in place of fair and of red-brown, and wine red and
slate cloth in place of the blue scarf and cream tunic, the teal bodice and its green hems.
Writes game/assets/temins/characters/mental_man_body.glb and mental_woman_body.glb, which
visuals.json grafts on the player as a Mental Warrior.
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

RANGER = {"Ranger_Body", "Ranger_ArmLeft", "Ranger_ArmRight", "Ranger_LegLeft", "Ranger_LegRight"}

# A dye is the colour a swatch's lightest shade becomes; the gradient under it is kept.
DYES = {
    "H": (0.17, 0.15, 0.17),  # hair: black, with enough light left in it to show its shape
    "R": (0.62, 0.11, 0.15),  # wine red
    "S": (0.30, 0.33, 0.40),  # slate
}

# Each texture is a sheet of swatches, 8 across and 4 down; "." stays as it is.
KNIGHT_HAIR = [
    ". H . . . . . .",
    ". . . . . . . .",
    ". . . . . . . .",
    ". . . . . . . .",
]

# The Ranger: the scarf's blue to wine red, the tunic's cream to slate.
RANGER_CLOTH = [
    ". . . . . S S .",
    "R . . . . . . .",
    ". R S . . . . .",
    ". . . . . . . .",
]

# The Rogue's head: every brown on it is hair or brow.
ROGUE_HAIR = [
    ". H . . . H H H",
    ". . . . . H H H",
    ". . . H H H H H",
    ". . . . . . . .",
]

# Her clothes: the teal bodice and skirt to wine red, the green hems and bands to slate.
ROGUE_CLOTH = [
    ". . . . . . . .",
    "R S . . . . . .",
    ". S . . . . . .",
    ". . . . . . . .",
]


def dye(image, sheet, name):
    """A copy of `image` with the swatches `sheet` names dyed."""
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


def wear(obj, material, image):
    """`obj` in `material`, a material of its own showing `image`."""
    for node in material.node_tree.nodes:
        if node.type == "TEX_IMAGE":
            node.image = image
    obj.data.materials[0] = material


def export(out):
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=False,
                              export_animations=False, export_skins=True, export_yup=True)
    print(f"[mental] -> {os.path.relpath(out, ROOT)}")


def man():
    """The Knight's head on the Ranger's body, on the Ranger's bones — the same bones, by name."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(KIT, "Ranger.glb"))
    for obj in list(bpy.data.objects):
        if obj.type == "MESH" and obj.name not in RANGER:
            bpy.data.objects.remove(obj, do_unlink=True)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    ours = set(bpy.data.objects)

    bpy.ops.import_scene.gltf(filepath=os.path.join(KIT, "Knight.glb"))
    head = bpy.data.objects["Knight_Head"]
    place = head.matrix_world.copy()
    head.parent = arm
    head.matrix_world = place
    for modifier in head.modifiers:
        if modifier.type == "ARMATURE":
            modifier.object = arm
    for obj in list(bpy.data.objects):
        if obj not in ours and obj is not head:
            bpy.data.objects.remove(obj, do_unlink=True)

    # A name of its own: the Knight's head it replaces is still on the player's model.
    head.name = "Mental_Head"

    hair = head.data.materials[0].copy()
    hair.name = "mental_hair"
    wear(head, hair, dye(image_of(head), KNIGHT_HAIR, "mental_hair"))

    body = bpy.data.objects["Ranger_Body"]
    cloth = body.data.materials[0].copy()
    cloth.name = "mental_cloth"
    image = dye(image_of(body), RANGER_CLOTH, "mental_cloth")
    for name in RANGER:
        wear(bpy.data.objects[name], cloth, image)

    export(os.path.join(OUT, "mental_man_body.glb"))


def woman_style(arm, image):
    """Run by woman_body.build once she is cut: her head in one dyed sheet, the rest in another."""
    body = bpy.data.objects["Rogue_Body"]
    base = body.data.materials[0]
    cloth, hair = base.copy(), base.copy()
    cloth.name, hair.name = "mental_cloth", "mental_hair"
    cloth_image, hair_image = dye(image, ROGUE_CLOTH, "mental_cloth"), dye(image, ROGUE_HAIR, "mental_hair")
    for name in w.KEEP:
        head = name == "Rogue_Head"
        wear(bpy.data.objects[name], hair if head else cloth, hair_image if head else cloth_image)


man()
w.build(os.path.join(OUT, "mental_woman_body.glb"), crop=True, style=woman_style)
