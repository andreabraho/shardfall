"""The Ninja's plain clothes (CLS-03, 2026-10-01): the KayKit Rogues in dark cloth.

    blender -b --factory-startup --python art/blender/ninja_body.py

"Dark clothes, a dark green is fine, and the woman a little more uncovered" (your call). The
man is the hooded and masked Rogue as he is; the woman is the lightened Rogue of woman_body.py
with the bodice cut short over a bare midriff. Both wear the Rogue's texture dyed: its teal to
charcoal, its green to a dark green, its leathers to a dark brown; skin, eyes and metal as they
were. Writes game/assets/temins/characters/ninja_man_body.glb and ninja_woman_body.glb, which
visuals.json grafts on the player as a Ninja.
"""

import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)

import woman_body as w  # noqa: E402

HOODED = os.path.join(ROOT, "game", "assets", "kaykit", "adventurers2", "Rogue_Hooded.glb")
OUT = os.path.join(ROOT, "game", "assets", "temins", "characters")

MAN = {"RogueHooded_Head", "RogueHooded_Mask", "RogueHooded_Body", "RogueHooded_ArmLeft", "RogueHooded_ArmRight",
       "RogueHooded_LegLeft", "RogueHooded_LegRight"}

# The texture is a sheet of swatches, 8 across and 4 down, each a gradient from light to dark.
# What each is dyed to, from the top row; "." stays as it is (skin, eyes, metal, the unused).
SHEET = [
    ". L . . . L L T",
    "C G . . . L L T",
    ". G . L L L T L",
    ". . . . . . . .",
]

# A dye is the colour a swatch's lightest shade becomes; the gradient under it is kept.
DYES = {
    "C": (0.12, 0.14, 0.15),  # cloth: the tunic, the skirt, the mask
    "G": (0.09, 0.30, 0.17),  # the hood, the hems, the bands
    "L": (0.23, 0.15, 0.12),  # leather: belt, boots, bracers — and the hair
    "T": (0.16, 0.14, 0.14),  # trousers
}


def dye(image):
    """A copy of the Rogue's texture in the Ninja's colours."""
    width, height = image.size
    pixels = np.array(image.pixels[:], dtype=np.float32).reshape(height, width, 4)
    rows, columns = len(SHEET), len(SHEET[0].split())
    for row, line in enumerate(SHEET):
        for column, key in enumerate(line.split()):
            if key == ".":
                continue
            # Pixels run from the bottom row up.
            y0, y1 = height - (row + 1) * height // rows, height - row * height // rows
            x0, x1 = column * width // columns, (column + 1) * width // columns
            block = pixels[y0:y1, x0:x1, :3]
            light = block.max(axis=2, keepdims=True)
            block[:] = np.array(DYES[key], dtype=np.float32) * (light / light.max())
    dyed = bpy.data.images.new("ninja_cloth", width, height, alpha=False)
    dyed.pixels = pixels.ravel()
    dyed.pack()
    return dyed


def man():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=HOODED)
    for obj in list(bpy.data.objects):
        if obj.type == "MESH" and obj.name not in MAN:
            bpy.data.objects.remove(obj, do_unlink=True)
    w.wear(dye(next(i for i in bpy.data.images if i.size[0] > 0)))
    out = os.path.join(OUT, "ninja_man_body.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=False,
                              export_animations=False, export_skins=True, export_yup=True)
    print(f"[ninja] -> {os.path.relpath(out, ROOT)}")


man()
w.build(os.path.join(OUT, "ninja_woman_body.glb"), crop=True, trim=w.SCARF, dye=dye)
