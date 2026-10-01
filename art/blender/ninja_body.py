"""The Ninja's plain clothes (CLS-03, 2026-10-01): the KayKit Rogues in dark cloth.

    blender -b --factory-startup --python art/blender/ninja_body.py

"Dark clothes, a dark green is fine, and the woman a little more uncovered" (your call). The
man is the hooded and masked Rogue; the woman is the lightened Rogue of woman_body.py
with the bodice cut short over a bare midriff and her hair tied up in a high tail
("a hairstyle more like a ninja's"), her skirt and trousers off for a short wrap round the hips
and bare legs ("uncover the legs a little too"). Both wear split-toed cloth shoes bound at the
ankle in place of the Rogue's boots ("shoes more like a ninja's"). Both wear the Rogue's texture dyed: its teal to
charcoal, its green to a dark green, its leathers to a dark brown; skin, eyes and metal as they
were. Writes game/assets/temins/characters/ninja_man_body.glb and ninja_woman_body.glb, which
visuals.json grafts on the player as a Ninja.
"""

import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)

import creaturekit as c  # noqa: E402
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

# Where her hair is cut, and the tail: (a point along it, its half width), from the tie to the tip.
NAPE = 1.55

# How far from the middle a leg stands.
LEG = 0.165
TAIL = [((0, 0.28, 2.02), 0.10), ((0, 0.46, 2.16), 0.13), ((0, 0.66, 2.10), 0.15), ((0, 0.78, 1.88), 0.14),
        ((0, 0.80, 1.62), 0.11), ((0, 0.76, 1.40), 0.07), ((0, 0.70, 1.26), 0.02)]


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


def swatch_uv(column, row, down=0.3):
    """A UV in the sheet's swatch at `column`, `row` from the top, `down` its gradient."""
    return Vector(((column + 0.5) / 8, 1 - (row + down) / 4))


def ponytail(arm, image):
    """Her hair tied up: cut at the nape, a tail from a green tie high on the back of her head."""
    head = bpy.data.objects["Rogue_Head"]
    brown = lambda col, at, size: col.x > 0.5 and col.y < 0.45  # noqa: E731

    # The loose strands down her back and by her neck.
    w.edit(head, image, drop=lambda col, at, size: brown(col, at, size) and at.z + size.z / 2 < 1.42,
           repaint=lambda col, at, size: False, paint_uv=None, expect_drop=6, expect_paint=0)

    # The long hair itself, one piece with the cap: what hangs below the ears drawn up and in.
    bm = bmesh.new()
    bm.from_mesh(head.data)
    world, back = head.matrix_world, head.matrix_world.inverted()
    mane = max(w.pieces(bm), key=len)
    for v in mane:
        p = world @ v.co
        if p.z < NAPE and p.y > -0.1:
            t = min(1.0, (NAPE - p.z) / 0.5)
            p = Vector((p.x * (1 - 0.3 * t), p.y * (1 - 0.3 * t), NAPE - (NAPE - p.z) * 0.2))
            v.co = back @ p
    bm.to_mesh(head.data)
    bm.free()

    material = head.data.materials[0]
    tail = c.loft("tail", [(Vector(p), r, r * 0.8) for p, r in TAIL], material, sides=16, per=3)
    w.painted(tail, swatch_uv(1, 0, 0.5), material)
    tie = c.loft("tie", [(Vector((0, 0.31, 2.05)), 0.125, 0.125), (Vector((0, 0.40, 2.12)), 0.125, 0.125)], material, sides=16, per=1)
    w.painted(tie, swatch_uv(1, 1), material)

    bpy.ops.object.select_all(action="DESELECT")
    for part in (tail, tie):
        w.weigh(part, arm, ["head"])
        part.select_set(True)
    head.select_set(True)
    bpy.context.view_layer.objects.active = head
    bpy.ops.object.join()


def put(target, arm, parts):
    """Joins new parts — (object, uv, bones) — into `target`, painted and weighted."""
    material = target.data.materials[0]
    bpy.ops.object.select_all(action="DESELECT")
    for part, uv, bones in parts:
        w.painted(part, uv, material)
        w.weigh(part, arm, bones)
        part.select_set(True)
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.join()


def strip(obj, keep):
    """Takes off every loose piece but those keep(lowest, highest) wants, heights in the world."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    doomed = []
    for piece in w.pieces(bm):
        heights = [(obj.matrix_world @ v.co).z for v in piece]
        if not keep(min(heights), max(heights)):
            doomed += piece
    bmesh.ops.delete(bm, geom=doomed, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()


def legs(arm, prefix, bare):
    """Split-toed cloth shoes bound at the ankle and shin in place of the Rogue's boots; with
    `bare`, a bare leg above them in place of the trousers."""
    for name, side in ((f"{prefix}_LegLeft", 1), (f"{prefix}_LegRight", -1)):
        leg = bpy.data.objects[name]
        material = leg.data.materials[0]
        strip(leg, keep=lambda low, high: not bare and high > 0.4)
        x = side * LEG
        bone = "l" if side > 0 else "r"
        shin, foot = [f"upperleg.{bone}", f"lowerleg.{bone}", f"foot.{bone}"], [f"foot.{bone}", f"toes.{bone}"]
        top = 0.21 if bare else 0.33
        parts = []

        def ring(z, r, y=0.005):
            return (Vector((x, y, z)), r, r)

        if bare:
            thigh = c.loft("thigh", [ring(0.62, 0.105), ring(0.40, 0.104), ring(0.18, 0.098)], material, sides=18, per=2)
            parts.append((thigh, swatch_uv(0, 0), ["hips"] + shin))

        # The sock up the shin, and the shoe: a rounded foot ending in two toes.
        sock = c.loft("sock", [ring(top, 0.112), ring(top - 0.02, 0.114), ring(0.12, 0.110), ring(0.05, 0.106)], material, sides=18, per=2)
        parts.append((sock, swatch_uv(0, 1, 0.45), shin))
        shoe = c.loft("shoe", [(Vector((x, y, h + 0.004)), wide, h) for y, wide, h in
                               ((0.13, 0.06, 0.035), (0.09, 0.10, 0.065), (0.0, 0.112, 0.078), (-0.10, 0.112, 0.068),
                                (-0.17, 0.110, 0.058), (-0.20, 0.10, 0.05))], material, sides=18, per=2)
        parts.append((shoe, swatch_uv(0, 1, 0.45), foot))
        for aside, wide in ((-0.052, 0.045), (0.042, 0.058)):  # the big toe inside, the rest outside
            toe = c.loft("toe", [(Vector((x + side * aside, y, h + 0.004)), wide * k, h) for y, k, h in
                                 ((-0.17, 1.0, 0.052), (-0.24, 1.0, 0.047), (-0.285, 0.75, 0.036))], material, sides=12, per=2)
            parts.append((toe, swatch_uv(0, 1, 0.45), [f"toes.{bone}"]))
        sole = c.loft("sole", [(Vector((x, y, 0.012)), wide, 0.012) for y, wide in
                               ((0.12, 0.07), (0.05, 0.118), (-0.10, 0.120), (-0.20, 0.112), (-0.28, 0.085))], material, sides=14, per=2)
        parts.append((sole, swatch_uv(1, 0, 0.6), foot))

        # The bindings: one at the top of the sock, two crossing the ankle.
        for z, r in ((top - 0.035, 0.121), (0.135, 0.117), (0.095, 0.115)):
            band = c.loft("band", [ring(z - 0.014, r), ring(z + 0.014, r)], material, sides=18, per=1)
            parts.append((band, swatch_uv(1, 1), shin))

        put(leg, arm, parts)


def bare_legs(arm, image):
    """Her skirt and trousers off: short cloth round the hips under the belt, bare legs, the shoes."""
    body = bpy.data.objects["Rogue_Body"]
    w.edit(body, image,
           drop=lambda col, at, size: (col.x < 0.1 and col.y > 0.2 and at.z + size.z / 2 < 0.6)
           or (abs(col.x - 0.39) < 0.04 and abs(col.y - 0.33) < 0.04 and at.z < 0.5),
           repaint=lambda col, at, size: False, paint_uv=None, expect_drop=12, expect_paint=0)

    material = body.data.materials[0]
    wrap = c.loft("wrap", [w.ring_path(0.66, 0.355, 0.30), w.ring_path(0.56, 0.365, 0.308), w.ring_path(0.49, 0.378, 0.318),
                           w.ring_path(0.455, 0.36, 0.30), w.ring_path(0.44, 0.30, 0.25)], material, sides=28, per=3)
    hem = c.loft("hem_low", [w.ring_path(0.468, 0.376, 0.316), w.ring_path(0.49, 0.384, 0.323)], material, sides=28, per=1)
    put(body, arm, [(wrap, swatch_uv(0, 1, 0.45), ["hips"]), (hem, swatch_uv(1, 1), ["hips"])])
    legs(arm, "Rogue", bare=True)


def woman_style(arm, image):
    ponytail(arm, image)
    bare_legs(arm, image)


def man():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=HOODED)
    for obj in list(bpy.data.objects):
        if obj.type == "MESH" and obj.name not in MAN:
            bpy.data.objects.remove(obj, do_unlink=True)
    legs(next(o for o in bpy.data.objects if o.type == "ARMATURE"), "RogueHooded", bare=False)
    w.wear(dye(next(i for i in bpy.data.images if i.size[0] > 0)))
    out = os.path.join(OUT, "ninja_man_body.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=False,
                              export_animations=False, export_skins=True, export_yup=True)
    print(f"[ninja] -> {os.path.relpath(out, ROOT)}")


man()
w.build(os.path.join(OUT, "ninja_woman_body.glb"), crop=True, trim=w.SCARF, dye=dye, style=woman_style)
